using ArchiveMaster.Configs;
using System.Collections.Concurrent;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Directory = System.IO.Directory;
using ImageMagick;
using Serilog;
using System.Diagnostics;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.IO;

namespace ArchiveMaster.Services
{
    public class PhotoSlimmingService(AppConfig appConfig) : TwoStepServiceBase<PhotoSlimmingConfig>(appConfig)
    {
        public List<SlimmingFilesInfo> Files { get; private set; }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            return Task.Run(() =>
            {
                // 三个待处理列表都从勾选集合里筛，分母取三者长度之和（旧代码列表未筛勾选，取消勾选后分母会偏小）。
                var checkedFiles = Files.CheckedOnly().ToList();
                var deletingFiles = checkedFiles.Where(p => p.SlimmingTaskType == SlimmingTaskType.Delete).ToList();
                var copyingFiles = checkedFiles.Where(p => p.SlimmingTaskType == SlimmingTaskType.Copy).ToList();
                var compressingFiles = checkedFiles.Where(p => p.SlimmingTaskType == SlimmingTaskType.Compress).ToList();
                var count = deletingFiles.Count + copyingFiles.Count + compressingFiles.Count;

                // 无事可做（含"可处理项全部被取消勾选"）时直接返回，且必须在清理目标目录之前，否则会清掉上一轮输出却不再生成。
                if (count == 0)
                {
                    NotifyMessage("没有需要处理的文件");
                    return;
                }

                if (Config.ClearAllBeforeRunning)
                {
                    if (Directory.Exists(Config.DistDir))
                    {
                        FileHelper.DeleteByConfig(Config.DistDir, "照片瘦身_清理目标目录");
                    }
                }

                Directory.CreateDirectory(Config.DistDir);

                //第一步：删除
                TryForFiles(deletingFiles, (file, s) =>
                {
                    int index = s.FileIndex;
                    NotifyMessageAndProgress(index, count, "删除", file);
                    FileHelper.DeleteByConfig(file.Path, "照片瘦身_需要删除的文件");
                }, ct, FilesLoopOptions.Builder().AutoApplyStatus().Build());
                //第二步：复制
                TryForFiles(copyingFiles, (file, s) =>
                {
                    int index = s.FileIndex + deletingFiles.Count;
                    NotifyMessageAndProgress(index, count, "复制", file);
                    Copy(file);
                }, ct, FilesLoopOptions.Builder().AutoApplyStatus().Build());
                //第三步：压缩
                TryForFiles(compressingFiles, (file, s) =>
                {
                    int index = s.FileIndex + deletingFiles.Count + copyingFiles.Count;
                    NotifyMessageAndProgress(index, count, "压缩", file);
                    Compress(file, ct);
                }, ct, FilesLoopOptions.Builder()
                    .AutoApplyStatus()
                    .WithMultiThreads(Config.Thread)
                    .Build());

                void NotifyMessageAndProgress(int index, int count, string operation, SlimmingFilesInfo file)
                {
                    NotifyMessage($"正在{operation}（{index}/{count}）：{file.Name}");
                    NotifyProgress(index, count);
                }
            }, ct);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            // 交给框架的是非 Skip 的行（不筛勾选）：全部是跳过项时框架会照旧提示"结果为空"并禁止开始。
            return Files.Where(p => p.SlimmingTaskType != SlimmingTaskType.Skip);
        }

        public override async Task InitializeAsync(CancellationToken ct)
        {
            Files = new List<SlimmingFilesInfo>();

            await Task.Run(() =>
            {
                SearchCopyingAndCompressingFiles(ct);
                SearchDeletingFiles(ct);
            }, ct);

            // 保留 Skip 项仅供界面展示（灰色"跳过"、底部统计）：它不进三个待处理列表，也不交给框架。
            Files = Files.OrderBy(p => p.SlimmingTaskType).ToList();
        }


        private void Compress(SlimmingFilesInfo file, CancellationToken ct)
        {
            if (file.DistFile.ExistsFile)
            {
                FileHelper.DeleteByConfig(file.DistFile.Path, "照片瘦身_被替换的压缩后文件");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(file.DistFile.Path));

            using (MagickImage image = new MagickImage(file.Path))
            {
                bool portrait = image.Height > image.Width;
                uint longSide = portrait ? image.Height : image.Width;
                uint shortSide = portrait ? image.Width : image.Height;
                uint width = image.Width;
                uint height = image.Height;

                if (longSide > Config.MaxLongSize || shortSide > Config.MaxShortSize)
                {
                    double ratio = longSide > Config.MaxLongSize ? 1.0 * Config.MaxLongSize / longSide : 1;
                    ratio = Math.Min(ratio, shortSide > Config.MaxShortSize ? 1.0 * Config.MaxShortSize / shortSide : 1);
                    width = (uint)(image.Width * ratio);
                    height = (uint)(image.Height * ratio);
                }

                bool useHeifGrid = false;
                if (IsHeifFormat(Config.CompressImageFormat))
                {
                    // 超过单张 HEVC 图的上限就改走网格编码（见 HeifEncoder.EncodeTiled 的注释）
                    useHeifGrid = (long)width * height > HeifSingleImageMaxPixels;
                    if (useHeifGrid)
                    {
                        if (TryFitToHeifGrid(width, height, out uint gridWidth, out uint gridHeight))
                        {
                            width = gridWidth;
                            height = gridHeight;
                        }
                        else
                        {
                            // 原尺寸不足一块瓦片，铺不满整图，只能用单张编码
                            useHeifGrid = false;
                        }
                    }

                    if (!useHeifGrid)
                    {
                        // 宽度对齐到 16 的倍数：libheif 在这个宽度是 16 的倍数时正常，否则编出来的图色度损坏
                        // （画面发灰、撕裂）。实测 200/520/1000/2100/4184/4200/4216/15000 宽全部损坏，
                        // 192/208/512/1008/4000/4096/4176/4192/4208/4224 宽全部正常；
                        // 与像素总数、质量、ICC、图像内容都无关（拿原图自己的像素重编一样坏）。
                        // 高度不需要对齐（4096x4104、4000x4200、6000x3000 都是好的），所以只改宽度。
                        // 其它格式不受此影响，故只在 HEIC/HEIF 分支里取整。
                        uint alignedWidth = width / HeifWidthAlignment * HeifWidthAlignment;
                        if (alignedWidth > 0)
                        {
                            width = alignedWidth;
                        }
                    }
                }

                // 尺寸真的变了才重采样，否则白白重编一遍像素
                if (width > 0 && height > 0 && (width != image.Width || height != image.Height))
                {
                    image.AdaptiveResize(width, height);
                }

                image.Quality = (uint)Config.Quality;

                // 缩放只改像素，EXIF/XMP 里的尺寸仍是源图的值（这两个 profile 是原样传递的），
                // 不同步就会出现"元数据写 17304x8652、像素实际 15000x7500"这类错位，详见 ImageMetadataHelper
                SyncDimensionMetadata(image, Config.CompressImageFormat, GetOutputMimeType(Config.CompressImageFormat));

                if (IsHeifFormat(Config.CompressImageFormat))
                {
                    WriteHeif(image, file.DistFile.Path, ct, useHeifGrid);
                }
                else
                {
                    // image.Write(file.DistFile.Path, Config.CompressImageFormat);
                    using (var stream = new FileStream(file.DistFile.Path, FileMode.Create))
                    {
                        image.Write(stream, Config.CompressImageFormat);
                    }
                }
            }

            File.SetLastWriteTime(file.DistFile.Path, file.Time);
        }

        /// <summary>
        /// HEIC/HEIF 走随包的 libheif 写出（Magick.NET 的 native 包里 libheif 未编入 HEVC 编码器，无法编码 HEIC），
        /// 其余格式仍由 Magick.NET 写出。
        /// </summary>
        private void WriteHeif(MagickImage image, string distPath, CancellationToken ct, bool useGrid)
        {
            if (!HeifEncoder.IsAvailable)
            {
                throw new InvalidOperationException(
                    $"输出格式 {Config.CompressImageFormat} 需要随包的 libheif，但当前不可用：{HeifEncoder.UnavailableReason}");
            }

            // 非 HEIC 分支交给 ImageMagick 写出，它会自行完成色彩空间转换与 alpha 处理；
            // 而这里是自己按 RGB 通道取字节，所以要先统一：
            //  · 非 sRGB（如 CMYK 源图）先转 sRGB，否则取到的是 C/M/Y/K 通道，颜色会错
            //  · 有 alpha 时关闭该通道（保留像素原值），与 JPEG 分支丢弃 alpha 的行为保持一致；
            //    注意不要用 AlphaOption.Remove——那会把透明区域合成到白底，与 JPEG 分支观感不同
            bool convertedToSrgb = false;
            if (image.ColorSpace != ColorSpace.sRGB)
            {
                image.ColorSpace = ColorSpace.sRGB;
                convertedToSrgb = true;
            }

            if (image.HasAlpha)
            {
                image.Alpha(AlphaOption.Off);
            }

            // 元数据在此之前的 SyncDimensionMetadata 里已经同步过尺寸；这里只负责取出来传递。
            // 特别注意：EXIF 必须保留 "Exif\0\0" 六字节前缀，否则小米等手机相册不显示 EXIF
            //（桌面工具 Pillow/Magick.NET 仍能读出，所以此点无法在本地验证）——详见 HeifEncoder 的注释。
            // ICC 与 XMP 仍是源图原样字节；EXIF 则是同步过尺寸后的重建结果（含补回的缩略图）。
            // 必须释放像素集合：它在 MagickImage 之外独立持有像素缓存，不释放则缓存落盘产生的
            // %TEMP%\magick-* 临时文件不会被删除（实测 8 张 50MP 图残留 1.49 GB），且每张泄漏一个
            byte[] rgb;
            using (IPixelCollection<ushort> pixels = image.GetPixels())
            {
                rgb = pixels.ToByteArray(PixelMapping.RGB);
            }

            byte[] exif = image.GetProfile("exif")?.ToByteArray();
            // 转换过色彩空间后原 ICC 已不再描述当前像素，故不再传递
            byte[] icc = convertedToSrgb ? null : image.GetColorProfile()?.ToByteArray();
            byte[] xmp = image.GetProfile("xmp")?.ToByteArray();

            if (useGrid)
            {
                HeifEncoder.EncodeTiled(rgb, (int)image.Width, (int)image.Height, Config.Quality,
                    (int)HeifTileSize, exif, icc, xmp, distPath, ct);
            }
            else
            {
                HeifEncoder.Encode(rgb, (int)image.Width, (int)image.Height, Config.Quality, exif, icc, xmp, distPath, ct);
            }
        }

        private static bool IsHeifFormat(MagickFormat format)
        {
            return format is MagickFormat.Heic or MagickFormat.Heif;
        }

        /// <summary>
        /// libheif 编码时宽度必须是 16 的倍数，否则输出的图色度损坏（详见 <see cref="Compress"/> 里的注释）。
        /// </summary>
        private const uint HeifWidthAlignment = 16;

        /// <summary>
        /// 网格编码的瓦片边长。必须是 16 的倍数（避开上面的色度损坏问题），
        /// 并与手机相机自己使用的瓦片尺寸一致（实测相机原片为 512x512）。
        /// </summary>
        /// <remarks>
        /// 缩小它可以降低量化带来的尺寸损失（每个方向的损失最多是瓦片边长减一个像素），代价是瓦片数按平方增长。
        /// 当前取 512 是因为这个尺寸已在真机上验证过；改动后需要重新做真机验证。
        /// </remarks>
        private const uint HeifTileSize = 512;

        /// <summary>
        /// 单张 HEVC 图的像素数上限，超过就改走网格编码。
        /// </summary>
        /// <remarks>
        /// 真机实测（小米相册）：单张 4096x4104（1681 万像素）能打开，6000x3000（1800 万像素）打不开，
        /// 故取 1600 万留余量。限制来自<b>像素总数</b>而不是单边长度——另一个对照是单张 8192x1024
        /// （839 万像素、长边 8192）能正常打开，所以长边不是瓶颈。
        /// 另注意这只是"单张图"的上限，不是照片分辨率上限：切成网格后一亿像素也能正常显示
        /// （手机相机自己拍的 5000 万像素照片就是 192 块 512x512 的网格）。
        /// </remarks>
        private const long HeifSingleImageMaxPixels = 16_000_000;

        /// <summary>
        /// 把目标尺寸收缩到瓦片边长的整数倍（libheif 的网格要求瓦片<b>完整均匀地铺满整图</b>，
        /// 不接受末行/末列更小），并按原始长宽比定行列数，尽量少变形。
        /// </summary>
        /// <remarks>
        /// 量化必然带来尺寸损失（每个方向最多损失"瓦片边长减一"个像素），这是网格机制的硬约束：
        /// 尺寸不能被瓦片边长整除时会写出解码端报 "Grid tiles do not cover whole image" 的文件。
        /// 之所以优先保住长宽比：2:1 的等距柱状全景必须精确 2:1，否则 360 播放器会拉伸渲染；
        /// 普通照片的长宽比变化在百分之几以内，肉眼不可见。
        /// </remarks>
        /// <returns>尺寸足够切成网格时返回 true；不足一块瓦片（会被放大）时返回 false，由调用方回退到单张编码。</returns>
        private static bool TryFitToHeifGrid(uint maxWidth, uint maxHeight, out uint width, out uint height)
        {
            width = 0;
            height = 0;
            if (maxWidth < HeifTileSize || maxHeight < HeifTileSize)
            {
                return false;
            }

            uint maxColumns = maxWidth / HeifTileSize;
            uint maxRows = maxHeight / HeifTileSize;

            // 以"行数排满"为起点、按长宽比定列数；列数被上限截断时必须**同步收缩行数**，
            // 否则行数不减会让长宽比失真（4000x8000 不收缩行数会得到 0.4667 而不是 0.5）
            uint rows = maxRows;
            uint columns = (uint)Math.Clamp(Math.Round((double)rows * maxWidth / maxHeight), 1, maxColumns);
            rows = (uint)Math.Clamp(Math.Round((double)columns * maxHeight / maxWidth), 1, maxRows);

            width = columns * HeifTileSize;
            height = rows * HeifTileSize;
            return true;
        }

        /// <summary>
        /// 把 EXIF / XMP 里记录的尺寸同步成当前像素尺寸，并修正 XMP 的 dc:format。
        /// 两段各自 try/catch：它们是互不依赖的两件事，EXIF 出问题不该连累 XMP 的修复。
        /// 任何失败都只记日志、保留原元数据——元数据不值得让整张图压不出来。
        /// </summary>
        private static void SyncDimensionMetadata(MagickImage image, MagickFormat format, string mimeType)
        {
            uint width = image.Width;
            uint height = image.Height;

            // 用字符串匹配而不是枚举分支：MagickFormat 里同一格式有多个别名
            //（Jpg=114 与 Jpeg=113 是两个不同成员，而配置默认值正是 Jpg），写成枚举分支会漏。
            // GetOutputMimeType 与 IsHeifFormat 也是有鉴于此才那么写的。
            string formatName = format.ToString().ToLowerInvariant();
            bool exifWorthSyncing = formatName is "jpg" or "jpeg" or "pjpeg" or "heic" or "heif";

            try
            {
                // 尺寸标签必须"存在且正确"，不能只改写已存在的：真机实测 MIUI 相册在这几个标签缺失时
                // 会显示错误的分辨率——8192x6144 显示成 32x24、4096x3072 显示成 16x12；带标签的那张全景显示正常。
                // 源图可能压根没有这几个标签（实测 Lightroom 导出的 DJI 图就没有），所以要新增。
                // 只在 JPEG/HEIC/HEIF 或源图本来就有 EXIF 时做：其它格式凭空多出一个只含尺寸的最小 EXIF 只是噪音。
                // 用 GetProfile（原始字节）而不是 GetExifProfile：前者不做解析，便宜得多。
                if (exifWorthSyncing || image.GetProfile("exif") != null)
                {
                    SyncExifSize(image, width, height);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Warning("同步 {Width}x{Height} 的 EXIF 尺寸失败，保留原 EXIF：{Message}",
                    width, height, ex.Message);
            }

            try
            {
                if (image.GetProfile("xmp")?.ToByteArray() is { Length: > 0 } xmp)
                {
                    byte[] fixedXmp = ImageMetadataHelper.SyncXmpDimensions(xmp, width, height, mimeType);
                    if (!ReferenceEquals(fixedXmp, xmp))
                    {
                        image.SetProfile(new ImageProfile("xmp", fixedXmp));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Warning("同步 {Width}x{Height} 的 XMP 尺寸失败，保留原 XMP：{Message}",
                    width, height, ex.Message);
            }
        }

        /// <summary>
        /// 让 EXIF 里的四个尺寸标签存在且等于当前像素尺寸。
        /// </summary>
        private static void SyncExifSize(MagickImage image, uint width, uint height)
        {
            // 先按"原始字节"取出：此时它还带着 Exif\0\0 前缀和完整的内嵌缩略图（IFD1）
            byte[] original = image.GetProfile("exif")?.ToByteArray();
            IExifProfile exif = image.GetExifProfile() ?? new ExifProfile();

            // 缩略图在原始载荷里的偏移／长度（相对整个载荷，含前缀）
            byte[] thumbnail = null;
            if (original != null && exif.ThumbnailLength is > 0 and <= int.MaxValue)
            {
                int start = (int)exif.ThumbnailOffset;
                int length = (int)exif.ThumbnailLength;
                if (start >= 0 && length > 0 && (long)start + length <= original.Length)
                {
                    thumbnail = new byte[length];
                    Array.Copy(original, start, thumbnail, 0, length);
                }
            }

            exif.SetValue(ExifTag.ImageWidth, new Number(width));
            exif.SetValue(ExifTag.ImageLength, new Number(height));
            exif.SetValue(ExifTag.PixelXDimension, new Number(width));
            exif.SetValue(ExifTag.PixelYDimension, new Number(height));

            // SetValue 会让 ExifProfile 重建整个 EXIF，而重建**不输出 IFD1（内嵌缩略图）**：
            // 实测 25,194 字节的源 EXIF 会缩到 1,104 字节、缩略图 JPEG 整个消失
            //（改之前 EXIF 是原始字节透传，缩略图是保留的）。Magick.NET 只有 RemoveThumbnail、没有 SetThumbnail，
            // 所以把刚切出来的缩略图补回末尾——见 ImageMetadataHelper.AppendExifThumbnail
            byte[] rebuilt = exif.ToByteArray();
            if (thumbnail != null)
            {
                rebuilt = ImageMetadataHelper.AppendExifThumbnail(rebuilt, thumbnail);
            }

            image.SetProfile(new ImageProfile("exif", rebuilt));
        }

        /// <summary>输出格式对应的 MIME 类型，用于写回 XMP 的 dc:format。</summary>
        private static string GetOutputMimeType(MagickFormat format)
        {
            // 用字符串而不是枚举分支：MagickFormat 里同一格式常有多个别名（Jpg/Jpeg/Pjpeg 等），
            // 这样写不会漏，也能对未知格式退化出一个合理的值
            string name = format.ToString().ToLowerInvariant();
            return name switch
            {
                "jpg" or "jpeg" or "pjpeg" => "image/jpeg",
                "tif" or "tiff" => "image/tiff",
                "heif" => "image/heif",
                _ => "image/" + name,
            };
        }


        private void Copy(SlimmingFilesInfo file)
        {
            if (file.DistFile.ExistsFile)
            {
                FileHelper.DeleteByConfig(file.DistFile.Path, "照片瘦身_被替换的复制后文件");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(file.DistFile.Path));

            File.Copy(file.Path, file.DistFile.Path);
        }


        private string GetDistPath(string sourceFileName, string newExtension)
        {
            char splitter = sourceFileName.Contains('\\') ? '\\' : '/';
            string subDir = Path.GetDirectoryName(Path.GetRelativePath(Config.SourceDir, sourceFileName));
            if (!Path.IsPathRooted(sourceFileName))
            {
                Debug.Assert(false);
            }

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFileName);
            string extension = Path.GetExtension(sourceFileName);

            if (Config.FileNameTemplate != PhotoSlimmingConfig.FileNamePlaceholder)
            {
                fileNameWithoutExtension = Config.FileNameTemplate.Replace(PhotoSlimmingConfig.FileNamePlaceholder,
                    fileNameWithoutExtension);
            }

            if (!string.IsNullOrEmpty(newExtension))
            {
                extension = $".{newExtension}";
            }

            int level = subDir.Count(c => c == splitter) + 1;

            if (level > Config.DeepestLevel)
            {
                string[] dirParts = subDir.Split(splitter);
                subDir = string.Join(splitter, dirParts[..Config.DeepestLevel]);
                fileNameWithoutExtension =
                    $"{string.Join(GlobalConfigs.Instance.FlattenPathSeparatorReplacement, dirParts[Config.DeepestLevel..])}{GlobalConfigs.Instance.FlattenPathSeparatorReplacement}{fileNameWithoutExtension}";
            }

            if (Config.FolderNameTemplate != PhotoSlimmingConfig.FolderNamePlaceholder && subDir.Length > 0)
            {
                string[] dirParts = subDir.Split(splitter);
                subDir = Path.Combine(dirParts.Select(p =>
                        Config.FolderNameTemplate.Replace(PhotoSlimmingConfig.FolderNamePlaceholder, p))
                    .ToArray());
            }

            return Path.Combine(Config.DistDir, subDir, fileNameWithoutExtension + extension);
        }


        private void SearchCopyingAndCompressingFiles(CancellationToken ct)
        {
            NotifyProgressIndeterminate();
            NotifyMessage("正在搜索目录");

            var compressFilterHelper =
                Config.CompressFilter.IsEnabled
                    ? new FileFilterHelper(Config.CompressFilter)
                    : null;
            var copyDirectlyFilterHelper = Config.CopyDirectlyFilter.IsEnabled
                ? new FileFilterHelper(Config.CopyDirectlyFilter)
                : null;
            var files = new DirectoryInfo(Config.SourceDir)
                .EnumerateSimpleFileInfos(ct)
                .ToList();

            TryForFiles(files, (file, s) =>
            {
                NotifyMessage($"正在查找文件{s.GetFileNumberMessage()}");

                SlimmingTaskType targetType;
                //判断该文件需要进行的操作（暂不考虑已存在文件需要跳过）
                if (compressFilterHelper != null && compressFilterHelper.IsMatched(file))
                {
                    targetType = SlimmingTaskType.Compress;
                }
                else if (copyDirectlyFilterHelper != null && copyDirectlyFilterHelper.IsMatched(file))
                {
                    targetType = SlimmingTaskType.Copy;
                }
                else
                {
                    return;
                }

                //目标文件，复制的话相同后缀名，压缩的话新的后缀名
                var newExtension = targetType is SlimmingTaskType.Copy
                    ? null
                    : Config.CompressImageFormat.ToString().ToLower();
                var distPath = GetDistPath(file.Path, newExtension);
                var distFile = new SimpleFileInfo(new FileInfo(distPath), Config.DistDir);

                //判断是否需要跳过
                if (Config.SkipIfExist)
                {
                    //文件存在、大小一致（若行为是复制）、修改时间一致，则跳过
                    if (distFile.ExistsFile
                        && file.Time == distFile.Time
                        && (targetType is SlimmingTaskType.Compress || file.Length == distFile.Length))
                    {
                        targetType = SlimmingTaskType.Skip;
                    }
                }

                var newFile = new SlimmingFilesInfo(file, targetType, distFile);
                Files.Add(newFile);
            }, ct, FilesLoopOptions.Builder().AutoApplyFileNumberProgress().Build());
        }

        private void SearchDeletingFiles(CancellationToken ct)
        {
            if (!Directory.Exists(Config.DistDir))
            {
                return;
            }

            NotifyProgressIndeterminate();
            NotifyMessage("正在筛选需要删除的文件");
            var desiredDistFiles = Files
                //.Where(p => p.SlimmingTaskType == SlimmingTaskType.Skip) //不能只检查跳过的，因为有一些可能因为文件被修改而不跳过，但文件也存在
                .Select(p => p.DistFile.Path)
                .ToFrozenSet();

            foreach (var file in new DirectoryInfo(Config.DistDir).EnumerateFiles("*",
                         FileEnumerateExtension.GetEnumerationOptions()))
            {
                ct.ThrowIfCancellationRequested();
                //如果期望的目标文件不包含该文件，则该文件需要被删除
                if (!desiredDistFiles.Contains(file.FullName))
                {
                    Files.Add(new SlimmingFilesInfo(file, Config.DistDir, SlimmingTaskType.Delete, null));
                }
            }

            //这部分有BUG，暂时就不要删除了
            // NotifyMessage("正在查找需要删除的文件夹");
            // ISet<string> desiredDistFolders = desiredDistFiles.Select(Path.GetDirectoryName).ToHashSet();
            // foreach (var leafDir in desiredDistFolders.ToList())
            // {
            //     string d = Path.GetDirectoryName(leafDir);
            //     while (d.Length > Config.DistDir.Length)
            //     {
            //         desiredDistFolders.Add(d);
            //         d = Path.GetDirectoryName(d);
            //     }
            // }
            //
            // desiredDistFolders = desiredDistFolders.ToFrozenSet();
            // foreach (var dir in Directory
            //              .EnumerateDirectories(Config.DistDir, "*", SearchOption.AllDirectories))
            // {
            //     if (!desiredDistFolders.Contains(dir))
            //     {
            //         DeleteFiles.Add(new SimpleFileInfo(new DirectoryInfo(dir), Config.DistDir));
            //     }
            // }
        }
    }
}