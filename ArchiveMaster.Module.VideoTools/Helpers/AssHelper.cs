using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Helpers;

public class AssHelper
{
    public static bool AutoGenerateVideoInfo(TimeAssFormat format, TimeAssVideoFileInfo file)
    {
        if (new[] { ".jpg", ".tif", ".raw", ".png", ".dng", ".arw", ".nef", ".cr2", ".rw2" }
            .Any(p => file.Name.EndsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            file.StartTime = file.Time;
            return true;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            FileName = "ffprobe",
            Arguments =
                $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{file.Path}\""
        };

        Process p = new Process() { StartInfo = startInfo };
        p.Start();
        p.WaitForExit();
        var output = p.StandardOutput.ReadToEnd().Trim();
        if (double.TryParse(output, out double length))
        {
            file.Length = TimeSpan.FromSeconds(length);
        }
        else
        {
            return false;
        }

        var modifiedTime = file.Time;
        file.StartTime = modifiedTime - file.Length;
        return true;
    }

    public static void Export(TimeAssFormat format, TimeAssVideoFileInfo file, string exportPath)
    {
        Export(format, [file], exportPath);
    }

    public static void Export(TimeAssFormat format, IList<TimeAssVideoFileInfo> files, string path)
    {
        Debug.Assert(!string.IsNullOrEmpty(path));
        StringBuilder outputs = GetAssHead(format, files);

        string timespanFormat = "hh\\:mm\\:ss\\:ff";
        var interval = TimeSpan.FromMilliseconds(format.Interval);
        TimeSpan totalTime = TimeSpan.Zero;
        foreach (var file in files)
        {
            TimeSpan currentTime = TimeSpan.Zero;
            while (true)
            {
                var nextTime = currentTime.Add(interval);
                if (nextTime > file.Length)
                {
                    nextTime = file.Length;
                }

                outputs.Append($"Dialogue: 3,")
                    .Append((totalTime + currentTime).ToString(timespanFormat))
                    .Append(",")
                    .Append((totalTime + nextTime).ToString(timespanFormat))
                    .Append(",Default,,0000,0000,0000,,")
                    .Append((file.StartTime.Value + currentTime * file.Ratio).ToString(format.TimeFormat))
                    .AppendLine();
                if (nextTime >= file.Length)
                {
                    break;
                }

                currentTime = nextTime;
            }

            totalTime += file.Length;
        }

        if (!Directory.Exists(Path.GetDirectoryName(path)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
        }

        File.WriteAllText(path, outputs.ToString());
    }

    public static string GetAssFileName(string path)
    {
        return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".ass");
    }

    public static StringBuilder GetAssHead(TimeAssFormat format, IList<TimeAssVideoFileInfo> files)
    {
        int size = format.Size;
        int margin = format.Margin;
        int al = format.Alignment + 1;
        int bw = format.BorderWidth;
        var c = (byte.MaxValue - format.TextColor.A).ToString("X2") + format.TextColor.ToString()[3..];
        var bc = (byte.MaxValue - format.BorderColor.A).ToString("X2") + format.BorderColor.ToString()[3..];
        int bold = format.Bold ? 1 : 0;
        int italic = format.Italic ? 1 : 0;
        int underline = format.Underline ? 1 : 0;

        StringBuilder outputs = new StringBuilder();
        outputs.AppendLine("[Script Info]")
            .AppendLine("; " + Parameters.AssSoftware)
            .AppendLine("; " + Parameters.AssAuthor)
            .AppendLine($"; {Parameters.AssFiles}={JsonSerializer.Serialize(files)}")
            .AppendLine($"; {Parameters.AssFormat}={JsonSerializer.Serialize(format)}")
            .AppendLine("ScriptType: v4.00+")
            .AppendLine("Collisions: Normal")
            .AppendLine("PlayResX: 1920")
            .AppendLine("PlayResY: 1080")
            .AppendLine()
            .AppendLine("[V4+ Styles]")
            .AppendLine(
                "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding")
            .AppendLine(
                $"Style: Default, Microsoft YaHei, {size}, &H{c}, &H{c}, &H{bc}, &H00000000, {bold}, {italic}, {underline}, 0, 100, 100, 0.00, 0.00, 1, {bw}, 0, {al}, {margin}, {margin}, {margin}, 0")
            .AppendLine()
            .AppendLine("[Events]")
            .AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");
        return outputs;
    }

    // public static (List<TimeAssVideoFileInfo> files, TimeAssFormat format) ImportFromAss(string path)
    // {
    //     string assText = File.ReadAllText(path);
    //     if (!assText.Contains(Parameters.AssSoftware))
    //     {
    //         throw new Exception("非本软件生成的ASS不可导入");
    //     }
    //
    //     var match = Regex.Match(assText, $@"{Parameters.AssFiles}=(?<Json>.+){Environment.NewLine}");
    //     if (match == null || match.Success == false)
    //     {
    //         throw new Exception("ASS格式错误");
    //     }
    //
    //     string json = match.Groups["Json"].Value;
    //
    //     var files = JsonSerializer.Deserialize<List<VideoFileInfo>>(json);
    //
    //     match = Regex.Match(assText, $@"{Parameters.AssFormat}=(?<Json>.+){Environment.NewLine}");
    //     if (match == null || match.Success == false)
    //     {
    //         throw new Exception("ASS格式错误");
    //     }
    //
    //     json = match.Groups["Json"].Value;
    //
    //     var format = JsonConvert.DeserializeObject<AssFormat>(json);
    //     return (files, format);
    // }

    static class Parameters
    {
        public const string AssSoftware = "Software=ArchiveMaster";
        public const string AssAuthor = "Author=f-shake";
        public const string AssFiles = "Files";
        public const string AssFormat = "Format";
    }
}