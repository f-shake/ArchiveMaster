using System.Collections.Frozen;
using System.Diagnostics;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Cryptography;
using FzLib.IO;
using System.Security.Cryptography;
using System.Text.Json;
using ArchiveMaster.ViewModels;
using WriteOnceFile = ArchiveMaster.ViewModels.FileSystem.WriteOnceFile;

namespace ArchiveMaster.Services
{
    public class RebuildService(AppConfig appConfig) : TwoStepServiceBase<RebuildConfig>(appConfig)
    {
        public TreeDirInfo FileTree { get; private set; }
        public List<WriteOnceFile> MatchedFiles { get; private set; }

        public RebuildInitializeReport InitializeReport { get; private set; }

        public override Task ExecuteAsync(CancellationToken ct)
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
                    var targetFile = Path.Combine(Config.TargetDir, file.RelativePath);
                    if (File.Exists(targetFile))
                    {
                        if (Config.SkipIfExisted)
                        {
                            file.Skip();
                            return; //continue
                        }
                        else
                        {
                            FileHelper.DeleteByConfig(targetFile);
                        }
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(targetFile));

                    byte[] hash = null;
                    if (!Config.CheckOnly)
                    {
                        if (file.IsEncrypted)
                        {
                            hash = await aes.DecryptFileAsync(file.PhysicalFile, targetFile,
                                progress: s.CreateFileProgressReporter("正在重建文件"),
                                hashAlgorithmType: WriteOnceArchiveParameters.HashType, cancellationToken: ct);
                        }
                        else
                        {
                            hash = await FileCopyHelper.CopyFileAsync(file.PhysicalFile, targetFile,
                                progress: s.CreateFileProgressReporter("正在重建文件"),
                                hashAlgorithmType: WriteOnceArchiveParameters.HashType, cancellationToken: ct);
                        }

                        File.SetLastWriteTime(targetFile, file.Time);
                    }
                    else
                    {
                        if (file.IsEncrypted)
                        {
                            hash = await aes.GetDecryptedFileHashAsync(file.PhysicalFile,
                                progress: s.CreateFileProgressReporter("正在验证文件"),
                                hashAlgorithmType: WriteOnceArchiveParameters.HashType, cancellationToken: ct);
                        }
                        else
                        {
                            hash = await FileHashHelper.ComputeHashAsync(file.PhysicalFile,
                                WriteOnceArchiveParameters.HashType,
                                progress: s.CreateFileProgressReporter("正在验证文件"),
                                cancellationToken: ct);
                        }
                    }

                    Debug.Assert(hash != null);
                    if (hash != null)
                    {
                        var hashString = Convert.ToHexString(hash);
                        if (hashString != file.Hash)
                        {
                            file.Error($"重建后的文件Hash{hashString}与源文件{file.Hash}不一致");
                        }
                    }
                }, ct, FilesLoopOptions.Builder().AutoApplyFileLengthProgress().AutoApplyStatus().Build());
            }, ct);
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

        private (TreeDirInfo Tree, IDictionary<string, object> hash2Files) GetHashFileMap(
            IEnumerable<WriteOnceFileInfo> allFiles)
        {
            var hash2Files = new Dictionary<string, object>();
            TreeDirInfo tree = TreeDirInfo.CreateEmptyTree();
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

            return (tree, hash2Files.ToFrozenDictionary());
        }

        public override async Task InitializeAsync(CancellationToken ct)
        {
            NotifyMessage("正在建立文件树");
            TreeDirInfo tree = null;
            var sourceDirs = FileNameHelper.GetDirNames(Config.SourceDirs);
            List<WriteOnceFileInfo> allFiles = await GetAllFilesAsync(sourceDirs);
            RebuildInitializeReport initializeReport = null;
            IDictionary<string, object> hash2Files = null;
            List<WriteOnceFile> matchFiles = new List<WriteOnceFile>();
            await Task.Run(() =>
            {
                HashSet<string> allHashes = new HashSet<string>();
                (tree, hash2Files) = GetHashFileMap(allFiles);
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

                        if (!FileHashHelper.IsValidHashString(name, WriteOnceArchiveParameters.HashType))
                        {
                            continue;
                        }

                        if (!allHashes.Add(name))
                        {
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

                initializeReport = new RebuildInitializeReport
                {
                    TotalFileCount = tree.SubFileCount,
                    TotalFileLength = tree.Flatten().Sum(p => p.Length),
                    MatchedFileCount = matchFiles.Count,
                    MatchedFileLength = matchFiles.Sum(p => p.Length)
                };
            }, ct);

            FileTree = tree;
            MatchedFiles = matchFiles;
            InitializeReport = initializeReport;
        }
    }
}