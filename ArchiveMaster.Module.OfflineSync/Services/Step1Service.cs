using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using SyncFileInfo = ArchiveMaster.ViewModels.FileSystem.SyncFileInfo;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Services
{
    public class Step1Service(AppConfig appConfig) : TwoStepServiceBase<OfflineSyncStep1Config>(appConfig)
    {
        public override async Task ExecuteAsync(CancellationToken ct = default)
        {
            Config.Check();
            int index = 0;
            List<SyncFileInfo> syncFiles = new List<SyncFileInfo>();
            var groups = Config.SyncDirs.GroupBy(p => Path.GetFileName(p));
            foreach (var group in groups)
            {
                if (group.Count() > 1)
                {
                    throw new ArgumentException("存在重复的顶级目录名：" + group.Key);
                }
            }

            NotifyProgressIndeterminate();

            await Task.Run(() =>
            {
                foreach (var dir in Config.SyncDirs)
                {
                    TryForFiles(new DirectoryInfo(dir)
                        .EnumerateFiles("*", SearchOption.AllDirectories)
                        .Select(p => new SyncFileInfo(p, dir)), (file, s) =>
                    {
                        syncFiles.Add(file);
                        index++;
                        NotifyMessage($"正在搜索：{dir}，已加入 {index} 个文件");
                    }, ct, FilesLoopOptions.Builder().AutoApplyStatus().Build());
                }


                NotifyMessage($"正在保存快照");

                Step1Model model = new Step1Model()
                {
                    Files = syncFiles.ToList(),
                };
                ZipService.WriteToZip(model, Config.OutputFile);
            }, ct);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return null;
        }
        public override Task InitializeAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}