using System.Reflection;
using System.Runtime.InteropServices;

namespace ArchiveMaster.Helpers;

/// <summary>
/// libheif 的 native 层：P/Invoke 声明、原生库解析与可用性探测。
/// </summary>
/// <remarks>
/// <para>
/// 只负责"能不能调到 libheif 的 C API"，不含任何 HEIC 编码策略——策略与编码流程在 <see cref="HeifEncoder"/>。
/// </para>
/// <para>
/// 原生库按平台命名，放在程序目录（或程序目录下的 <c>native</c> 子目录）：
/// <c>libheif.dll</c> / <c>libheif.so</c> / <c>libheif.dylib</c>；
/// 其依赖（Windows 下为 <c>libx265.dll</c>、<c>libde265.dll</c>）需与它同目录。
/// 缺少原生库时 <see cref="IsAvailable"/> 为 false。
/// </para>
/// </remarks>
internal static class LibHeifNative
{
    private const string LibraryName = "libheif";

    #region libheif 常量（取自 heif_context.h / heif_image.h）

    internal const int HeifCompressionHevc = 1;       // heif_compression_HEVC
    internal const int HeifColorspaceRgb = 1;         // heif_colorspace_RGB
    internal const int HeifChromaInterleavedRgb = 10; // heif_chroma_interleaved_RGB
    internal const int HeifChannelInterleaved = 10;   // heif_channel_interleaved

    #endregion

    /// <summary>本机是否有可用的 libheif 及其 HEVC 编码器。</summary>
    internal static bool IsAvailable => Availability.Value.Available;

    /// <summary>不可用时的原因（可用时为空字符串）。</summary>
    internal static string UnavailableReason => Availability.Value.Reason;

    /// <summary>原生库版本号，形如 "1.23.5"（探测失败时为 "unknown"）。</summary>
    internal static string Version => Availability.Value.Version;

    private static readonly Lazy<ProbeResult> Availability = new(Probe);

    static LibHeifNative()
    {
        // 把本程序集对 "libheif" 的调用解析到随包的原生库。
        // 必须在本类首次使用（也就是第一次 P/Invoke）之前注册，所以放在静态构造里。
        NativeLibrary.SetDllImportResolver(typeof(LibHeifNative).Assembly, ResolveLibrary);
    }

    internal static void ThrowIfFailed(HeifError error, string operation)
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

    internal readonly record struct ProbeResult(bool Available, string Version, string Reason);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct HeifError
    {
        internal readonly int Code;
        internal readonly int SubCode;
        internal readonly nint Message;
    }

    #region libheif P/Invoke（签名对应 libheif 的 heif_*.h）

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern nint heif_get_version();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern HeifError heif_init(nint initParams);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern nint heif_context_alloc();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void heif_context_free(nint context);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern HeifError heif_context_get_encoder_for_format(nint context, int format, out nint encoder);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void heif_encoder_release(nint encoder);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern HeifError heif_encoder_set_lossy_quality(nint encoder, int quality);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern HeifError heif_image_create(int width, int height, int colorspace, int chroma, out nint image);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void heif_image_release(nint image);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern HeifError heif_image_add_plane(nint image, int channel, int width, int height, int bitDepth);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern nint heif_image_get_plane(nint image, int channel, out int outStride);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern HeifError heif_image_set_raw_color_profile(nint image,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string profileType, byte[] profileData, nuint profileSize);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern nint heif_encoding_options_alloc();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void heif_encoding_options_free(nint options);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern HeifError heif_context_encode_image(nint context, nint image, nint encoder, nint options,
        out nint outImageHandle);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void heif_image_handle_release(nint imageHandle);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern HeifError heif_context_add_exif_metadata(nint context, nint imageHandle, byte[] data, int size);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern HeifError heif_context_add_XMP_metadata(nint context, nint imageHandle, byte[] data, int size);

    // 注意：libheif 在 Windows 下会把该 UTF-8 路径转成 UTF-16 再开文件，所以必须按 UTF-8 封送（不能用 ANSI）
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern HeifError heif_context_write_to_file(nint context,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string filename);

    // --- 网格(grid)编码，签名取自 libheif 的 heif_tiling.h（1.23.5）---
    // 注意 add_grid_image 收的是「列数、行数」而不是瓦片宽高；add_image_tile 收的是「瓦片序号」而不是像素坐标

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern HeifError heif_context_add_grid_image(nint context,
        uint imageWidth, uint imageHeight, uint tileColumns, uint tileRows,
        nint encodingOptions, out nint outGridImageHandle);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern HeifError heif_context_add_image_tile(nint context, nint tiledImage,
        uint tileX, uint tileY, nint image, nint encoder);

    #endregion
}
