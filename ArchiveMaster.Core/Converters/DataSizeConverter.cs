using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public abstract class DataSizeConverterBase : IValueConverter
{
    public abstract string[] Units { get; set; }

    // 提供默认精度，并在XAML中可配置
    public int DecimalDigits { get; set; } = 2;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return null;

        // 统一处理 int, long, float, double
        if (value is IConvertible convertible)
        {
            double numericValue = convertible.ToDouble(CultureInfo.InvariantCulture);
            return FormatSize(numericValue, Units, DecimalDigits);
        }

        return null;
    }

    private static string FormatSize(double size, string[] units, int decimalDigits)
    {
        if (size < 0 || units == null || units.Length == 0)
            return "";

        double num = size;
        int index = 0;

        // 循环换算单位
        while (index < units.Length - 1 && (num >= 1024.0 || units[index] == null))
        {
            num /= 1024.0;
            index++;
        }

        // 格式化处理：如果是个位单位(B)，通常不需要小数；否则应用精度格式
        // 如果你需要所有单位都保留小数，可以将此处的逻辑简化为 string format = $"N{decimalDigits}";
        string format = (index == 0) ? "F0" : $"N{decimalDigits}";

        return num.ToString(format) + units[index];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ByteSizeConverter : DataSizeConverterBase
{
    public override string[] Units { get; set; } = { " B", " KB", " MB", " GB", " TB" };
}

public class BitSizeConverter : DataSizeConverterBase
{
    public override string[] Units { get; set; } = { " b", " Kb", " Mb", " Gb", " Tb" };
}

public class BitRateConverter : DataSizeConverterBase
{
    public override string[] Units { get; set; } = { " bps", " Kbps", " Mbps", " Gbps", " Tbps" };
}