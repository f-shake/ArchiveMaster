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
using System.Text.Json.Nodes;
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

                                             2.任务：对用户提供的图片进行内容提炼，按维度输出 {TagCount} 个核心关键词。

                                             3.【强制原子化规则】：
                                             每个关键词必须是不可再分的最小语义单位。
                                             严禁输出组合词或短语（如：禁止“蓝色海洋”，必须拆分为“蓝色，海洋”）。

                                             4.【强制长度规则】：
                                             每个关键词严格限制在 2~4 个汉字。

                                             5.【语义去重规则】：
                                             禁止输出重复或近义词，只保留一个最合适的词。

                                             6.【维度定义与 JSON 键名】：
                                             请按以下英文键名对内容归类：
                                             - `objects`: 核心主体（人物、动物、单体建筑、具体物件）
                                             - `scene`: 场景环境（地理位置、空间、宏观环境）
                                             - `mood`: 氛围情绪（如：宁静、热闹、现代、复古）
                                             - `colors`: 色彩基调（如：蓝色、昏暗、明亮）
                                             - `technique`: 拍摄方式（如：航拍、微距、特写、仰拍）
                                             - `text`: 文字内容（图中可见的招牌、标题或文字摘要）
                                             - `desc`: 对图像的整体概括和描述，大约{DescriptionLength}字

                                             7.【输出格式要求】：
                                             必须仅输出一个纯 JSON 对象，不得包含 Markdown 代码块标记（如 ```json）或任何额外说明。格式如下：
                                             {
                                               "objects": [],
                                               "scene": [],
                                               "mood": [],
                                               "colors": [],
                                               "technique": [],
                                               "text": [],
                                               "desc": "string"
                                             }
                                             注：若某维度无内容，数组必须为空 []。

                                             8.【输出前强制自检】：
                                             - 词数是否在 {MinTagCount} 到 {MaxTagCount} 之间？
                                             - 所有的词是否均为 2-4 个汉字？

                                             9.【示例输出】：
                                             {
                                               "objects": ["大桥", "斜拉桥", "高楼"],
                                               "scene": ["河流", "城市"],
                                               "mood": ["现代", "宏伟"],
                                               "colors": ["蓝色", "明亮"],
                                               "technique": ["航拍"],
                                               "text": [],
                                               "desc": "晴天下繁忙的工业港口码头，整齐堆放的彩色集装箱与大型起重机械交织，远处可见城市轮廓与河流。"
                                             }
                                             """;


        private string systemPrompts;

        public List<TaggingPhotoFileInfo> Files { get; private set; }

        public override async Task ExecuteAsync(CancellationToken ct = default)
        {
            systemPrompts = SYSTEM_PROMPT
                .Replace("{TagCount}", Config.TargetTagCount.ToString())
                .Replace("{DescriptionLength}", Config.DescriptionLength.ToString());

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

                var existingFiles = new Dictionary<string, TaggedPhoto>();
                if (File.Exists(Config.TagFile))
                {
                    existingFiles = (await TagFileHelper.ReadPhotoTagCollectionAsync(Config.TagFile, ct)).Photos
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

        private async Task<PhotoTags> GetTagsAsync(LlmCallerService llm,
            byte[] imageBytes, CancellationToken ct)
        {
            var sys = AiChatMessage.CreateSystemMessage(systemPrompts);
            var user = AiChatMessage.CreateUserMessage("", [imageBytes]);

            var result = await llm.CallAsync([sys, user], ct: ct);
            return ParseTags(result);
        }

        private PhotoTags ParseTags(string tagResult)
        {
            List<string> ParseTags(JsonNode jNode)
            {
                if (jNode is not JsonArray jArray)
                {
                    throw new JsonException("AI返回的JSON格式不正确");
                }

                return jArray.Select(p => p.GetValue<string>()).ToList();
            }

            var jObj = (JsonObject)JsonNode.Parse(tagResult);
            if (jObj.ContainsKey("objects")
                && jObj.ContainsKey("scene")
                && jObj.ContainsKey("mood")
                && jObj.ContainsKey("colors")
                && jObj.ContainsKey("technique")
                && jObj.ContainsKey("text")
                && jObj.ContainsKey("desc"))
            {
                return new PhotoTags(
                    ParseTags(jObj["objects"]),
                    ParseTags(jObj["scene"]),
                    ParseTags(jObj["mood"]),
                    ParseTags(jObj["colors"]),
                    ParseTags(jObj["technique"]),
                    ParseTags(jObj["text"]),
                    jObj["desc"].GetValue<string>()
                );
            }

            throw new JsonException("AI返回的JSON格式不正确");
        }

        private async Task SaveTagsAsync()
        {
            var photoTags = Files
                .Where(p => p.HasGenerated)
                .OrderBy(p => p.RelativePath)
                .Select(p => new TaggedPhoto(p.RelativePath, p.Tags))
                .ToList();
            await TagFileHelper.WritePhotoTagCollectionAsync(Config.TagFile, new TaggedPhotoCollection(photoTags));
        }
    }
}