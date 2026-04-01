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
                if (Config.ClearAllBeforeRunning)
                {
                    if (Directory.Exists(Config.DistDir))
                    {
                        FileHelper.DeleteByConfig(Config.DistDir, "照片瘦身_清理目标目录");
                    }
                }

                Directory.CreateDirectory(Config.DistDir);

                var totalFiles = Files.CheckedOnly();
                var deletingFiles = Files.Where(p => p.SlimmingTaskType == SlimmingTaskType.Delete).ToList();
                var copyingFiles = Files.Where(p => p.SlimmingTaskType == SlimmingTaskType.Copy).ToList();
                var compressingFiles = Files.Where(p => p.SlimmingTaskType == SlimmingTaskType.Compress).ToList();
                var count = totalFiles.Count();
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
                    Compress(file);
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
            return Files;
        }

        public override async Task InitializeAsync(CancellationToken ct)
        {
            Files = new List<SlimmingFilesInfo>();

            await Task.Run(() =>
            {
                SearchCopyingAndCompressingFiles(ct);
                SearchDeletingFiles(ct);
                Files = Files.Where(p => p.SlimmingTaskType != SlimmingTaskType.Skip).ToList();
            }, ct);

            Files = Files.OrderBy(p => p.SlimmingTaskType).ToList();
        }


        private void Compress(SlimmingFilesInfo file)
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
                image.Write(file.DistFile.Path, Config.CompressImageFormat);
            }

            File.SetLastWriteTime(file.DistFile.Path, file.Time);
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