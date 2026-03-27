using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class NumberToMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value != null)
        {
            return (double)value;
        }

        throw new ArgumentNullException(nameof(value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}