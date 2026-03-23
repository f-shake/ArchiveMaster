using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.IO;

namespace ArchiveMaster.Services
{
    public class TimeAssService(AppConfig appConfig) : TwoStepServiceBase<TimeAssConfig>(appConfig)
    {

        public override Task ExecuteAsync(CancellationToken token = default)
        {
        //     var files = Files.Where(p => p.IsChecked).ToList();
        //     var totalLength = files.Sum(p => p.Length);
        //     long currentLength = 0;
        //     return TryForFilesAsync(files,
        //         async (file, state) =>
        //         {
        //             int index = state.FileIndex;
        //             int count = state.FileCount;
        //             NotifyMessage($"正在复制（{index}/{count}），当前文件：{Path.GetFileName(file.Name)}");
        //
        //             await FileCopyHelper.CopyFileAsync(file.Path, file.DestinationPath,
        //                 progress: new Progress<FileProcessProgress>(
        //                     p =>
        //                     {
        //                         NotifyMessage(
        //                             $"正在复制（{index}/{count}，当前文件{1.0 * p.ProcessedBytes / 1024 / 1024:0}MB/{1.0 * p.TotalBytes / 1024 / 1024:0}MB），当前文件：{Path.GetFileName(p.SourceFilePath)}");
        //                       
        //                         NotifyProgress(1.0 * (currentLength + p.ProcessedBytes) / totalLength);
        //                     }),
        //                 cancellationToken: token);
        //             File.SetLastWriteTimeUtc(file.DestinationPath, file.Time);
        //             currentLength += file.Length;
        //         },
        //         token, FilesLoopOptions.Builder().AutoApplyFileLengthProgress().AutoApplyStatus().Build());
        return Task.CompletedTask;
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            // return Files.Cast<SimpleFileInfo>();
            return [];
        }

        public override async Task InitializeAsync(CancellationToken token = default)
        {
            // var files = new DirectoryInfo(Config.SourceDir)
            //     .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
            //     .ApplyFilter(token)
            //     .Select(f => new CopyingFile(f, Config.SourceDir));
            // Files = new List<CopyingFile>();
            // await TryForFilesAsync(files,
            //     (f, s) =>
            //     {
            //         f.DestinationPath = Path.Combine(Config.DestinationDir,
            //             Path.GetRelativePath(Config.SourceDir, f.Path));
            //         Files.Add(f);
            //     }, token, FilesLoopOptions.DoNothing());
        }
    }
}