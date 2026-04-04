using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class NumbersToMarginConverter : IMultiValueConverter
{
    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Count != 2)
        {
            throw new ArgumentException("提供的值必须为2个");
        }

        if (values.Any(p => p  is UnsetValueType))
        {
            return new Thickness();
        }
        if (values.Any(p => p is not double && p is not int))
        {
            throw new ArgumentException("提供的值必须为数字");
        }
        
        var marginH=System.Convert.ToDouble(values[0]);
        var marginV=System.Convert.ToDouble(values[1]);
        return new Thickness(marginH, marginV, marginH, marginV);
    }
}