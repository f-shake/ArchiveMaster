using System.Collections;
using System.Globalization;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class TreeFileDataGridStatisticsConverter : IValueConverter
{
    public static readonly TreeFileDataGridStatisticsConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return null;
        }

        if (value is not IEnumerable e)
        {
            throw new Exception("数据类型必须为IEnumerable<TreeDirInfo>");
        }

        var dirs = e.OfType<TreeDirInfo>();

        int folderCount = 0;
        int fileCount = 0;
        foreach (var dir in dirs)
        {
            folderCount += dir.SubFolderCount + 1;
            fileCount += dir.SubFileCount;
        }

        fileCount += e.OfType<SimpleFileInfo>().Count();

        return $"共{folderCount}个文件夹，{fileCount}个文件";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}