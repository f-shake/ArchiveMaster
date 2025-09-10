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
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Services
{
    public class PhotoSlimmingService(AppConfig appConfig) : TwoStepServiceBase<PhotoSlimmingConfig>(appConfig)
    {
        private ConcurrentBag<string> errorMessages;
        private Regex rCompress;
        private Regex rCopy;

        public enum TaskType
        {
            Compress,
            Copy,
            Delete
        }

        public SlimmingFilesInfo CompressFiles { get; private set; }

        public SlimmingFilesInfo CopyFiles { get; private set; }

        public SlimmingFilesInfo DeleteFiles { get; private set; }

        public IReadOnlyCollection<string> ErrorMessages => errorMessages;

        public override Task ExecuteAsync(CancellationToken ct)
        {
            return Task.Run(() =>
            {
                if (Config.ClearAllBeforeRunning)
                {
                    if (Directory.Exists(Config.DistDir))
                    {
                        FileHelper.DeleteByConfig(Config.DistDir);
                    }
                }

                if (!Directory.Exists(Config.DistDir))
                {
                    Directory.CreateDirectory(Config.DistDir);
                }

                Clear(ct);
                Compress(ct);
                Copy(ct);
            }, ct);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return CompressFiles.ProcessingFiles
                .Concat(CopyFiles.ProcessingFiles)
                .Concat(DeleteFiles.ProcessingFiles)
                .Cast<SimpleFileInfo>();
        }

        public override Task InitializeAsync(CancellationToken ct)
        {
            rCopy = new Regex(@$"\.({string.Join('|', Config.CopyDirectlyExtensions)})$", RegexOptions.IgnoreCase);
            rCompress = new Regex(@$"\.({string.Join('|', Config.CompressExtensions)})$", RegexOptions.IgnoreCase);

            CompressFiles = new SlimmingFilesInfo(Config.SourceDir);
            CopyFiles = new SlimmingFilesInfo(Config.SourceDir);
            DeleteFiles = new SlimmingFilesInfo(Config.SourceDir);
            errorMessages = new ConcurrentBag<string>();

            return Task.Run(() =>
            {
                SearchCopyingAndCompressingFiles(ct);
                SearchDeletingFiles(ct);
            }, ct);
        }

        private void Clear(CancellationToken ct)
        {
            TryForFiles(DeleteFiles.ProcessingFiles, (file, s) =>
            {
                NotifyMessage($"（第一步，共三步）正在删除{s.GetFileNumberMessage()}：{file.Name}");

                if (file.IsDir)
                {
                    FileHelper.DeleteByConfig(file.Path);
                }
                else
                {
                    FileHelper.DeleteByConfig(file.Path);
                }
            }, ct, FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileNumberProgress().Build());
        }

        private void Compress(CancellationToken ct)
        {
            TryForFiles(CompressFiles.ProcessingFiles, (file, s) =>
            {
                NotifyMessage($"（第二步，共三步）正在压缩{s.GetFileNumberMessage()}：{file.Name}");
                string distPath = GetDistPath(file.Path, Config.OutputFormat, out _);
                if (File.Exists(distPath))
                {
                    FileHelper.DeleteByConfig(distPath);
                }

                string dir = Path.GetDirectoryName(distPath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                Console.OutputEncoding = System.Text.Encoding.Unicode;

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
                    image.Write(distPath);
                }

                File.SetLastWriteTime(distPath, file.Time);

                FileInfo distFile = new FileInfo(distPath);
                if (distFile.Length > file.Length)
                {
                    File.Copy(file.Path, distPath, true);
                }
            }, ct, FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileLengthProgress()
                .WithMultiThreads(Config.Thread).Catch((file, ex) =>
                {
                    errorMessages.Add($"压缩 {Path.GetRelativePath(Config.SourceDir, file.Path)} 失败：{ex.Message}");
                }).Build());
        }

        private void Copy(CancellationToken ct)
        {
            TryForFiles(CopyFiles.ProcessingFiles, (file, s) =>
            {
                NotifyMessage($"（第三步，共三步）正在复制{s.GetFileNumberMessage()}：{file.Name}");

                string distPath = GetDistPath(file.Path, null, out string subPath);
                if (File.Exists(distPath))
                {
                    FileHelper.DeleteByConfig(distPath);
                }

                string dir = Path.GetDirectoryName(distPath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.Copy(file.Path, distPath, true);
            }, ct, FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileLengthProgress()
                .WithMultiThreads(Config.Thread).Catch((file, ex) =>
                {
                    errorMessages.Add($"压缩 {Path.GetRelativePath(Config.SourceDir, file.Path)} 失败：{ex.Message}");
                }).Build());
        }

        private string GetDistPath(string sourceFileName, string newExtension, out string subPath)
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

            subPath = Path.Combine(subDir, fileNameWithoutExtension + extension);

            return Path.Combine(Config.DistDir, subPath);
        }

        private bool NeedProcess(TaskType type, SimpleFileInfo file)
        {
            if (type is TaskType.Delete)
            {
                return true;
            }

            if (!Config.SkipIfExist)
            {
                return true;
            }


            var distFile =
                new FileInfo(GetDistPath(file.Path, type is TaskType.Copy ? null : Config.OutputFormat, out _));

            if (distFile.Exists && (type is TaskType.Compress ||
                                    file.Length == distFile.Length && file.Time == distFile.LastWriteTime))
            {
                return false;
            }

            return true;
        }

        private void SearchCopyingAndCompressingFiles(CancellationToken ct)
        {
            NotifyProgressIndeterminate();
            NotifyMessage("正在搜索目录");
            var files = new DirectoryInfo(Config.SourceDir)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .ApplyFilter(ct, Config.Filter)
                .Select(p => new SimpleFileInfo(p, Config.SourceDir));

            TryForFiles(files, (file, s) =>
            {
                NotifyMessage($"正在查找文件{s.GetFileNumberMessage()}");

                if (rCompress.IsMatch(file.Name))
                {
                    if (NeedProcess(TaskType.Compress, file))
                    {
                        CompressFiles.Add(file);
                    }
                    else
                    {
                        CompressFiles.AddSkipped(file);
                    }
                }
                else if (rCopy.IsMatch(file.Name))
                {
                    if (NeedProcess(TaskType.Copy, file))
                    {
                        CopyFiles.Add(file);
                    }
                    else
                    {
                        CopyFiles.AddSkipped(file);
                    }
                }
            }, ct, FilesLoopOptions.DoNothing());
        }

        private void SearchDeletingFiles(CancellationToken ct)
        {
            if (!Directory.Exists(Config.DistDir))
            {
                return;
            }

            NotifyProgressIndeterminate();
            NotifyMessage("正在筛选需要删除的文件");
            ISet<string> desiredDistFiles = CopyFiles.SkippedFiles
                .Select(file => GetDistPath(file.Path, null, out _))
                .Concat(CompressFiles.SkippedFiles
                    .Select(file => GetDistPath(file.Path, Config.OutputFormat, out _)))
                .ToFrozenSet();

            foreach (var file in Directory
                         .EnumerateFiles(Config.DistDir, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                if (!desiredDistFiles.Contains(file))
                {
                    DeleteFiles.Add(new SimpleFileInfo(new FileInfo(file), Config.DistDir));
                }
            }

            NotifyMessage("正在查找需要删除的文件夹");
            ISet<string> desiredDistFolders = desiredDistFiles.Select(Path.GetDirectoryName).ToHashSet();
            foreach (var leafDir in desiredDistFolders.ToList())
            {
                string d = Path.GetDirectoryName(leafDir);
                while (d.Length > Config.DistDir.Length)
                {
                    desiredDistFolders.Add(d);
                    d = Path.GetDirectoryName(d);
                }
            }

            desiredDistFolders = desiredDistFolders.ToFrozenSet();
            foreach (var dir in Directory
                         .EnumerateDirectories(Config.DistDir, "*", SearchOption.AllDirectories))
            {
                if (!desiredDistFolders.Contains(dir))
                {
                    DeleteFiles.Add(new SimpleFileInfo(new DirectoryInfo(dir), Config.DistDir));
                }
            }
        }
    }
}