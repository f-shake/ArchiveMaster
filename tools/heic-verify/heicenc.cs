// 复刻 ArchiveMaster.Module.PhotoTools.Helpers.HeifEncoder 的调用序列，
// 用于按不同分辨率重编码同一张图做兼容性测试。
// 用法: dotnet run heicenc.cs <rgb24文件> <width> <height> <quality> <exif|-> <xmp|-> <icc|-> <输出.heic>

using System.Diagnostics;
using System.Runtime.InteropServices;

string rgbFile = args[0];
int width = int.Parse(args[1]);
int height = int.Parse(args[2]);
int quality = int.Parse(args[3]);
string exifFile = args[4];
string xmpFile = args[5];
string iccFile = args[6];
string outFile = args[7];

byte[] rgb = File.ReadAllBytes(rgbFile);
long need = (long)width * height * 3;
if (rgb.LongLength < need)
{
    Console.Error.WriteLine($"RGB 数据不足: {rgb.LongLength} < {need}");
    return 1;
}

byte[]? exif = exifFile == "-" ? null : File.ReadAllBytes(exifFile);
byte[]? xmp = xmpFile == "-" ? null : File.ReadAllBytes(xmpFile);
byte[]? icc = iccFile == "-" ? null : File.ReadAllBytes(iccFile);

Console.WriteLine($"libheif {Marshal.PtrToStringUTF8(Native.heif_get_version())}");
var sw = Stopwatch.StartNew();

Native.Check(Native.heif_init(0), "heif_init");
nint context = Native.heif_context_alloc();
nint encoder = 0, image = 0, options = 0, handle = 0;
try
{
    Native.Check(Native.heif_context_get_encoder_for_format(context, 1, out encoder), "取编码器");
    Native.Check(Native.heif_encoder_set_lossy_quality(encoder, quality), "设置质量");
    Native.Check(Native.heif_image_create(width, height, 1, 10, out image), "创建图像");
    Native.Check(Native.heif_image_add_plane(image, 10, width, height, 8), "分配平面");

    nint plane = Native.heif_image_get_plane(image, 10, out int stride);
    if (plane == 0) throw new InvalidOperationException("plane 为空");
    int rowBytes = width * 3;
    if (stride < rowBytes) throw new InvalidOperationException($"行距 {stride} < {rowBytes}");

    // tight: 忽略 libheif 报出的（带填充的）stride，按紧密行距写，用来验证它内部是不是按紧密行距读的
    bool tight = args.Length > 8 && args[8] == "tight";
    int pitch = tight ? rowBytes : stride;
    Console.WriteLine($"  width={width} rowBytes={rowBytes} stride={stride} 写入行距={pitch}{(tight ? " (tight)" : "")}");

    // 分块拷贝，避免为 112MP 图再造一份大缓冲
    const int chunkRows = 64;
    for (int y = 0; y < height; y += chunkRows)
    {
        int rows = Math.Min(chunkRows, height - y);
        Marshal.Copy(rgb, (int)((long)y * rowBytes), nint.Add(plane, y * pitch), rows * rowBytes);
    }
    Console.WriteLine($"  拷贝像素完成 {sw.Elapsed.TotalSeconds:F1}s");

    if (icc is { Length: > 0 })
        Native.Check(Native.heif_image_set_raw_color_profile(image, "prof", icc, (nuint)icc.Length), "ICC");

    options = Native.heif_encoding_options_alloc();
    Native.Check(Native.heif_context_encode_image(context, image, encoder, options, out handle), "编码");

    if (exif is { Length: > 0 })
        Native.Check(Native.heif_context_add_exif_metadata(context, handle, exif, exif.Length), "EXIF");
    if (xmp is { Length: > 0 })
        Native.Check(Native.heif_context_add_XMP_metadata(context, handle, xmp, xmp.Length), "XMP");

    Native.Check(Native.heif_context_write_to_file(context, outFile), "写出");
}
finally
{
    if (handle != 0) Native.heif_image_handle_release(handle);
    if (options != 0) Native.heif_encoding_options_free(options);
    if (image != 0) Native.heif_image_release(image);
    if (encoder != 0) Native.heif_encoder_release(encoder);
    if (context != 0) Native.heif_context_free(context);
}

var fi = new FileInfo(outFile);
Console.WriteLine($"  完成 {sw.Elapsed.TotalSeconds:F1}s -> {outFile}  {fi.Length:N0} bytes");
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
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_context_encode_image(nint c, nint img, nint e, nint o, out nint h);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern void heif_image_handle_release(nint h);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_context_add_exif_metadata(nint c, nint h, byte[] d, int n);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_context_add_XMP_metadata(nint c, nint h, byte[] d, int n);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_context_write_to_file(nint c, [MarshalAs(UnmanagedType.LPUTF8Str)] string f);
}
