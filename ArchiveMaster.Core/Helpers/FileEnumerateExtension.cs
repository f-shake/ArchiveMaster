using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.IO;

namespace ArchiveMaster.Helpers;

public static class FileEnumerateExtension
{
    public static EnumerationOptions GetEnumerationOptions(bool includingSubDirs = true,
        MatchCasing matchCasing = MatchCasing.PlatformDefault)
    {
        return new EnumerationOptions()
        {
            IgnoreInaccessible = true,
            AttributesToSkip = 0,
            RecurseSubdirectories = includingSubDirs,
            MatchCasing = matchCasing
        };
    }

    public static IEnumerable<T> CheckedOnly<T>(this IEnumerable<T> files) where T : SimpleFileInfo
    {
        foreach (var file in files)
        {
            if (file.IsChecked)
            {
                yield return file;
            }
        }
    }

    public static IEnumerable<T> ApplyFilter<T>(this IEnumerable<T> source,
        CancellationToken cancellationToken, FileFilterRule filter = null, bool skipRecycleBin = true)
    {
        var filterHelper = (filter == null || filter.IsEnabled == false) ? null : new FileFilterHelper(filter);
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool canYieldReturn = true;
            if (skipRecycleBin)
            {
                string path = item switch
                {
                    string str => str,
                    FileSystemInfo fi => fi.FullName,
                    SimpleFileInfo sf => sf.Path,
                    _ => throw new NotSupportedException($"不支持ApplyFilter的类型：{typeof(T).Name}")
                };

                if (path != null && path.Contains("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase))
                {
                    canYieldReturn = false;
                }
            }

            if (filter != null && canYieldReturn)
            {
                canYieldReturn = item switch
                {
                    string str => filterHelper.IsMatched(str),
                    FileSystemInfo fi => filterHelper.IsMatched(fi),
                    SimpleFileInfo sf => filterHelper.IsMatched(sf),
                    _ => throw new NotSupportedException($"不支持ApplyFilter的类型：{typeof(T).Name}")
                };
            }

            if (canYieldReturn)
            {
                yield return item;
            }
        }
    }

    public static List<SimpleFileInfo> GetSimpleFileInfos(this DirectoryInfo directoryInfo,
        CancellationToken cancellationToken = default)
    {
        return directoryInfo.EnumerateSimpleFileInfos(cancellationToken).ToList();
    }

    public static IEnumerable<SimpleFileInfo> EnumerateSimpleFileInfos(this DirectoryInfo directoryInfo,
        CancellationToken cancellationToken = default)
    {
        return directoryInfo.EnumerateFiles("*", GetEnumerationOptions())
            .ApplyFilter(cancellationToken)
            .Select(p => new SimpleFileInfo(p, directoryInfo.FullName));
    }
}