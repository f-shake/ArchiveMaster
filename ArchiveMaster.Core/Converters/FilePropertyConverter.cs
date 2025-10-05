using System.Globalization;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Data.Converters;
using FzLib.Numeric;

namespace ArchiveMaster.Converters;

public class FilePropertyConverter : IValueConverter
{
    public enum FilePropertyType
    {
        Name,
        Extension,
        Length,
        LengthText,
        LastWriteTime,
    }

    public FilePropertyType PropertyType { get; set; } = FilePropertyType.Name;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return null;
        }

        string path = value switch
        {
            string s => s,
            FileInfo f => f.FullName,
            SimpleFileInfo f => f.Path,
            _ => throw new Exception("值必须为string、FileInfo或SimpleFileInfo类型")
        };

        return PropertyType switch
        {
            FilePropertyType.Name => Path.GetFileName(path),
            FilePropertyType.Extension => Path.GetExtension(path),
            FilePropertyType.Length => new FileInfo(path).Length,
            FilePropertyType.LengthText => NumberConverter.ByteToFitString(new FileInfo(path).Length),
            _ => throw new ArgumentOutOfRangeException(nameof(PropertyType)),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}