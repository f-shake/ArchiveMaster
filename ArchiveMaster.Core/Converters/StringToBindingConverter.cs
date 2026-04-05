using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class StringToBindingConverter : IMultiValueConverter
{
    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Count < 2)
        {
            throw new ArgumentException("应当有2/3个参数，第一个是为对象，第二个是为属性名称，第三个（可选）为转换器");
        }

        var obj = values[0];
        var propertyName = values[1];
        var converter = values.Count > 2 ? values[2] : null;
        if (obj == null || propertyName == null || propertyName is UnsetValueType ||
            propertyName is not string propertyNameStr)
        {
            return null;
        }

        object value = null;
        if (propertyNameStr == ".")
        {
            value = obj;
        }
        else
        {
            var property = obj.GetType().GetProperty(propertyNameStr);
            if (property == null)
            {
                throw new ArgumentException($"对象{obj.GetType()}不存在属性{propertyName}");
            }

            value = property.GetValue(obj);
        }

        if (converter is null or UnsetValueType)
        {
            if (targetType == typeof(string))
            {
                return value?.ToString();
            }

            if (targetType == typeof(InlineCollection))
            {
                return new InlineCollection() { new Run(value?.ToString()) };
            }

            throw new ArgumentException($"{nameof(targetType)}必须是string或InlineCollection");
        }

        if (converter is IValueConverter c)
        {
            var convertedValue = c.Convert(value, targetType, parameter, culture);
            return convertedValue;
        }

        throw new ArgumentException("第三个参数必须是转换器");
    }
}