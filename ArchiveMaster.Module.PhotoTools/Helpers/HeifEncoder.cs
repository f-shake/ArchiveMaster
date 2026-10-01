using ImageMagick;
using System.Runtime.InteropServices;

namespace ArchiveMaster.Helpers;

/// <summary>
/// 把 RGB24 图像编码为 HEIC。
/// </summary>
/// <remarks>
/// <para>
/// 本类只负责 HEIC 的<b>编码策略与流程</b>：尺寸要求、单张与网格的选择、瓦片布局。
/// libheif 的 P/Invoke 与原生库解析在 <see cref="LibHeifNative"/>。
/// </para>
/// <para>
/// 为什么不用 Magick.NET：它的 native 包里 libheif 没有编入 HEVC 编码器
/// （实测 <c>Heic Read=True Write=False</c>，写入会抛
/// <c>no encode delegate for this image format `HEIC'</c>），
/// 因此本项目随包附带自行构建的 libheif（见 build_scripts/native），由本类调用其 C API。
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
/// </remarks>
public static class HeifEncoder
{
    private const int RgbBytesPerPixel = 3;

    #region 编码策略常量

    /// <summary>网格编码瓦片边长的默认值，可被 <see cref="Encode"/> 的 <c>tileSize</c> 覆盖。</summary>
    /// <remarks>
    /// 必须是 16 的倍数（理由同 <see cref="WidthAlignment"/>），并与手机相机自己使用的瓦片尺寸一致
    /// （实测相机原片是 512x512 的网格）。缩小它可以减少"最后一行/列补边"浪费的像素，
    /// 代价是瓦片数按平方增长、每块瓦片的头与参数集开销变大。
    /// <para>
    /// 512 与 2048 已确认可在手机上正常打开查看，1024 未验证。之所以允许调大：瓦片太小会让
    /// x265 的 WPP 并行受限于 CTU 行数（512/64=8 行），实测单张网格图的并行度只有 2.4x，
    /// 而 1024/2048 分别为 4.6x/8.3x，墙钟在 48MPix 图上从 44.5s 降到 22.7s/12.6s（CPU 总工作量不变）。
    /// </para>
    /// </remarks>
    public const int DefaultTileSize = 512;

    /// <summary>可用的瓦片边长（512 与 2048 已确认可在手机上正常打开，1024 未验证）。</summary>
    /// <remarks>
    /// 上限止步于 2048：<b>一块瓦片就是一张独立的 HEVC 图</b>（手机解码器逐个解），所以它同样受
    /// <see cref="MaxSingleImagePixels"/> 的约束，而 4096x4096 = 1680 万像素已经越过该上限——
    /// 让瓦片比"单张打不开"的尺寸还大，等于绕开了当初设这条上限的理由。
    /// 2048 只有 419 万像素，远在安全区内。
    /// </remarks>
    public static readonly IReadOnlyList<int> TileSizes = new[] { 512, 1024, 2048 };

    /// <summary>
    /// 界面可选的 x265 preset（由快到慢）。名字即 x265 的 preset 名，libheif 原样转交给 x265。
    /// </summary>
    /// <remarks>
    /// 只列真正不同的档位：实测 <c>veryfast</c> / <c>faster</c> / <c>fast</c> 在本版本 libheif 上是
    /// <b>同一套配置</b>（产物逐字节相同，换过内容复核），所以只保留 <c>fast</c> 一个名字。
    /// 五档实测体积（18MPix 网格图、质量 50）：ultrafast 8761 KB、superfast 8669 KB、fast 7358 KB、
    /// medium 7361 KB、slow 7352 KB。
    /// </remarks>
    public static readonly IReadOnlyList<string> Presets =
        new[] { "ultrafast", "superfast", "fast", "medium", "slow" };

    /// <summary>与 <see cref="Presets"/> 等价的名字 → 规范名。</summary>
    /// <remarks>
    /// 实测 <c>veryfast</c> / <c>faster</c> 与 <c>fast</c> 在本版本 libheif 上是同一套配置（产物逐字节相同，
    /// 换过内容复核），所以界面只保留 <c>fast</c>。旧配置里存着的那两个名字由 <see cref="NormalizePreset"/>
    /// 迁到规范名，而不是判为非法——否则一份存量的配置会在开始前报错，而且那名字根本不在下拉框的候选里，
    /// 界面会显示为空。
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> PresetAliases = new Dictionary<string, string>
    {
        ["veryfast"] = "fast",
        ["faster"] = "fast",
    };

    /// <summary>把等价名字换成规范名；不认识的值原样返回，交给调用方去校验。</summary>
    public static string NormalizePreset(string preset)
    {
        return preset != null && PresetAliases.TryGetValue(preset, out string canonical) ? canonical : preset;
    }

    /// <summary>
    /// 默认 preset。libheif 的 x265 插件不设置时用的是 <c>slow</c>（最慢档，见 <see cref="Encode"/> 的 remarks），
    /// 这里取 <c>fast</c>：实测比 <c>slow</c> 快 2.4 倍（14.3s → 5.9s）而体积几乎不变（7352 KB → 7358 KB）。
    /// </summary>
    public const string DefaultPreset = "fast";

    /// <summary>单张编码时图像宽度必须对齐到的倍数。</summary>
    /// <remarks>
    /// libheif 在图像<b>宽度</b>不是 16 的倍数时输出的色度是坏的（画面发灰、撕裂）。实测
    /// 200/520/1000/2100/4184/4200/4216/15000 宽全部损坏，
    /// 192/208/512/1008/4000/4096/4176/4192/4208/4224 宽全部正常；与像素总数、质量、ICC、图像内容都无关。
    /// 高度不需要对齐（4096x4104、4000x4200、6000x3000 都是好的）。
    /// 注意这条只约束"单张图"：网格里每块瓦片都是 <see cref="DefaultTileSize"/>（或其可选值）宽，
    /// 与整图宽度无关。
    /// </remarks>
    private const uint WidthAlignment = 16;

    /// <summary>单张 HEVC 图的像素数上限，超过就改走网格编码。</summary>
    /// <remarks>
    /// 真机实测（小米相册）：单张 4096x4104（1681 万像素）能打开，6000x3000（1800 万像素）打不开，
    /// 故取 1600 万留余量。限制来自<b>像素总数</b>而不是单边长度——单张 8192x1024（839 万像素、长边 8192）
    /// 能正常打开。另注意这只是"单张图"的上限，不是照片分辨率上限：切成网格后一亿像素也能正常显示
    /// （手机相机自己拍的 5000 万像素照片就是 192 块 512x512 的网格）。
    /// <see cref="GetTargetSize"/> 与 <see cref="Encode"/> 必须用这一个阈值判断，否则会出现
    /// "缩放到 A 尺寸、却按 B 策略编码"的错位。
    /// </remarks>
    private const long MaxSingleImagePixels = 16_000_000;

    /// <summary>网格单方向允许的最大瓦片数。</summary>
    /// <remarks>
    /// HEIF 的网格结构里行列数各用一个字节存"减一"（libheif 的 <c>ImageGrid::write</c>），
    /// 超过 256 会被<b>静默截断</b>写出无法解码的文件，libheif 不会报错。
    /// 按界面上限（长边 20000、短边 10000）最多 40x20，这里只是兜底。
    /// </remarks>
    private const int MaxTilesPerSide = 256;

    #endregion

    /// <summary>本机是否有可用的 libheif 及其 HEVC 编码器。</summary>
    public static bool IsAvailable => LibHeifNative.IsAvailable;

    /// <summary>不可用时的原因（可用时为空字符串），可用于日志或界面提示。</summary>
    public static string UnavailableReason => LibHeifNative.UnavailableReason;

    /// <summary>原生库版本号，形如 "1.23.5"（探测失败时为 "unknown"）。</summary>
    public static string NativeVersion => LibHeifNative.Version;

    /// <summary>该输出格式是否走本类编码（而非交给 Magick.NET）。</summary>
    /// <remarks>
    /// 统一在这里判定：配置的校验、界面控制显隐、服务分发三处都要用它，各写一份枚举匹配迟早会漏一处。
    /// </remarks>
    public static bool IsHeifFormat(MagickFormat format)
    {
        return format is MagickFormat.Heic or MagickFormat.Heif;
    }

    /// <summary>
    /// 按 HEIC 编码的要求算出应当缩放到的目标像素尺寸；调用方据此缩放后再调 <see cref="Encode"/>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 两种情况：
    /// <list type="bullet">
    /// <item>
    /// 像素数超过 <see cref="MaxSingleImagePixels"/>：走网格，<b>尺寸原样返回</b>。网格允许瓦片盖出图外
    /// （解码端按声明尺寸裁掉），所以既不改分辨率、也不改长宽比——对 360 全景尤其重要。
    /// </item>
    /// <item>
    /// 否则走单张编码：把<b>宽度</b>对齐到 <see cref="WidthAlignment"/> 的倍数（不足一个对齐单位时向上取），
    /// 高度不动（理由见 <see cref="WidthAlignment"/>）。
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// 本方法与 <see cref="Encode"/> 共用 <see cref="MaxSingleImagePixels"/> 判断，
    /// 所以"缩放到哪个尺寸"与"编码时走哪条路"不会打架。
    /// </para>
    /// </remarks>
    public static (uint Width, uint Height) GetTargetSize(uint width, uint height)
    {
        if (UsesGrid(width, height))
        {
            return (width, height);
        }

        uint aligned = width / WidthAlignment * WidthAlignment;
        // 宽度不足一个对齐单位时向下对齐会得到 0，此时向上取到 16：编码器要求宽度必须是 16 的倍数，
        // 给不出合规尺寸就只能被它拒绝（这种尺寸的图不会出现在照片瘦身里，这里只是把契约补完整）
        return (aligned > 0 ? aligned : WidthAlignment, height);
    }

    /// <summary>
    /// 把一张 RGB24 图像编码为 HEIC 文件。编码方式（单张或网格）按像素数自动选择。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 调用方应先用 <see cref="GetTargetSize"/> 取得目标尺寸并精确缩放到该尺寸——本方法不会替调用方缩放，
    /// 而单张编码要求宽度是 <see cref="WidthAlignment"/> 的倍数（网格对尺寸没有要求）。
    /// </para>
    /// <para>
    /// <b>并发数完全由调用方（<c>Config.Thread</c>）决定，本类不加闸门。</b>这里曾有一个
    /// <c>SemaphoreSlim(2,2)</c>，理由是"避免 x265 自身的线程数与调用方线程数相乘"。2026-10-01 实测后移除：
    /// <list type="bullet">
    /// <item>
    /// 它挡不住内存：调用方是在进入本方法<b>之前</b>就按 <c>Config.Thread</c> 分配好整幅 RGB 拷贝的
    /// （10000x5000 时约 150 MB 一份），缓冲早已驻留，闸门只不过让它们排队干等。
    /// </item>
    /// <item>
    /// 它挡住的是并行度：网格路径单张图的实测并行度只有 2.4x（512 的瓦片对 x265 太小，WPP 受 CTU 行数限制，
    /// 且瓦片是串行编码的），必须靠多张图并发才能填满 CPU。实测 18MPix 网格图在 1/2/4/8 张并发下的吞吐为
    /// 1.1/2.2/4.3/6.1 MPix/s（近线性），而闸门为 2 时 8 线程设置下 CPU 只有 50%。
    /// </item>
    /// </list>
    /// 单张路径（≤1600 万像素）一张图自己就能用掉十几个线程，并发收益小，但也未见反效果。
    /// </para>
    /// </remarks>
    /// <param name="rgb24">按行紧密排列的 RGB24 像素数据。</param>
    /// <param name="width">像素宽。</param>
    /// <param name="height">像素高。</param>
    /// <param name="quality">有损质量（0-100，与 JPEG 的质量刻度不可比）。</param>
    /// <param name="exifWithPrefix">EXIF 载荷，<b>须保留 <c>Exif\0\0</c> 前缀</b>；无则传 null。</param>
    /// <param name="iccProfile">ICC 色彩配置；无则传 null。</param>
    /// <param name="xmp">XMP 元数据；无则传 null。</param>
    /// <param name="outputPath">输出文件路径（支持中文等非 ASCII 路径）。</param>
    /// <param name="tileSize">
    /// 网格编码的瓦片边长，默认 <see cref="DefaultTileSize"/>；仅在走网格时生效，取值必须是
    /// <see cref="TileSizes"/> 之一（见那里的注释：瓦片也是独立的 HEVC 图，受同一像素上限约束）。
    /// </param>
    /// <param name="preset">
    /// x265 的 preset 名（见 <see cref="Presets"/>），默认 <see cref="DefaultPreset"/>；传 null 或空串表示
    /// 不设置、沿用 libheif 的默认（<c>slow</c>）。
    /// </param>
    /// <param name="ct">取消令牌：进入编码前检查一次；编码过程本身不可中断。</param>
    public static void Encode(byte[] rgb24, int width, int height, int quality,
        byte[] exifWithPrefix, byte[] iccProfile, byte[] xmp, string outputPath,
        int tileSize = DefaultTileSize, string preset = DefaultPreset, CancellationToken ct = default)
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

        if (!IsAvailable)
        {
            throw new InvalidOperationException($"HEIC 编码不可用：{UnavailableReason}");
        }

        // 单张编码对宽度有硬要求（见 WidthAlignment）：违反的话不会报错，只会写出色度损坏的画面，
        // 而那种损坏在桌面工具上照常能读出来、只有手机相册看得见。所以这里显式挡住，
        // 而不是把它留给"调用方记得先调 GetTargetSize"这个约定。
        bool useGrid = UsesGrid(width, height);
        if (!useGrid && width % WidthAlignment != 0)
        {
            throw new ArgumentException(
                $"单张编码要求宽度是 {WidthAlignment} 的倍数（当前 {width}）：请先按 GetTargetSize 的结果缩放",
                nameof(width));
        }

        // 瓦片同样受色度对齐约束（理由同 WidthAlignment）：非 16 倍数时 libheif 会写出色度损坏的瓦片，
        // 而且和"单张宽度"一样是静默的。取值限制在 <see cref="TileSizes"/>（都是 16 的倍数，且像素数
        // 远低于单张上限），不给调用方留下"挑一个没验证过、甚至越过单张上限的尺寸"的口子。
        if (useGrid && !TileSizes.Contains(tileSize))
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize), tileSize,
                $"瓦片边长只能是 {string.Join("/", TileSizes)}（原因见 TileSizes 的注释）");
        }

        ct.ThrowIfCancellationRequested();

        if (useGrid)
        {
            EncodeTiledCore(rgb24, width, height, quality, exifWithPrefix, iccProfile, xmp, outputPath, tileSize,
                preset);
        }
        else
        {
            EncodeCore(rgb24, width, height, quality, exifWithPrefix, iccProfile, xmp, outputPath, preset);
        }
    }

    /// <summary>是否该走网格：像素数超过单张上限（见 <see cref="MaxSingleImagePixels"/>）。</summary>
    private static bool UsesGrid(long width, long height)
    {
        return width * height > MaxSingleImagePixels;
    }

    /// <summary>按调用方的设置配置 x265 编码器（单张与网格两条路径共用）。</summary>
    private static void ConfigureEncoder(nint encoder, int quality, string preset)
    {
        LibHeifNative.ThrowIfFailed(LibHeifNative.heif_encoder_set_lossy_quality(encoder, quality), "设置质量");

        // 不设置时 libheif 的 x265 插件用的是 slow（最慢档）+ tune=ssim：实测 18MPix 网格图在 slow 下要
        // 14.3 秒，换成 fast 只要 5.9 秒，而产物体积几乎不变（7352 KB → 7358 KB）。这个默认值是
        // libheif 定的，不是我们选的，所以显式设置比"留着不设"更可控。
        if (!string.IsNullOrEmpty(preset))
        {
            LibHeifNative.ThrowIfFailed(
                LibHeifNative.heif_encoder_set_parameter_string(encoder, "preset", preset),
                $"设置编码档位 preset={preset}");
        }
    }

    /// <summary>单张编码：整幅图作为一个 HEVC 图像项。</summary>
    private static void EncodeCore(byte[] rgb24, int width, int height, int quality,
        byte[] exifWithPrefix, byte[] iccProfile, byte[] xmp, string outputPath, string preset)
    {
        nint context = 0, encoder = 0, image = 0, options = 0, handle = 0;
        try
        {
            context = LibHeifNative.heif_context_alloc();
            if (context == 0)
            {
                throw new InvalidOperationException("heif_context_alloc 返回空指针");
            }

            LibHeifNative.ThrowIfFailed(
                LibHeifNative.heif_context_get_encoder_for_format(context, LibHeifNative.HeifCompressionHevc, out encoder),
                "获取 HEVC 编码器");
            ConfigureEncoder(encoder, quality, preset);

            LibHeifNative.ThrowIfFailed(
                LibHeifNative.heif_image_create(width, height, LibHeifNative.HeifColorspaceRgb,
                    LibHeifNative.HeifChromaInterleavedRgb, out image), "创建图像");
            LibHeifNative.ThrowIfFailed(
                LibHeifNative.heif_image_add_plane(image, LibHeifNative.HeifChannelInterleaved, width, height, 8),
                "分配像素平面");

            nint plane = LibHeifNative.heif_image_get_plane(image, LibHeifNative.HeifChannelInterleaved, out int stride);
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
                LibHeifNative.ThrowIfFailed(
                    LibHeifNative.heif_image_set_raw_color_profile(image, "prof", iccProfile, (nuint)iccProfile.Length),
                    "写入 ICC 色彩配置");
            }

            options = LibHeifNative.heif_encoding_options_alloc();
            LibHeifNative.ThrowIfFailed(
                LibHeifNative.heif_context_encode_image(context, image, encoder, options, out handle), "编码");

            // 元数据必须在 encode 之后写入
            if (exifWithPrefix is { Length: > 0 })
            {
                LibHeifNative.ThrowIfFailed(
                    LibHeifNative.heif_context_add_exif_metadata(context, handle, exifWithPrefix, exifWithPrefix.Length),
                    "写入 EXIF");
            }

            if (xmp is { Length: > 0 })
            {
                LibHeifNative.ThrowIfFailed(
                    LibHeifNative.heif_context_add_XMP_metadata(context, handle, xmp, xmp.Length), "写入 XMP");
            }

            LibHeifNative.ThrowIfFailed(LibHeifNative.heif_context_write_to_file(context, outputPath), "写出文件");
        }
        finally
        {
            if (handle != 0) LibHeifNative.heif_image_handle_release(handle);
            if (options != 0) LibHeifNative.heif_encoding_options_free(options);
            if (image != 0) LibHeifNative.heif_image_release(image);
            if (encoder != 0) LibHeifNative.heif_encoder_release(encoder);
            if (context != 0) LibHeifNative.heif_context_free(context);
        }
    }

    /// <summary>
    /// 网格编码：把图像切成若干块 <see cref="DefaultTileSize"/>（或其可选值）见方的瓦片分别编码，容器里再拼回整张图。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 手机的硬件 HEVC 解码器对<b>单张</b> HEVC 图有尺寸上限：真机实测小米相册能开 4096x4104（1681 万像素）
    /// 的单张图，但 6000x3000（1800 万像素）就打不开。手机相机自己拍的 5000 万像素照片之所以能显示，
    /// 是因为它存成了 192 块 512x512 的网格（相机原片的 item 结构：grid x1 + hvc1 x192），
    /// 解码器每次只需要解 512x512。
    /// </para>
    /// <para>
    /// <b>关键：瓦片可以盖出图外，所以这里不缩放、不量化尺寸。</b>
    /// ISO/IEC 23008-12 §6.6.2.3.1 只要求瓦片<b>盖满</b>画布（<c>tile_width*columns ≥ output_width</c>、
    /// <c>tile_height*rows ≥ output_height</c>，是 <b>≥ 不是等号</b>），重建时在右侧和底部裁掉多余部分；
    /// 另要求<b>所有瓦片同尺寸</b>。libheif 的实现一直如此：判据是 <c>&lt;</c>，从未要求整除，
    /// 只有"盖不满"才报 <c>Grid tiles do not cover whole image</c>。
    /// 于是做法是：行列数向上取整、每块瓦片都是整块 <see cref="DefaultTileSize"/>，越出图外的像素用
    /// <b>边缘像素复制</b>填满，由解码端裁掉——输出的分辨率与长宽比因此完全不变。
    /// （反例警示：把最后一行/列做成小瓦片会让 libheif 报 <c>Grid tiles have different sizes</c>，
    /// 以前据此得出的"尺寸必须是瓦片边长的整数倍"是错误结论。）
    /// </para>
    /// <para>
    /// <b>约束（改动本方法前务必先读）：</b>
    /// <list type="number">
    /// <item>瓦片边长必须是 16 的倍数：同 <see cref="WidthAlignment"/> 的色度问题。512 既满足这条，又与相机一致。</item>
    /// <item>
    /// 网格的输入尺寸可以是任意值，<b>包括奇数</b>：色度问题出在"编码单张图的宽度"上，而这里每块瓦片都是
    /// 512 宽，声明尺寸只影响解码端的裁剪。MIAF（ISO/IEC 23000-22 7.3.11.4.2）要求网格输出尺寸是色度采样的
    /// 倍数，libheif 自己也没执行（源码里挂着 TODO）；2026-10-01 真机实测声明 9151x2698（奇数宽）与
    /// 3523x4650 都能正常打开，所以这里不取偶——取偶会平白多出边缘复制的一列。
    /// </item>
    /// <item>
    /// 色彩配置只取自<b>第 0 块瓦片</b>（libheif 仅在 <c>tile_x==0 且 tile_y==0</c> 时收集 colr 属性），
    /// 所以 ICC 只挂在第一块上，不是每块都挂。
    /// </item>
    /// <item>ffmpeg 不支持 HEIF 网格拼接，对这种文件只会解出单块瓦片且不报错，验证必须用 libheif。</item>
    /// </list>
    /// </para>
    /// </remarks>
    private static void EncodeTiledCore(byte[] rgb24, int width, int height, int quality,
        byte[] exifWithPrefix, byte[] iccProfile, byte[] xmp, string outputPath, int tileSize, string preset)
    {
        // 向上取整：瓦片盖住整图、允许超出，由解码端按声明尺寸裁掉
        int columns = (width + tileSize - 1) / tileSize;
        int rows = (height + tileSize - 1) / tileSize;
        if (columns > MaxTilesPerSide || rows > MaxTilesPerSide)
        {
            throw new ArgumentException(
                $"网格规模 {columns}x{rows} 超过单方向 {MaxTilesPerSide} 块的上限（HEIF 网格结构的行列字段只有一个字节）",
                nameof(width));
        }

        nint context = 0, encoder = 0, options = 0, gridHandle = 0;
        try
        {
            context = LibHeifNative.heif_context_alloc();
            if (context == 0)
            {
                throw new InvalidOperationException("heif_context_alloc 返回空指针");
            }

            LibHeifNative.ThrowIfFailed(
                LibHeifNative.heif_context_get_encoder_for_format(context, LibHeifNative.HeifCompressionHevc, out encoder),
                "获取 HEVC 编码器");
            ConfigureEncoder(encoder, quality, preset);

            options = LibHeifNative.heif_encoding_options_alloc();
            // 这里传的是真实整图尺寸（不取整到瓦片倍数、也不取偶）；参数是「列数、行数」，不是瓦片宽高，
            // 瓦片尺寸由解码端从第 0 块瓦片反推
            LibHeifNative.ThrowIfFailed(
                LibHeifNative.heif_context_add_grid_image(context, (uint)width, (uint)height,
                    (uint)columns, (uint)rows, options, out gridHandle), "创建网格");

            int sourceRowBytes = width * RgbBytesPerPixel;
            int tileRowBytes = tileSize * RgbBytesPerPixel;
            // 补边用一条复用的行缓冲；整块落在图内的瓦片也走它（多一次 1.5 KB 拷贝，
            // 相比每块瓦片一次 HEVC 编码可以忽略），省得在一个循环里写两套拷贝代码
            byte[] tileRow = new byte[tileRowBytes];

            for (int row = 0; row < rows; row++)
            {
                int y0 = row * tileSize;
                // 因 rows 向上取整，每块瓦片至少有一行真实像素
                int realRowCount = Math.Min(tileSize, height - y0);

                for (int column = 0; column < columns; column++)
                {
                    int x0 = column * tileSize;
                    // 因 columns 向上取整，每块瓦片至少有一列真实像素
                    int realWidth = Math.Min(tileSize, width - x0);
                    int realWidthBytes = realWidth * RgbBytesPerPixel;
                    int lastPixel = (realWidth - 1) * RgbBytesPerPixel;

                    nint tile = 0;
                    try
                    {
                        LibHeifNative.ThrowIfFailed(
                            LibHeifNative.heif_image_create(tileSize, tileSize, LibHeifNative.HeifColorspaceRgb,
                                LibHeifNative.HeifChromaInterleavedRgb, out tile), "创建瓦片");
                        LibHeifNative.ThrowIfFailed(
                            LibHeifNative.heif_image_add_plane(tile, LibHeifNative.HeifChannelInterleaved,
                                tileSize, tileSize, 8), "分配瓦片平面");

                        nint plane = LibHeifNative.heif_image_get_plane(tile, LibHeifNative.HeifChannelInterleaved,
                            out int stride);
                        if (plane == 0)
                        {
                            throw new InvalidOperationException("heif_image_get_plane 返回空指针");
                        }

                        if (stride < tileRowBytes)
                        {
                            throw new InvalidOperationException($"瓦片行距（{stride}）小于一行的字节数（{tileRowBytes}）");
                        }

                        for (int y = 0; y < tileSize; y++)
                        {
                            if (y < realRowCount)
                            {
                                long sourceOffset = (long)(y0 + y) * sourceRowBytes + (long)x0 * RgbBytesPerPixel;
                                Buffer.BlockCopy(rgb24, (int)sourceOffset, tileRow, 0, realWidthBytes);

                                // 横向越界：把该行最后一个真实像素复制到行尾
                                for (int x = realWidth; x < tileSize; x++)
                                {
                                    int target = x * RgbBytesPerPixel;
                                    tileRow[target] = tileRow[lastPixel];
                                    tileRow[target + 1] = tileRow[lastPixel + 1];
                                    tileRow[target + 2] = tileRow[lastPixel + 2];
                                }
                            }
                            // 纵向越界时不重取像素：缓冲里留着的就是最后一行真实像素（y 递增，故必然已填过）

                            Marshal.Copy(tileRow, 0, nint.Add(plane, y * stride), tileRowBytes);
                        }

                        // ICC 只挂第一块：libheif 仅在 tile_x==0 且 tile_y==0 时把 colr 属性收集到网格 item 上
                        if (column == 0 && row == 0 && iccProfile is { Length: > 0 })
                        {
                            LibHeifNative.ThrowIfFailed(
                                LibHeifNative.heif_image_set_raw_color_profile(tile, "prof", iccProfile,
                                    (nuint)iccProfile.Length),
                                "写入 ICC 色彩配置");
                        }

                        LibHeifNative.ThrowIfFailed(
                            LibHeifNative.heif_context_add_image_tile(context, gridHandle, (uint)column, (uint)row,
                                tile, encoder), $"加入瓦片 {column},{row}");
                    }
                    finally
                    {
                        // 用完立刻释放：add_image_tile 内部是同步完成编码的
                        //（libheif 的 ImageItem_Grid::add_image_tile 里直接调用 encode_image），不需要留到写盘。
                        // 若把整张网格的瓦片都保活，1.1 亿像素会切成 400 多块、多占 300 MB 以上。
                        // 注意必须放在 finally 里——创建成功但后续任一步抛异常时，这块瓦片同样要释放。
                        if (tile != 0)
                        {
                            LibHeifNative.heif_image_release(tile);
                        }
                    }
                }
            }

            // 元数据同样要挂在网格的 handle 上
            if (exifWithPrefix is { Length: > 0 })
            {
                LibHeifNative.ThrowIfFailed(
                    LibHeifNative.heif_context_add_exif_metadata(context, gridHandle, exifWithPrefix, exifWithPrefix.Length),
                    "写入 EXIF");
            }

            if (xmp is { Length: > 0 })
            {
                LibHeifNative.ThrowIfFailed(
                    LibHeifNative.heif_context_add_XMP_metadata(context, gridHandle, xmp, xmp.Length), "写入 XMP");
            }

            LibHeifNative.ThrowIfFailed(LibHeifNative.heif_context_write_to_file(context, outputPath), "写出文件");
        }
        finally
        {
            if (gridHandle != 0) LibHeifNative.heif_image_handle_release(gridHandle);
            if (options != 0) LibHeifNative.heif_encoding_options_free(options);
            if (encoder != 0) LibHeifNative.heif_encoder_release(encoder);
            if (context != 0) LibHeifNative.heif_context_free(context);
        }
    }
}
