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
                                             每个关键词严格限制在 1~4 个汉字。

                                             5.【语义去重规则】：
                                             禁止输出重复或近义词，只保留一个最合适的词。

                                             6.【维度定义与输出规范】：
                                             - `objects`: 核心主体。参考：男性、小孩、集体、高楼、桥梁、猫、狗、花、森林、轿车、飞机、轮船、纸张、屏幕、桌子、椅子。
                                             - `scene`: 场景环境。参考：室内、户外、街头、公园、海滨、办公室、商场、山地、工业区等。以上仅为建议，非约束条件。
                                             - `mood`: 氛围情绪。参考：宁静、热闹、孤独、现代、复古、唯美、压抑、明快、自然、庄严、温馨、生活化、工业感。
                                             - `colors`: 色彩基调。参考：红色、蓝色、绿色、冷色调、暖色调、明亮、昏暗、高饱和、黑白。
                                             - `technique`: 拍摄方式。参考：航拍、俯拍、仰拍、平视、特写、微距、全景、长曝光、大场景、剪影、抓拍、对称构图、扫描、夜景、截屏。
                                             - `ocr`: 文字内容。 图中可见的文字。尽可能忠于原文，无文字则为空字符串。
                                             - `desc`: 整体描述。图像整体概括，限制在{DescriptionLength}字左右。

                                             注意：
                                             - 标签（不含ocr和desc）的总数量应当在{TagCount}左右。若图像内容过少，无法达到这个数量，可以略微减少。
                                             - 以上提到的参考，仅为建议，非约束条件。

                                             7.【输出格式要求】：
                                             必须仅输出一个纯 JSON 对象，不得包含 Markdown 代码块标记（如 ```json）或任何额外说明。格式如下：
                                             {
                                               "objects": [],
                                               "scene": [],
                                               "mood": [],
                                               "colors": [],
                                               "technique": [],
                                               "ocr": "string",
                                               "desc": "string"
                                             }
                                             注：若某维度无内容，数组必须为空 []，字符串应当为空字符串""。
                                             严禁输出任何非JSON文本，包括但不限于开场白、解释或总结。

                                             8.【输出前强制自检】：
                                             - 词数是否在{TagCount}左右？
                                             - 每个标签的词是否均为 1-4 个汉字？
                                             - 非标签字段（ocr、desc）是否为字符串类型？
                                             - 标签字段（objects、scene、mood、colors、technique）是否为数组类型？
                                             - 是否以{开头并以}结尾？

                                             9.【示例输出】（仅供格式参考，标签数量和描述长度请以上面的要求为准）：
                                             {
                                               "objects": ["大桥", "斜拉桥", "高楼"],
                                               "scene": ["河流", "城市"],
                                               "mood": ["现代", "宏伟"],
                                               "colors": ["蓝色", "明亮"],
                                               "technique": ["航拍"],
                                               "ocr": "宁波舟山港北仑港区",
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
            var channel = Channel.CreateBounded<(TaggingPhotoFileInfo File, byte[] Bytes)>(new BoundedChannelOptions(5)
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
                int generated = Files.Count(f => f.HasGenerated);
                var semaphore = new SemaphoreSlim(Math.Min(Math.Max(1, Config.MaxDegreeOfParallelism), 8));
                var tasks = new List<Task>();

                await foreach (var item in channel.Reader.ReadAllAsync(ct))
                {
                    ct.ThrowIfCancellationRequested();
                    await semaphore.WaitAsync(ct); // 等待空闲槽位

                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            int currentIndex = Interlocked.Increment(ref index);
                            NotifyMessage(
                                $"正在生成图片标签（{currentIndex}/{files.Count}，累计{generated + 1}/{Files.Count}）：{item.File.RelativePath}");
                            NotifyProgress(currentIndex - 1, files.Count);

                            if (item.Bytes == null)
                            {
                                throw new Exception("图片处理失败");
                            }

                            bool hasGenerated = item.File.HasGenerated;
                            item.File.Processing();
                            UpdateCurrentProcessingFile(item.File, true);

                            int retryCount = Config.RetryCount;
                            int currentTry = 0;
                            if (retryCount <= 0)
                            {
                                item.File.Tags = await GetTagsAsync(llm, item.Bytes, ct);
                            }
                            else
                            {
                                bool isSuccess = false;
                                while (currentTry <= retryCount)
                                {
                                    try
                                    {
                                        item.File.Tags = await GetTagsAsync(llm, item.Bytes, ct);
                                        isSuccess = true;
                                        break;
                                    }
                                    catch (Exception ex) when (currentTry < retryCount && !ct.IsCancellationRequested)
                                    {
                                        currentTry++;
                                        int delayMs = (int)Math.Pow(2, currentTry) * 200;

                                        Debug.WriteLine(
                                            $"图片 {item.File.RelativePath} 生成失败，正在进行第 {currentTry} 次重试。错误: {ex.Message}");

                                        await Task.Delay(delayMs, ct);
                                    }
                                }

                                if (!isSuccess)
                                {
                                    throw new Exception($"在重试 {retryCount} 次后依然失败。");
                                }
                            }


                            item.File.HasGenerated = true;
                            if (!hasGenerated) //如果原来没有生成，现在生成了，那么累计生成数量+1
                            {
                                Interlocked.Increment(ref generated);
                            }

                            if (currentTry > 0)
                            {
                                item.File.Success($"重试{currentTry}次后成功");
                            }
                            else
                            {
                                item.File.Success();
                            }

                            if (Config.AutoSaveInterval > 0)
                            {
                                if (currentIndex % Config.AutoSaveInterval == 0)
                                {
                                    await SaveTagsAsync();
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            item.File.Cancel();
                            await SaveTagsAsync();
                            throw;
                        }
                        catch (Exception ex)
                        {
                            item.File.Error(ex);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, ct);

                    tasks.Add(task);
                }

                await Task.WhenAll(tasks); // 等待所有已启动的任务完成
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

            string ParseText(JsonNode jNode)
            {
                if (jNode is not JsonValue jValue || !jValue.TryGetValue(out string value))
                {
                    throw new JsonException("AI返回的JSON格式不正确");
                }

                return value;
            }

            var jObj = (JsonObject)JsonNode.Parse(tagResult);
            if (jObj.ContainsKey("objects")
                && jObj.ContainsKey("scene")
                && jObj.ContainsKey("mood")
                && jObj.ContainsKey("colors")
                && jObj.ContainsKey("technique")
                && jObj.ContainsKey("ocr")
                && jObj.ContainsKey("desc"))
            {
                return new PhotoTags(
                    ParseTags(jObj["objects"]),
                    ParseTags(jObj["scene"]),
                    ParseTags(jObj["mood"]),
                    ParseTags(jObj["colors"]),
                    ParseTags(jObj["technique"]),
                    ParseText(jObj["ocr"]),
                    ParseText(jObj["desc"])
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