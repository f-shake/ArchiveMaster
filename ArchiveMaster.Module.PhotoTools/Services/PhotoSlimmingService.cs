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
                uint width = portrait ? image.Height : image.Width;
                uint height = portrait ? image.Width : image.Height;
                if (width > Config.MaxLongSize || height > Config.MaxShortSize)
                {
                    double ratio = width > Config.MaxLongSize ? 1.0 * Config.MaxLongSize / width : 1;
                    ratio = Math.Min(ratio, height > Config.MaxShortSize ? 1.0 * Config.MaxShortSize / height : 1);
                    width = (uint)(width * ratio);
                    height = (uint)(height * ratio);
                    if (portrait)
                    {
                        (width, height) = (height, width);
                    }

                    image.AdaptiveResize(width, height);
                }

                image.Quality = (uint)Config.Quality;

                if (IsHeifFormat(Config.CompressImageFormat))
                {
                    WriteHeif(image, file.DistFile.Path, ct);
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
        private void WriteHeif(MagickImage image, string distPath, CancellationToken ct)
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

            // 元数据取自源图、原样传递。
            // 特别注意：EXIF 必须保留 "Exif\0\0" 六字节前缀，否则小米等手机相册不显示 EXIF
            //（桌面工具 Pillow/Magick.NET 仍能读出，所以此点无法在本地验证）——详见 HeifEncoder 的注释。
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

            HeifEncoder.Encode(rgb, (int)image.Width, (int)image.Height, Config.Quality, exif, icc, xmp, distPath, ct);
        }

        private static bool IsHeifFormat(MagickFormat format)
        {
            return format is MagickFormat.Heic or MagickFormat.Heif;
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