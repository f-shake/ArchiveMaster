using System.Globalization;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class ColorConverter : IValueConverter
{
    private (int a, int r, int g, int b) GetArgb(object value)
    {
        return value switch
        {
            System.Drawing.Color dc => (dc.A, dc.R, dc.G, dc.B),
            Avalonia.Media.Color ac => (ac.A, ac.R, ac.G, ac.B),
            Avalonia.Media.SolidColorBrush b => (b.Color.A, b.Color.R, b.Color.G, b.Color.B),
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    private object ToTargetType((int a, int r, int g, int b) argb, Type targetType)
    {
        if (targetType == typeof(System.Drawing.Color))
        {
            return System.Drawing.Color.FromArgb(argb.a, argb.r, argb.g, argb.b);
        }

        if (targetType == typeof(Avalonia.Media.Color))
        {
            return new Avalonia.Media.Color((byte)argb.a, (byte)argb.r, (byte)argb.g, (byte)argb.b);
        }

        if (targetType == typeof(Avalonia.Media.SolidColorBrush) || targetType == typeof(Avalonia.Media.IBrush))
        {
            return new Avalonia.Media.SolidColorBrush(new Avalonia.Media.Color((byte)argb.a, (byte)argb.r, (byte)argb.g,
                (byte)argb.b));
        }

        throw new ArgumentOutOfRangeException(nameof(targetType));
    }

    private object ConvertOrBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return null;
        }

        if (value.GetType() == targetType)
        {
            return value;
        }

        return ToTargetType(GetArgb(value), targetType);
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ConvertOrBack(value, targetType, parameter, culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ConvertOrBack(value, targetType, parameter, culture);
    }
}