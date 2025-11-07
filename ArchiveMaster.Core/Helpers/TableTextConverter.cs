using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;

namespace ArchiveMaster.Helpers;

public class TableTextConverter
{
    public TableTextConverter(Type type, IEnumerable<PropertyFieldInfo> properties)
    {
        Type = type;
        Properties = properties?.ToArray();
    }


    /// <summary>
    /// 要导出的属性列表，如果为 null 或空，则导出所有公共属性
    /// </summary>
    public PropertyFieldInfo[] Properties { get; }

    /// <summary>
    /// 集合中元素的类型
    /// </summary>
    public Type Type { get; init; }

    /// <summary>
    /// 将数据集合转换为指定格式的表格文本
    /// </summary>
    /// <param name="data"></param>
    /// <param name="formatType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public string ConvertToTableText(IEnumerable data, TableTextType formatType)
    {
        if (data == null)
        {
            return "";
        }

        IList<PropertyFieldInfo> properties = null;
        if (Properties is { Length: > 0 })
        {
            foreach (var p in Properties)
            {
                p.PropertyInfo = Type.GetProperty(p.PropertyName, BindingFlags.Public | BindingFlags.Instance) ??
                                 throw new ArgumentException($"无法找到属性{p.PropertyName}");
            }

            properties = Properties;
        }
        else
        {
            properties = Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => new PropertyFieldInfo()
                {
                    PropertyInfo = p,
                    PropertyName = p.Name,
                    FieldName = p.Name,
                }).ToList();
        }


        return formatType switch
        {
            TableTextType.TabDelimited => ConvertToTabDelimited(data, properties),
            TableTextType.Csv => ConvertToCsv(data, properties),
            TableTextType.SpaceDelimited => ConvertToSpaceDelimited(data, properties),
            TableTextType.Json => ConvertToJson(data),
            TableTextType.Xml => ConvertToXml(data),
            TableTextType.Html => ConvertToHtml(data, properties),
            _ => throw new ArgumentOutOfRangeException(nameof(formatType), formatType, null)
        };
    }

    private static string GetValue(object obj, PropertyFieldInfo prop, TableTextType formatType)
    {
        var propValue = prop.PropertyInfo.GetValue(obj);
        if (propValue == null)
        {
            return "";
        }

        string str = null;
        if (prop.Converter != null)
        {
            str = prop.Converter(propValue);
        }
        else
        {
            if (propValue is string strValue)
            {
                str = strValue;
            }
            else
            {
                str = propValue.ToString();
            }
        }

        // 根据格式类型进行不同的转义处理
        switch (formatType)
        {
            case TableTextType.Csv:
                // 对 CSV 特殊字符进行转义（逗号、制表符、换行符等）
                if (str.Contains('"'))
                {
                    str = str.Replace("\"", "\"\"");
                }
                if (str.Contains(',') || str.Contains('\n') || str.Contains('\t') || str.Contains(' '))
                {
                    str = $"\"{str}\""; // 如果有逗号或换行符等，就加上双引号
                }
                break;

            case TableTextType.TabDelimited:
                // 对 TabDelimited 特殊字符进行转义（制表符、换行符等）
                if (str.Contains('\t') || str.Contains('\n') || str.Contains(' '))
                {
                    str = $"\"{str}\""; // 如果有制表符或换行符，就加上双引号
                }
                break;

            case TableTextType.SpaceDelimited:
                // 对 SpaceDelimited 特殊字符进行转义（空格等）
                if (str.Contains(' '))
                {
                    str = $"\"{str}\""; // 如果有空格，就加上双引号
                }
                break;

            case TableTextType.Html:
                // 对 HTML 进行实体编码（如 <, >, & 等字符）
                str = System.Net.WebUtility.HtmlEncode(str);
                break;

            case TableTextType.Xml:
                // 对 XML 特殊字符进行编码
                str = System.Security.SecurityElement.Escape(str);
                break;

            case TableTextType.Json:
                // JSON 中的特殊字符转义（如 \t、\n、引号等）
                str = str.Replace("\\", "\\\\").Replace("\"", "\\\"");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(formatType), formatType, "Unsupported format type");
        }

        return str;
    }

    private string ConvertToCsv(IEnumerable items, IList<PropertyFieldInfo> properties)
    {
        StringBuilder sb = new StringBuilder();

        // 生成表头
        foreach (var prop in properties)
        {
            sb.Append(prop.FieldName ?? prop.PropertyName);
            sb.Append(',');
        }

        sb.Length--;
        sb.AppendLine();

        // 生成数据行
        foreach (var item in items)
        {
            foreach (var prop in properties)
            {
                sb.Append(GetValue(item, prop, TableTextType.Csv));
                sb.Append(',');
            }

            sb.AppendLine();
        }

        // 移除最后一个多余的逗号
        sb.Length--;

        return sb.ToString();
    }

    private string ConvertToHtml(IEnumerable items, IList<PropertyFieldInfo> properties)
    {
        // 这里可以生成一个简单的 HTML 表格
        var sb = new StringBuilder();
        sb.AppendLine("<table cellspacing=\"0\">");

        // 生成表头
        sb.AppendLine("<tr>");
        foreach (var prop in properties)
        {
            sb.AppendLine($"<th>{prop.FieldName ?? prop.PropertyName}</th>");
        }

        sb.AppendLine("</tr>");

        // 生成数据行
        foreach (var item in items)
        {
            sb.AppendLine("<tr>");
            foreach (var prop in properties)
            {
                sb.AppendLine($"<td>{GetValue(item, prop, TableTextType.Html)}</td>");
            }

            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</table>");
        sb.AppendLine("""
                      <style>
                      table,table tr th, table tr td { border:1px solid black; }
                      </style>
                      """);
        return sb.ToString();
    }

    private string ConvertToJson(IEnumerable items)
    {
        // 直接使用 System.Text.Json 序列化集合
        return JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
    }

    private string ConvertToSpaceDelimited(IEnumerable items, IList<PropertyFieldInfo> properties)
    {
        StringBuilder sb = new StringBuilder();

        // 生成表头
        foreach (var prop in properties)
        {
            sb.Append(prop.FieldName ?? prop.PropertyName);
            sb.Append(' ');
        }

        sb.Length--;
        sb.AppendLine();

        // 生成数据行
        foreach (var item in items)
        {
            foreach (var prop in properties)
            {
                sb.Append(GetValue(item, prop, TableTextType.SpaceDelimited));
                sb.Append(' ');
            }

            sb.Length--;
            sb.AppendLine();
        }

        return sb.ToString();
    }
    private string ConvertToTabDelimited(IEnumerable items, IList<PropertyFieldInfo> properties)
    {
        StringBuilder sb = new StringBuilder();
        foreach (var prop in properties)
        {
            sb.Append(prop.FieldName ?? prop.PropertyName);
            sb.Append('\t');
        }

        sb.Length--;
        sb.AppendLine();

        foreach (var item in items)
        {
            foreach (var prop in properties)
            {
                sb.Append(GetValue(item, prop, TableTextType.TabDelimited));
                sb.Append('\t');
            }

            sb.Length--;
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string ConvertToXml(IEnumerable items)
    {
        // 使用 XmlSerializer 序列化集合
        var sb = new StringBuilder();
        var serializer = new XmlSerializer(items.GetType());

        using (var writer = new StringWriter(sb))
        {
            serializer.Serialize(writer, items);
        }

        return sb.ToString();
    }
}