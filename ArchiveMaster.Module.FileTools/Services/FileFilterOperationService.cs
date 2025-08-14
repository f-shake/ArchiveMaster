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
            return Files.Cast<SimpleFileInfo>();
        }

        public override Task ExecuteAsync(CancellationToken token)
        {
            FilesLoopOptions loopOptions = Config.Type switch
            {
                FileFilterOperationType.Copy or FileFilterOperationType.Move =>
                    FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileLengthProgress().Build(),
                FileFilterOperationType.Delete =>
                    FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileNumberProgress().Build(),
                _ => throw new ArgumentOutOfRangeException()
            };
            return TryForFilesAsync(Files.CheckedOnly().ToList(), async (file, s) =>
                {
                    switch (Config.Type)
                    {
                        case FileFilterOperationType.Copy:
                            break;
                        case FileFilterOperationType.Move:
                            break;
                        case FileFilterOperationType.Delete:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                },
                token, loopOptions);
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
            if (Config.Type == FileFilterOperationType.Delete)
            {
                return targetFile;
            }

            var relativePath = targetFile.RelativePath;
            targetFile.TargetPath = Config.TargetFileNameMode switch
            {
                FileFilterOperationTargetFileNameMode.PreserveDirectoryStructure => relativePath,
                FileFilterOperationTargetFileNameMode.FlattenWithOriginalNames =>targetFile.Name,
                FileFilterOperationTargetFileNameMode.FlattenWithRelativePathNames => relativePath.Replace('/', GlobalConfigs.Instance.FlattenPathSeparatorReplacement)
                        .Replace('\\', GlobalConfigs.Instance.FlattenPathSeparatorReplacement),
                _ => throw new ArgumentOutOfRangeException(),
            };

            return targetFile;
        }
    }
}