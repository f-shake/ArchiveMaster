using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class IntToMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value != null)
        {
            return (int)value;
        }

        throw new ArgumentNullException(nameof(value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}