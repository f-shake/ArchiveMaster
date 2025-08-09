using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using DiscUtils.Udf;
using FzLib.IO;
using WriteOnceFile = ArchiveMaster.ViewModels.FileSystem.WriteOnceFile;

namespace ArchiveMaster.Services
{
    public class PackingService(AppConfig appConfig) : WriteOnceServiceBase<PackingConfig>(appConfig)
    {
        private const FileHashHelper.HashAlgorithmType HashType = FileHashHelper.HashAlgorithmType.SHA256;

        /// <summary>
        /// 光盘文件包
        /// </summary>
        public WriteOncePackageCollection Packages { get; private set; }

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
                bool isHexDigit = (c >= '0' && c <= '9') ||
                                  (c >= 'a' && c <= 'f') ||
                                  (c >= 'A' && c <= 'F');
                if (!isHexDigit)
                    return false;
            }

            return true;
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return null;
        }

        private IReadOnlySet<string> GetPackagedFileHashes()
        {
            //TODO
            return new HashSet<string>();
        }

        private IReadOnlyDictionary<long, string> GetFileHashCache()
        {
            //TODO
            return new Dictionary<long, string>();
        }

        private long GetFileHashCode(SimpleFileInfo file)
        {
            return file.GetHashCode();
        }

        public override async Task InitializeAsync(CancellationToken token)
        {
            long maxSize = 1L * 1024 * 1024 * Config.PackageSizeMB;
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

                //第二步：计算Hash
                List<WriteOnceFile> packageFiles = new List<WriteOnceFile>(files.Count);

                var hashCaches = GetFileHashCache();
                var packagedHashes = GetPackagedFileHashes();
                string hashCacheFilePath = Path.Combine(Config.TargetDir, "caches.woahc");
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


                        await hashCacheFile.WriteLineAsync($"{GetFileHashCode(file)}\t{hash}");
                    }, token, FilesLoopOptions.Builder().AutoApplyFileLengthProgress().Build());
                }

                //第三步：打包文件

                // string packagedHashFilePath = Path.Combine(Config.TargetDir, "hashes.woaph");
                // await using var packagedHashFile = new StreamWriter(packagedHashFilePath);
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
                        var package = new WriteOncePackage() { Index = packages.Count + 1 };
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


        public override async Task ExecuteAsync(CancellationToken token)
        {
            // if (!Directory.Exists(Config.TargetDir))
            // {
            //     Directory.CreateDirectory(Config.TargetDir);
            // }
            //
            // long length = 0;
            // await Task.Run(() =>
            // {
            //     long totalLength = Packages.WriteOnceFilePackages
            //         .Where(p => p.IsChecked && p.Index > 0)
            //         .Sum(p => p.Files.Sum(q => q.Length));
            //     foreach (var package in Packages.WriteOnceFilePackages.Where(p => p.IsChecked && p.Index > 0))
            //     {
            //         token.ThrowIfCancellationRequested();
            //         string dir = Path.Combine(Config.TargetDir, package.Index.ToString());
            //         Directory.CreateDirectory(dir);
            //         string fileListName = $"filelist-{DateTime.Now:yyyyMMddHHmmss}.txt";
            //         CDBuilder builder = null;
            //         if (Config.PackingType == PackingType.ISO)
            //         {
            //             builder = new CDBuilder();
            //             builder.UseJoliet = true;
            //         }
            //
            //         using (var fileListStream = File.OpenWrite(Path.Combine(dir, fileListName)))
            //         using (var writer = new StreamWriter(fileListStream))
            //         {
            //             writer.WriteLine(
            //                 $"{package.EarliestTime.ToString(DateTimeFormat)}\t{package.LatestTime.ToString(DateTimeFormat)}\t{package.TotalLength}");
            //
            //
            //             foreach (var file in package.Files)
            //             {
            //                 length += file.Length;
            //
            //                 try
            //                 {
            //                     var relativePath = Path.GetRelativePath(Config.SourceDir, file.Path);
            //                     string newName = relativePath.Replace(":", "#c#").Replace("\\", "#s#");
            //                     string md5 = null;
            //                     NotifyMessage($"正在复制第{package.Index}个光盘文件包中的{relativePath}");
            //
            //                     switch (Config.PackingType)
            //                     {
            //                         case PackingType.Copy:
            //                             md5 = CopyAndGetHash(file.Path, Path.Combine(dir, newName));
            //                             break;
            //                         case PackingType.ISO:
            //                             builder!.AddFile(newName, file.Path);
            //                             md5 = GetMD5(file.Path);
            //                             break;
            //                         case PackingType.HardLink:
            //                             HardLinkCreator.CreateHardLink(Path.Combine(dir, newName), file.Path);
            //                             md5 = GetMD5(file.Path);
            //                             break;
            //                     }
            //
            //                     writer.WriteLine(
            //                         $"{newName}\t{relativePath}\t{file.Time.ToString(DateTimeFormat)}\t{file.Length}\t{md5}");
            //                     file.Complete();
            //                 }
            //                 catch (Exception ex)
            //                 {
            //                     file.Error(ex);
            //                 }
            //                 finally
            //                 {
            //                     NotifyProgress(1.0 * length / totalLength);
            //                 }
            //             }
            //         }
            //
            //         NotifyProgressIndeterminate();
            //         if (Config.PackingType == PackingType.ISO)
            //         {
            //             NotifyMessage($"正在创第 {package.Index} 个ISO");
            //             builder.AddFile(fileListName, Path.Combine(dir, fileListName));
            //             builder.Build(Path.Combine(Path.GetDirectoryName(dir), Path.GetFileName(dir) + ".iso"));
            //         }
            //     }
            // }, token);
        }
    }
}