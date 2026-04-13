using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class StringJoinConverter : IValueConverter
{
    public string Separator { get; set; } = "，";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null or UnsetValueType)
        {
            return string.Empty;
        }
        if (value is IEnumerable<string> es)
        {
            return string.Join(Separator, es);
        }

        throw new InvalidOperationException(
            $"StringJoinConverter只能转换IEnumerable<string>类型的值，当前值的类型为{value?.GetType()}");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new InvalidOperationException();
    }
}