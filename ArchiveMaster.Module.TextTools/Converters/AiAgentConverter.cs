using System.Globalization;
using ArchiveMaster.Attributes;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class AiAgentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return null;
        }

        var field = value.GetType().GetField(value.ToString());
        var attr = field?.GetCustomAttributes(typeof(AiAgentAttribute), false);
        return attr is { Length: > 0 }
            ? ((AiAgentAttribute)attr[0]).Name
            : throw new ArgumentException("未找到提示");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}