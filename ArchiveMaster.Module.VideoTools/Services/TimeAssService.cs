using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.IO;

namespace ArchiveMaster.Services
{
    public class TimeAssService(AppConfig appConfig) : TwoStepServiceBase<TimeAssConfig>(appConfig)
    {
        public const string ASS_AUTHOR = "Author=f-shake";
        public const string ASS_FILE = "Files";
        public const string ASS_FORMAT = "Format";
        public const string ASS_SOFTWARE = "Software=ArchiveMaster";

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

            using Process p = new Process();
            p.StartInfo = startInfo;
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
            int al = format.HorizontalAlignment + format.VerticalAlignment * 3 + 1;
            int bw = format.BorderWidth;
            var c = (byte.MaxValue - format.TextColor.A).ToString("X2") + format.TextColor.ToString()[3..];
            var bc = (byte.MaxValue - format.BorderColor.A).ToString("X2") + format.BorderColor.ToString()[3..];
            int bold = format.Bold ? 1 : 0;
            int italic = format.Italic ? 1 : 0;
            int underline = format.Underline ? 1 : 0;

            StringBuilder outputs = new StringBuilder();
            outputs.AppendLine("[Script Info]")
                .AppendLine("; " + ASS_SOFTWARE)
                .AppendLine("; " + ASS_AUTHOR)
                .AppendLine($"; {ASS_FILE}={JsonSerializer.Serialize(files)}")
                .AppendLine($"; {ASS_FORMAT}={JsonSerializer.Serialize(format)}")
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

        public static (List<TimeAssVideoFileInfo> files, TimeAssFormat format) ImportFromAss(string path)
        {
            string assText = File.ReadAllText(path);
            if (!assText.Contains(ASS_SOFTWARE))
            {
                throw new Exception("非本软件生成的ASS不可导入");
            }

            var match = Regex.Match(assText, $@"{ASS_FILE}=(?<Json>.+){Environment.NewLine}");
            if (match == null || match.Success == false)
            {
                throw new Exception("ASS格式错误");
            }

            string json = match.Groups["Json"].Value;

            var files = JsonSerializer.Deserialize<List<TimeAssVideoFileInfo>>(json);

            match = Regex.Match(assText, $@"{ASS_FORMAT}=(?<Json>.+){Environment.NewLine}");
            if (match == null || match.Success == false)
            {
                throw new Exception("ASS格式错误");
            }

            json = match.Groups["Json"].Value;

            var format = JsonSerializer.Deserialize<TimeAssFormat>(json);
            return (files, format);
        }


        public override Task ExecuteAsync(CancellationToken token = default)
        {
            //     var files = Files.Where(p => p.IsChecked).ToList();
            //     var totalLength = files.Sum(p => p.Length);
            //     long currentLength = 0;
            //     return TryForFilesAsync(files,
            //         async (file, state) =>
            //         {
            //             int index = state.FileIndex;
            //             int count = state.FileCount;
            //             NotifyMessage($"正在复制（{index}/{count}），当前文件：{Path.GetFileName(file.Name)}");
            //
            //             await FileCopyHelper.CopyFileAsync(file.Path, file.DestinationPath,
            //                 progress: new Progress<FileProcessProgress>(
            //                     p =>
            //                     {
            //                         NotifyMessage(
            //                             $"正在复制（{index}/{count}，当前文件{1.0 * p.ProcessedBytes / 1024 / 1024:0}MB/{1.0 * p.TotalBytes / 1024 / 1024:0}MB），当前文件：{Path.GetFileName(p.SourceFilePath)}");
            //                       
            //                         NotifyProgress(1.0 * (currentLength + p.ProcessedBytes) / totalLength);
            //                     }),
            //                 cancellationToken: token);
            //             File.SetLastWriteTimeUtc(file.DestinationPath, file.Time);
            //             currentLength += file.Length;
            //         },
            //         token, FilesLoopOptions.Builder().AutoApplyFileLengthProgress().AutoApplyStatus().Build());
            return Task.CompletedTask;
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            // return Files.Cast<SimpleFileInfo>();
            return [];
        }

        public override async Task InitializeAsync(CancellationToken token = default)
        {
            // var files = new DirectoryInfo(Config.SourceDir)
            //     .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
            //     .ApplyFilter(token)
            //     .Select(f => new CopyingFile(f, Config.SourceDir));
            // Files = new List<CopyingFile>();
            // await TryForFilesAsync(files,
            //     (f, s) =>
            //     {
            //         f.DestinationPath = Path.Combine(Config.DestinationDir,
            //             Path.GetRelativePath(Config.SourceDir, f.Path));
            //         Files.Add(f);
            //     }, token, FilesLoopOptions.DoNothing());
        }
    }
}