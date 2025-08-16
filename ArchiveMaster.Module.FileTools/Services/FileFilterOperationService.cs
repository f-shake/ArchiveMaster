using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels.FileSystem;
using FilesTimeDirInfo = ArchiveMaster.ViewModels.FileSystem.FilesTimeDirInfo;
using FzLib.IO;

namespace ArchiveMaster.Services
{
    public class FileFilterOperationService(AppConfig appConfig)
        : TwoStepServiceBase<FileFilterOperationConfig>(appConfig)
    {
        public List<FileFilterOperationFileInfo> Files { get; set; }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return Files;
        }

        public override Task ExecuteAsync(CancellationToken token)
        {
            FilesLoopOptions loopOptions = Config.Type switch
            {
                FileFilterOperationType.Copy or FileFilterOperationType.Move or FileFilterOperationType.Hash =>
                    FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileLengthProgress().Build(),
                _ =>
                    FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileNumberProgress().Build(),
            };
            return Task.Run(() =>
            {
                HashSet<string> usedPaths = new HashSet<string>();
                return TryForFilesAsync(Files.CheckedOnly().ToList(), async (file, s) =>
                    {
                        string targetPath = null;
                        if (Config.Type is FileFilterOperationType.Copy or FileFilterOperationType.Move
                            or FileFilterOperationType.HardLink or FileFilterOperationType.SymbolLink)
                        {
                            targetPath = Path.Combine(Config.TargetDir, file.TargetPath);
                            targetPath = FileNameHelper.GenerateUniquePath(targetPath, usedPaths);
                            usedPaths.Add(targetPath);
                        }

                        switch (Config.Type)
                        {
                            case FileFilterOperationType.Copy:
                                await FileCopyHelper.CopyFileAsync(file.Path, targetPath,
                                    progress: s.GetFileProcessProgress("正在复制文件", file.Name, NotifyProgress,
                                        NotifyMessage),
                                    cancellationToken: token);
                                File.SetLastWriteTime(targetPath, File.GetLastWriteTime(file.Path));
                                break;
                            case FileFilterOperationType.Move:
                                await FileCopyHelper.CopyFileAsync(file.Path, targetPath,
                                    progress: s.GetFileProcessProgress("正在移动文件", file.Name, NotifyProgress,
                                        NotifyMessage),
                                    cancellationToken: token);
                                File.SetLastWriteTime(targetPath, File.GetLastWriteTime(file.Path));
                                FileHelper.DeleteByConfig(file.Path);
                                break;
                            case FileFilterOperationType.HardLink:
                                HardLinkCreator.CreateHardLink(targetPath, file.Path);
                                break;
                            case FileFilterOperationType.SymbolLink:
                                File.CreateSymbolicLink(targetPath, file.Path);
                                break;
                            case FileFilterOperationType.Delete:
                                FileHelper.DeleteByConfig(file.Path);
                                break;
                            case FileFilterOperationType.Hash:
                                file.Success(await FileHashHelper.ComputeHashStringAsync(file.Path, Config.HashType,
                                    progress: s.GetFileProcessProgress("正在计算文件Hash", file.Name, NotifyProgress,
                                        NotifyMessage),
                                    cancellationToken: token));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    },
                    token, loopOptions);
            }, token);
        }

        public override async Task InitializeAsync(CancellationToken token)
        {
            List<FileFilterOperationFileInfo> files = new List<FileFilterOperationFileInfo>();
            await Task.Run(() =>
            {
                foreach (var sourceDir in FileNameHelper.GetDirNames(Config.SourceDirs))
                {
                    NotifyMessage($"正在搜索目录{sourceDir}下的文件");
                    var tempFiles = new DirectoryInfo(sourceDir)
                        .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                        .ApplyFilter(token, Config.Filter)
                        .Select(p => GetTargetFile(sourceDir, p));
                    files.AddRange(tempFiles);
                }
            }, token);
            Files = files;
        }

        private FileFilterOperationFileInfo GetTargetFile(string sourceDir, FileInfo file)
        {
            var targetFile = new FileFilterOperationFileInfo(file, sourceDir);
            if (Config.Type is FileFilterOperationType.Delete or FileFilterOperationType.Hash) //不需要目标文件
            {
                return targetFile;
            }

            var relativePath = targetFile.RelativePath;
            targetFile.TargetPath = Config.TargetFileNameMode switch
            {
                FileFilterOperationTargetFileNameMode.PreserveDirectoryStructure => relativePath,
                FileFilterOperationTargetFileNameMode.FlattenWithOriginalNames => targetFile.Name,
                FileFilterOperationTargetFileNameMode.FlattenWithRelativePathNames => relativePath
                    .Replace('/', GlobalConfigs.Instance.FlattenPathSeparatorReplacement)
                    .Replace('\\', GlobalConfigs.Instance.FlattenPathSeparatorReplacement),
                _ => throw new ArgumentOutOfRangeException(),
            };

            return targetFile;
        }
    }
}