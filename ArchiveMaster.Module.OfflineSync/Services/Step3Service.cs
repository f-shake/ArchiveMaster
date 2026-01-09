using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using FzLib.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Cryptography;
using SyncFileInfo = ArchiveMaster.ViewModels.FileSystem.SyncFileInfo;

namespace ArchiveMaster.Services
{
    public class Step3Service(AppConfig appConfig) : TwoStepServiceBase<OfflineSyncStep3Config>(appConfig)
    {
        private readonly DateTime createTime = DateTime.Now;
        private Aes aes;
        public List<SyncFileInfo> DeletingDirectories { get; private set; }
        public Dictionary<string, List<string>> LocalDirectories { get; private set; }
        public List<SyncFileInfo> UpdateFiles { get; private set; }


        public Task DeleteEmptyDirectoriesAsync()
        {
            return Task.Run(() =>
            {
                foreach (var dir in DeletingDirectories)
                {
                    Delete(dir.TopDirectory, dir.Path);
                }
            });
        }

        public override async Task ExecuteAsync(CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(Config.Password))
            {
                aes = AesHelper.GetDefaultAes(Config.Password);
            }

            long totalLength = 0;
            List<SyncFileInfo> files = null;
            await Task.Run(async () =>
            {
                var updateFiles = UpdateFiles.Where(p => p.IsChecked).ToList();
                totalLength = updateFiles
                    .Where(p => p.UpdateType is not (FileUpdateType.Delete or FileUpdateType.Move))
                    .Sum(p => p.Length);
                files = updateFiles.OrderByDescending(p => p.UpdateType).ToList();

                long length = 0;
                await TryForFilesAsync(files, async (file, s) =>
                {
                    //先处理移动，然后处理修改，这样能避免一些问题（2022-12-17）
                    string numMsg = s.GetFileNumberMessage("{0}/{1}");
                    NotifyMessage($"正在处理（{numMsg}）：{file.RelativePath}");
                    string patch = file.TempName == null ? null : Path.Combine(Config.PatchDir, file.TempName);
                    if (file.UpdateType is not (FileUpdateType.Delete or FileUpdateType.Move) &&
                        !File.Exists(patch))
                    {
                        throw new Exception("补丁文件不存在");
                    }

                    string target = file.Path;
                    string oldPath = file.OldRelativePath == null
                        ? null
                        : Path.Combine(file.TopDirectory, file.OldRelativePath);
                    if (!Directory.Exists(Path.GetDirectoryName(target)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                    }

                    Task CopyThisFileAsync()
                    {
                        return CopyFileAsync(patch, target, file.Time,
                            s.CreateFileProgressReporter("正在复制", p => length + p.ProcessedBytes, () => totalLength,
                                p => Path.GetFileName(p.DestinationFilePath)),
                            ct);
                    }

                    switch (file.UpdateType)
                    {
                        case FileUpdateType.Add:
                            if (File.Exists(target))
                            {
                                Delete(file.TopDirectory, target);
                            }

                            await CopyThisFileAsync();
                            break;
                        case FileUpdateType.Modify:
                            if (File.Exists(target))
                            {
                                Delete(file.TopDirectory, target);
                            }

                            await CopyThisFileAsync();
                            break;
                        case FileUpdateType.Delete:
                            if (!File.Exists(target))
                            {
                                throw new Exception("应当为待删除文件，但文件不存在");
                            }

                            Delete(file.TopDirectory, target);
                            break;

                        case FileUpdateType.Move:
                            if (!File.Exists(oldPath))
                            {
                                throw new Exception("应当为移动后文件，但源文件不存在");
                            }
                            else if (File.Exists(target))
                            {
                                throw new Exception("应当为移动后文件，但目标文件已存在");
                            }

                            File.Move(oldPath, target);
                            break;
                        default:
                            throw new InvalidEnumArgumentException();
                    }
                }, ct, FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileNumberProgress().Finally(file =>
                {
                    var f = file as SyncFileInfo;
                    if (f.UpdateType is FileUpdateType.Add or FileUpdateType.Modify)
                    {
                        length += f.Length;
                    }

                    NotifyProgress(1.0 * length / totalLength);
                }).Build());

                NotifyMessage($"正在查找空目录");
                NotifyProgressIndeterminate();
                AnalyzeEmptyDirectories(ct);
            }, ct);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return UpdateFiles.Cast<SimpleFileInfo>();
        }

        public override async Task InitializeAsync(CancellationToken ct = default)
        {
            var patchFile = Path.Combine(Config.PatchDir, "file.os2");
            if (!File.Exists(patchFile))
            {
                throw new FileNotFoundException("目录中不存在os2文件");
            }

            await Task.Run(() =>
            {
                var step2 = ZipService.ReadFromZip<Step2Model>(patchFile);

                UpdateFiles = step2.Files;
                LocalDirectories = step2.LocalDirectories;
                if (string.IsNullOrWhiteSpace(Config.Password))
                {
                    if (UpdateFiles.Any(p =>
                            p.TempName != null && p.TempName.EndsWith(Step2Service.EncryptionFileSuffix)))
                    {
                        throw new ArgumentException("备份文件已加密，但没有提供密码");
                    }
                }

                TryForFiles(UpdateFiles, (file, s) =>
                {
                    string patch = file.TempName == null ? null : Path.Combine(Config.PatchDir, file.TempName);

                    string target = file.Path;
                    string oldPath = file.OldRelativePath == null
                        ? null
                        : Path.Combine(file.TopDirectory, file.OldRelativePath);
                    if (file.UpdateType is not (FileUpdateType.Delete or FileUpdateType.Move) && !File.Exists(patch))
                    {
                        file.Warn("补丁文件不存在");
                        file.IsChecked = false;
                    }
                    else
                    {
                        NotifyMessage($"正在处理{s.GetFileNumberMessage()}：{file.Path}");
                        switch (file.UpdateType)
                        {
                            case FileUpdateType.Add:
                                if (File.Exists(target))
                                {
                                    file.Warn("应当为新增文件，但文件已存在");
                                    file.IsChecked = false;
                                }

                                break;
                            case FileUpdateType.Modify:
                                if (!File.Exists(target))
                                {
                                    file.Warn("应当为修改后文件，但文件不存在");
                                }

                                break;
                            case FileUpdateType.Delete:
                                if (!File.Exists(target))
                                {
                                    file.Warn("应当为待删除文件，但文件不存在");
                                    file.IsChecked = false;
                                }

                                break;
                            case FileUpdateType.Move:
                                if (!File.Exists(oldPath))
                                {
                                    file.Warn("应当为移动后文件，但源文件不存在");
                                    file.IsChecked = false;
                                }
                                else if (File.Exists(target))
                                {
                                    file.Warn("应当为移动后文件，但目标文件已存在");
                                    file.IsChecked = false;
                                }

                                break;
                            default:
                                throw new InvalidEnumArgumentException();
                        }
                    }
                }, ct, FilesLoopOptions.Builder().AutoApplyFileNumberProgress().Build());
            }, ct);
        }


        private void AnalyzeEmptyDirectories(CancellationToken ct)
        {
            DeletingDirectories = new List<SyncFileInfo>();
            string[] systemFiles = ["thumbs.db", "thumb.db", ".DS_Store", "desktop.ini"];

            foreach (var topDir in LocalDirectories.Keys)
            {
                if (!Directory.Exists(topDir))
                {
                    continue;
                }

                //20251214更新
                //原来存在一个BUG，假设异地目录中有A目录，下面有B目录，B目录下有文件，A目录下仅有B目录而无文件。
                //如果A目录恰好在Step2中被加入黑名单，那么它将不会出现在LocalDirectories[topDir]中。
                //此时，原来的算法会查找A目录下有无顶级文件，发现没有，则会误删A目录。
                //本次更新尝试修复了这些问题，首先检查子目录，如果子目录包含文件，会将该子目录的所有父目录标记为包含文件，
                //当后续检查到该目录时，会跳过该目录的检查。
                //这还同时增加了检查空目录的速度，因为如果某个目录下没有文件，则该目录的所有子目录也一定没有文件，因此可以直接跳过检查。
                HashSet<string> deletingDirsInThisTopDir = new HashSet<string>();
                HashSet<string> dirsContainingFiles = new HashSet<string>();
                foreach (var offsiteSubDir in Directory
                             .EnumerateDirectories(topDir, "*", SearchOption.AllDirectories)
                             .OrderByDescending(p => p.Length) //从最长路径开始，确保先检查子目录
                             .ToList())
                {
                    ct.ThrowIfCancellationRequested();
                    if (LocalDirectories[topDir].Contains(Path.GetRelativePath(topDir, offsiteSubDir)))
                    {
                        //确保本地已经没有远程的这个目录了
                        continue;
                    }

                    if (dirsContainingFiles.Contains(offsiteSubDir))
                    {
                        //该目录的某个子目录包含文件，因此该目录不可以删除。
                        //通过剪枝，避免重复检查
                        continue;
                    }

                    var allFiles = Directory.EnumerateFiles(offsiteSubDir);
                    if (!allFiles.Any() //并且远程的这个目录是空的
                        || allFiles.All(p =>
                            systemFiles.Contains(Path.GetFileName(p), StringComparer.OrdinalIgnoreCase))) //或者仅有缩略图
                    {
                        deletingDirsInThisTopDir.Add(offsiteSubDir);
                    }
                    else
                    {
                        var tempDir = offsiteSubDir;
                        while (tempDir != null && tempDir.Length > topDir.Length && tempDir != topDir) //直到找到顶层目录
                        {
                            //当前，offsiteSubDir是包含文件的目录，因此它的父目录也应该包含文件，不可以删除
                            tempDir = Path.GetDirectoryName(tempDir);
                            dirsContainingFiles.Add(tempDir);
                        }
                    }
                }


                //通过两层循环，删除位于空目录下的空目录
                foreach (var dir1 in deletingDirsInThisTopDir.ToList()) //外层循环，dir1为内层空目录
                {
                    ct.ThrowIfCancellationRequested();
                    foreach (var dir2 in deletingDirsInThisTopDir) //内层循环，dir2为外层空目录
                    {
                        if (dir1 == dir2)
                        {
                            continue;
                        }

                        if (dir1.StartsWith(dir2)) //如果dir2位于dir1的外层，那么dir1就不需要单独删除
                        {
                            deletingDirsInThisTopDir.Remove(dir1);
                            break;
                        }
                    }
                }

                DeletingDirectories.AddRange(deletingDirsInThisTopDir.Select(p => new SyncFileInfo()
                    { Path = p, TopDirectory = topDir }));
            }
        }

        private async Task CopyFileAsync(string source, string destination,
            DateTime fileTime,
            Progress<FileProcessProgress> progress,
            CancellationToken cancellationToken)
        {
            if (source.EndsWith(Step2Service.EncryptionFileSuffix))
            {
                if (string.IsNullOrWhiteSpace(Config.Password))
                {
                    throw new ArgumentException("备份文件已加密，但没有提供密码");
                }

                await aes.DecryptFileAsync(source, destination, progress: progress,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await FileCopyHelper.CopyFileAsync(source, destination, progress: progress,
                    cancellationToken: cancellationToken);
            }

            File.SetLastWriteTime(destination, fileTime);
        }

        private void Delete(string rootDir, string filePath)
        {
            if (!filePath.StartsWith(rootDir))
            {
                throw new ArgumentException("文件不在目录中");
            }

            string relative = Path.GetRelativePath(rootDir, filePath); //文件关于备份根目录的相对路径
            string absRootDir = Path.GetPathRoot(filePath); //文件的绝对根目录（如，"C:\","/"）
            string absRelative = Path.GetRelativePath(absRootDir, rootDir); //备份目录关于根目录的相对路径
            string backupPathDir = absRelative
                .Replace('\\', GlobalConfigs.Instance.FlattenPathSeparatorReplacement)
                .Replace('/', GlobalConfigs.Instance.FlattenPathSeparatorReplacement);
            string specialRelativePath = Path.Combine(backupPathDir, relative);
            FileHelper.DeleteByConfig(filePath, "异地备份离线同步", specialRelativePath);
        }
    }
}