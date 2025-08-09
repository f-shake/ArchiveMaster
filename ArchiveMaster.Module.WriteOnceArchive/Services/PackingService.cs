using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using DiscUtils.Iso9660;
using FzLib.IO;
using WriteOnceFile = ArchiveMaster.ViewModels.FileSystem.WriteOnceFile;

namespace ArchiveMaster.Services
{
    public class PackingService(AppConfig appConfig) : WriteOnceServiceBase<PackingConfig>(appConfig)
    {
        /// <summary>
        /// 记录整个源目录的文件结构，格式为<see cref="WriteOnceFileModel"/>的<see cref="List{T}"/>的JSON序列化
        /// </summary>
        public const string DirStructureFileName = "files.wds";

        /// <summary>
        /// 记录每个文件的Hash值，格式为每行一个文件，每行格式为：<see cref="SimpleFileInfo"/>的HashCode + '\t' + Hash值
        /// </summary>
        public const string HashCacheFileName = "caches.whc";

        /// <summary>
        /// 记录数据包中所有文件的Hash值，格式为每行一个Hash值
        /// </summary>
        public const string HashListFileName = "hashes.wph";

        private const FileHashHelper.HashAlgorithmType HashType = FileHashHelper.HashAlgorithmType.SHA256;

        private string dirStructureJson;

        /// <summary>
        /// 光盘文件包
        /// </summary>
        public WriteOncePackageCollection Packages { get; private set; }

        private IEnumerable<WriteOncePackage> ExecutingPackages =>
            Packages.Packages.Where(p => p.IsChecked && p.Index > 0);

        public static bool IsValidHashString(string hash, FileHashHelper.HashAlgorithmType type)
        {
            if (string.IsNullOrEmpty(hash))
                return false;

            int expectedLength = type switch
            {
                FileHashHelper.HashAlgorithmType.MD5 => 32,
                FileHashHelper.HashAlgorithmType.SHA1 => 40,
                FileHashHelper.HashAlgorithmType.SHA256 => 64,
                FileHashHelper.HashAlgorithmType.SHA384 => 96,
                FileHashHelper.HashAlgorithmType.SHA512 => 128,
                _ => 0
            };

            if (hash.Length != expectedLength)
                return false;

            foreach (char c in hash)
            {
                bool isHexDigit = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
                if (!isHexDigit)
                    return false;
            }

            return true;
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!Directory.Exists(Config.TargetDir))
            {
                Directory.CreateDirectory(Config.TargetDir);
            }

            await Task.Run(async () =>
            {
                NotifyMessage($"正在清理遗留目录");
                ClearTargetPackageDir();

                switch (Config.PackingType)
                {
                    case PackingType.Copy:
                        await ExecuteCopy(token);
                        break;
                    case PackingType.ISO:
                        await ExecuteISO(token);
                        break;
                    case PackingType.HardLink:
                        await ExecuteHardLink(token);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(Config.PackingType));
                }

                string dirStructureFilePath = Path.Combine(Config.TargetDir, DirStructureFileName);
                await File.WriteAllTextAsync(dirStructureFilePath, dirStructureJson, token);
                string packagedHashFilePath = Path.Combine(Config.TargetDir, HashListFileName);
                await File.WriteAllLinesAsync(packagedHashFilePath,
                    Packages.Packages.SelectMany(p => p.Files.Select(p => p.Hash)), token);
            }, token);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return null;
        }

        public override async Task InitializeAsync(CancellationToken token)
        {
            long maxSize = (long)(1024.0 * 1024 * 1024 * Config.PackageSizeGB);
            List<WriteOncePackage> packages = [];
            List<WriteOnceFile> outOfSizeFiles = new List<WriteOnceFile>();

            await Task.Run(async () =>
            {
                //第一步：枚举文件
                NotifyMessage("正在搜索文件");
                var files = new DirectoryInfo(Config.SourceDir)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .ApplyFilter(token, Config.Filter)
                    .OrderBy(p => p.LastWriteTime)
                    .Select(p => new WriteOnceFile(p, Config.SourceDir))
                    .ToList();

                List<WriteOnceFileModel> dirStructure = new List<WriteOnceFileModel>();

                //第二步：计算Hash
                List<WriteOnceFile> packageFiles = new List<WriteOnceFile>(files.Count);

                var hashCaches = GetFileHashCache();
                var packagedHashes = GetPackagedFileHashes();
                string hashCacheFilePath = Path.Combine(Config.TargetDir, HashCacheFileName);
                await using (var hashCacheFile = new StreamWriter(hashCacheFilePath))
                {
                    if (hashCaches.Count > 0)
                    {
                        foreach (var cache in hashCaches)
                        {
                            await hashCacheFile.WriteLineAsync($"{cache.Key}\t{cache.Value}");
                        }

                        await hashCacheFile.FlushAsync(token);
                    }

                    await TryForFilesAsync(files, async (file, s) =>
                    {
                        //从记忆中提取Hash
                        if (!hashCaches.TryGetValue(GetFileHashCode(file), out var hash))
                        {
                            hash = await FileHashHelper.ComputeHashAsync(file.Path, HashType, token);
                        }

                        //放入文件目录结构
                        dirStructure.Add(new WriteOnceFileModel(file.Path, hash, file.Length, file.Time));
                        //写入Hash缓存
                        await hashCacheFile.WriteLineAsync($"{GetFileHashCode(file)}\t{hash}");

                        //如果这个文件已经被打包过了，那么跳过
                        if (packagedHashes.Contains(hash))
                        {
                            return; //continue
                        }

                        NotifyMessage($"正在搜索文件{s.GetFileNumberMessage()}：{file.Path}");

                        //文件超过单盘大小
                        if (file.Length > maxSize)
                        {
                            outOfSizeFiles.Add(file);
                            return; //continue
                        }

                        file.Hash = hash;
                        packageFiles.Add(file);
                    }, token, FilesLoopOptions.Builder().AutoApplyFileLengthProgress().Build());
                }

                //写入文件目录结构
                string dirStructureFilePath = Path.Combine(Config.TargetDir, DirStructureFileName);
                dirStructureJson =
                    JsonSerializer.Serialize(dirStructure, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(dirStructureFilePath, dirStructureJson, token);


                //第三步：计算打包文件
                packageFiles = packageFiles.OrderByDescending(p => p.Length).ToList();
                foreach (var file in packageFiles)
                {
                    Debug.Assert(file.Length <= maxSize);
                    bool added = false;
                    for (int i = 0; i < packages.Count; i++)
                    {
                        var package = packages[i];
                        var freeSpace = maxSize - package.TotalLength;
                        if (freeSpace >= file.Length)
                        {
                            added = true;
                            package.Files.Add(file);
                            package.TotalLength += file.Length;
                            break;
                        }
                    }

                    //所有包的容量都不够大，新开一个
                    if (!added)
                    {
                        var package = new WriteOncePackage()
                        {
                            Index = packages.Count + 1,
                            IsChecked = true
                        };
                        package.TotalLength = file.Length;
                        package.Files.Add(file);
                        packages.Add(package);
                    }
                }
            }, token);

            Packages = new WriteOncePackageCollection()
            {
                Packages = packages,
                OutOfSizeFiles = outOfSizeFiles
            };
        }

        private void ClearTargetPackageDir()
        {
            foreach (var package in ExecutingPackages)
            {
                string packageDir = Path.Combine(Config.TargetDir, package.Index.ToString());
                if (Directory.Exists(packageDir))
                {
                    FileHelper.DeleteByConfig(packageDir);
                }

                var iso = Path.Combine(Config.TargetDir, package.Index + ".iso");
                if (File.Exists(iso))
                {
                    FileHelper.DeleteByConfig(iso);
                }

                if (Config.PackingType != PackingType.ISO)
                {
                    Directory.CreateDirectory(packageDir);
                }
            }
        }

        private async Task ExecuteCopy(CancellationToken token)
        {
            Debug.Assert(Config.PackingType == PackingType.Copy);

            long length = 0;
            int indexOfPackage = 0;
            int totalPackages = ExecutingPackages.Count();
            int indexOfFile = 0;
            int totalFiles = 0;
            long totalLength = ExecutingPackages.Sum(p => p.TotalLength);

            Progress<FileProcessProgress> progress = null;
            progress = new Progress<FileProcessProgress>(p =>
            {
                NotifyProgress(1.0 * (length + p.ProcessedBytes) / totalLength);
                NotifyMessage(
                    $"正在复制（{indexOfPackage}/{totalPackages}包，{indexOfFile}/{totalFiles}文件，本文件{1.0 * p.ProcessedBytes / 1024 / 1024:0}MB/{1.0 * p.TotalBytes / 1024 / 1024:0}MB）：{Path.GetFileName(p.SourceFilePath)}");
            });

            foreach (var package in ExecutingPackages)
            {
                token.ThrowIfCancellationRequested();

                indexOfPackage++;
                indexOfFile = 0;
                totalFiles = package.Files.Count;

                string packageDir = Path.Combine(Config.TargetDir, package.Index.ToString());

                foreach (var file in package.Files)
                {
                    indexOfFile++;
                    try
                    {
                        await FileCopyHelper.CopyFileAsync(file.Path,
                            Path.Combine(packageDir, file.Hash),
                            progress: progress, cancellationToken: token);
                        file.Complete();
                    }
                    catch (Exception ex)
                    {
                        file.Error(ex);
                    }
                    finally
                    {
                        length += file.Length;
                        NotifyProgress(1.0 * length / totalLength);
                    }
                }

                string dirStructureFilePath = Path.Combine(packageDir, DirStructureFileName);
                await File.WriteAllTextAsync(dirStructureFilePath, dirStructureJson, token);
                string packagedHashFilePath = Path.Combine(packageDir, HashListFileName);
                await File.WriteAllLinesAsync(packagedHashFilePath, package.Files.Select(p => p.Hash), token);
            }
        }

        private async Task ExecuteHardLink(CancellationToken token)
        {
            int indexOfPackage = 0;
            int totalPackages = ExecutingPackages.Count();

            foreach (var package in ExecutingPackages)
            {
                token.ThrowIfCancellationRequested();
                NotifyMessage($"正在创建第{package.Index}个文件包的硬链接");

                indexOfPackage++;

                string packageDir = Path.Combine(Config.TargetDir, package.Index.ToString());

                foreach (var file in package.Files)
                {
                    try
                    {
                        HardLinkCreator.CreateHardLink(Path.Combine(packageDir, file.Hash), file.Path);
                        file.Complete();
                    }
                    catch (Exception ex)
                    {
                        file.Error(ex);
                    }
                }

                string dirStructureFilePath = Path.Combine(packageDir, DirStructureFileName);
                await File.WriteAllTextAsync(dirStructureFilePath, dirStructureJson, token);
                string packagedHashFilePath = Path.Combine(packageDir, HashListFileName);
                await File.WriteAllLinesAsync(packagedHashFilePath, package.Files.Select(p => p.Hash), token);

                NotifyProgress(1.0 * indexOfPackage / totalPackages);
            }
        }

        private async Task ExecuteISO(CancellationToken token)
        {
            int indexOfPackage = 0;
            int totalPackages = ExecutingPackages.Count();

            foreach (var package in ExecutingPackages)
            {
                token.ThrowIfCancellationRequested();

                indexOfPackage++;

                CDBuilder builder = new CDBuilder
                {
                    UseJoliet = true
                };

                foreach (var file in package.Files)
                {
                    try
                    {
                        builder.AddFile(file.Hash, file.Path);

                        file.Complete();
                    }
                    catch (Exception ex)
                    {
                        file.Error(ex);
                    }
                }


                string dirStructureFilePath = Path.GetTempFileName();
                await File.WriteAllTextAsync(dirStructureFilePath, dirStructureJson, token);
                string packagedHashFilePath = Path.GetTempFileName();
                await File.WriteAllLinesAsync(packagedHashFilePath, package.Files.Select(p => p.Hash), token);

                NotifyMessage($"正在创建第{package.Index}个ISO");
                builder.AddFile(HashListFileName, packagedHashFilePath);
                builder.AddFile(DirStructureFileName, dirStructureFilePath);
                builder.Build(Path.Combine(Config.TargetDir, $"{package.Index}.iso"));
                NotifyProgress(1.0 * indexOfPackage / totalPackages);
            }
        }

        private IReadOnlyDictionary<long, string> GetFileHashCache()
        {
            if (string.IsNullOrEmpty(Config.HashCacheFile) || !File.Exists(Config.HashCacheFile))
            {
                return new Dictionary<long, string>();
            }

            var lines = File.ReadAllLines(Config.HashCacheFile);
            List<KeyValuePair<long, string>> dic = [];
            foreach (var line in lines.Select(p => p.Trim()))
            {
                var parts = line.Split('\t');
                if (parts.Length != 2)
                {
                    throw new FormatException($"文件{Config.HashCacheFile}中存在不正确的格式：{line}");
                }

                if (!IsValidHashString(parts[1], HashType))
                {
                    throw new Exception($"文件{Config.HashCacheFile}中存在无效的Hash值：{line}");
                }

                dic.Add(new KeyValuePair<long, string>(long.Parse(parts[0]), parts[1]));
            }

            return dic.ToFrozenDictionary();
        }

        private long GetFileHashCode(SimpleFileInfo file)
        {
            return file.GetHashCode();
        }

        private IReadOnlySet<string> GetPackagedFileHashes()
        {
            if (string.IsNullOrEmpty(Config.ArchivedFilesHashFile) || !File.Exists(Config.ArchivedFilesHashFile))
            {
                return new HashSet<string>();
            }

            var lines = File.ReadAllLines(Config.ArchivedFilesHashFile);
            foreach (var line in lines.Select(p => p.Trim()))
            {
                if (!IsValidHashString(line, HashType))
                {
                    throw new Exception($"文件{Config.ArchivedFilesHashFile}中存在无效的Hash值：{line}");
                }
            }

            return lines.ToFrozenSet();
        }
    }
}