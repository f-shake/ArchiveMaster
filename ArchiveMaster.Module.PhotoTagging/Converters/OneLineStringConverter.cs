using System.Globalization;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class OneLineStringConverter : IValueConverter
{
    public static OneLineStringConverter Instance { get; } = new OneLineStringConverter();
    public string EmptyString { get; set; } = "（无）";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str.Replace('\n', ' ').Replace('\r', ' ');
        }

        return EmptyString;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new InvalidOperationException();
    }
}