using System.Diagnostics;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Cryptography;
using FzLib.IO;
using System.Security.Cryptography;
using System.Text.Json;
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels;
using WriteOnceFile = ArchiveMaster.ViewModels.FileSystem.WriteOnceFile;

namespace ArchiveMaster.Services
{
    public class VerifyService(AppConfig appConfig) : TwoStepServiceBase<VerifyConfig>(appConfig)
    {
        public TreeDirInfo FileTree { get; private set; }
        public List<WriteOnceFile> Files { get; private set; }

        public RebuildInitializeReport Report { get; private set; }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                Aes aes = null;
                var files = Files.Where(p => p.Status == ProcessStatus.Ready).ToList();
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
                    try
                    {
                        byte[] hash;

                        Stopwatch sw = Stopwatch.StartNew();
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

                        sw.Stop();
                        file.ReadTimeCostSecond = sw.Elapsed.TotalSeconds;
                        Debug.WriteLine(sw.Elapsed);

                        Debug.Assert(hash != null);
                        if (hash != null)
                        {
                            var hashString = Convert.ToHexString(hash);
                            if (hashString != file.Hash)
                            {
                                file.ErrorHashNotMatched = true;
                                file.Error($"重建后的文件Hash{hashString}与源文件{file.Hash}不一致");
                            }

                            file.Success();
                        }
                        else
                        {
                            file.ErrorFileReadFailed = true;
                            file.Error($"文件Hash获取失败（未知原因）");
                        }
                    }
                    catch (Exception ex)
                    {
                        file.ErrorFileReadFailed = true;
                        file.Error($"文件验证失败：{ex.Message}");
                    }
                }, ct, FilesLoopOptions.Builder().AutoApplyFileLengthProgress().Build());
                
                Report.TotalReadTimeCostSecond= files.Sum(p => p.ReadTimeCostSecond);
            }, ct);
            
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return Files;
        }


        public override async Task InitializeAsync(CancellationToken ct)
        {
            NotifyMessage("正在建立文件树");
            TreeDirInfo tree = null;
            var packageInfo = await WriteOnceArchiveHelper.ReadPackageInfoAsync([Config.PackageDir], null);
            RebuildInitializeReport initializeReport = null;
            IDictionary<string, object> hash2Files = null;
            List<WriteOnceFile> files = new List<WriteOnceFile>();
            await Task.Run(() =>
            {
                HashSet<string> allHashes = new HashSet<string>();
                (tree, hash2Files) = WriteOnceArchiveHelper.GetHashFileMap(packageInfo.AllFiles);

                // foreach (var hash in packageInfo.Hashes)
                // {
                //     if (!FileHashHelper.IsValidHashString(hash, WriteOnceArchiveParameters.HashType))
                //     {
                //         continue;
                //     }
                //
                //     var unEncryptedFile = Path.Combine(Config.PackageDir, hash);
                //     var encryptedFile = unEncryptedFile + WriteOnceArchiveParameters.EncryptedFileSuffix;
                //     var physicalFile = File.Exists(unEncryptedFile) ? unEncryptedFile :
                //         File.Exists(encryptedFile) ? encryptedFile : null;
                //     
                //
                //     var file = new WriteOnceFile
                //     {
                //         Hash = hash,
                //         PhysicalFile = physicalFile,
                //         HasPhysicalFile = physicalFile != null,
                //         ErrorNoPhysicalFile = physicalFile == null,
                //     };
                //     if (hash2Files.TryGetValue(hash, out var fileOrFiles))
                //     {
                //         if (fileOrFiles is WriteOnceFile f)
                //         {
                //             file.SetRelativePath(f.RelativePath);
                //         }
                //         else if (fileOrFiles is List<WriteOnceFile> fs)
                //         {
                //             file.SetRelativePath(fs[0].RelativePath);
                //         }
                //         else
                //         {
                //             Debug.Assert(false);
                //         }
                //     }
                //     else
                //     {
                //         file.ErrorNotInFileList = true;
                //     }
                // }

                var phyFiles = Directory.GetFiles(Config.PackageDir);
                var packageInfoHashes = packageInfo.Hashes;
                foreach (var phyFile in phyFiles)
                {
                    bool hasEncrypt = false;
                    string hash = Path.GetFileName(phyFile);
                    if (hash.EndsWith(WriteOnceArchiveParameters.EncryptedFileSuffix))
                    {
                        hash = hash[..^WriteOnceArchiveParameters.EncryptedFileSuffix.Length];
                        hasEncrypt = true;
                    }

                    if (!FileHashHelper.IsValidHashString(hash, WriteOnceArchiveParameters.HashType))
                    {
                        continue;
                    }

                    if (!allHashes.Add(hash))
                    {
                    }

                    if (!hash2Files.ContainsKey(hash)) //包信息中没有这个文件
                    {
                        var f = new WriteOnceFile
                        {
                            Hash = hash,
                            PhysicalFile = phyFile,
                            HasPhysicalFile = true,
                            IsEncrypted = hasEncrypt,
                            ErrorNotInFileList = true,
                        };
                        f.Warn("包信息中没有这个文件");
                        files.Add(f);

                        continue;
                    }

                    packageInfoHashes.Remove(hash);
                    if (hash2Files[hash] is WriteOnceFile file)
                    {
                        file.HasPhysicalFile = true;
                        files.Add(file);
                        file.PhysicalFile = phyFile;
                        file.IsEncrypted = hasEncrypt;
                    }
                    else
                    {
                        foreach (var p in (List<WriteOnceFile>)hash2Files[hash])
                        {
                            p.HasPhysicalFile = true;
                            files.Add(p);
                            p.PhysicalFile = phyFile;
                            p.IsEncrypted = hasEncrypt;
                        }
                    }
                }

                foreach (var hash in packageInfoHashes)
                {
                    // 说明包信息中有这个文件，但物理上没有
                    var f = new WriteOnceFile
                    {
                        Hash = hash,
                        PhysicalFile = null,
                        HasPhysicalFile = false,
                        ErrorNoPhysicalFile = true,
                    };
                    f.Error("不存在对应的物理文件");
                    files.Add(f);
                }


                initializeReport = new RebuildInitializeReport
                {
                    TotalFileCount = tree.SubFileCount,
                    TotalFileLength = tree.Flatten().Sum(p => p.Length),
                    MatchedFileCount = files.Count,
                    MatchedFileLength = files.Sum(p => p.Length)
                };
            }, ct);

            FileTree = tree;
            Files = files;
            Report = initializeReport;
        }
    }
}