using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace ArchiveMaster.Converters;

public class IntToHorizontalAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int)
        {
            return (HorizontalAlignment)value;
        }

        throw new ArgumentOutOfRangeException();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is HorizontalAlignment)
        {
            return (int)value;
        }

        throw new ArgumentOutOfRangeException();
    }
}