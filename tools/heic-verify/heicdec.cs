// 用 libheif 的解码器读出主图（网格会被正确拼回整图），导出为 RGB24。
// ffmpeg 不支持 HEIF 网格拼接，只能解出单块瓦片，所以用它验证不了。
// 用法: dotnet run heicdec.cs <输入.heic> <输出.raw>
using System.Runtime.InteropServices;

string inFile = args[0];
string outFile = args[1];

nint ctx = 0, handle = 0, img = 0;
try
{
    Native.Check(Native.heif_init(0), "heif_init");
    ctx = Native.heif_context_alloc();
    Native.Check(Native.heif_context_read_from_file(ctx, inFile, 0), "读取文件");
    Native.Check(Native.heif_context_get_primary_image_handle(ctx, out handle), "取主图");

    Console.WriteLine($"主图 ispe: {Native.heif_image_handle_get_ispe_width(handle)}x{Native.heif_image_handle_get_ispe_height(handle)}");

    Native.Check(Native.heif_decode_image(handle, out img, 1 /*RGB*/, 10 /*interleaved_RGB*/, 0), "解码");

    int w = Native.heif_image_get_width(img, 10);
    int h = Native.heif_image_get_height(img, 10);
    nint plane = Native.heif_image_get_plane_readonly(img, 10, out int stride);
    Console.WriteLine($"解码得到 {w}x{h}  行距 {stride}");

    using var fs = new FileStream(outFile, FileMode.Create, FileAccess.Write);
    byte[] row = new byte[w * 3];
    for (int y = 0; y < h; y++)
    {
        Marshal.Copy(nint.Add(plane, y * stride), row, 0, w * 3);
        fs.Write(row, 0, row.Length);
    }
    Console.WriteLine($"已写出 {fs.Length:N0} 字节 -> {outFile}");
}
finally
{
    if (img != 0) Native.heif_image_release(img);
    if (handle != 0) Native.heif_image_handle_release(handle);
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

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_init(nint p);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern nint heif_context_alloc();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern void heif_context_free(nint c);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_context_read_from_file(nint c, [MarshalAs(UnmanagedType.LPUTF8Str)] string f, nint o);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_context_get_primary_image_handle(nint c, out nint h);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern void heif_image_handle_release(nint h);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern int heif_image_handle_get_ispe_width(nint h);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern int heif_image_handle_get_ispe_height(nint h);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern HeifError heif_decode_image(nint h, out nint img, int colorspace, int chroma, nint options);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern void heif_image_release(nint img);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern int heif_image_get_width(nint img, int ch);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern int heif_image_get_height(nint img, int ch);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] public static extern nint heif_image_get_plane_readonly(nint img, int ch, out int stride);
}
