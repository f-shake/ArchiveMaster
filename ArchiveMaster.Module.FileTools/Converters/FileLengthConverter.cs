using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class FileLengthConverter : IValueConverter
{
    // 字节单位映射
    private static readonly Dictionary<string, long> UnitMultipliers =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["B"] = 1L,
            ["BYTE"] = 1L,
            ["BYTES"] = 1L,
            ["字节"] = 1L,
            ["KB"] = 1024L,
            ["K"] = 1024L,
            ["MB"] = 1024L * 1024L,
            ["M"] = 1024L * 1024L,
            ["GB"] = 1024L * 1024L * 1024L,
            ["G"] = 1024L * 1024L * 1024L,
            ["TB"] = 1024L * 1024L * 1024L * 1024L,
            ["T"] = 1024L * 1024L * 1024L * 1024L,
            ["PB"] = 1024L * 1024L * 1024L * 1024L * 1024L,
            ["P"] = 1024L * 1024L * 1024L * 1024L * 1024L,
            ["EB"] = 1024L * 1024L * 1024L * 1024L * 1024L * 1024L,
            ["E"] = 1024L * 1024L * 1024L * 1024L * 1024L * 1024L,
        };

    // 单位列表，从大到小排列
    private static readonly (string unit, long multiplier)[] Units = new[]
    {
        ("EB", 1024L * 1024L * 1024L * 1024L * 1024L * 1024L),
        ("PB", 1024L * 1024L * 1024L * 1024L * 1024L),
        ("TB", 1024L * 1024L * 1024L * 1024L),
        ("GB", 1024L * 1024L * 1024L),
        ("MB", 1024L * 1024L),
        ("KB", 1024L),
        ("B", 1L)
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return "（无限制）";
        }

        if (value is long fileSize)
        {
            if (fileSize == 0)
            {
                return "0 B";
            }

            // 遍历所有单位，从大到小检查
            foreach (var (unit, multiplier) in Units)
            {
                // 只有当文件大小大于等于当前单位，并且能被当前单位整除时，才使用这个单位
                if (fileSize >= multiplier && (multiplier == 1L || fileSize % multiplier == 0))
                {
                    long result = fileSize / multiplier;

                    // 只有当结果大于0时才使用这个单位显示
                    if (result > 0)
                    {
                        // 如果单位是字节，显示带千位分隔符的数字
                        if (unit == "B")
                        {
                            return $"{fileSize.ToString("N0", culture)} B";
                        }
                        else
                        {
                            return $"{result.ToString("N0", culture)} {unit}";
                        }
                    }
                }
            }

            // 如果没有找到合适的单位，或者数字不能被单位整除，显示原始字节数
            return $"{fileSize.ToString("N0", culture)} B";
        }

        return "0";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string input || string.IsNullOrWhiteSpace(input))
        {
            return 0L;
        }

        input = input.Trim();

        if (input is "∞" or "（无限制）" or "" or ".")
        {
            return null;
        }

        try
        {
            if (long.TryParse(input.Replace(",", ""), NumberStyles.Any, culture, out long result))
            {
                return result;
            }

            var match = Regex.Match(input, @"^\s*([\d,]+(?:\.\d+)?)\s*([A-Za-z\u4e00-\u9fa5]+)?\s*$");
            if (!match.Success)
            {
                return null;
            }

            var numberPart = match.Groups[1].Value.Replace(",", "");
            if (!double.TryParse(numberPart, NumberStyles.Any, culture, out double number))
            {
                return 0L;
            }

            var unit = match.Groups[2].Value;
            if (string.IsNullOrEmpty(unit))
            {
                unit = "B";
            }

            if (UnitMultipliers.TryGetValue(unit, out long multiplier))
            {
                checked
                {
                    return (long)(number * multiplier);
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}