using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels.FileSystem;
using DiscUtils.Iso9660;
using FzLib.Cryptography;
using FzLib.IO;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using WriteOnceFile = ArchiveMaster.ViewModels.FileSystem.WriteOnceFile;

namespace ArchiveMaster.Services
{
    public class PackingService(AppConfig appConfig) : TwoStepServiceBase<PackingConfig>(appConfig)
    {
        private List<WriteOnceFileInfo> allFiles;

        /// <summary>
        /// 光盘文件包
        /// </summary>
        public WriteOncePackageCollection Packages { get; private set; }

        private IEnumerable<WriteOncePackage> ExecutingPackages =>
            Packages.Packages.Where(p => p.IsChecked && p.Index > 0);

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

                await WritePackageInfoFileAsync(
                    Path.Combine(Config.TargetDir, WriteOnceArchiveParameters.PackageInfoFileName),
                    ExecutingPackages.Select(p => p.TotalLength).Sum(),
                    ExecutingPackages.SelectMany(p => p.Files).Select(p => p.Hash));
            }, token);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return null;
        }

        public override async Task InitializeAsync(CancellationToken token)
        {
            long maxSize = 1024L * 1024 * Config.PackageSizeMB;
            List<WriteOnceFile> outOfSizeFiles = new List<WriteOnceFile>();
            List<WriteOncePackage> packages = null;

            await Task.Run(async () =>
            {
                Directory.CreateDirectory(Config.TargetDir);
                //第一步：枚举文件
                NotifyMessage("正在搜索文件");
                var files = new DirectoryInfo(Config.SourceDir)
                    .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                    .ApplyFilter(token, Config.Filter)
                    .OrderBy(p => p.LastWriteTime)
                    .Select(p => new WriteOnceFile(p, Config.SourceDir))
                    .ToList();

                allFiles = new List<WriteOnceFileInfo>();

                //第二步：计算Hash
                List<WriteOnceFile> packageFiles = new List<WriteOnceFile>(files.Count);
                var hashCaches = await GetFileHashCacheAsync();
                var packagedHashes = await GetPackagedFileHashesAsync();
                string hashCacheFilePath = Path.Combine(Config.TargetDir, WriteOnceArchiveParameters.HashCacheFileName);
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
                        string numMsg = s.GetFileNumberMessage("{0}/{1}");
                        Progress<FileProcessProgress> progress = new Progress<FileProcessProgress>(p =>
                        {
                            NotifyProgress(1.0 * (s.AccumulatedLength + p.ProcessedBytes) / s.TotalLength);
                            NotifyMessage(
                                $"正在计算文件Hash（{numMsg}，本文件{1.0 * p.ProcessedBytes / 1024 / 1024:0}MB/{1.0 * p.TotalBytes / 1024 / 1024:0}MB）：{file.RelativePath}");
                        });

                        //从记忆中提取Hash
                        if (!hashCaches.TryGetValue(GetFileHashCode(file), out var hash))
                        {
                            hash = await FileHashHelper.ComputeHashStringAsync(file.Path,
                                WriteOnceArchiveParameters.HashType, cancellationToken: token,
                                progress: s.CreateFileProgressReporter("正在计算文件Hash"));
                        }

                        //放入文件目录结构
                        allFiles.Add(new WriteOnceFileInfo(file.RelativePath, hash, file.Length, file.Time));
                        //写入Hash缓存
                        await hashCacheFile.WriteLineAsync($"{GetFileHashCode(file)}\t{hash}");

                        //如果这个文件已经被打包过了，那么跳过
                        if (!packagedHashes.Add(hash))
                        {
                            return; //continue
                        }
                        //如果存在多个文件，拥有相同的Hash，他们仍然都会被放到AllFiles中，作为目录结构，
                        //但是只有第一个文件会被打包为物理文件，其他文件会被跳过

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

                //第三步：计算打包文件
                packages = Pack(packageFiles, maxSize);
            }, token);

            Packages = new WriteOncePackageCollection()
            {
                Packages = packages,
                OutOfSizeFiles = outOfSizeFiles
            };
        }

        private static void Shuffle<T>(IList<T> list)
        {
            Random rng = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
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

            string baseMessage = string.IsNullOrWhiteSpace(Config.Password) ? "正在复制" : "正在加密";
            Progress<FileProcessProgress> progress = new Progress<FileProcessProgress>(p =>
            {
                NotifyProgress(1.0 * (length + p.ProcessedBytes) / totalLength);
                NotifyMessage(
                    $"{baseMessage}（{indexOfPackage}/{totalPackages}包，{indexOfFile}/{totalFiles}文件，本文件{1.0 * p.ProcessedBytes / 1024 / 1024:0}MB/{1.0 * p.TotalBytes / 1024 / 1024:0}MB）：{Path.GetFileName(p.SourceFilePath)}");
            });
            Aes aes = null;
            if (!string.IsNullOrWhiteSpace(Config.Password))
            {
                aes = AesHelper.GetDefaultAes(Config.Password);
            }

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
                        var targetFile = Path.Combine(packageDir, file.Hash);
                        if (aes == null) //复制
                        {
                            await FileCopyHelper.CopyFileAsync(file.Path, targetFile,
                                progress: progress, cancellationToken: token);
                        }
                        else //加密
                        {
                            await aes.EncryptFileAsync(file.Path,
                                targetFile + WriteOnceArchiveParameters.EncryptedFileSuffix,
                                progress: progress, cancellationToken: token);
                        }

                        file.Success();
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

                await WritePackageInfoFileAsync(
                    Path.Combine(packageDir, WriteOnceArchiveParameters.PackageInfoFileName), package.TotalLength,
                    package.Files.Select(p => p.Hash));
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
                        file.Success();
                    }
                    catch (Exception ex)
                    {
                        file.Error(ex);
                    }
                }

                await WritePackageInfoFileAsync(
                    Path.Combine(packageDir, WriteOnceArchiveParameters.PackageInfoFileName), package.TotalLength,
                    package.Files.Select(p => p.Hash));

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

                        file.Success();
                    }
                    catch (Exception ex)
                    {
                        file.Error(ex);
                    }
                }

                string tempFile = Path.GetTempFileName();
                await WritePackageInfoFileAsync(tempFile, package.TotalLength, package.Files.Select(p => p.Hash));

                NotifyMessage($"正在创建第{package.Index}个ISO");
                builder.AddFile(WriteOnceArchiveParameters.PackageInfoFileName, tempFile);
                builder.Build(Path.Combine(Config.TargetDir, $"{package.Index}.iso"));
                NotifyProgress(1.0 * indexOfPackage / totalPackages);
            }
        }

        private async Task<IReadOnlyDictionary<long, string>> GetFileHashCacheAsync()
        {
            if (string.IsNullOrEmpty(Config.HashCacheFile) || !File.Exists(Config.HashCacheFile))
            {
                return new Dictionary<long, string>();
            }

            var lines = await File.ReadAllLinesAsync(Config.HashCacheFile);
            List<KeyValuePair<long, string>> dic = [];
            foreach (var line in lines.Select(p => p.Trim()))
            {
                var parts = line.Split('\t');
                if (parts.Length != 2)
                {
                    throw new FormatException($"文件{Config.HashCacheFile}中存在不正确的格式：{line}");
                }

                if (!FileHashHelper.IsValidHashString(parts[1], WriteOnceArchiveParameters.HashType))
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

        private async Task<HashSet<string>> GetPackagedFileHashesAsync()
        {
            HashSet<string> hashes = new HashSet<string>();
            if (string.IsNullOrEmpty(Config.PreviousPackageInfoFiles))
            {
                return hashes;
            }

            var files = FileNameHelper.GetFileNames(Config.PreviousPackageInfoFiles);
            if (files.Length == 0)
            {
                return hashes;
            }

            foreach (var file in files)
            {
                var packageInfo = JsonSerializer.Deserialize<WriteOncePackageInfo>(await File.ReadAllTextAsync(file));
                hashes.UnionWith(packageInfo.Hashes);
            }

            return hashes;
        }

        private List<WriteOncePackage> Pack(IEnumerable<WriteOnceFile> packageFiles, long maxSize)
        {
            //BFD，但是不进行排序，打乱顺序，避免前面的箱子文件少、后面的箱子文件多

            // 先按文件长度降序排序
            // var filesSorted = packageFiles.OrderByDescending(f => f.Length).ToList();
            var filesSorted = packageFiles.ToList();
            Shuffle(filesSorted);

            List<WriteOncePackage> packages = new List<WriteOncePackage>();

            foreach (var file in filesSorted)
            {
                Debug.Assert(file.Length <= maxSize, $"文件长度 {file.Length} 超过包最大容量 {maxSize}");

                int bestIndex = -1;
                long minFreeSpace = long.MaxValue;

                // 找到剩余容量 >= 文件长度且剩余容量最小的包
                for (int i = 0; i < packages.Count; i++)
                {
                    long freeSpace = maxSize - packages[i].TotalLength;
                    if (freeSpace >= file.Length && freeSpace < minFreeSpace)
                    {
                        minFreeSpace = freeSpace;
                        bestIndex = i;
                    }
                }

                if (bestIndex >= 0)
                {
                    // 放入找到的最合适包中
                    var package = packages[bestIndex];
                    package.Files.Add(file);
                    package.TotalLength += file.Length;
                }
                else
                {
                    // 新开一个包
                    var newPackage = new WriteOncePackage()
                    {
                        Index = packages.Count + 1,
                        IsChecked = true
                    };
                    newPackage.Files.Add(file);
                    newPackage.TotalLength = file.Length;
                    packages.Add(newPackage);
                }
            }

            return packages;
        }
        private async Task WritePackageInfoFileAsync(string path, long totalLength, IEnumerable<string> hashes)
        {
            var packageInfo = new WriteOncePackageInfo(allFiles, totalLength, DateTime.Now, hashes.ToList());
            var json = JsonSerializer.Serialize(packageInfo, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            });
            await File.WriteAllTextAsync(path, json);
        }
    }
}