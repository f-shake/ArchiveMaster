using FzLib;
using ArchiveMaster.Configs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using ImageMagick;

namespace ArchiveMaster.Services
{
    public class PhotoTagGeneratorService(AppConfig appConfig)
        : TwoStepServiceBase<PhotoTagGeneratorConfig>(appConfig)
    {
        private const string SYSTEM_PROMPT = """
                                             1.角色：图像语义原子化标注员
                                             2.任务：请对用户提供的图片进行内容提炼，输出 {MinTagCount}-{MaxTagCount} 个核心关键词。
                                             3.原子化拆解：每个词语必须是不可再分的最小语义单位。严禁合成词，例如：禁止“夕阳西下”，应拆分为“夕阳，日落”；禁止“城市建筑”，应拆分为“城市，建筑”。
                                             4.字数约束：每个关键词限 2-3 字，要么是两个字的词语，要么是三个字的词语，尽量使用两个字的词语。
                                             5.输出格式：仅输出结果，禁止任何开场白、解释或总结。词语间固定使用中文逗号（，）分隔。
                                             6.维度要求：关键词应涵盖【核心主体、场景环境、氛围情感、色彩基调、拍摄方式】。
                                             7.例如：大桥，斜拉桥，高楼，河流，水面，城市，蓝天，建筑，现代，航拍
                                             8.负面词实例：水库风光，梯田景观，乡村道路，绿色植被，航拍视角，田园风光，山水相依，农业种植，蜿蜒公路，生态和谐，现代城市，公园绿化
                                             """;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true,
        };

        private string systemPrompts;

        public List<TaggingPhotoFileInfo> Files { get; private set; }

        public override async Task ExecuteAsync(CancellationToken ct = default)
        {
            await Task.Run(async () =>
            {
                systemPrompts = SYSTEM_PROMPT
                    .Replace("{MinTagCount}", Config.MinTagCount.ToString())
                    .Replace("{MaxTagCount}", Config.MaxTagCount.ToString());
                LlmCallerService llm = new LlmCallerService(GlobalConfigs.Instance.AiProviders.CurrentProvider);
                var files = Files.CheckedOnly().ToList();
                await TryForFilesAsync(files,
                    async (file, s) =>
                    {
                        NotifyMessage($"正在生成图片标签{s.GetFileNumberMessage()}：{file.RelativePath}");
                        file.Tags = await GetTagsAsync(llm, file, ct);
                        file.HasGenerated = true;
                        if (s.FileIndex % 10 == 0)
                        {
                            await SaveTagsAsync();
                        }
                    },
                    ct,
                    FilesLoopOptions.Builder().AutoApplyFileNumberProgress().AutoApplyStatus().Build());
                await SaveTagsAsync();
            }, ct);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return Files;
        }

        public override async Task InitializeAsync(CancellationToken ct = default)
        {
            NotifyMessage("正在查找文件");
            List<TaggingPhotoFileInfo> files = new List<TaggingPhotoFileInfo>();
            await Task.Run(async () =>
            {
                var enumerableFiles = new DirectoryInfo(Config.Dir)
                    .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                    .ApplyFilter(ct, Config.Filter)
                    .Select(p => new TaggingPhotoFileInfo(p, Config.Dir));

                var existingFiles = new Dictionary<string, PhotoTag>();
                if (File.Exists(Config.TagFile))
                {
                    var fileContent = await File.ReadAllTextAsync(Config.TagFile, ct);
                    existingFiles = JsonSerializer.Deserialize<PhotoTagCollection>(fileContent, JsonOptions)
                        .Photos
                        .ToDictionary(p => p.RelativePath);
                }

                await TryForFilesAsync(enumerableFiles, (file, s) =>
                    {
                        NotifyMessage($"正在{s.GetFileNumberMessage()}");
                        if (existingFiles.TryGetValue(file.RelativePath, out var f))
                        {
                            file.Tags = f.Tags;
                            file.HasGenerated = true;
                        }

                        file.IsChecked = !file.HasGenerated;
                        files.Add(file);
                    },
                    ct,
                    FilesLoopOptions.DoNothing());
            }, ct);

            Files = files;
        }

        private byte[] GetPhotoBytes(string path)
        {
            using var image = new MagickImage(path);

            // 1. 获取目标总像素 (单位: 万像素 -> 实际像素)
            // 例如：800 * 10,000 = 8,000,000 像素
            long targetPixels = (long)Config.ResizingTargetResolutionIn10k * 10000;
            long currentPixels = (long)image.Width * image.Height;

            // 2. 如果当前像素超过目标，进行等比例缩放
            if (currentPixels > targetPixels)
            {
                // 计算缩放比例的平方根，以保持宽高比
                double ratio = Math.Sqrt((double)targetPixels / currentPixels);
                image.Resize(new Percentage(ratio * 100), FilterType.Lanczos);
            }

            image.Format = MagickFormat.Jpeg;
            return image.ToByteArray();
        }

        private async Task<List<TagInfo>> GetTagsAsync(LlmCallerService llm, TaggingPhotoFileInfo file,
            CancellationToken ct)
        {
            IEnumerable<string> GetTags(string tagResult)
            {
                return tagResult.Split([",", "，", "、"], StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Distinct();
            }

            var imageBytes = GetPhotoBytes(file.Path);
            var sys = AiChatMessage.CreateSystemMessage(systemPrompts);
            var user = AiChatMessage.CreateUserMessage("", [imageBytes]);
            List<TagInfo> tags = new List<TagInfo>();
            if (Config.EnableMajorityVote)
            {
                var tasks = new List<Task<string>>();
                for (int i = 0; i < Config.VoteCount; i++)
                {
                    tasks.Add(llm.CallAsync([sys, user], ct: ct));
                }

                await Task.WhenAll(tasks);
                Dictionary<string, int> tagVotes = new Dictionary<string, int>();
                foreach (var t in tasks)
                {
                    var taskResult = t.Result;
                    var taskTags = GetTags(taskResult);
                    foreach (var tag in taskTags)
                    {
                        if (!tagVotes.TryAdd(tag, 1))
                        {
                            tagVotes[tag]++;
                        }
                    }
                }

                foreach (var tagVote in tagVotes)
                {
                    if (tagVote.Value < Config.MinVoteThreshold)
                    {
                        continue;
                    }

                    tags.Add(new TagInfo(tagVote.Key, tagVote.Value));
                }

                tags.Sort((tag1, tag2) =>
                {
                    int voteComparison = tag2.Votes.CompareTo(tag1.Votes);
                    return voteComparison != 0
                        ? voteComparison
                        : string.Compare(tag1.Tag, tag2.Tag, StringComparison.Ordinal);
                });
            }
            else
            {
                var result = await llm.CallAsync([sys, user], ct: ct);
                tags.AddRange(GetTags(result).Select(p => new TagInfo(p, 1)));
            }

            return tags;
        }

        private async Task SaveTagsAsync()
        {
            var photoTags = Files
                .Where(p => p.HasGenerated)
                .Select(p => p.ToPhotoTag())
                .OrderBy(p => p.RelativePath)
                .ToList();
            var photoTagCollection = new PhotoTagCollection(photoTags);
            await File.WriteAllTextAsync(Config.TagFile, JsonSerializer.Serialize(photoTagCollection, JsonOptions));
        }
    }
}