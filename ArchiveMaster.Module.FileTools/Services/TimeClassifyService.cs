using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;
using FilesTimeDirInfo = ArchiveMaster.ViewModels.FileSystem.FilesTimeDirInfo;

namespace ArchiveMaster.Services
{
    public class TimeClassifyService(AppConfig appConfig)
        : TwoStepServiceBase<TimeClassifyConfig>(appConfig)
    {
        public List<FilesTimeDirInfo> TargetDirs { get; set; }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            return TryForFilesAsync(TargetDirs, (dir, s) =>
            {
                NotifyMessage($"正在移动{s.GetFileNumberMessage()}");
                string newDirName = dir.EarliestTime.ToString("yyyyMMdd-HHmmss");
                string newDirPath = Path.Combine(Config.Dir, newDirName);
                Directory.CreateDirectory(newDirPath);
                foreach (var sub in dir.Subs)
                {
                    string targetPath = Path.Combine(newDirPath, sub.Name);
                    Debug.WriteLine($"{sub.Path} => {targetPath}");
                    if (sub.IsDir)
                    {
                        Directory.Move(sub.Path, targetPath);
                    }
                    else
                    {
                        File.Move(sub.Path, targetPath);
                    }
                }
            }, ct, FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileNumberProgress().Build());
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return TargetDirs.Cast<SimpleFileInfo>();
        }

        public override async Task InitializeAsync(CancellationToken ct)
        {
            List<SimpleFileInfo> files = null;
            List<FilesTimeDirInfo> subDirs = null;
            List<FilesTimeDirInfo> targetDirs = new List<FilesTimeDirInfo>();

            await Task.Run(() =>
            {
                NotifyMessage("正在搜索文件");
                files = new DirectoryInfo(Config.Dir).EnumerateFiles()
                    .Select(p => new SimpleFileInfo(p, Config.Dir))
                    .OrderBy((Func<SimpleFileInfo, DateTime>)(p => (DateTime)p.Time))
                    .ToList();
                ct.ThrowIfCancellationRequested();
                subDirs = new DirectoryInfo(Config.Dir).EnumerateDirectories()
                    .Select(p => new FilesTimeDirInfo(p, Config.Dir))
                    .Where(p => p.FilesCount > 0)
                    .OrderBy(p => p.EarliestTime)
                    .ToList();
                if (files.Count == 0 && subDirs.Count == 0)
                {
                    throw new Exception("目录为空");
                }

                ct.ThrowIfCancellationRequested();

                NotifyMessage("正在分配目录");
                DateTime time = DateTime.MinValue;
                int filesIndex = 0;
                int dirsIndex = 0;
                while (filesIndex < files.Count || dirsIndex < subDirs.Count)
                {
                    ct.ThrowIfCancellationRequested();
                    var file = filesIndex < files.Count ? files[filesIndex] : null;
                    var dir = dirsIndex < subDirs.Count ? subDirs[dirsIndex] : null;
                    //没有未处理目录，或文件的时间早于目录中最早文件的时间
                    if (dir == null || (file != null && file.Time <= dir.EarliestTime))
                    {
                        //如果和上一个的时间间隔超过了阈值，那么新建目录存放
                        if (file.Time - time > Config.MinTimeInterval)
                        {
                            FilesTimeDirInfo newDir = new FilesTimeDirInfo();
                            targetDirs.Add(newDir);
                        }

                        Debug.Assert(targetDirs.Count > 0);
                        targetDirs[^1].Subs.Add(file);
                        time = time < file.Time ? file.Time : time; //存在dir.LatestTime>file.Time的可能
                        filesIndex++;
                    }
                    else if (file == null || (dir != null && dir.EarliestTime <= file.Time))
                    {
                        if (dir.EarliestTime - time > Config.MinTimeInterval)
                        {
                            FilesTimeDirInfo newDir = new FilesTimeDirInfo();
                            targetDirs.Add(newDir);
                        }

                        Debug.Assert(targetDirs.Count > 0);
                        targetDirs[^1].Subs.Add(dir);
                        time = time < dir.LatestTime ? dir.LatestTime : time;
                        dirsIndex++;
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }, ct);

            foreach (var dir in targetDirs)
            {
                ct.ThrowIfCancellationRequested();
                dir.EarliestTime = dir.Subs.Select(p => p.IsDir ? (p as FilesTimeDirInfo).EarliestTime : p.Time)
                    .Min();
                dir.LatestTime = dir.Subs.Select(p =>
                {
                    return p switch
                    {
                        FilesTimeDirInfo d => d.LatestTime,
                        SimpleFileInfo f => f.Time,
                        _ => throw new NotImplementedException()
                    };
                }).Max();
                dir.Name = $"{dir.EarliestTime:yyyy-MM-dd HH:mm:ss} ~ {dir.LatestTime:yyyy-MM-dd HH:mm:ss}";
            }

            TargetDirs = targetDirs;
        }
    }
}