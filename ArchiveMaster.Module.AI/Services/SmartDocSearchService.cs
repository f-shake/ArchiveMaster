using FzLib;
using ArchiveMaster.Configs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Services
{
    public class SmartDocSearchService(AppConfig appConfig)
        : TwoStepServiceBase<SmartDocSearchConfig>(appConfig)
    {
        public ConcurrentBag<ExifTimeFileInfo> Files { get; } = new ConcurrentBag<ExifTimeFileInfo>();

        public override Task ExecuteAsync(CancellationToken ct)
        {
            return TryForFilesAsync(Files, (file, s) =>
            {
                if (!file.ExifTime.HasValue)
                {
                    return;
                }

                NotifyMessage($"正在处理{s.GetFileNumberMessage()}：{file.Name}");
                File.SetLastWriteTime(file.Path, file.ExifTime.Value);
            }, ct, FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileNumberProgress().Build());
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return Files.Cast<SimpleFileInfo>();
        }
        public override async Task InitializeAsync(CancellationToken ct)
        {
            NotifyProgressIndeterminate();
            NotifyMessage("正在查找文件");
            List<ExifTimeFileInfo> files = null;
            await Task.Run(() =>
            {
                files = new DirectoryInfo(Config.Dir)
                    .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                    .ApplyFilter(ct, Config.Filter)
                    .Select(p => new ExifTimeFileInfo(p, Config.Dir))
                    .ToList();
            });
            await TryForFilesAsync(files, (file, s) =>
                {
                    NotifyMessage($"正在扫描照片日期{s.GetFileNumberMessage()}");

                    // DateTime? exifTime = ExifHelper.FindExifTime(file.Path);
                    //
                    // if (exifTime.HasValue)
                    // {
                    //     var fileTime = file.Time;
                    //     var duration = (exifTime.Value - fileTime).Duration();
                    //     if (duration > Config.MaxDurationTolerance)
                    //     {
                    //         file.ExifTime = exifTime.Value;
                    //         Files.Add(file);
                    //     }
                    // }
                }, ct,
                FilesLoopOptions.Builder()
                    .AutoApplyFileNumberProgress()
                    .WithMultiThreads(Config.ThreadCount)
                    .Catch((file, ex) => { Files.Add(file as ExifTimeFileInfo); }).Build());
        }
    }
}