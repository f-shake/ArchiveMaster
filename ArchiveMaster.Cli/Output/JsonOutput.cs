using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace ArchiveMaster.Cli;

public static class JsonOutput
{
    /// <summary>
    /// 结果输出用：camelCase、紧凑、省略 null。
    /// </summary>
    public static readonly JsonSerializerOptions Result = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, Result);
    }

    public static string ReadAllInput(string path)
    {
        if (path == "-")
        {
            return Console.In.ReadToEnd();
        }

        if (!File.Exists(path))
        {
            throw new CliException(2, $"配置文件不存在：{path}");
        }

        return File.ReadAllText(path, Encoding.UTF8);
    }
}
