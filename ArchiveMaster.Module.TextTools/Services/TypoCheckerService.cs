using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using ArchiveMaster.Configs;
using ArchiveMaster.Events;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.InkML;

namespace ArchiveMaster.Services;

public class TypoCheckerService(TypoCheckerConfig config, AppConfig appConfig)
    : AiTwoStepServiceBase<TypoCheckerConfig>(appConfig)
{
    public const string SYSTEM_PROMPT = """
                                        你是一个错别字检查机器人，检查用户输入的语段是否存在错误。需要检查的内容包括：
                                        1.错别字（包括中文和英文等）；
                                        2.标点符号（误用、中文语段用了英文标点、英文语段用了中文标点）。

                                        输出要求：
                                        1. 以JSON数组格式返回结果，每个错误是一个对象，包含以下字段：
                                        {
                                          "context": "错误位置前后5-10字",
                                          "original": "原词",
                                          "corrected": "修正词",
                                          "explanation": "说明"
                                        }
                                        如果该段话中有多个错别字，应当全部进行修正。
                                        2. 不需要对文本进行优化，仅指出明显错误的地方
                                        3. 以下情况豁免：网络用语、标注方言、代码/专有名词

                                        示例输入："新iphone很贵，但 销量很好，她笑的很开心，因为他是销售经历。"
                                        示例输出：
                                        {
                                            "errors":
                                            [
                                              {
                                                "context": "新iphone很贵",
                                                "original": "iphone",
                                                "corrected": "iPhone",
                                                "explanation": "品牌大小写错误"
                                              },
                                              {
                                                "context": "但 销量很好",
                                                "original": "（空格）",
                                                "corrected": "（无空格）",
                                                "explanation": "中文之间不该有空格"
                                              },
                                              {
                                                "context": "她笑的很开心",
                                                "original": "的",
                                                "corrected": "得",
                                                "explanation": "动词在前，应使用'得'"
                                              },
                                              {
                                                "context": "因为他是销售经历",
                                                "original": "他",
                                                "corrected": "她",
                                                "explanation": "前后代词不一致"
                                              },
                                              {
                                                "context": "因为他是销售经历",
                                                "original": "经历",
                                                "corrected": "经理",
                                                "explanation": "错别字"
                                              }
                                            ]
                                        }
                                        有错误时，只输出上文要求的JSON格式的错误内容，不输出其他内容；
                                        无错误时输出一个空的JSON数组，即：{"errors": []}。

                                        请严格按上述JSON格式输出结果，不要包含任何额外的解释或说明。
                                        输出的格式应该严格遵从JSON格式，该转义的地方记得转义。
                                        """;

    public const int MAX_LENGTH = 300_000;

    public TypoCheckerConfig Config { get; } = config;

    public event GenericEventHandler<TypoItem> TypoItemGenerated;

    private void CheckStringLength(StringBuilder str)
    {
        if (str.Length > MAX_LENGTH)
        {
            throw new Exception($"文本长度超过限制（{MAX_LENGTH}）");
        }
    }


    public static List<TypoSegment> SegmentTypos(string rawText, IList<TypoItem> typos)
    {
        //AI的回复中，正确修正后语段，如果一句话中存在多个错误，只会在最后一个修正时全部修正正确。
        var temp = typos;
        typos = new List<TypoItem>();
        foreach (var t in temp)
        {
            if (typos.Count == 0 || typos[^1].Context != t.Context)
            {
                typos.Add(t);
            }
        }

        var segments = new List<TypoSegment>();

        if (string.IsNullOrEmpty(rawText))
        {
            return segments;
        }

        if (typos == null || typos.Count == 0)
        {
            segments.Add(new TypoSegment
            {
                Text = rawText,
                HasTypo = false,
                Typo = null
            });
            return segments;
        }

        // 先按句子长度降序排序，优先处理更长的匹配项
        var sortedTypos = typos
            .Where(t => !string.IsNullOrEmpty(t.Context))
            .OrderByDescending(t => t.Context.Length)
            .ToList();

        // 记录每个字符属于哪个错误句子(null表示不属于任何错误)
        TypoItem[] charTypoMap = new TypoItem[rawText.Length];

        // 遍历所有错误句子
        foreach (var typo in sortedTypos)
        {
            int index = 0;
            while (index < rawText.Length)
            {
                int foundIndex = rawText.IndexOf(typo.Context, index, StringComparison.Ordinal);
                if (foundIndex == -1)
                    break;

                // 检查这个匹配是否完全未被标记
                bool canMark = true;
                for (int i = foundIndex; i < foundIndex + typo.Context.Length; i++)
                {
                    if (charTypoMap[i] != null)
                    {
                        canMark = false;
                        break;
                    }
                }

                if (canMark)
                {
                    // 标记这部分为当前错误
                    for (int i = foundIndex; i < foundIndex + typo.Context.Length; i++)
                    {
                        charTypoMap[i] = typo;
                    }
                }

                index = foundIndex + typo.Context.Length;
            }
        }

        // 现在根据标记构建分段
        int currentPos = 0;
        while (currentPos < rawText.Length)
        {
            // 处理非错误文本
            if (charTypoMap[currentPos] == null)
            {
                int start = currentPos;
                while (currentPos < rawText.Length && charTypoMap[currentPos] == null)
                {
                    currentPos++;
                }

                segments.Add(new TypoSegment
                {
                    Text = rawText.Substring(start, currentPos - start),
                    HasTypo = false,
                    Typo = null
                });
                continue;
            }

            // 处理错误文本
            TypoItem currentTypo = charTypoMap[currentPos];
            int typoStart = currentPos;

            // 找到连续的相同错误类型的文本
            while (currentPos < rawText.Length && charTypoMap[currentPos] == currentTypo)
            {
                currentPos++;
            }

            segments.Add(new TypoSegment
            {
                Text = rawText.Substring(typoStart, currentPos - typoStart),
                HasTypo = true,
                Typo = currentTypo
            });
        }

        return segments;
    }

    public async IAsyncEnumerable<ICheckItem> CheckAsync(string text,
        IProgress<double> progress,
        [EnumeratorCancellation]
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        LlmCallerService llm = new LlmCallerService(AI);

        var segments = SplitText(text, config.MinSegmentLength);
        for (int index = 0; index < segments.Count; index++)
        {
            ct.ThrowIfCancellationRequested();

            string segment = segments[index];
            progress?.Report((double)(0 + index) / segments.Count);

            yield return new PromptItem(segment);

            string result = await llm.CallAsync(SYSTEM_PROMPT, segment, ct);

            yield return new OutputItem(result);
            var lines = result.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            int startLine = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == "</think>")
                {
                    startLine = i + 1;
                    break;
                }
            }

            if (startLine > 0)
            {
                result = string.Join(Environment.NewLine, lines[startLine..]);
            }

            IEnumerable<TypoItem> results = [];
            try
            {
                results = Parse(result).ToList();
            }
            catch (FormatException ex)
            {
                continue;
            }

            foreach (var r in results)
            {
                yield return r;
            }
        }
    }

    private IEnumerable<TypoItem> Parse(string text)
    {
        if (!JsonNode.Parse(text).AsObject().TryGetPropertyValue("errors", out var errors))
        {
            throw new FormatException("输出内容中找不到errors对象");
        }

        if (errors is not JsonArray errorArray)
        {
            throw new FormatException("输出内容中的errors不是数组");
        }

        foreach (var error in errorArray)
        {
            if (error is not JsonObject errorObj)
            {
                throw new FormatException("输出内容中的errors数组元素不是对象");
            }

            if (!errorObj.TryGetPropertyValue("context", out var context) ||
                !errorObj.TryGetPropertyValue("original", out var original) ||
                !errorObj.TryGetPropertyValue("corrected", out var corrected) ||
                !errorObj.TryGetPropertyValue("fixed_segment", out var fixedSegment) ||
                !errorObj.TryGetPropertyValue("explanation", out var explanation))
            {
                throw new FormatException("输出内容中的errors对象缺少必要字段");
            }

            yield return new TypoItem
            {
                Context = context.ToString(),
                Original = original.ToString(),
                Corrected = corrected.ToString(),
                FixedSegment = fixedSegment.ToString(),
                Explanation = explanation.ToString()
            };
        }
    }

    private List<string> SplitText(string text, int minSegmentLength)
    {
        // 定义分隔符（保留原分隔符）
        char[] delimiters = ['。', '？', '！', '\n', '\r'];

        // 使用StringBuilder提高性能
        var finalSegments = new List<string>();
        var currentSegment = new StringBuilder();
        int lastDelimiterIndex = -1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            currentSegment.Append(c);

            // 检查是否是分隔符
            if (delimiters.Contains(c))
            {
                // 如果是换行符，可能需要特殊处理
                if (c == '\n' || c == '\r')
                {
                    // 处理Windows风格的换行(\r\n)
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        currentSegment.Append('\n');
                        i++;
                    }
                }

                string segment = currentSegment.ToString();

                // 如果当前分段足够长，或者下一个字符是文本结束
                if (segment.Length >= minSegmentLength || i == text.Length - 1)
                {
                    finalSegments.Add(segment);
                    currentSegment.Clear();
                    lastDelimiterIndex = -1;
                }
                else
                {
                    lastDelimiterIndex = currentSegment.Length - 1;
                }
            }
            // 检查是否达到最小长度但未找到分隔符
            else if (currentSegment.Length >= minSegmentLength && lastDelimiterIndex >= 0)
            {
                // 在最后一个分隔符处分割
                string segment = currentSegment.ToString(0, lastDelimiterIndex + 1);
                finalSegments.Add(segment);

                // 保留分隔符后的内容
                string remaining = currentSegment.ToString(lastDelimiterIndex + 1,
                    currentSegment.Length - lastDelimiterIndex - 1);
                currentSegment.Clear();
                currentSegment.Append(remaining);
                lastDelimiterIndex = -1;
            }
        }

        // 添加最后剩余的部分
        if (currentSegment.Length > 0)
        {
            finalSegments.Add(currentSegment.ToString());
        }

        // 过滤空段落
        return finalSegments.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    public override async Task ExecuteAsync(CancellationToken ct = default)
    {
        StringBuilder str = new StringBuilder();

        Progress<double> progress = new Progress<double>(NotifyProgress);

        await Task.Run(async () =>
        {
            await foreach (var line in Config.Source.GetPlainTextAsync(true, ct))
            {
                await foreach (var item in CheckAsync(line.Text, progress, ct))
                {
                    switch (item)
                    {
                        case TypoItem t:
                            TypoItemGenerated?.Invoke(this, new GenericEventArgs<TypoItem>(t));
                            break;
                        case OutputItem f:
                            // Outputs.Add(f);
                            break;
                        case PromptItem p:
                            // Prompts.Add(p);
                            break;
                        default:
                            break;
                    }
                }
            }
        }, ct);
    }

    public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
    {
        return null;
    }

    public override Task InitializeAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}