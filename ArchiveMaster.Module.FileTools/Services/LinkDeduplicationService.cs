using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.IO;

namespace ArchiveMaster.Services;

public class LinkDeduplicationService(AppConfig appConfig)
    : TwoStepServiceBase<LinkDeduplicationConfig>(appConfig)
{
    public TreeDirInfo TreeRoot { get; private set; }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Run(() =>
        {
            // CheckedOnly<T> 会读基类 IsChecked，这里按 TreeDirInfo 的隐藏属性筛选
            var groups = TreeRoot.SubDirs.Where(p => p.IsChecked).ToList();
            TryForFiles(groups, (group, s) =>
            {
                var sourceFile = group.SubFiles[0];
                sourceFile.Success();
                foreach (var file in group.SubFiles.Skip(1))
                {
                    NotifyMessage($"正在创建硬链接{s.GetFileNumberMessage()}：{file.RelativePath}");
                    // 先建临时硬链接，成功后再删原名：任何一步失败都不会丢掉文件
                    string tempLink = file.Path + "." + Guid.NewGuid().ToString("N") + ".lnk";
                    bool originalDeleted = false;
                    try
                    {
                        HardLinkCreator.CreateHardLink(tempLink, sourceFile.Path);
                        FileHelper.DeleteByConfig(file.Path, "硬链接去重");
                        originalDeleted = true;
                        File.Move(tempLink, file.Path);
                        originalDeleted = false;
                        file.Success();
                    }
                    catch (Exception ex)
                    {
                        if (originalDeleted)
                        {
                            // 原文件已经删除（可能配置为直接删除），tempLink 是唯一剩下的副本，绝不能清理
                            throw new IOException(
                                $"硬链接去重失败，原文件名已不可用；文件内容保留在临时链接“{tempLink}”，请手工改回原名。{ex.Message}",
                                ex);
                        }

                        if (File.Exists(tempLink))
                        {
                            File.Delete(tempLink);
                        }

                        throw;
                    }
                }
            }, ct, FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileNumberProgress().Build());
        }, ct);
    }

    public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
    {
        return TreeRoot.Flatten();
    }

    public override Task InitializeAsync(CancellationToken ct)
    {
        List<LinkDeduplicationFileInfo> files = new List<LinkDeduplicationFileInfo>();
        Dictionary<string, LinkDeduplicationFileInfo> hash2File = new Dictionary<string, LinkDeduplicationFileInfo>();
        return Task.Run(async () =>
        {
            NotifyMessage("正在枚举文件");

            files = new DirectoryInfo(Config.Dir)
                .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                .Select(p => new LinkDeduplicationFileInfo(p, Config.Dir))
                .ApplyFilter(ct, Config.Filter)
                .ToList();

            NotifyMessage("正在计算文件Hash");
            await TryForFilesAsync(files, async (f, s) =>
            {
                string hash = await FileHashHelper.ComputeHashStringAsync(f.Path, Config.HashType,
                    cancellationToken: ct,
                    progress: s.CreateFileProgressReporter("正在计算Hash"));
                f.Hash = hash;
                if (!hash2File.TryAdd(hash, f))
                {
                    var sameFile = hash2File[hash];
                    sameFile.CanMakeHardLink = true;
                    f.CanMakeHardLink = true;
                }
            }, ct, FilesLoopOptions.Builder().AutoApplyFileLengthProgress().Build());

            NotifyMessage("正在统计相同文件");
            var tree = TreeDirInfo.CreateEmptyTree();
            foreach (var sameHashFiles in files
                         .Where(p => p.CanMakeHardLink)
                         .GroupBy(p => p.Hash))
            {
                var sameHashFileList = sameHashFiles.ToList();
                var group = tree.AddSubDir($"{sameHashFileList.Count}个相同文件");
                group.SetRelativePath(sameHashFiles.Key);
                bool hasDifferentTime = sameHashFiles.DistinctBy(p => p.Time).Count() > 1;
                if (sameHashFiles.DistinctBy(p => p.Length).Count() == 1)
                {
                    group.Length = sameHashFileList[0].Length;
                    if (!hasDifferentTime)
                    {
                        group.Time = sameHashFileList[0].Time;
                        group.IsChecked = true;
                    }
                    else
                    {
                        group.Warn("文件哈希相同，但修改日期不同");
                        if (Config.AllowDifferentTime)
                        {
                            group.IsChecked = true;
                        }
                    }
                }
                else
                {
                    group.Error("文件哈希相同，但大小不同");
                }

                foreach (var file in sameHashFileList)
                {
                    group.AddSubFile(file);
                }

                // 子项默认勾选会向上覆盖，需在加完后显式取消
                if (hasDifferentTime && !Config.AllowDifferentTime)
                {
                    group.IsChecked = false;
                }
            }

            TreeRoot = tree;
        }, ct);
    }
}