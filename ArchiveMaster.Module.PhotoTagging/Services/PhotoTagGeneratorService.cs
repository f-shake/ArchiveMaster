using FzLib;
using ArchiveMaster.Configs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Channels;
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

                                             2.任务：对用户提供的图片进行内容提炼，输出 {MinTagCount}-{MaxTagCount} 个核心关键词。

                                             3.【强制原子化规则】：
                                             每个关键词必须是不可再分的最小语义单位，能够独立表达清晰含义。
                                             严禁输出任何组合词、修饰结构或短语。

                                             错误示例（禁止出现）：
                                             晴朗天空、大型机械、建设场景、蓝色海洋、繁忙城市、夕阳西下、城市建筑

                                             正确示例：
                                             晴朗，天空，机械，建设，场景，蓝色，海洋，城市，夕阳，日落，建筑

                                             4.【强制长度规则】：
                                             每个关键词必须严格为 2 或 3 个汉字。

                                             禁止出现：
                                             - 1个字词语
                                             - 4个或以上汉字词语

                                             若产生4字或以上词语，必须拆分为多个2-3字词语后再输出

                                             5.【语义去重规则】：
                                             禁止输出语义重复或近义词，例如：
                                             天空，蓝天
                                             城市，都市
                                             只保留一个最合适的词

                                             6.【维度覆盖要求】：
                                             关键词必须尽量覆盖以下维度：
                                             - 核心主体（人物、物体）
                                             - 场景环境（地点、空间）
                                             - 氛围情绪（如宁静、热闹）
                                             - 色彩基调（如蓝色、昏暗）
                                             - 拍摄方式（仅当存在，如航拍、微距）

                                             7.【输出前强制自检】：
                                             在输出前逐个检查所有关键词：
                                             - 是否为2或3个字
                                             - 是否仍可拆分
                                             - 是否包含组合词
                                             - 是否存在语义重复

                                             如存在任一问题，必须修改后再输出

                                             8.【输出格式】：
                                             仅输出关键词列表，不得包含任何解释、说明或额外内容。
                                             使用中文逗号（，）分隔所有关键词。

                                             9.【示例输出】：
                                             大桥，斜拉桥，高楼，河流，水面，城市，蓝天，建筑，现代，航拍
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
            systemPrompts = SYSTEM_PROMPT
                .Replace("{MinTagCount}", Config.MinTagCount.ToString())
                .Replace("{MaxTagCount}", Config.MaxTagCount.ToString());

            var files = Files.CheckedOnly().ToList();
            var llm = new LlmCallerService(GlobalConfigs.Instance.AiProviders.CurrentProvider);

            //存在两个耗时操作：图片处理和AI调用。使用流水线，避免等待时候的浪费。
            var channel = Channel.CreateBounded<(TaggingPhotoFileInfo File, byte[] Bytes)>(new BoundedChannelOptions(10)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

            // 生产者：负责图片处理
            Dictionary<string, Exception> photoProcessingErrors = new Dictionary<string, Exception>();
            var producer = Task.Run(async () =>
            {
                int index = 0;
                try
                {
                    foreach (var file in files)
                    {
                        index++;
                        Debug.WriteLine($"正在压缩第{index}张图片");
                        ct.ThrowIfCancellationRequested();

                        byte[] bytes = null;
                        try
                        {
                            bytes = GetPhotoBytes(file.Path);
                        }
                        catch (Exception ex)
                        {
                            photoProcessingErrors.Add(file.RelativePath, ex);
                        }

                        await channel.Writer.WriteAsync((file, bytes), ct);
                    }
                }
                finally
                {
                    channel.Writer.Complete(); // 标记生产完成
                }
            }, ct);

            // 消费者：负责AI调用
            var consumer = Task.Run(async () =>
            {
                int index = 0;
                await foreach (var item in channel.Reader.ReadAllAsync(ct))
                {
                    ct.ThrowIfCancellationRequested();
                    index++;
                    Debug.WriteLine($"正在AI打标签第{index}张图片");
                    NotifyMessage($"正在生成图片标签（{index}/{files.Count}）：{item.File.RelativePath}");
                    NotifyProgress(index, files.Count);
                    try
                    {
                        if (item.Bytes == null)
                        {
                            if (photoProcessingErrors.TryGetValue(item.File.RelativePath, out var ex))
                            {
                                throw new Exception("图片处理失败", ex);
                            }
                            else
                            {
                                throw new Exception("图片处理失败，未知错误");
                            }
                        }

                        item.File.Tags = await GetTagsAsync(llm, item.Bytes, ct);
                        item.File.HasGenerated = true;
                        item.File.Success();
                    }
                    catch (OperationCanceledException)
                    {
                        
                    }
                    catch (Exception ex)
                    {
                        item.File.Error(ex);
                    }

                    if (index % 10 == 0)
                    {
                        await SaveTagsAsync();
                    }
                }
            }, ct);

            await Task.WhenAll(producer, consumer);
            await SaveTagsAsync();
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
                        NotifyMessage($"正在搜索文件{s.GetFileNumberMessage()}");
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

        private async Task<List<TagInfo>> GetTagsAsync(LlmCallerService llm, byte[] imageBytes,
            CancellationToken ct)
        {
            IEnumerable<string> GetTags(string tagResult)
            {
                return tagResult.Split([",", "，", "、"], StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Distinct();
            }

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