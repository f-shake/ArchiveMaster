using System.Globalization;
using ArchiveMaster.Models;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class TagsToStringConverter : IValueConverter
{
    public static TagsToStringConverter Instance { get; } = new TagsToStringConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not List<TagInfo> tags)
        {
            return null;
        }

        return string.Join("，", tags.Select(t =>
        {
            if (t.Votes == 1)
            {
                return t.Tag;
            }

            return $"{t.Tag} ({t.Votes})";
        }));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new InvalidOperationException();
    }
}