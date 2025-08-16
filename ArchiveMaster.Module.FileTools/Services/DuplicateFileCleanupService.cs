using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Xml.Serialization;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Controls.Platform;
using FzLib.Cryptography;
using FzLib.IO;
using Microsoft.Win32.SafeHandles;

namespace ArchiveMaster.Services
{
    public class DuplicateFileCleanupService(AppConfig appConfig)
        : TwoStepServiceBase<DuplicateFileCleanupConfig>(appConfig)
    {
        private FileMatchHelper matcher;

        public TreeDirInfo DuplicateGroups { get; private set; }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            await Task.Run(() =>
            {
                NotifyMessage("正在检查问题");
                if (Config.CleaningDir == Config.ReferenceDir) //删除自身
                {
                    foreach (var group in DuplicateGroups.SubDirs)
                    {
                        if (group.SubFiles.Count(p => p.IsChecked) == group.SubFileCount) //如果所有文件均被勾选
                        {
                            throw new InvalidOperationException($"文件{group.Name}的所有相同文件均被勾选待删除，会造成数据丢失");
                        }
                    }
                }

                NotifyMessage("正在将文件移动到回收站");
                int index = 0;
                foreach (var group in DuplicateGroups.SubDirs)
                {
                    NotifyMessage($"正在删除与“{group.Name}”相同的文件");
                    foreach (var file in group.SubFiles.CheckedOnly())
                    {
                        try
                        {
                            var distPath = Path.Combine(Config.RecycleBin, file.RelativePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(distPath));
                            File.Move(file.Path, distPath);
                            file.Success();
                        }
                        catch (Exception ex)
                        {
                            file.Error(ex);
                        }
                    }

                    NotifyProgress(1.0 * index++ / DuplicateGroups.SubFolderCount);
                }
            }, token);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return DuplicateGroups.Flatten();
        }

        public override Task InitializeAsync(CancellationToken token)
        {
            List<DuplicateFileInfo> files = new List<DuplicateFileInfo>();
            Config.Check();
            return Task.Run(() =>
            {
                NotifyMessage("正在构建待清理文件特征");
                matcher = BuildFileFeatures(token);

                NotifyMessage("正在枚举参考文件");
                List<SimpleFileInfo> allReferenceFiles = new DirectoryInfo(Config.ReferenceDir)
                    .EnumerateSimpleFileInfos(token)
                    .OrderBy(file => file.Path.StartsWith(Config.CleaningDir, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                //由于ReferenceDir可能包含CleaningDir，因此需要优先考虑不在CleaningDir里的文件，尽可能删除CleaningDir内的重复文件

                NotifyMessage("正在匹配文件");
                MatchAndGroup(allReferenceFiles, token);
            }, token);
        }

        private FileMatchHelper BuildFileFeatures(CancellationToken token)
        {
            matcher = new FileMatchHelper(Config.CompareName, Config.CompareLength, Config.CompareTime,
                Config.TimeToleranceSecond);
            matcher.AddReferenceDir(Config.CleaningDir, token);

            return matcher;
        }

        private void MatchAndGroup(IEnumerable<SimpleFileInfo> referenceFiles, CancellationToken token)
        {
            HashSet<string> checkedFiles = new HashSet<string>();

            List<DuplicateFileInfo> duplicateFiles = new List<DuplicateFileInfo>();
            foreach (var file in referenceFiles)
            {
                token.ThrowIfCancellationRequested();
                if (!checkedFiles.Add(file.Path))
                {
                    continue;
                }

                var matchedFiles = matcher.GetMatchedFiles(file.FileSystemInfo as FileInfo);
                if (matchedFiles.Count == 0 || matchedFiles.Count == 1 && matchedFiles.Contains(file.Path))
                {
                    //没有匹配到，或者只匹配到了自己
                    continue;
                }

                foreach (var matchedFile in matchedFiles)
                {
                    checkedFiles.Add(matchedFile);
                    duplicateFiles.Add(new DuplicateFileInfo(new FileInfo(matchedFile), Config.CleaningDir, file));
                }
            }

            var tree = TreeDirInfo.CreateEmptyTree();
            foreach (var group in duplicateFiles.GroupBy(p => p.ExistedFile, SimpleFileInfo.EqualityComparer))
            {
                token.ThrowIfCancellationRequested();
                var refFile = new TreeDirInfo()
                {
                    Name = group.Key.Name,
                    Path = group.Key.Path,
                    TopDirectory = Config.ReferenceDir,
                    Depth = 1,
                    Index = tree.Subs.Count,
                    IsDir = true,
                };
                tree.AddSub(refFile);
                bool first = true;
                foreach (var file in group.OrderBy(p => p.Path))
                {
                    //file肯定是在CleaningDir里的，group.Key有可能在CleaningDirect里。
                    //如果group.Key是属于CleaningDir的，那么保留第一个
                    var treeFile = refFile.AddSubFile(file);
                    treeFile.IsChecked = !(first && group.Key.Path.StartsWith(Config.CleaningDir));
                    first = false;
                }
            }

            DuplicateGroups = tree;
        }
    }
}