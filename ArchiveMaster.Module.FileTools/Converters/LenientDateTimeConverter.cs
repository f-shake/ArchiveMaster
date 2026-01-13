using System.Globalization;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class LenientDateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return "（无限制）";
        }

        if (value is DateTime dateTime)
        {
            // 如果参数指定了显示格式，使用指定格式
            if (parameter is string format && !string.IsNullOrWhiteSpace(format))
            {
                return dateTime.ToString(format, culture);
            }

            // 默认显示格式
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss", culture);
        }

        return "（无效日期）";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string input || string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        input = input.Trim();

        if (input is "∞" or "（无限制）" or "" or ".")
        {
            return null;
        }

        try
        {
            if (DateTime.TryParse(input, culture, DateTimeStyles.AllowWhiteSpaces, out DateTime dateTime))
            {
                return dateTime;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}