using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ArchiveMaster.Attributes;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Events;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.InkML;
using FzLib.Collections;
using Microsoft.Extensions.AI;

namespace ArchiveMaster.Services;

public partial class LineByLineProcessorService(AppConfig appConfig)
    : AiTwoStepServiceBase<LineByLineProcessorConfig>(appConfig)
{
    private Regex indexPrefix = GenerateIndexPrefix();

    //下一步计划：
    //1、示例支持文本框导入
    //2、导出到文件、复制到剪切板
    //3、AI多次检查，如果有错误，则重新调用AI，直到没有错误为止
    //4、AI多次检查，避免单次误判
    public ObservableCollection<LineByLineItem> Items { get; } = new ObservableCollection<LineByLineItem>();
    
    public override async Task ExecuteAsync(CancellationToken ct = default)
    {
        LlmCallerService llm = new LlmCallerService(AI);

        await Task.Run(async () =>
        {
            foreach (var item in Items)
            {
                item.Output = "";
            }

            for (var i = 0; i < Config.Examples.Count; i++)
            {
                Config.Examples[i].Index = i + 1;
            }

            var chunks = Items.Chunk(Config.MaxLineEachCall);
            int processed = 0;

            //使用StringBuilder记录当前行的内容。当遇到换行符或一次回答完毕后，将内容写入Items[processed + index].Output中。
            StringBuilder str = new StringBuilder(1000);
            foreach (var chunk in chunks)
            {
                int index = 0;
                await foreach (var r in llm.CallStreamAsync(GetSystemPrompt(chunk.Length),
                                   string.Join('\n', chunk.Select(x => $"【{x.Index}】{x.Input}")), ct: ct))
                {
                    foreach (var c in r)
                    {
                        if (c == '\n') //AI使用\n作为换行符
                        {
                            CompleteLine();
                            index++;
                        }
                        else
                        {
                            str.Append(c);
                        }
                    }
                }

                CompleteLine();

                if (index != chunk.Length - 1)
                {
                    throw new Exception($"向AI发送了{chunk.Length}行输入，但收到了{index}行输出。请检查AI模型是否正常运行");
                }

                processed += chunk.Length;
                continue;

                void CompleteLine()
                {
                    if (processed + index >= Items.Count)
                    {
                        throw new Exception("AI返回的行数超过了输入的行数。");
                    }

                    Items[processed + index].Output = indexPrefix.Replace(str.ToString(), string.Empty);
                    str.Clear();
                }
            }
        }, ct);
    }

    public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
    {
        return null;
    }

    public override async Task InitializeAsync(CancellationToken ct = default)
    {
        Items.Clear();
        NotifyMessage("正在读取文本源");
        int index = 0;
        await foreach (var line in Config.Source.GetPlainTextAsync(TextSourceReadUnit.PerParagraph, ct))
        {
            if (Config.SkipEmptyLine && string.IsNullOrWhiteSpace(line.Text))
            {
                continue;
            }

            Items.Add(new LineByLineItem
            {
                Input = line.Text,
                Index = ++index
            });
        }
    }


    [GeneratedRegex(@"^【(\d+)】")]
    private static partial Regex GenerateIndexPrefix();

    private string GetSystemPrompt(int count)
    {
        var sb = new StringBuilder();

        sb.AppendLine("你是一名智能文本转换器。请根据“转换要求”判断如何处理每行输入文本，并生成相应输出。");
        sb.AppendLine();
        sb.AppendLine("【转换要求】：");
        sb.AppendLine(Config.Prompt); // 这里的 prompt 是你的 private string prompt;
        sb.AppendLine();
        sb.AppendLine("【示例输入】：");

        foreach (var item in Config.Examples)
        {
            sb.AppendLine($"【{item.Index}】{item.Input}");
        }

        sb.AppendLine("【示例输出】：");

        foreach (var item in Config.Examples)
        {
            sb.AppendLine($"【{item.Index}】{item.Output}");
        }

        if (Config.Examples.Any(p => !string.IsNullOrWhiteSpace(p.Explain)))
        {
            sb.AppendLine("【示例解释】：");
            
            foreach (var item in Config.Examples.Where(p => !string.IsNullOrWhiteSpace(p.Explain)))
            {
                sb.AppendLine($"【{item.Index}】{item.Explain}");
            }
        }

        sb.AppendLine("输出规则：");
        sb.AppendLine("1. 每行输入文本对应一行输出，使用\\n作为换行符。");
        sb.AppendLine("2. 输出仅包含结果，不包含说明或额外解释。");
        sb.AppendLine("3. 保持输出风格与示例一致。");
        //输出类似的内容，AI在数量上容易出错，所以通过添加序号前缀，来避免AI的错误。
        sb.AppendLine("4. 每一行的回答前面，使用【序号】标明序号。");
        sb.AppendLine("5. 最后一行之后不要添加换行符。");
        //sb.AppendLine($"4. 用户将输入{count}条文本，请根据转换要求，生成{count}条输出。");

        return sb.ToString();
    }
}