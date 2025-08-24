using System.Collections.Frozen;
using System.Text.Json;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Services;

public static class WriteOnceArchiveHelper
{
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