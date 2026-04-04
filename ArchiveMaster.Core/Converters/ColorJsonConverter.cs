using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchiveMaster.Converters;

public class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string colorString = reader.GetString();
        if (string.IsNullOrEmpty(colorString))
        {
            return Color.Empty;
        }

        try
        {
            return ColorTranslator.FromHtml(colorString);
        }
        catch
        {
            throw new JsonException($"无法将字符串\"{colorString}\"转换为 Color。");
        }
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        // 将 Color 转换为 #RRGGBB 格式，如果包含 Alpha 通道则为 #AARRGGBB
        string hex;
        if (value.A == 255)
        {
            hex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
        }
        else
        {
            hex = $"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}";
        }

        writer.WriteStringValue(hex);
    }
}