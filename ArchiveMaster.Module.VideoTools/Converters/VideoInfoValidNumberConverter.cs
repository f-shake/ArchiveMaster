using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class VideoInfoValidNumberConverter : IValueConverter
{
    public static VideoInfoValidNumberConverter Instance { get; } = new();
    public string DoubleFormat { get; set; } = "0.##";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return "";
        }

        if (value is double d)
        {
            if (double.IsNaN(d))
            {
                return "";
            }

            return d.ToString(DoubleFormat);
        }

        if (value is int i)
        {
            if (i < 0)
            {
                return "";
            }

            return i.ToString();
        }

        if (value is long l)
        {
            if (l < 0)
            {
                return "";
            }

            return l.ToString();
        }

        throw new ArgumentException($"无法将{value.GetType().Name}类型转为{targetType.Name}");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new InvalidOperationException();
    }
}