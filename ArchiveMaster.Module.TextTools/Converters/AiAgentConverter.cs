using System.Globalization;
using System.Reflection;
using ArchiveMaster.Attributes;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class EnumAttributePropertyConverter<TAttribute> : IValueConverter where TAttribute : Attribute
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(parameter);

        var field = value.GetType().GetField(value.ToString());
        var attr = field?.GetCustomAttributes(typeof(TAttribute), false);
        var agent = attr is { Length: > 0 }
            ? ((TAttribute)attr[0])
            : throw new ArgumentException($"未找到{nameof(TAttribute)}");

        var attributeType = typeof(TAttribute);
        var property = attributeType.GetProperty(parameter.ToString());
        if (property == null)
        {
            throw new ArgumentException($"未找到{nameof(TAttribute)}的{parameter}属性");
        }
        return property.GetValue(agent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class AiAgentConverter : EnumAttributePropertyConverter<AiAgentAttribute>
{
}