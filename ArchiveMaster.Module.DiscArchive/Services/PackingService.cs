using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using DiscUtils.Iso9660;
using FzLib.IO;
using DiscFile = ArchiveMaster.ViewModels.FileSystem.DiscFile;
using DiscFilePackage = ArchiveMaster.ViewModels.FileSystem.DiscFilePackage;

namespace ArchiveMaster.Services
{
    public class PackingService(AppConfig appConfig) : DiscServiceBase<PackingConfig>(appConfig)
    {
        /// <summary>
        /// 光盘文件包
        /// </summary>
        public DiscFilePackageCollection Packages { get; private set; }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return null;
        }
        public override async Task InitializeAsync(CancellationToken token)
        {
            DiscFilePackageCollection packages = new DiscFilePackageCollection();
            NotifyMessage("正在搜索文件");

            await Task.Run(() =>
            {
                var filesOrderedByTime = new DirectoryInfo(Config.SourceDir)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .ApplyFilter(token, Config.Filter)
                    .Where(p => p.LastWriteTime > Config.EarliestTime)
                    .OrderBy(p => p.LastWriteTime)
                    .Select(p => new DiscFile(p, Config.SourceDir));

                packages.DiscFilePackages.Add(new DiscFilePackage());
                long maxSize = 1L * 1024 * 1024 * Config.DiscSizeMB;

                TryForFiles(filesOrderedByTime, (file, s) =>
                {
                    NotifyMessage($"正在搜索文件{s.GetFileNumberMessage()}：{file.Path}");

                    //文件超过单盘大小
                    if (file.Length > maxSize)
                    {
                        packages.SizeOutOfRangeFiles.Add(file);
                        return;
                    }

                    //文件超过剩余空间
                    var package = packages.DiscFilePackages[^1];
                    if (file.Length > maxSize - package.TotalLength)
                    {
                        package.EarliestTime = package.Files[0].Time;
                        package.LatestTime = package.Files[^1].Time;
                        package.Index = packages.DiscFilePackages.Count;
                        if (packages.DiscFilePackages.Count >= Config.MaxDiscCount)
                        {
                            Packages = packages;
                            s.Break();
                            return;
                        }

                        package = new DiscFilePackage();
                        packages.DiscFilePackages.Add(package);
                    }

                    //加入文件
                    package.Files.Add(file);
                    package.TotalLength += file.Length;
                }, token, FilesLoopOptions.DoNothing());


                //处理最后一个
                var lastPackage = packages.DiscFilePackages[^1];
                lastPackage.EarliestTime = lastPackage.Files[0].Time;
                lastPackage.LatestTime = lastPackage.Files[^1].Time;
                lastPackage.Index = packages.DiscFilePackages.Count;
            }, token);

            Packages = packages;
        }


        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (!Directory.Exists(Config.TargetDir))
            {
                Directory.CreateDirectory(Config.TargetDir);
            }

            long length = 0;
            await Task.Run(() =>
            {
                long totalLength = Packages.DiscFilePackages
                    .Where(p => p.IsChecked && p.Index > 0)
                    .Sum(p => p.Files.Sum(q => q.Length));
                foreach (var package in Packages.DiscFilePackages.Where(p => p.IsChecked && p.Index > 0))
                {
                    token.ThrowIfCancellationRequested();
                    string dir = Path.Combine(Config.TargetDir, package.Index.ToString());
                    Directory.CreateDirectory(dir);
                    string fileListName = $"filelist-{DateTime.Now:yyyyMMddHHmmss}.txt";
                    CDBuilder builder = null;
                    if (Config.PackingType == PackingType.ISO)
                    {
                        builder = new CDBuilder();
                        builder.UseJoliet = true;
                    }

                    using (var fileListStream = File.OpenWrite(Path.Combine(dir, fileListName)))
                    using (var writer = new StreamWriter(fileListStream))
                    {
                        writer.WriteLine(
                            $"{package.EarliestTime.ToString(DateTimeFormat)}\t{package.LatestTime.ToString(DateTimeFormat)}\t{package.TotalLength}");


                        foreach (var file in package.Files)
                        {
                            length += file.Length;

                            try
                            {
                                var relativePath = Path.GetRelativePath(Config.SourceDir, file.Path);
                                string newName = relativePath.Replace(":", "#c#").Replace("\\", "#s#");
                                string md5 = null;
                                NotifyMessage($"正在复制第{package.Index}个光盘文件包中的{relativePath}");

                                switch (Config.PackingType)
                                {
                                    case PackingType.Copy:
                                        md5 = CopyAndGetHash(file.Path, Path.Combine(dir, newName));
                                        break;
                                    case PackingType.ISO:
                                        builder!.AddFile(newName, file.Path);
                                        md5 = GetMD5(file.Path);
                                        break;
                                    case PackingType.HardLink:
                                        HardLinkCreator.CreateHardLink(Path.Combine(dir, newName), file.Path);
                                        md5 = GetMD5(file.Path);
                                        break;
                                }

                                writer.WriteLine(
                                    $"{newName}\t{relativePath}\t{file.Time.ToString(DateTimeFormat)}\t{file.Length}\t{md5}");
                                file.Success();
                            }
                            catch (Exception ex)
                            {
                                file.Error(ex);
                            }
                            finally
                            {
                                NotifyProgress(1.0 * length / totalLength);
                            }
                        }
                    }

                    NotifyProgressIndeterminate();
                    if (Config.PackingType == PackingType.ISO)
                    {
                        NotifyMessage($"正在创第 {package.Index} 个ISO");
                        builder.AddFile(fileListName, Path.Combine(dir, fileListName));
                        builder.Build(Path.Combine(Path.GetDirectoryName(dir), Path.GetFileName(dir) + ".iso"));
                    }
                }
            }, token);
        }
    }
}