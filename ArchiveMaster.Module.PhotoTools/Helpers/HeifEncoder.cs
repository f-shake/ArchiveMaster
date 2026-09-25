using System.Reflection;
using System.Runtime.InteropServices;

namespace ArchiveMaster.Helpers;

/// <summary>
/// 直接调用随包的 libheif（内含 x265 编码器）输出 HEIC。
/// </summary>
/// <remarks>
/// <para>
/// 为什么不用 Magick.NET：它的 native 包里 libheif 没有编入 HEVC 编码器
/// （实测 <c>Heic Read=True Write=False</c>，写入会抛
/// <c>no encode delegate for this image format `HEIC'</c>），
/// 因此本项目随包附带自行构建的 libheif，由本类通过 P/Invoke 调用其 C API。
/// </para>
/// <para>
/// <b>真机实测结论（2026-09-25，小米手机相册），改动本类前务必先读：</b>
/// <list type="number">
/// <item>
/// 传给 <c>heif_context_add_exif_metadata</c> 的 EXIF 载荷<b>必须保留 <c>Exif\0\0</c> 六字节前缀</b>，
/// libheif 会据此把 item 内的 <c>exif_tiff_header_offset</c> 写成 6。若去掉前缀（偏移=0），
/// 小米相册<b>完全读不到 EXIF</b>（机型、拍摄时间、GPS 全无），而 Pillow、Magick.NET 等桌面工具仍能正常读出——
/// 即"能不能读"无法用桌面工具验证，必须真机确认。所以调用方要传
/// <c>MagickImage.GetProfile("exif").ToByteArray()</c> 的<b>原样</b>字节，不要裁掉前 6 字节。
/// </item>
/// <item>输出扩展名用 <c>.heic</c>，实测比 <c>.heif</c> 的相册兼容性更好。</item>
/// </list>
/// </para>
/// <para>
/// 原生库按平台命名，放在程序目录（或程序目录下的 <c>native</c> 子目录）：
/// <c>libheif.dll</c> / <c>libheif.so</c> / <c>libheif.dylib</c>；
/// 其依赖（Windows 下为 <c>libx265.dll</c>、<c>libde265.dll</c>）需与它同目录。
/// 缺少原生库时 <see cref="IsAvailable"/> 为 false，调用 <see cref="Encode"/> 会抛异常。
/// </para>
/// </remarks>
public static class HeifEncoder
{
    #region libheif 常量（取自 heif_context.h / heif_image.h）

    private const string LibraryName = "libheif";

    private const int HeifCompressionHevc = 1;       // heif_compression_HEVC
    private const int HeifColorspaceRgb = 1;         // heif_colorspace_RGB
    private const int HeifChromaInterleavedRgb = 10; // heif_chroma_interleaved_RGB
    private const int HeifChannelInterleaved = 10;   // heif_channel_interleaved

    private const int RgbBytesPerPixel = 3;

    #endregion

    /// <summary>
    /// HEIC 编码是内存与 CPU 密集型：每张图要一次全尺寸 RGB 拷贝（4080×3060 约 37 MB），
    /// 且 x265 自身就是多线程的，而本工具的压缩本身还跑在 <c>Config.Thread</c> 的线程池里，
    /// 因此这里限制同时编码的图片数，避免线程数相乘导致内存峰值过高。
    /// </summary>
    private static readonly SemaphoreSlim EncodeSemaphore = new(2, 2);

    private static readonly Lazy<ProbeResult> Availability = new(Probe);

    static HeifEncoder()
    {
        // 把本程序集对 "libheif" 的调用解析到随包的原生库
        NativeLibrary.SetDllImportResolver(typeof(HeifEncoder).Assembly, ResolveLibrary);
    }

    /// <summary>本机是否有可用的 libheif 及其 HEVC 编码器。</summary>
    public static bool IsAvailable => Availability.Value.Available;

    /// <summary>不可用时的原因（可用时为空字符串），可用于日志或界面提示。</summary>
    public static string UnavailableReason => Availability.Value.Reason;

    /// <summary>原生库版本号，形如 "1.23.5"（探测失败时为 "unknown"）。</summary>
    public static string NativeVersion => Availability.Value.Version;

    /// <summary>
    /// 把一张 RGB24 图像编码为 HEIC 文件。
    /// </summary>
    /// <param name="rgb24">按行紧密排列的 RGB24 像素数据。</param>
    /// <param name="width">像素宽。</param>
    /// <param name="height">像素高。</param>
    /// <param name="quality">有损质量（0-100，与 JPEG 的质量刻度不可比）。</param>
    /// <param name="exifWithPrefix">EXIF 载荷，<b>须保留 <c>Exif\0\0</c> 前缀</b>；无则传 null。</param>
    /// <param name="iccProfile">ICC 色彩配置；无则传 null。</param>
    /// <param name="xmp">XMP 元数据；无则传 null。</param>
    /// <param name="outputPath">输出文件路径（支持中文等非 ASCII 路径）。</param>
    /// <param name="ct">取消令牌：等待并发许可时可被取消。</param>
    public static void Encode(byte[] rgb24, int width, int height, int quality,
        byte[] exifWithPrefix, byte[] iccProfile, byte[] xmp, string outputPath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rgb24);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "图像尺寸必须为正数");
        }

        if (quality is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "质量必须在 0-100 之间");
        }

        if (rgb24.Length < (long)width * height * RgbBytesPerPixel)
        {
            throw new ArgumentException(
                $"RGB 数据长度不足：需要 {(long)width * height * RgbBytesPerPixel} 字节，实际 {rgb24.Length} 字节",
                nameof(rgb24));
        }

        ProbeResult probe = Availability.Value;
        if (!probe.Available)
        {
            throw new InvalidOperationException($"HEIC 编码不可用：{probe.Reason}");
        }

        EncodeSemaphore.Wait(ct);
        try
        {
            EncodeCore(rgb24, width, height, quality, exifWithPrefix, iccProfile, xmp, outputPath);
        }
        finally
        {
            EncodeSemaphore.Release();
        }
    }

    private static void EncodeCore(byte[] rgb24, int width, int height, int quality,
        byte[] exifWithPrefix, byte[] iccProfile, byte[] xmp, string outputPath)
    {
        nint context = 0, encoder = 0, image = 0, options = 0, handle = 0;
        try
        {
            context = heif_context_alloc();
            if (context == 0)
            {
                throw new InvalidOperationException("heif_context_alloc 返回空指针");
            }

            ThrowIfFailed(heif_context_get_encoder_for_format(context, HeifCompressionHevc, out encoder),
                "获取 HEVC 编码器");
            ThrowIfFailed(heif_encoder_set_lossy_quality(encoder, quality), "设置质量");

            ThrowIfFailed(heif_image_create(width, height, HeifColorspaceRgb, HeifChromaInterleavedRgb, out image),
                "创建图像");
            ThrowIfFailed(heif_image_add_plane(image, HeifChannelInterleaved, width, height, 8), "分配像素平面");

            nint plane = heif_image_get_plane(image, HeifChannelInterleaved, out int stride);
            if (plane == 0)
            {
                throw new InvalidOperationException("heif_image_get_plane 返回空指针");
            }

            int rowBytes = width * RgbBytesPerPixel;
            if (stride < rowBytes)
            {
                throw new InvalidOperationException($"像素平面行距（{stride}）小于一行的字节数（{rowBytes}）");
            }

            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(rgb24, y * rowBytes, nint.Add(plane, y * stride), rowBytes);
            }

            if (iccProfile is { Length: > 0 })
            {
                ThrowIfFailed(heif_image_set_raw_color_profile(image, "prof", iccProfile, (nuint)iccProfile.Length),
                    "写入 ICC 色彩配置");
            }

            options = heif_encoding_options_alloc();
            ThrowIfFailed(heif_context_encode_image(context, image, encoder, options, out handle), "编码");

            // 元数据必须在 encode 之后写入
            if (exifWithPrefix is { Length: > 0 })
            {
                ThrowIfFailed(heif_context_add_exif_metadata(context, handle, exifWithPrefix, exifWithPrefix.Length),
                    "写入 EXIF");
            }

            if (xmp is { Length: > 0 })
            {
                ThrowIfFailed(heif_context_add_XMP_metadata(context, handle, xmp, xmp.Length), "写入 XMP");
            }

            ThrowIfFailed(heif_context_write_to_file(context, outputPath), "写出文件");
        }
        finally
        {
            if (handle != 0) heif_image_handle_release(handle);
            if (options != 0) heif_encoding_options_free(options);
            if (image != 0) heif_image_release(image);
            if (encoder != 0) heif_encoder_release(encoder);
            if (context != 0) heif_context_free(context);
        }
    }

    private static ProbeResult Probe()
    {
        try
        {
            string version = Marshal.PtrToStringUTF8(heif_get_version()) ?? "unknown";

            // heif_init 是引用计数的，且允许传 nullptr 取默认参数
            ThrowIfFailed(heif_init(0), "初始化");

            nint context = heif_context_alloc();
            if (context == 0)
            {
                return new ProbeResult(false, version, "heif_context_alloc 返回空指针");
            }

            try
            {
                HeifError error = heif_context_get_encoder_for_format(context, HeifCompressionHevc, out nint encoder);
                // 注意：libheif 是先把编码器写进 out 指针、再返回 alloc() 的结果，
                // 所以"指针非空"并不代表成功，必须同时检查错误码
                if (error.Code != 0 || encoder == 0)
                {
                    if (encoder != 0)
                    {
                        heif_encoder_release(encoder);
                    }

                    return new ProbeResult(false, version,
                        $"没有可用的 HEVC 编码器（code={error.Code}，libheif 可能是只读构建）");
                }

                heif_encoder_release(encoder);
                return new ProbeResult(true, version, "");
            }
            finally
            {
                heif_context_free(context);
            }
        }
        catch (Exception ex)
        {
            // 原生库缺失（DllNotFoundException）、架构不符（BadImageFormatException）等都归为不可用
            return new ProbeResult(false, "unknown", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ThrowIfFailed(HeifError error, string operation)
    {
        if (error.Code == 0)
        {
            return;
        }

        string message = error.Message == 0
            ? "(无消息)"
            : Marshal.PtrToStringUTF8(error.Message) ?? "(消息无法解析)";
        throw new InvalidOperationException($"HEIC{operation}失败：code={error.Code}, subcode={error.SubCode}, {message}");
    }

    private static nint ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName)
        {
            return 0;
        }

        foreach (string candidate in GetCandidatePaths())
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out nint handle))
            {
                return handle;
            }
        }

        // 返回 0 表示交回运行时按默认规则查找（例如系统已装 libheif）
        return 0;
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        string fileName = OperatingSystem.IsWindows() ? "libheif.dll"
            : OperatingSystem.IsMacOS() ? "libheif.dylib"
            : "libheif.so";

        yield return Path.Combine(AppContext.BaseDirectory, fileName);
        yield return Path.Combine(AppContext.BaseDirectory, "native", fileName);
    }

    private readonly record struct ProbeResult(bool Available, string Version, string Reason);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct HeifError
    {
        public readonly int Code;
        public readonly int SubCode;
        public readonly nint Message;
    }

    #region libheif P/Invoke（签名对应 libheif 的 heif_*.h）

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern nint heif_get_version();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern HeifError heif_init(nint initParams);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern nint heif_context_alloc();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void heif_context_free(nint context);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern HeifError heif_context_get_encoder_for_format(nint context, int format, out nint encoder);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void heif_encoder_release(nint encoder);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern HeifError heif_encoder_set_lossy_quality(nint encoder, int quality);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern HeifError heif_image_create(int width, int height, int colorspace, int chroma, out nint image);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void heif_image_release(nint image);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern HeifError heif_image_add_plane(nint image, int channel, int width, int height, int bitDepth);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern nint heif_image_get_plane(nint image, int channel, out int outStride);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern HeifError heif_image_set_raw_color_profile(nint image,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string profileType, byte[] profileData, nuint profileSize);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern nint heif_encoding_options_alloc();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void heif_encoding_options_free(nint options);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern HeifError heif_context_encode_image(nint context, nint image, nint encoder, nint options,
        out nint outImageHandle);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void heif_image_handle_release(nint imageHandle);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern HeifError heif_context_add_exif_metadata(nint context, nint imageHandle, byte[] data, int size);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern HeifError heif_context_add_XMP_metadata(nint context, nint imageHandle, byte[] data, int size);

    // 注意：libheif 在 Windows 下会把该 UTF-8 路径转成 UTF-16 再开文件，所以必须按 UTF-8 封送（不能用 ANSI）
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern HeifError heif_context_write_to_file(nint context,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string filename);

    #endregion
}
