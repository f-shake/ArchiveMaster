// 用 libheif 的网格(grid)接口编码：把大图切成瓦片分别编码，容器里再拼回整图。
// 手机上能打开 = 每张 HEVC 小图都在硬件解码器的能力范围内（实测单张上限约 1681 万像素）。
// 用法: dotnet run heicgrid.cs <rgb24文件> <宽> <高> <quality> <瓦片边长> <exif|-> <xmp|-> <输出.heic>
//
// 说明：libheif 的 add_grid_image 收的是整图尺寸 + 行列数，瓦片尺寸由解码端从第 0 块瓦片反推。
// 规范（ISO/IEC 23008-12 §6.6.2.3.1）只要求瓦片**盖满**画布（tile*columns ≥ 整图宽，是 ≥ 不是等号），
// 多出来的部分解码端按整图尺寸裁掉；但要求**所有瓦片同尺寸**——把最后一行/列做成小瓦片会报
// "Grid tiles have different sizes"。所以这里行列数向上取整、每块瓦片都是整块 tileSize，
// 越出图外的像素用边缘像素复制补满（与生产的 HeifEncoder 一致）。
// 瓦片边长取 16 的倍数：既避开 libheif 宽度非 16 倍数时输出色度损坏的问题，也和手机相机自己用的 512 一致。

using System.Diagnostics;
using System.Runtime.InteropServices;

string rgbFile = args[0];
int W = int.Parse(args[1]);
int H = int.Parse(args[2]);
int quality = int.Parse(args[3]);
int tileSize = int.Parse(args[4]);
string exifFile = args[5];
string xmpFile = args[6];
string outFile = args[7];
string iccFile = args.Length > 8 ? args[8] : "-";

if (tileSize <= 0 || tileSize % 16 != 0)
{
    Console.Error.WriteLine($"瓦片边长 {tileSize} 必须是 16 的正倍数");
    return 1;
}

int columns = (W + tileSize - 1) / tileSize;
int rows = (H + tileSize - 1) / tileSize;

Console.WriteLine($"libheif {Marshal.PtrToStringUTF8(Native.heif_get_version())}");
Console.WriteLine($"整图 {W}x{H}  瓦片边长 {tileSize}  网格 {columns}x{rows} = {columns * rows} 块（瓦片一律整块，越界补边）");
if (W % tileSize != 0) Console.WriteLine($"  最后一列真实内容 {W - (columns - 1) * tileSize} px");
if (H % tileSize != 0) Console.WriteLine($"  最后一行真实内容 {H - (rows - 1) * tileSize} px");

byte[] rgb = File.ReadAllBytes(rgbFile);
long need = (long)W * H * 3;
if (rgb.LongLength < need) { Console.Error.WriteLine($"RGB 不足: {rgb.LongLength} < {need}"); return 1; }

byte[]? exif = exifFile == "-" ? null : File.ReadAllBytes(exifFile);
byte[]? xmp = xmpFile == "-" ? null : File.ReadAllBytes(xmpFile);
byte[]? icc = iccFile == "-" ? null : File.ReadAllBytes(iccFile);

var sw = Stopwatch.StartNew();
Native.Check(Native.heif_init(0), "heif_init");
nint ctx = Native.heif_context_alloc();
nint encoder = 0, options = 0, gridHandle = 0;
try
{
    Native.Check(Native.heif_context_get_encoder_for_format(ctx, 1, out encoder), "取编码器");
    Native.Check(Native.heif_encoder_set_lossy_quality(encoder, quality), "设置质量");
    options = Native.heif_encoding_options_alloc();

    Native.Check(Native.heif_context_add_grid_image(ctx, (uint)W, (uint)H, (uint)columns, (uint)rows,
        options, out gridHandle), "创建网格");

    int srcRowBytes = W * 3;
    int tileRowBytes = tileSize * 3;
    byte[] tileRow = new byte[tileRowBytes];
    for (int r = 0; r < rows; r++)
    {
        int y0 = r * tileSize;
        int realRows = Math.Min(tileSize, H - y0);
        for (int c = 0; c < columns; c++)
        {
            int x0 = c * tileSize;
            int realWidth = Math.Min(tileSize, W - x0);
            int realWidthBytes = realWidth * 3;
            int lastPixel = (realWidth - 1) * 3;

            nint tile = 0;
            try
            {
                Native.Check(Native.heif_image_create(tileSize, tileSize, 1, 10, out tile), "创建瓦片");
                Native.Check(Native.heif_image_add_plane(tile, 10, tileSize, tileSize, 8), "分配瓦片平面");
                nint plane = Native.heif_image_get_plane(tile, 10, out int stride);
                if (plane == 0) throw new InvalidOperationException("瓦片平面为空");
                if (stride < tileRowBytes) throw new InvalidOperationException($"瓦片行距（{stride}）小于一行字节数（{tileRowBytes}）");

                for (int y = 0; y < tileSize; y++)
                {
                    if (y < realRows)
                    {
                        long srcOff = ((long)(y0 + y) * srcRowBytes) + (long)x0 * 3;
                        Buffer.BlockCopy(rgb, (int)srcOff, tileRow, 0, realWidthBytes);
                        // 横向越界：把该行最后一个真实像素复制到行尾
                        for (int x = realWidth; x < tileSize; x++)
                        {
                            int t = x * 3;
                            tileRow[t] = tileRow[lastPixel];
                            tileRow[t + 1] = tileRow[lastPixel + 1];
                            tileRow[t + 2] = tileRow[lastPixel + 2];
                        }
                    }
                    // 纵向越界：沿用上一行（缓冲里留着的就是最后一行真实像素）
                    Marshal.Copy(tileRow, 0, nint.Add(plane, y * stride), tileRowBytes);
                }

                // ICC 只挂第一块：libheif 仅在 tile (0,0) 时收集 colr 属性
                if (c == 0 && r == 0 && icc is { Length: > 0 })
                {
                    Native.Check(Native.heif_image_set_raw_color_profile(tile, "prof", icc, (nuint)icc.Length),
                        "写入 ICC");
                }

                Native.Check(Native.heif_context_add_image_tile(ctx, gridHandle, (uint)c, (uint)r, tile, encoder),
                    $"加入瓦片 {c},{r}");
            }
            finally
            {
                // 验证「用完立刻释放」是否安全（生产代码改成这样以降低峰值内存）
                if (tile != 0) Native.heif_image_release(tile);
            }
        }
        if (r % 5 == 4 || r == rows - 1)
            Console.WriteLine($"  第 {r + 1}/{rows} 行完成  {sw.Elapsed.TotalSeconds:F1}s");
    }

    if (exif is { Length: > 0 })
        Native.Check(Native.heif_context_add_exif_metadata(ctx, gridHandle, exif, exif.Length), "EXIF");
    if (xmp is { Length: > 0 })
        Native.Check(Native.heif_context_add_XMP_metadata(ctx, gridHandle, xmp, xmp.Length), "XMP");

    Native.Check(Native.heif_context_write_to_file(ctx, outFile), "写出");
    Console.WriteLine($"  完成 {sw.Elapsed.TotalSeconds:F1}s -> {outFile}  {new FileInfo(outFile).Length:N0} 字节");
}
finally
{
    if (gridHandle != 0) Native.heif_image_handle_release(gridHandle);
    if (options != 0) Native.heif_encoding_options_free(options);
    if (encoder != 0) Native.heif_encoder_release(encoder);
    if (ctx != 0) Native.heif_context_free(ctx);
}
return 0;

internal static class Native
{
    private const string Lib = "libheif";

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct HeifError
    {
        public readonly int Code;
        public readonly int SubCode;
        public readonly nint Message;
    }

    public static void Check(HeifError e, string what)
    {
        if (e.Code == 0) return;
        string msg = e.Message == 0 ? "(无消息)" : Marshal.PtrToStringUTF8(e.Message) ?? "?";
        throw new InvalidOperationException($"{what} 失败: code={e.Code} sub={e.SubCode} {msg}");
    }

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern nint heif_get_version();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_init(nint p);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern nint heif_context_alloc();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern void heif_context_free(nint c);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_context_get_encoder_for_format(nint c, int f, out nint e);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern void heif_encoder_release(nint e);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_encoder_set_lossy_quality(nint e, int q);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_image_create(int w, int h, int cs, int cr, out nint img);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern void heif_image_release(nint img);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_image_add_plane(nint img, int ch, int w, int h, int bd);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern nint heif_image_get_plane(nint img, int ch, out int stride);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_image_set_raw_color_profile(nint img, [MarshalAs(UnmanagedType.LPUTF8Str)] string t, byte[] d, nuint n);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern nint heif_encoding_options_alloc();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern void heif_encoding_options_free(nint o);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern void heif_image_handle_release(nint h);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_context_add_exif_metadata(nint c, nint h, byte[] d, int n);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_context_add_XMP_metadata(nint c, nint h, byte[] d, int n);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_context_write_to_file(nint c, [MarshalAs(UnmanagedType.LPUTF8Str)] string f);

    // 网格接口（签名取自 libheif 的 heif_tiling.h）
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern HeifError heif_context_add_grid_image(nint ctx, uint w, uint h,
        uint tileColumns, uint tileRows, nint options, out nint outHandle);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern HeifError heif_context_add_image_tile(nint ctx, nint tiledImage,
        uint tileX, uint tileY, nint image, nint encoder);
}
