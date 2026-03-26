using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace ArchiveMaster.Converters;

public class IntToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i)
        {
            if (targetType == typeof(HorizontalAlignment))
            {
                return (HorizontalAlignment)(i + 1);
            }

            if (targetType == typeof(VerticalAlignment))
            {
                return (VerticalAlignment)(3-i);
            }
        }

        throw new ArgumentOutOfRangeException();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is HorizontalAlignment ha)
        {
            return ha - 1;
        }

        if (value is VerticalAlignment va)
        {
            return 3 - va;
        }
        
        throw new ArgumentOutOfRangeException();
    }
}