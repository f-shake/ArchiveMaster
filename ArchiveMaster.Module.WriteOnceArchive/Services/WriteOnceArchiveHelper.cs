using System.Collections.Frozen;
using System.Text.Json;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.IO;

namespace ArchiveMaster.Services;

public static class WriteOnceArchiveHelper
{
    public static Task<(TreeDirInfo, List<WriteOnceFile>, RebuildInitializeReport)> ReadPackageFilesAsync(
        WriteOncePackageInfo packageInfo, ICollection<string> packageDirs, bool checkMissingFiles, CancellationToken ct)
    {
        return Task.Run<(TreeDirInfo, List<WriteOnceFile>, RebuildInitializeReport)>(() =>
        {
            List<WriteOnceFile> files = new List<WriteOnceFile>();
            HashSet<string> allHashes = new HashSet<string>();
            (var tree, var hash2Files) = GetHashFileMap(packageInfo.AllFiles);

            foreach (var packageDir in packageDirs)
            {
                var phyFiles = Directory.GetFiles(packageDir);
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
                        if (checkMissingFiles)
                        {
                            var f = new WriteOnceFile
                            {
                                Hash = hash,
                                PhysicalFile = phyFile,
                                HasPhysicalFile = true,
                                IsEncrypted = hasEncrypt,
                                ErrorNotInFileList = true,
                                Length = new FileInfo(phyFile).Length
                            };
                            f.Warn("包信息中没有这个文件");
                            files.Add(f);
                        }

                        continue;
                    }

                    if (checkMissingFiles)
                    {
                        packageInfoHashes.Remove(hash);
                    }

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

                if (packageDirs.Count == 1 && checkMissingFiles)
                {
                    //只有当单一目录时，才检查缺失文件
                    foreach (var hash in packageInfoHashes)
                    {
                        // 说明包信息中有这个文件，但物理上没有
                        var f = new WriteOnceFile
                        {
                            Hash = hash,
                            PhysicalFile = null,
                            HasPhysicalFile = false,
                            ErrorNoPhysicalFile = true,
                            Length = hash2Files[hash] is WriteOnceFile file
                                ? file.Length
                                : ((List<WriteOnceFile>)hash2Files[hash])[0].Length,
                        };
                        f.Error("不存在对应的物理文件");
                        files.Add(f);
                    }
                }
            }


            var initializeReport = new RebuildInitializeReport
            {
                TotalFiles = new FileCountLength(tree.SubFileCount, tree.Flatten().Sum(p => p.Length)),
                PackageFiles = new FileCountLength(files.Count(p => !p.ErrorNotInFileList),
                    files.Where(p => !p.ErrorNotInFileList).Sum(p => p.Length)),
                FoundPhysicalFiles = new FileCountLength(files.Count(p => p.Status == ProcessStatus.Ready),
                    files.Where(p => p.Status == ProcessStatus.Ready).Sum(p => p.Length)),
                PackageTime = packageInfo.PackageTime,
                UnreferencedFiles = new FileCountLength(files.Count(p => p.ErrorNotInFileList),
                    files.Where(p => p.ErrorNotInFileList).Sum(p => p.Length)),
                LostFiles = new FileCountLength(files.Count(p => p.ErrorNoPhysicalFile),
                    files.Where(p => p.ErrorNoPhysicalFile).Sum(p => p.Length))
            };
            return (tree, files, initializeReport);
        }, ct);
    }

    public static (TreeDirInfo Tree, IDictionary<string, object> hash2Files) GetHashFileMap(
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

    public static async Task<WriteOncePackageInfo> ReadPackageInfoAsync(IEnumerable<string> packageDirs,
        string forcePackageInfoFile)
    {
        string packageInfoFile = null;
        if (string.IsNullOrWhiteSpace(forcePackageInfoFile))
        {
            packageInfoFile = packageDirs
                .Select(p => Path.Combine(p, WriteOnceArchiveParameters.PackageInfoFileName))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
        }
        else
        {
            packageInfoFile = forcePackageInfoFile;
        }

        if (string.IsNullOrWhiteSpace(packageInfoFile) || !File.Exists(packageInfoFile))
        {
            throw new FileNotFoundException("未找到包信息文件");
        }

        var json = await File.ReadAllTextAsync(packageInfoFile);
        var info = JsonSerializer.Deserialize<WriteOncePackageInfo>(json);
        if (info.AllFiles == null)
        {
            throw new Exception("包信息文件格式错误，不包含目录结构");
        }

        return info;
    }
}