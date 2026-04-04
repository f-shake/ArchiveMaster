using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ArchiveMaster.Converters;

public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is System.Drawing.Color dc)
        {
            return new SolidColorBrush(new Color(dc.A, dc.R, dc.G, dc.B));
        }
        else if (value is Color c)
        {
            return new SolidColorBrush(c);
        }
        throw new ArgumentOutOfRangeException(nameof(value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}