using System.Globalization;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class VerifyErrorTagConverter : IValueConverter
{
    public static readonly VerifyErrorTagConverter Instance = new VerifyErrorTagConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        List<string> tags = new List<string>();
        if (value is not WriteOnceFile file)
        {
            return tags;
        }

        if (file.ErrorNoPhysicalFile)
        {
            tags.Add("物理文件不存在");
        }

        if (file.ErrorHashNotMatched)
        {
            tags.Add("文件Hash不匹配");
        }

        if (file.ErrorFileReadFailed)
        {
            tags.Add("文件读取失败");
        }

        if (file.ErrorNotInFileList)
        {
            tags.Add("文件不在包信息中");
        }

        return tags;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}