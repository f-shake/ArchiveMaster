using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ArchiveMaster.Configs;
using static System.Net.Mime.MediaTypeNames;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Cryptography;
using FzLib.IO;
using LocalAndOffsiteDir = ArchiveMaster.ViewModels.FileSystem.LocalAndOffsiteDir;
using SyncFileInfo = ArchiveMaster.ViewModels.FileSystem.SyncFileInfo;

namespace ArchiveMaster.Services
{
    public class Step2Service(AppConfig appConfig) : TwoStepServiceBase<OfflineSyncStep2Config>(appConfig)
    {
        public const string EncryptionFileSuffix = "_enc";
        private Aes aes;
        public Dictionary<string, List<string>> LocalDirectories { get; } = new Dictionary<string, List<string>>();

        public List<SyncFileInfo> UpdateFiles { get; } = new List<SyncFileInfo>();

        public static async Task<IList<LocalAndOffsiteDir>> MatchLocalAndOffsiteDirsAsync(string snapshotPath,
            string[] localSearchingDirs)
        {
            List<LocalAndOffsiteDir> matchingDirs = null;
            await Task.Run(() =>
            {
                Step1Model step1 = ZipService.ReadFromZip<Step1Model>(snapshotPath);
                matchingDirs =
                    step1.Files
                        .Select(p => p.TopDirectory)
                        .Distinct()
                        .Select(p => new LocalAndOffsiteDir() { OffsiteDir = p, })
                        .ToList();
                var matchingDirsDic = matchingDirs.ToDictionary(p => Path.GetFileName(p.OffsiteDir), p => p);
                foreach (var localSearchingDir in localSearchingDirs)
                {
                    foreach (var subLocalDir in new DirectoryInfo(localSearchingDir).EnumerateDirectories())
                    {
                        if (matchingDirsDic.TryGetValue(subLocalDir.Name, out var value))
                        {
                            value.LocalDir = subLocalDir.FullName;
                        }
                    }
                }
            });
            return matchingDirs;
        }

        public override async Task ExecuteAsync(CancellationToken ct = default)
        {
            if (!Directory.Exists(Config.PatchDir))
            {
                Directory.CreateDirectory(Config.PatchDir);
            }

            if (Config.EnableEncryption)
            {
                aes = AesHelper.GetDefaultAes(Config.EncryptionPassword);
            }

            var files = UpdateFiles.Where(p => p.IsChecked).ToList();
            Dictionary<string, string> offsiteTopDir2LocalDir =
                Config.MatchingDirs.ToDictionary(p => p.OffsiteDir, p => p.LocalDir);
            long totalLength = files.Where(p => p.UpdateType is not (FileUpdateType.Delete or FileUpdateType.Move))
                .Sum(p => p.Length);
            long length = 0;
            StringBuilder batScript = new StringBuilder();
            StringBuilder ps1Script = new StringBuilder();
            BeginScript(batScript, ps1Script);
            using var sha256 = SHA256.Create();
            await Task.Run(async () =>
            {
                await TryForFilesAsync(files, async (file, s) =>
                {
                    if (file.UpdateType is FileUpdateType.Delete or FileUpdateType.Move)
                    {
                        return;
                    }

                    file.TempName = GetTempFileName(file, sha256) +
                                    (Config.EnableEncryption ? EncryptionFileSuffix : string.Empty);


                    string numMsg = s.GetFileNumberMessage("{0}/{1}");
                    NotifyMessage($"正在处理（{numMsg}）：{file.RelativePath}");
                    string sourceFile = Path.Combine(offsiteTopDir2LocalDir[file.TopDirectory], file.RelativePath);
                    string destFile = Path.Combine(Config.PatchDir, file.TempName);
                    var destFileInfo = new FileInfo(destFile);
                    var sourceFileInfo = new FileInfo(sourceFile);

                    if (File.Exists(destFile) && Config.ExportMode != ExportMode.Script &&
                        destFileInfo.LastWriteTime == sourceFileInfo.LastWriteTime &&
                        (
                            Config.EnableEncryption &&
                            destFileInfo.Length == aes.GetEncryptedFileSize(sourceFileInfo.Length) //加密文件
                            || !Config.EnableEncryption && destFileInfo.Length == sourceFileInfo.Length //非加密文件
                        ))
                    {
                        NotifyMessage($"正在处理（{numMsg}）：{file.RelativePath} 已存在，跳过");
                        return;
                    }

                    switch (Config.ExportMode)
                    {
                        case ExportMode.PreferHardLink:
                            try
                            {
                                HardLinkCreator.CreateHardLink(destFile, sourceFile);
                            }
                            catch (IOException)
                            {
                                goto copy;
                            }

                            break;
                        case ExportMode.Copy:
                            copy:
                            int tryCount = 10;
                            
                            while (--tryCount > 0)
                            {
                                if (File.Exists(destFile))
                                {
                                    File.Delete(destFile);
                                }

                                try
                                {
                                    await CopyFileAsync(sourceFile, destFile,
                                            s.CreateFileProgressReporter("正在复制", 
                                                p => length + p.ProcessedBytes,
                                                () => totalLength,
                                                p => Path.GetFileName(p.SourceFilePath))
                                        , ct);
                                    tryCount = 0;
                                }
                                catch (IOException ex)
                                {
                                    Debug.WriteLine($"复制文件{sourceFile}到{destFile}失败：{ex.Message}，剩余{tryCount}次重试");
                                    if (tryCount == 0)
                                    {
                                        throw new IOException($"复制文件{sourceFile}到{destFile}失败：已重试10次", ex);
                                    }

                                    Thread.Sleep(1000);
                                }
                            }

                            break;
                        case ExportMode.HardLink:
                            HardLinkCreator.CreateHardLink(destFile, sourceFile);
                            break;
                        case ExportMode.Script:
                            ExportWithScript(sourceFile, file, batScript, ps1Script);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }, ct, FilesLoopOptions.Builder().AutoApplyStatus().Finally(file =>
                {
                    var f = file as SyncFileInfo;
                    if (f.UpdateType is FileUpdateType.Delete or FileUpdateType.Move)
                    {
                        return;
                    }

                    length += f.Length;
                    NotifyProgress(1.0 * length / totalLength);
                }).Build());


                if (Config.ExportMode == ExportMode.Script)
                {
                    FinishScript(batScript, ps1Script);
                }

                Step2Model model = new Step2Model()
                {
                    Files = files,
                    LocalDirectories = LocalDirectories
                };
                ZipService.WriteToZip(model, Path.Combine(Config.PatchDir, "file.os2"));
            }, ct);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return UpdateFiles.Cast<SimpleFileInfo>();
        }

        public override async Task InitializeAsync(CancellationToken ct = default)
        {
            UpdateFiles.Clear();
            LocalDirectories.Clear();
            NotifyProgressIndeterminate();
            NotifyMessage($"正在初始化");
            var filter = new FileFilterHelper(Config.Filter);

            await Task.Run(() =>
            {
                var step1Model = ZipService.ReadFromZip<Step1Model>(Config.OffsiteSnapshot);
                //将异地文件根据顶级目录
                var offsiteTopDir2Files =
                    step1Model.Files.GroupBy(p => p.TopDirectory)
                        .ToDictionary(p => p.Key, p => p.ToList());
                //用于之后寻找差异文件的哈希表
                Dictionary<string, byte> localFiles = new Dictionary<string, byte>();
                HashSet<string> offsiteTopDirs = Config.MatchingDirs.Select(p => p.OffsiteDir).ToHashSet();
                if (offsiteTopDirs.Count != Config.MatchingDirs.Count)
                {
                    throw new ArgumentException("异地顶级目录存在重复", nameof(Config.MatchingDirs));
                }

                if (Config.MatchingDirs.Any(p =>
                        string.IsNullOrEmpty(p.OffsiteDir) || string.IsNullOrEmpty(p.LocalDir)))
                {
                    throw new ArgumentException("目录匹配未完成");
                }

                foreach (var file in offsiteTopDir2Files)
                {
                    if (!offsiteTopDirs.Contains(file.Key))
                    {
                        throw new ArgumentException($"快照中存在顶级目录{file.Key}但{nameof(Config.MatchingDirs)}未提供",
                            nameof(Config.MatchingDirs));
                    }
                }

                //枚举本地文件，寻找离线快照中是否存在相同文件
                foreach (var localAndOffsiteDir in Config.MatchingDirs)
                {
                    var localDir = new DirectoryInfo(localAndOffsiteDir.LocalDir);
                    var offsiteDir = new DirectoryInfo(localAndOffsiteDir.OffsiteDir);
                    NotifyMessage($"正在查找：{offsiteDir}→{localDir}");
                    var localFileList = localDir
                        .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                        .ApplyFilter(ct)
                        .ToList();
                    var localFilePathSet = localFileList.Select(p => p.FullName).ToHashSet();

                    //从路径、文件名、时间、长度寻找本地文件的字典
                    string offsiteTopDirectory = localAndOffsiteDir.OffsiteDir;
                    var a = offsiteTopDir2Files[offsiteTopDirectory].Select(p => p.RelativePath).ToList();
                    Dictionary<string, SyncFileInfo> offsitePath2File =
                        offsiteTopDir2Files[offsiteTopDirectory].ToDictionary(p => p.RelativePath);
                    Dictionary<string, List<SyncFileInfo>> offsiteName2File = offsiteTopDir2Files[offsiteTopDirectory]
                        .GroupBy(p => p.Name).ToDictionary(p => p.Key, p => p.ToList());
                    Dictionary<long, List<SyncFileInfo>> offsiteLength2File = offsiteTopDir2Files[offsiteTopDirectory]
                        .GroupBy(p => p.Length).ToDictionary(p => p.Key, p => p.ToList());

                    //对于时间，有点特殊。
                    //如果MaxTimeToleranceSecond为0，那么就是严格的时间相等
                    //如果MaxTimeToleranceSecond>0，那么需要将这个文件放入文件时间前后几秒的时间字典内
                    Dictionary<DateTime, List<SyncFileInfo>> offsiteTime2File;
                    if (Config.MaxTimeToleranceSecond > 0)
                    {
                        offsiteTime2File = new Dictionary<DateTime, List<SyncFileInfo>>();
                        foreach (var file in offsiteTopDir2Files[offsiteTopDirectory])
                        {
                            var t = file.Time;
                            DateTime start = t.TruncateToSecond().AddSeconds(-Config.MaxTimeToleranceSecond);

                            DateTime end = t.TruncateToSecond();
                            if (t.Ticks != end.Ticks) //毫秒数不为0
                            {
                                end = end.AddSeconds(1);
                            }

                            end = end.AddSeconds(Config.MaxTimeToleranceSecond);

                            for (DateTime time = start; time <= end; time = time.AddTicks(TimeSpan.TicksPerSecond))
                            {
                                if (offsiteTime2File.TryGetValue(time, out List<SyncFileInfo> list))
                                {
                                    list.Add(file);
                                }
                                else
                                {
                                    offsiteTime2File.Add(time, [file]);
                                }
                            }
                        }
                    }
                    else
                    {
                        offsiteTime2File = offsiteTopDir2Files[offsiteTopDirectory]
                            .GroupBy(p => p.Time).ToDictionary(p => p.Key, p => p.ToList());
                    }

                    int index = 0;
                    //开始对比文件
                    /*
                        这段代码主要用于比对本地文件与异地快照文件，判断文件的变化类型（新增、修改、移动/重命名或无变化），并将需要同步的文件信息添加到 UpdateFiles 列表中。整体流程如下：


                        1. 遍历本地文件：对每个本地文件，首先检查是否被取消操作，然后获取其相对路径，并记录到本地文件哈希表中。
                        2. 过滤文件：通过自定义的过滤器 filter 判断文件是否需要参与比对，不匹配的文件直接跳过。
                        3. 路径直接匹配：如果本地文件的相对路径在异地快照中存在，说明文件未移动或重命名。进一步比较文件的修改时间和长度：
                        - 如果时间和长度都在容忍范围内，认为文件未发生变化，跳过。
                        - 否则，认为文件被修改，创建一个 SyncFileInfo 对象，标记为 Modify，并添加到 UpdateFiles。
                        - 如果异地文件时间晚于本地文件，还会添加警告信息。
                        4. 路径不匹配（新增/移动/重命名）：如果本地文件在异地快照中找不到相同路径，则进一步判断是否为移动/重命名或新增文件：
                        - 通过文件名、时间和长度等条件，在异地快照中查找唯一匹配的文件（可配置是否忽略文件名）。
                        - 如果只找到一个匹配文件，并且本地没有相同位置的文件，认为是移动或重命名，创建 SyncFileInfo，标记为 Move，并添加到 UpdateFiles，同时将异地文件路径加入本地哈希表，避免后续被误判为删除。
                        - 否则，认为是新增文件，创建 SyncFileInfo，标记为 Add，并添加到 UpdateFiles。
                        5. 最终结果：所有需要同步的文件（新增、修改、移动/重命名）都会被收集到 UpdateFiles，供后续同步或生成脚本使用。
                    */
                    foreach (var file in localFileList)
                    {
                        ct.ThrowIfCancellationRequested();

                        string relativePath = Path.GetRelativePath(localDir.FullName, file.FullName);
                        NotifyMessage($"正在比对（{++index}/{localFileList.Count}）：{relativePath}");
                        localFiles.Add(Path.Combine(localDir.Name, relativePath), 0);

                        if (!filter.IsMatched(file))
                        {
                            continue;
                        }

                        if (offsitePath2File.TryGetValue(relativePath, out var offsiteFile)) //路径相同，说明是没有变化或者文件被修改
                        {
                            if ((offsiteFile.Time - file.LastWriteTime).Duration().TotalSeconds <
                                Config.MaxTimeToleranceSecond
                                && offsiteFile.Length == file.Length) //文件没有发生改动
                            {
                                continue;
                            }

                            //文件发生改变
                            var newFile = new SyncFileInfo()
                            {
                                Path = Path.Combine(offsiteTopDirectory, relativePath),
                                Name = file.Name,
                                Length = file.Length,
                                Time = file.LastWriteTime,
                                UpdateType = FileUpdateType.Modify,
                                TopDirectory = offsiteTopDirectory,
                            };
                            if ((offsiteFile.Time - file.LastWriteTime).TotalSeconds > Config.MaxTimeToleranceSecond)
                            {
                                newFile.Warn($"异地文件时间（{offsiteFile.Time}）晚于本地文件时间");
                            }

                            UpdateFiles.Add(newFile);
                        }
                        else //新增文件或文件被移动或重命名
                        {
                            var time = Config.MaxTimeToleranceSecond > 0
                                ? file.LastWriteTime.TruncateToSecond()
                                : file.LastWriteTime;
                            List<SyncFileInfo> sameFiles;
                            if (Config.CheckMoveIgnoreFileName)
                            {
                                //如果忽略文件名，那么只需要根据时间和长度来寻找相同文件
                                sameFiles = offsiteTime2File.GetValueOrDefault(time, [])
                                    .Intersect(offsiteLength2File.GetValueOrDefault(file.Length, []))
                                    .ToList();
                            }
                            else
                            {
                                //如果不忽略文件名，那么需要根据路径、时间和长度来寻找相同文件
                                sameFiles = offsiteName2File.GetValueOrDefault(file.Name, [])
                                    .Intersect(offsiteTime2File.GetValueOrDefault(time, []))
                                    .Intersect(offsiteLength2File.GetValueOrDefault(file.Length, []))
                                    .ToList();
                            }

                            bool move = false;

                            if (sameFiles.Count == 1)
                            {
                                //满足以下条件时，文件将被移动：
                                //1、异地磁盘中，满足要求的相同文件仅找到一个
                                //2、在找到的这个相同文件对应的本地的位置，不存在相同文件
                                //      这一条时避免出现本地存在2个及以上的相同文件时，错误移动异地文件
                                string localSameLocation = sameFiles.First().RelativePath;
                                localSameLocation = Path.Combine(localDir.FullName, localSameLocation);
                                if (!localFilePathSet.Contains(localSameLocation))
                                {
                                    move = true;
                                }
                            }

                            if (move) //存在被移动或重命名的文件，并且为一对一关系
                            {
                                var offsiteMovedFile = sameFiles.First();
                                var movedFile = new SyncFileInfo()
                                {
                                    Path = Path.Combine(offsiteTopDirectory, relativePath),
                                    OldRelativePath = offsiteMovedFile.RelativePath,
                                    Name = file.Name,
                                    Length = file.Length,
                                    Time = file.LastWriteTime,
                                    UpdateType = FileUpdateType.Move,
                                    TopDirectory = offsiteTopDirectory,
                                };
                                UpdateFiles.Add(movedFile);
                                localFiles.TryAdd(Path.Combine(offsiteDir.Name, offsiteMovedFile.RelativePath),
                                    0); //如果被移动了，那么不需要进行删除判断，所以要把异地的文件地址也加入进去。
                            }
                            else //新增文件
                            {
                                var newFile = new SyncFileInfo()
                                {
                                    Path = Path.Combine(offsiteTopDirectory, relativePath),
                                    Name = file.Name,
                                    Length = file.Length,
                                    Time = file.LastWriteTime,
                                    UpdateType = FileUpdateType.Add,
                                    TopDirectory = offsiteTopDirectory,
                                };
                                UpdateFiles.Add(newFile);
                            }
                        }
                    }

                    ct.ThrowIfCancellationRequested();


                    NotifyMessage($"正在查找删除的文件:{offsiteDir}→{localDir}");
                    List<string> localSubDirs = new List<string>();
                    foreach (var subDir in localDir.EnumerateDirectories("*", SearchOption.AllDirectories))
                    {
                        string relativePath = Path.GetRelativePath(localDir.FullName, subDir.FullName);
                        localSubDirs.Add(relativePath);
                    }

                    LocalDirectories.Add(localAndOffsiteDir.OffsiteDir, localSubDirs);

                    //枚举异地快照，查找本地文件中不存在的文件
                    index = 0;
                    foreach (var file in offsiteTopDir2Files[offsiteTopDirectory])
                    {
                        var offsitePathWithTopDir =
                            Path.Combine(Path.GetFileName(file.TopDirectory), file.RelativePath);

                        if (!filter.IsMatched(file))
                        {
                            continue;
                        }

                        NotifyMessage(
                            $"正在查找删除的文件（{++index} / {offsiteTopDir2Files[offsiteTopDirectory].Count}）:{offsiteDir}→{localDir}");
                        if (!localFiles.ContainsKey(offsitePathWithTopDir))
                        {
                            file.UpdateType = FileUpdateType.Delete;
                            UpdateFiles.Add(file);
                        }
                    }
                }
            }, ct);
        }

        private static void BeginScript(StringBuilder batScript, StringBuilder ps1Script)
        {
            batScript.AppendLine("@echo off");
            ps1Script.AppendLine("Import-Module BitsTransfer");
        }

        private static void ExportWithScript(string sourceFile, SyncFileInfo file, StringBuilder batScript,
            StringBuilder ps1Script)
        {
            string sourceFileWithReplaceSpecialChars = sourceFile.Replace("%", "%%");
            batScript.AppendLine($"if exist \"{file.TempName}\" (");
            batScript.AppendLine($"echo \"文件 {sourceFileWithReplaceSpecialChars} 已存在\"");
            batScript.AppendLine($") else (");
            batScript.AppendLine($"echo 正在复制 \"{sourceFileWithReplaceSpecialChars}\"");
            batScript.AppendLine(
                $"copy \"{sourceFileWithReplaceSpecialChars}\" \"{file.TempName}\"");
            batScript.AppendLine($")");

            string ps1SourceName = sourceFile.Replace("'", "''");
            ps1Script.AppendLine($"if ([System.IO.File]::Exists(\"{file.TempName}\")){{");
            ps1Script.AppendLine($"'文件 {ps1SourceName} 已存在'");
            ps1Script.AppendLine($"}}else{{");
            ps1Script.AppendLine($"'正在复制 {sourceFile}'");
            string sourceFileWithNoWildcards = sourceFile.Replace("`", "``").Replace("[", "`[")
                .Replace("]", "`]").Replace("?", "`?").Replace("?", "`?");
            ps1Script.AppendLine(
                $"Start-BitsTransfer -Source '{sourceFileWithNoWildcards}' -Destination '{file.TempName}' -DisplayName '正在复制文件' -Description '{sourceFile} => {file.TempName}'");
            ps1Script.AppendLine($"}}");
        }

        private static string GetTempFileName(SyncFileInfo file, SHA256 sha256)
        {
            string featureCode = $"{file.TopDirectory}{file.RelativePath}{file.Time}{file.Length}";

            var bytes = Encoding.UTF8.GetBytes(featureCode);
            var code = sha256.ComputeHash(bytes);
            return Convert.ToHexString(code);
        }

        private async Task CopyFileAsync(string source, string destination,
            Progress<FileProcessProgress> progress,
            CancellationToken cancellationToken)
        {
            if (Config.EnableEncryption)
            {
                await aes.EncryptFileAsync(source, destination, progress: progress,
                    cancellationToken: cancellationToken);
                File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source));
            }
            else
            {
                await FileCopyHelper.CopyFileAsync(source, destination, progress: progress,
                    cancellationToken: cancellationToken);
            }
        }

        private void FinishScript(StringBuilder batScript, StringBuilder ps1Script)
        {
            batScript.AppendLine("echo 复制完成");
            batScript.AppendLine("pause");
            var encoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
            File.WriteAllText(Path.Combine(Config.PatchDir, "CopyToHere.bat"), batScript.ToString(), encoding);

            ps1Script.AppendLine("\"复制完成\"");
            ps1Script.AppendLine("pause");
            File.WriteAllText(Path.Combine(Config.PatchDir, "CopyToHere.ps1"), ps1Script.ToString(),
                Encoding.UTF8);
        }
    }
}