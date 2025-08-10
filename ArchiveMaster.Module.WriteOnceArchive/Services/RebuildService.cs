using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Cryptography;
using FzLib.IO;
using System.Security.Cryptography;
using System.Text.Json;
using WriteOnceFile = ArchiveMaster.ViewModels.FileSystem.WriteOnceFile;

namespace ArchiveMaster.Services
{
    public class RebuildService(AppConfig appConfig) : TwoStepServiceBase<RebuildConfig>(appConfig)
    {
        public TreeDirInfo FileTree { get; private set; }
        public List<WriteOnceFile> MatchedFiles { get; private set; }

        public override Task ExecuteAsync(CancellationToken token)
        {
            return Task.Run(async () =>
            {
                var files = MatchedFiles.CheckedOnly().ToList();

                Aes aes = null;
                if (files.Any(p => p.IsEncrypted))
                {
                    if (string.IsNullOrWhiteSpace(Config.Password))
                    {
                        throw new Exception("部分文件被加密，但未提供密码");
                    }

                    aes = AesHelper.GetDefaultAes(Config.Password);
                }

                await TryForFilesAsync(files, async (file, s) =>
                {
                    string numMsg = s.GetFileNumberMessage("{0}/{1}");

                    Progress<FileProcessProgress> progress = new Progress<FileProcessProgress>(p =>
                    {
                        NotifyProgress(1.0 * (s.AccumulatedLength + p.ProcessedBytes) / s.TotalLength);
                        NotifyMessage(
                            $"正在计算Hash（{numMsg}，本文件{1.0 * p.ProcessedBytes / 1024 / 1024:0}MB/{1.0 * p.TotalBytes / 1024 / 1024:0}MB）：{file.RelativePath}");
                    });

                    var targetFile = Path.Combine(Config.TargetDir, file.RelativePath);
                    if (File.Exists(targetFile))
                    {
                        if (Config.SkipIfExisted)
                        {
                            file.Complete("目标文件已存在，跳过");
                            return; //continue
                        }
                        else
                        {
                            FileHelper.DeleteByConfig(targetFile);
                        }
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                    //后续加入检查Hash
                    if (file.IsEncrypted)
                    {
                        await aes.DecryptFileAsync(file.PhysicalFile, targetFile, progress: progress,
                            cancellationToken: token);
                    }
                    else
                    {
                        await FileCopyHelper.CopyFileAsync(file.PhysicalFile, targetFile, progress: progress,
                            cancellationToken: token);
                    }

                    File.SetLastWriteTime(targetFile, file.Time);
                }, token, FilesLoopOptions.Builder().AutoApplyFileLengthProgress().AutoApplyStatus().Build());
            }, token);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return MatchedFiles;
        }

        private async Task<List<WriteOnceFileInfo>> GetAllFilesAsync(IEnumerable<string> sourceDirs)
        {
            string file = Config.PackageInfoFile;
            if (string.IsNullOrWhiteSpace(file))
            {
                file = sourceDirs
                    .Select(p => Path.Combine(p, WriteOnceArchiveParameters.PackageInfoFileName))
                    .Where(File.Exists)
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            {
                throw new FileNotFoundException("未找到包信息文件");
            }

            var json = await File.ReadAllTextAsync(file);
            var info = JsonSerializer.Deserialize<WriteOncePackageInfo>(json);
            if (info.AllFiles == null)
            {
                throw new Exception("包信息文件格式错误，不包含目录结构");
            }

            return info.AllFiles;
        }

        public override async Task InitializeAsync(CancellationToken token)
        {
            NotifyMessage("正在建立文件树");
            TreeDirInfo tree = TreeDirInfo.CreateEmptyTree();
            var sourceDirs = FileNameHelper.GetDirNames(Config.SourceDirs);
            var allFiles = await GetAllFilesAsync(sourceDirs);
            Dictionary<string, object> hash2Files = new Dictionary<string, object>();
            List<WriteOnceFile> matchFiles = new List<WriteOnceFile>();
            await Task.Run(() =>
            {
                //构建文件树
                foreach (var modelFile in allFiles)
                {
                    var vmFile = new WriteOnceFile
                    {
                        Name = Path.GetFileName(modelFile.RelativePath),
                        Length = modelFile.Length,
                        Time = modelFile.LastWriteTime,
                        Hash = modelFile.Hash
                    };
                    vmFile.SetRelativePath(modelFile.RelativePath);
                    tree.AddFile(vmFile);
                    if (hash2Files.ContainsKey(modelFile.Hash))
                    {
                        if (hash2Files[vmFile.Hash] is WriteOnceFile anotherFile)
                        {
                            hash2Files[vmFile.Hash] = new List<WriteOnceFile>() { anotherFile, vmFile };
                        }
                        else
                        {
                            ((List<WriteOnceFile>)hash2Files[vmFile.Hash]).Add(vmFile);
                        }
                    }
                    else
                    {
                        hash2Files.Add(vmFile.Hash, vmFile);
                    }
                }

                foreach (var dir in sourceDirs)
                {
                    var phyFiles = Directory.GetFiles(dir);
                    foreach (var phyFile in phyFiles)
                    {
                        bool hasEncrypt = false;
                        string name = Path.GetFileName(phyFile);
                        if (name.EndsWith(WriteOnceArchiveParameters.EncryptedFileSuffix))
                        {
                            name = name[..^WriteOnceArchiveParameters.EncryptedFileSuffix.Length];
                            hasEncrypt = true;
                        }

                        if (!FileHelper.IsValidHashString(name, WriteOnceArchiveParameters.HashType))
                        {
                            continue;
                        }

                        if (!hash2Files.ContainsKey(name))
                        {
                            continue;
                        }

                        if (hash2Files[name] is WriteOnceFile file)
                        {
                            file.HasPhysicalFile = true;
                            matchFiles.Add(file);
                            file.PhysicalFile = phyFile;
                            file.IsEncrypted = hasEncrypt;
                        }
                        else
                        {
                            foreach (var p in (List<WriteOnceFile>)hash2Files[name])
                            {
                                p.HasPhysicalFile = true;
                                matchFiles.Add(p);
                                p.PhysicalFile = phyFile;
                                p.IsEncrypted = hasEncrypt;
                            }
                        }
                    }
                }
            }, token);
            FileTree = tree;
            MatchedFiles = matchFiles;
        }
    }
}