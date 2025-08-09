using System.Globalization;
using System.IO;
using System.Reflection.Metadata;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using WriteOnceFile = ArchiveMaster.ViewModels.FileSystem.WriteOnceFile;

namespace ArchiveMaster.Services
{  
    // /// <summary>
    // /// 解析filelist文件
    // /// </summary>
    // /// <param name="dirs"></param>
    // /// <returns></returns>
    // /// <exception cref="Exception"></exception>
    // /// <exception cref="FormatException"></exception>
    // protected Dictionary<string, List<WriteOnceFile>> ReadFileList(string dirs)
    // {
    //     Dictionary<string, List<WriteOnceFile>> files = new Dictionary<string, List<WriteOnceFile>>();
    //     foreach (var dir in FileNameHelper.GetFileNames(dirs))
    //     {
    //         string filelistName = Directory
    //             .EnumerateFiles(dir, "filelist-*.txt")
    //             .MaxBy(p => p);
    //         if (filelistName == null)
    //         {
    //             throw new Exception("不存在filelist，目录有误或文件缺失！");
    //         }
    //
    //         var lines = File.ReadAllLines(filelistName);
    //         var header = lines[0].Split('\t');
    //         files.Add(dir,
    //             lines.Skip(1).Select(p =>
    //             {
    //                 var parts = p.Split('\t');
    //                 if (parts.Length != 5)
    //                 {
    //                     throw new FormatException("filelist格式错误，无法解析");
    //                 }
    //
    //                 var file = new WriteOnceFile()
    //                 {
    //                     WriteOnceName = parts[0],
    //                     Path = parts[1],
    //                     Time = DateTime.Parse(parts[2]),
    //                     Length = long.Parse(parts[3]),
    //                     Md5 = parts[4],
    //                     Name = Path.GetFileName(parts[1]),
    //                 };
    //                 return file;
    //             }).ToList());
    //     }
    //
    //     return files;
    // }
    // public class RebuildService(AppConfig appConfig) : WriteOnceServiceBase<RebuildConfig>(appConfig)
    // {
    //     public List<RebuildError> rebuildErrors;
    //     private Dictionary<string, List<WriteOnceFile>> files;
    //
    //     public TreeFileDirInfo FileTree { get; private set; }
    //     public IReadOnlyList<RebuildError> RebuildErrors => rebuildErrors.AsReadOnly();
    //
    //     public override Task ExecuteAsync(CancellationToken token)
    //     {
    //         rebuildErrors = new List<RebuildError>();
    //         long length = 0;
    //         int count = 0;
    //         return Task.Run(() =>
    //         {
    //             int count = files.Sum(p => p.Value.Count);
    //             int index = 0;
    //             long currentLength = 0;
    //             long totalLength = files.Values.Sum(p => p.Sum(q => q.Length));
    //
    //             foreach (var dir in files.Keys)
    //             {
    //                 token.ThrowIfCancellationRequested();
    //                 FilesLoopOptions options = FilesLoopOptions.Builder()
    //                     .SetCount(index, count)
    //                     .SetLength(currentLength, totalLength)
    //                     .AutoApplyFileLengthProgress()
    //                     .AutoApplyStatus()
    //                     .Catch((file, ex) => { rebuildErrors.Add(new RebuildError(file as WriteOnceFile, ex.Message)); })
    //                     .Build();
    //
    //                 var states = TryForFiles(files[dir], (file, s) =>
    //                 {
    //                     length += file.Length;
    //                     var srcPath = Path.Combine(dir, file.WriteOnceName);
    //                     var distPath = Path.Combine(Config.TargetDir, file.Path);
    //                     var distFileDir = Path.GetDirectoryName(distPath);
    //                     NotifyMessage($"正在重建{s.GetFileNumberMessage()}：{file.Path}");
    //                     if (!Directory.Exists(distFileDir) && !Config.CheckOnly)
    //                     {
    //                         Directory.CreateDirectory(distFileDir);
    //                     }
    //
    //                     if (File.Exists(distPath) && Config.SkipIfExisted)
    //                     {
    //                         throw new Exception("文件已存在");
    //                     }
    //
    //                     string md5;
    //                     md5 = Config.CheckOnly ? GetMD5(srcPath) : CopyAndGetHash(srcPath, distPath);
    //
    //                     if (md5 != file.Md5)
    //                     {
    //                         throw new Exception("MD5验证失败");
    //                     }
    //
    //                     if ((File.GetLastWriteTime(srcPath) - file.Time).Duration().TotalSeconds >
    //                         Config.MaxTimeToleranceSecond)
    //                     {
    //                         throw new Exception("修改时间不一致");
    //                     }
    //                 }, token, options);
    //                 index = states.FileIndex;
    //                 currentLength = states.AccumulatedLength;
    //             }
    //         }, token);
    //     }
    //
    //     public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
    //     {
    //         return null;
    //     }
    //
    //     public override async Task InitializeAsync(CancellationToken token)
    //     {
    //         NotifyProgressIndeterminate();
    //         NotifyMessage("正在建立文件树");
    //         FileSystemTree tree = FileSystemTree.CreateRoot();
    //         await Task.Run(() =>
    //         {
    //             files = ReadFileList(Config.WriteOnceDirs);
    //             int count = files.Sum(p => p.Value.Count);
    //             int index = 0;
    //             foreach (var dir in files.Keys)
    //             {
    //                 FilesLoopOptions options = FilesLoopOptions.Builder()
    //                     .SetCount(index, count)
    //                     .AutoApplyFileNumberProgress()
    //                     .Build();
    //                 var states = TryForFiles(files[dir], (file, s) =>
    //                 {
    //                     NotifyMessage($"正在列举目录{dir}中的文件：{file.Name}");
    //                     string filePath = Path.Combine(dir, file.WriteOnceName);
    //                     if (!File.Exists(filePath))
    //                     {
    //                         throw new FileNotFoundException(filePath);
    //                     }
    //
    //                     var pathParts = file.Path.Split('\\', '/');
    //                     var current = tree;
    //                     for (int i = 0; i < pathParts.Length - 1; i++)
    //                     {
    //                         var part = pathParts[i];
    //                         if (current.Directories.Any(p => p.Name == part))
    //                         {
    //                             current = current.Directories.First(p => p.Name == part);
    //                         }
    //                         else
    //                         {
    //                             current = current.AddChild(part);
    //                         }
    //                     }
    //
    //                     var treeFile = current.AddFile(file.Name);
    //                     treeFile.File = file;
    //                 }, token, options);
    //                 index = states.FileIndex;
    //             }
    //         }, token);
    //         FileTree = tree;
    //     }
    // }
}