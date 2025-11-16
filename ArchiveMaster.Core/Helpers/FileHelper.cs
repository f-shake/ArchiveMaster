using System.Runtime.CompilerServices;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Platform.Storage;
using FzLib.Cryptography;
using FzLib.IO;
using Serilog;

namespace ArchiveMaster.Helpers;

public static class FileHelper
{
    public static string AndroidExternalFilesDir { get; set; }

    public static FileFilterRule ImageFileFilterRule => new FileFilterRule()
    {
        IncludeFiles = """
                       *.heic
                       *.heif
                       *.jpg
                       *.jpeg
                       *.dng
                       *.arw
                       """
    };

    public static FileFilterRule NoRawImageFileFilterRule => new FileFilterRule()
    {
        IncludeFiles = """
                       *.heic
                       *.heif
                       *.jpg
                       *.jpeg
                       """
    };

    private static void TryDirectlyDelete(string path)
    {
        try
        {
            FileDeleteHelper.DirectlyDelete(path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除文件{Path}时出错", path);
            throw new IOException($"删除文件{path}时出错：{ex.Message}", ex);
        }
    }

    private static void TryDeleteToRecycleBin(string path)
    {
        try
        {
            FileDeleteHelper.DeleteToRecycleBin(path);
        }
        catch (Exception ex1)
        {
            Log.Error(ex1, "删除文件{Path}到回收站时出错", path);
            try
            {
                FileDeleteHelper.DirectlyDelete(path);
            }
            catch (Exception ex2)
            {
                Log.Error(ex2, "删除文件{Path}时出错", path);
                throw new IOException($"无法删除到回收站，也无法直接删除文件{path}：{ex2.Message}", ex2);
            }
        }
    }

    private static void TryDeleteToSpecialFolder(string path, string toolName, string specialDeletedFileRelativePath)
    {
        try
        {
            string rootDir = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(GlobalConfigs.Instance.SpecialDeleteFolderName))
            {
                throw new InvalidOperationException("未指定删除文件夹");
            }

            if (rootDir == null)
            {
                throw new InvalidOperationException($"无法找到文件{path}的根目录");
            }

            string target = Path.Combine(Path.GetPathRoot(path),
                GlobalConfigs.Instance.SpecialDeleteFolderName,
                toolName);

            if (!string.IsNullOrWhiteSpace(specialDeletedFileRelativePath))
            {
                target = Path.Combine(target, specialDeletedFileRelativePath);
            }
            else
            {
                var relativePath = Path.GetRelativePath(rootDir, path);
                target = Path.Combine(target, relativePath);
            }

            string targetDir = Path.GetDirectoryName(target);
            Directory.CreateDirectory(targetDir);

            if (IsDirectory(path))
            {
                Directory.Move(path, GetNoDuplicateDirectory(target));
            }
            else
            {
                File.Move(path, GetNoDuplicateFile(target));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "移动文件{Path}到指定删除文件夹时出错", path);
            throw new IOException($"移动文件{path}到指定删除文件夹时出错：{ex.Message}", ex);
        }
    }

    public static void DeleteByConfig(string path,
        string toolName,
        string specialDeletedFileRelativePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        switch (GlobalConfigs.Instance.DeleteMode)
        {
            case DeleteMode.RecycleBinPrefer:
                TryDeleteToRecycleBin(path);

                break;
            case DeleteMode.DeleteDirectly:
                TryDirectlyDelete(path);
                break;
            case DeleteMode.MoveToSpecialFolder:
                TryDeleteToSpecialFolder(path, toolName, specialDeletedFileRelativePath);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(GlobalConfigs.Instance.DeleteMode),
                    GlobalConfigs.Instance.DeleteMode, null);
        }
    }

    public static string GetNoDuplicateDirectory(string path, string suffixFormat = " ({i})")
    {
        if (!Directory.Exists(path))
        {
            return path;
        }

        if (!suffixFormat.Contains("{i}"))
        {
            throw new ArgumentException("后缀应包含“{i}”以表示索引");
        }

        int num = 2;
        string directoryName = Path.GetDirectoryName(path);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        string text;
        while (true)
        {
            text = Path.Combine(directoryName,
                fileNameWithoutExtension + suffixFormat.Replace("{i}", num.ToString()) + extension);
            if (!Directory.Exists(text))
            {
                break;
            }

            num++;
        }

        return text;
    }

    public static string GetNoDuplicateFile(string path, string suffixFormat = " ({i})")
    {
        if (!File.Exists(path))
        {
            return path;
        }

        if (!suffixFormat.Contains("{i}"))
        {
            throw new ArgumentException("后缀应包含“{i}”以表示索引");
        }

        int num = 2;
        string directoryName = Path.GetDirectoryName(path);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        string text;
        while (true)
        {
            text = Path.Combine(directoryName,
                fileNameWithoutExtension + suffixFormat.Replace("{i}", num.ToString()) + extension);
            if (!File.Exists(text))
            {
                break;
            }

            num++;
        }

        return text;
    }

    public static bool IsDirectory(string path)
    {
        FileAttributes attr = File.GetAttributes(path);
        return attr.HasFlag(FileAttributes.Directory);
    }


    public static bool IsMatched(this FileFilterHelper fileFilterHelper, SimpleFileInfo file)
    {
        return fileFilterHelper.IsMatched(file.Path);
    }


    public static StringComparer GetStringComparer()
    {
        switch (GlobalConfigs.Instance.FileNameCase)
        {
            case FilenameCasePolicy.Auto:
                if (OperatingSystem.IsWindows())
                {
                    return StringComparer.OrdinalIgnoreCase;
                }
                else if (OperatingSystem.IsLinux())
                {
                    return StringComparer.Ordinal;
                }

                return StringComparer.OrdinalIgnoreCase;
            case FilenameCasePolicy.Ignore:
                return StringComparer.OrdinalIgnoreCase;
            case FilenameCasePolicy.Sensitive:
                return StringComparer.Ordinal;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static int GetOptimalBufferSize(long fileLength)
    {
        return fileLength switch
        {
            < 1 * 1024 * 1024 => 16 * 1024, // 小文件（<1MB）：16KB
            < 32 * 1024 * 1024 => 1 * 1024 * 1024, // 中等文件（1MB~32MB）：1MB
            _ => 4 * 1024 * 1024 // 大文件（>32MB）：4MB
        };
    }

    public static string GetPath(this IStorageItem file)
    {
        if (OperatingSystem.IsAndroid())
        {
            if (AndroidExternalFilesDir == null)
            {
                throw new ArgumentException(
                    "在Android中使用时，应当设置AndroidExternalFilesDir。" +
                    "值可以从Android项目中使用GetExternalFilesDir(string.Empty)" +
                    ".AbsolutePath.Split([\"Android\"], StringSplitOptions.None)[0]赋值");
            }

            var path = file.Path.LocalPath;
            return Path.Combine(AndroidExternalFilesDir, path.Split(':')[^1]);
        }

        return file.TryGetLocalPath();
    }
}