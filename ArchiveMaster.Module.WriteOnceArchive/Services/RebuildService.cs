using System.Globalization;
using System.IO;
using System.Reflection.Metadata;
using System.Text.Json;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.IO;
using WriteOnceFile = ArchiveMaster.ViewModels.FileSystem.WriteOnceFile;

namespace ArchiveMaster.Services
{
    public class RebuildService(AppConfig appConfig) : TwoStepServiceBase<RebuildConfig>(appConfig)
    {
        public TreeDirInfo FileTree { get; private set; }
        public List<WriteOnceFile> MatchedFiles { get; private set; }

        public override Task ExecuteAsync(CancellationToken token)
        {
            // rebuildErrors = new List<RebuildError>();
            // long length = 0;
            // int count = 0;
            // return Task.Run(() =>
            // {
            //     int count = files.Sum(p => p.Value.Count);
            //     int index = 0;
            //     long currentLength = 0;
            //     long totalLength = files.Values.Sum(p => p.Sum(q => q.Length));
            //
            //     foreach (var dir in files.Keys)
            //     {
            //         token.ThrowIfCancellationRequested();
            //         FilesLoopOptions options = FilesLoopOptions.Builder()
            //             .SetCount(index, count)
            //             .SetLength(currentLength, totalLength)
            //             .AutoApplyFileLengthProgress()
            //             .AutoApplyStatus()
            //             .Catch((file, ex) =>
            //             {
            //                 rebuildErrors.Add(new RebuildError(file as WriteOnceFile, ex.Message));
            //             })
            //             .Build();
            //
            //         var states = TryForFiles(files[dir], (file, s) =>
            //         {
            //             length += file.Length;
            //             var srcPath = Path.Combine(dir, file.WriteOnceName);
            //             var distPath = Path.Combine(Config.TargetDir, file.Path);
            //             var distFileDir = Path.GetDirectoryName(distPath);
            //             NotifyMessage($"正在重建{s.GetFileNumberMessage()}：{file.Path}");
            //             if (!Directory.Exists(distFileDir) && !Config.CheckOnly)
            //             {
            //                 Directory.CreateDirectory(distFileDir);
            //             }
            //
            //             if (File.Exists(distPath) && Config.SkipIfExisted)
            //             {
            //                 throw new Exception("文件已存在");
            //             }
            //
            //             string md5;
            //             md5 = Config.CheckOnly ? GetMD5(srcPath) : CopyAndGetHash(srcPath, distPath);
            //
            //             if (md5 != file.Md5)
            //             {
            //                 throw new Exception("MD5验证失败");
            //             }
            //
            //             if ((File.GetLastWriteTime(srcPath) - file.Time).Duration().TotalSeconds >
            //                 Config.MaxTimeToleranceSecond)
            //             {
            //                 throw new Exception("修改时间不一致");
            //             }
            //         }, token, options);
            //         index = states.FileIndex;
            //         currentLength = states.AccumulatedLength;
            //     }
            // }, token);
            return Task.CompletedTask;
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
                    var phyFiles = Directory.EnumerateFiles(dir)
                        .Where(p => FileHelper.IsValidHashString(Path.GetFileName(p),
                            WriteOnceArchiveParameters.HashType))
                        .ToList();
                    foreach (var phyFile in phyFiles)
                    {
                        if (hash2Files.ContainsKey(Path.GetFileName(phyFile)))
                        {
                            if (hash2Files[Path.GetFileName(phyFile)] is WriteOnceFile file)
                            {
                                file.HasPhysicalFile = true;
                                matchFiles.Add(file);
                            }
                            else
                            {
                                foreach (var p in ((List<WriteOnceFile>)hash2Files[Path.GetFileName(phyFile)]))
                                {
                                    p.HasPhysicalFile = true;
                                    matchFiles.Add(p);
                                }
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