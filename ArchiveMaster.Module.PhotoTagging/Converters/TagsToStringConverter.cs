using System.Globalization;
using System.Text;
using ArchiveMaster.Models;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Converters;

public class TagsToStringConverter : IValueConverter
{
    public static TagsToStringConverter Instance { get; } = new TagsToStringConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PhotoTags tags)
        {
            StringBuilder sb = new StringBuilder();
            AddToString(sb, "对象", tags.ObjectTags);
            AddToString(sb, "场景", tags.SceneTags);
            AddToString(sb, "情绪", tags.MoodTags);
            AddToString(sb, "颜色", tags.ColorTags);
            AddToString(sb, "拍摄", tags.TechniqueTags);
            AddToString(sb, "文本", tags.TextTags);

            return sb.ToString();
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new InvalidOperationException();
    }

    private void AddToString(StringBuilder sb, string name, List<string> tags)
    {
        sb.Append('【')
            .Append(name)
            .Append('】');
        if (tags.Count > 0)
        {
            sb.Append(tags[0]);
            foreach (var t in tags.Skip(1))
            {
                sb.Append('，');
                sb.Append(t);
            }
        }
        else
        {
            sb.Append("（无）");
        }

        sb.Append(' ');
    }
}