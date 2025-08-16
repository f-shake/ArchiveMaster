using System.Globalization;
using ArchiveMaster.Enums;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ArchiveMaster.Converters;

public class ProcessStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        Color color = (ProcessStatus)value switch
        {
            ProcessStatus.Ready => Colors.Transparent,
            ProcessStatus.Warn => Colors.Orange,
            ProcessStatus.Error => Colors.Red,
            ProcessStatus.Processing => Colors.Transparent,
            ProcessStatus.Success => Colors.Green,
            ProcessStatus.Skip => Colors.GreenYellow,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
        if (targetType == typeof(Color))
        {
            return color;
        }

        if (targetType == typeof(Brush) || targetType == typeof(IBrush))
        {
            return new SolidColorBrush(color);
        }

        throw new ArgumentOutOfRangeException(nameof(targetType), targetType, null);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}