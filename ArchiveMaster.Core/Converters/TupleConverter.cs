using System.Globalization;
using System.Reflection;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class TupleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
        {
            return null;
        }

        var type = value.GetType();

        // 先处理参数，既支持索引也支持字段名
        if (parameter is string paramStr)
        {
            // 通过字段名 "Item{index+1}" 取值
            return GetValueByFieldName(value, int.TryParse(paramStr, out int index) ? 
                $"Item{index + 1}" : paramStr);
        }

        if (parameter is int indexParam)
        {
            return GetValueByFieldName(value, $"Item{indexParam + 1}");
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private object GetValueByFieldName(object tuple, string fieldName)
    {
        var type = tuple.GetType();
        // 元组的元素是字段，不是属性，反射取字段值
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null)
        {
            // 兼容命名元组的实际字段名
            // 反射只取字段，字段名就是真实名字，无需额外处理
            return null;
        }

        return field.GetValue(tuple);
    }
}