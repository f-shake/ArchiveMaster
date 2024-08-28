﻿using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using FzLib.Collection;
using ArchiveMaster.Model;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ArchiveMaster.Configs;
using static System.Net.Mime.MediaTypeNames;
using ArchiveMaster.Enums;
using ArchiveMaster.Utilities;

namespace ArchiveMaster.Utilities
{
    public class Step2Utility(Step2Config config) : TwoStepUtilityBase<Step2Config>(config)
    {
        public override Step2Config Config { get; } = config;
        public Dictionary<string, List<string>> LocalDirectories { get; } = new Dictionary<string, List<string>>();
        public List<SyncFileInfo> UpdateFiles { get; } = new List<SyncFileInfo>();

        public static async Task<IList<LocalAndOffsiteDir>> MatchLocalAndOffsiteDirsAsync(string snapshotPath,
            string[] localSearchingDirs)
        {
            Step1Model step1 = ZipUtility.ReadFromZip<Step1Model>(snapshotPath);
            var matchingDirs =
                step1.Files
                    .Select(p => p.TopDirectory)
                    .Distinct()
                    .Select(p => new LocalAndOffsiteDir() { OffsiteDir = p, })
                    .ToList();
            await Task.Run(() =>
            {
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

        public override async Task ExecuteAsync(CancellationToken token = default)
        {
            if (!Directory.Exists(Config.PatchDir))
            {
                Directory.CreateDirectory(Config.PatchDir);
            }

            NotifyProgressIndeterminate();
            await Task.Run(() =>
            {
                var files = UpdateFiles.Where(p => p.IsChecked).ToList();
                Dictionary<string, string> offsiteTopDir2LocalDir =
                    Config.MatchingDirs.ToDictionary(p => p.OffsiteDir, p => p.LocalDir);
                long totalLength = files.Where(p => p.UpdateType is not (FileUpdateType.Delete or FileUpdateType.Move))
                    .Sum(p => p.Length);
                long length = 0;
                StringBuilder batScript = new StringBuilder();
                StringBuilder ps1Script = new StringBuilder();
                batScript.AppendLine("@echo off");
                ps1Script.AppendLine("Import-Module BitsTransfer");
                using var sha256 = SHA256.Create();

                TryForFiles(files, (file, s) =>
                {
                    if (file.UpdateType is FileUpdateType.Delete or FileUpdateType.Move)
                    {
                        return;
                    }

                    file.TempName = GetTempFileName(file, sha256);
                    NotifyMessage($"正在处理{s.GetFileNumberMessage()}：{file.RelativePath}");
                    string sourceFile = Path.Combine(offsiteTopDir2LocalDir[file.TopDirectory], file.RelativePath);
                    string destFile = Path.Combine(Config.PatchDir, file.TempName);
                    if (File.Exists(destFile))
                    {
                        FileInfo existingFile = new FileInfo(destFile);
                        if (existingFile.Length == file.Length
                            && existingFile.LastWriteTime == file.Time
                            && Config.ExportMode != ExportMode.Script)
                        {
                            return;
                        }

                        try
                        {
                            File.Delete(destFile);
                        }
                        catch (IOException ex)
                        {
                            throw new IOException(
                                $"修改时间或长度与待写入文件{file.RelativePath}不同的目标补丁文件{destFile}已存在，但无法删除：{ex.Message}", ex);
                        }
                    }

                    switch (Config.ExportMode)
                    {
                        case ExportMode.PreferHardLink:
                            try
                            {
                                CreateHardLink(destFile, sourceFile);
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
                                if (tryCount < 9 && File.Exists(destFile))
                                {
                                    File.Delete(destFile);
                                }

                                try
                                {
                                    File.Copy(sourceFile, destFile);
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
                            CreateHardLink(destFile, sourceFile);
                            break;
                        case ExportMode.Script:
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
                            break;
                    }
                }, token, FilesLoopOptions.Builder().AutoApplyStatus().Finally(file =>
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
                    batScript.AppendLine("echo 复制完成");
                    batScript.AppendLine("pause");
                    var encoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
                    File.WriteAllText(Path.Combine(Config.PatchDir, "CopyToHere.bat"), batScript.ToString(), encoding);

                    ps1Script.AppendLine("\"复制完成\"");
                    ps1Script.AppendLine("pause");
                    File.WriteAllText(Path.Combine(Config.PatchDir, "CopyToHere.ps1"), ps1Script.ToString(),
                        Encoding.UTF8);
                }

                Step2Model model = new Step2Model()
                {
                    Files = files,
                    LocalDirectories = LocalDirectories
                };

                ZipUtility.WriteToZip(model, Path.Combine(Config.PatchDir, "file.os2"));
            }, token);
        }

        public override async Task InitializeAsync(CancellationToken token = default)
        {
            bool checkMoveIgnoreFileName = false;
            UpdateFiles.Clear();
            LocalDirectories.Clear();
            int index = 0;
            NotifyProgressIndeterminate();
            NotifyMessage($"正在初始化");
            var blacks = new BlackListUtility(Config.BlackList, Config.BlackListUseRegex);
            await Task.Run(() =>
            {
                var step1Model = ZipUtility.ReadFromZip<Step1Model>(Config.OffsiteSnapshot);
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
                    NotifyMessage($"正在查找：{localDir}");
                    var localFileList = localDir.EnumerateFiles("*", SearchOption.AllDirectories).ToList();
                    var localFilePathSet = localFileList.Select(p => p.FullName).ToHashSet();

                    //从路径、文件名、时间、长度寻找本地文件的字典
                    string offsiteTopDirectory = localAndOffsiteDir.OffsiteDir;
                    Dictionary<string, SyncFileInfo> offsitePath2File =
                        offsiteTopDir2Files[offsiteTopDirectory].ToDictionary(p => p.RelativePath);
                    Dictionary<string, List<SyncFileInfo>> offsiteName2File = offsiteTopDir2Files[offsiteTopDirectory]
                        .GroupBy(p => p.Name).ToDictionary(p => p.Key, p => p.ToList());
                    Dictionary<DateTime, List<SyncFileInfo>> offsiteTime2File = offsiteTopDir2Files[offsiteTopDirectory]
                        .GroupBy(p => p.Time).ToDictionary(p => p.Key, p => p.ToList());
                    Dictionary<long, List<SyncFileInfo>> offsiteLength2File = offsiteTopDir2Files[offsiteTopDirectory]
                        .GroupBy(p => p.Length).ToDictionary(p => p.Key, p => p.ToList());


                    foreach (var file in localFileList)
                    {
                        token.ThrowIfCancellationRequested();

                        string relativePath = Path.GetRelativePath(localDir.FullName, file.FullName);
                        NotifyMessage($"正在比对第（{++index} 个）：{relativePath}");
                        localFiles.Add(Path.Combine(localDir.Name, relativePath), 0);

                        if (blacks.IsInBlackList(file))
                        {
                            continue;
                        }

                        if (offsitePath2File.TryGetValue(relativePath, out var offsiteFile)) //路径相同，说明是没有变化或者文件被修改
                        {
                            if ((offsiteFile.Time - file.LastWriteTime).Duration().TotalSeconds <
                                OfflineSyncConfig.MaxTimeTolerance
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
                            if ((offsiteFile.Time - file.LastWriteTime).TotalSeconds >
                                OfflineSyncConfig.MaxTimeTolerance)
                            {
                                newFile.Warn("异地文件时间晚于本地文件时间");
                            }

                            UpdateFiles.Add(newFile);
                        }
                        else //新增文件或文件被移动或重命名
                        {
                            var sameFiles = !checkMoveIgnoreFileName
                                ? (offsiteTime2File.GetOrDefault(file.LastWriteTime) ??
                                   Enumerable.Empty<SyncFileInfo>())
                                .Intersect(offsiteLength2File.GetOrDefault(file.Length) ??
                                           Enumerable.Empty<SyncFileInfo>())
                                : (offsiteName2File.GetOrDefault(file.Name) ?? Enumerable.Empty<SyncFileInfo>())
                                .Intersect(offsiteTime2File.GetOrDefault(file.LastWriteTime) ??
                                           Enumerable.Empty<SyncFileInfo>())
                                .Intersect(offsiteLength2File.GetOrDefault(file.Length) ??
                                           Enumerable.Empty<SyncFileInfo>());
                            bool move = false;
                            if (sameFiles.Count() == 1)
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
                                localFiles.Add(Path.Combine(offsiteDir.Name, offsiteMovedFile.RelativePath),
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

                    token.ThrowIfCancellationRequested();

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
                        if (blacks.IsInBlackList(file))
                        {
                            continue;
                        }

                        NotifyMessage($"正在查找删除的文件：{++index} / {step1Model.Files.Count}");
                        if (!localFiles.ContainsKey(offsitePathWithTopDir))
                        {
                            file.UpdateType = FileUpdateType.Delete;
                            UpdateFiles.Add(file);
                        }
                    }
                }
            }, token);
        }

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName,
            IntPtr lpSecurityAttributes);

        private static void CreateHardLink(string link, string source)
        {
            if (!File.Exists(source))
            {
                throw new FileNotFoundException(source);
            }

            if (File.Exists(link))
            {
                File.Delete(link);
            }

            if (Path.GetPathRoot(link) != Path.GetPathRoot(source))
            {
                throw new IOException("硬链接的两者必须在同一个分区中");
            }

            bool value = CreateHardLink(link, source, IntPtr.Zero);
            if (!value)
            {
                throw new Exception($"未知错误，无法创建硬链接：" + Marshal.GetLastWin32Error());
            }
        }

        private static string GetTempFileName(SyncFileInfo file, SHA256 sha256)
        {
            string featureCode = $"{file.TopDirectory}{file.RelativePath}{file.Time}{file.Length}";

            var bytes = Encoding.UTF8.GetBytes(featureCode);
            var code = sha256.ComputeHash(bytes);
            return Convert.ToHexString(code);
        }
    }
}