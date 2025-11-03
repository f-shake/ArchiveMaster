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
using Microsoft.VisualBasic.FileIO;

namespace ArchiveMaster.Services;

public partial class LineByLineProcessorService(AppConfig appConfig)
    : AiTwoStepServiceBase<LineByLineProcessorConfig>(appConfig)
{
    private Regex indexPrefix = GenerateIndexPrefix();

    public ObservableCollection<LineByLineItem> Items { get; } = new ObservableCollection<LineByLineItem>();

    public override async Task ExecuteAsync(CancellationToken ct = default)
    {
        LlmCallerService llm = new LlmCallerService(AI);

        await Task.Run(async () =>
        {
            //输出置空，投票数组初始化
            foreach (var item in Items)
            {
                item.Initialize(Config.EnableMajorityVote ? Config.VoteCount : 0);
            }

            //给每个示例添加序号
            for (var i = 0; i < Config.Examples.Count; i++)
            {
                Config.Examples[i].Index = i + 1;
            }

            var chunks = Items.Chunk(Config.MaxLineEachCall); //分组
            int processed = 0; //已经处理的行数（截止上一个chunk）
            int chunkIndex = 0; //当前chunk的序号
            int chunkCount = (Items.Count + Config.MaxLineEachCall - 1) / Config.MaxLineEachCall; //总chunk数

            foreach (var chunk in chunks)
            {
                chunkIndex++;

                //设置投票索引数组，如果启用投票，则为0到VoteCount-1，否则为-1
                var voteIndexes = Config.EnableMajorityVote ? Enumerable.Range(0, Config.VoteCount) : [-1];

                //循环每一轮投票
                foreach (var voteIndex in voteIndexes)
                {
                    NotifyCurrentMessage(chunkIndex, chunkCount, voteIndex);

                    //重试机制
                    int retryCount = Config.EnableRetry ? Config.MaxRetryCount : 1;
                    while (retryCount > 0)
                    {
                        try
                        {
                            await ProcessSingleChunkAsync(llm, chunk, processed, voteIndex, ct);
                            break;
                        }
                        catch (AiUnexpectedFormatException ex)
                        {
                            retryCount--;
                            if (retryCount <= 0)
                            {
                                throw;
                            }
                        }
                    }
                }

                //每一次投票结束（每一个chunk的末尾），如果启用投票，则处理投票结果
                if (Config.EnableMajorityVote)
                {
                    foreach (var item in chunk)
                    {
                        ProcessVoteResult(item);
                    }
                }

                processed += chunk.Length;
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

    private void NotifyCurrentMessage(int chunkIndex, int chunkCount, int voteIndex)
    {
        var strMessage = new StringBuilder()
            .Append("正在处理第")
            .Append(chunkIndex)
            .Append("个分块，共")
            .Append(chunkCount)
            .Append("个分块");
        if (Config.EnableMajorityVote)
        {
            strMessage.Append("，正在进行第")
                .Append(voteIndex + 1)
                .Append("次投票");
        }

        NotifyMessage(strMessage.ToString());
    }

    private void NotifyCurrentProgress(int processed, int voteIndex, int indexInChunk, int countOfChunk)
    {
        int total = Config.EnableMajorityVote ? Config.VoteCount * Items.Count : Items.Count;
        int current = Config.EnableMajorityVote
            ? Config.VoteCount * processed + voteIndex * countOfChunk + indexInChunk + 1
            : processed + indexInChunk + 1;
        Debug.WriteLine($"{current}/{total}");
        NotifyProgress(current, total);
    }

    private async Task ProcessSingleChunkAsync(LlmCallerService llm, LineByLineItem[] chunk, int processed,
        int voteIndex, CancellationToken ct)
    {
        //使用StringBuilder记录当前行的内容。当遇到换行符或一次回答完毕后，将内容写入Items[processed + index].Output中。
        StringBuilder str = new StringBuilder(1000);
        int index = 0;

        //调用AI
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
            throw new AiUnexpectedFormatException(
                $"向AI发送了{chunk.Length}行输入，但收到了{index}行输出。请检查AI模型是否正常运行");
        }

        void CompleteLine()
        {
            if (processed + index >= Items.Count)
            {
                throw new AiUnexpectedFormatException($"AI返回的总行数（{processed + index}）超过了输入的行数。");
            }

            var result = indexPrefix.Replace(str.ToString(), string.Empty);
            var item = Items[processed + index];

            //如果不启用投票，则直接将结果写入Output中，否则写入EachVote中
            if (voteIndex == -1)
            {
                item.Output = result;
            }
            else
            {
                item.EachVote[voteIndex] = result;
            }

            str.Clear();

            NotifyCurrentProgress(processed, voteIndex, index, chunk.Length);
        }
    }

    private void ProcessVoteResult(LineByLineItem item)
    {
        //按结果进行分组，按票数排序
        var groups = item.EachVote
            .GroupBy(p => p)
            .Select(p => new
            {
                p.Key,
                Count = p.Count()
            }).OrderByDescending(p => p.Count)
            .ToList();

        //将票数最多的结果写入Output中
        item.Output = groups[0].Key;
        if (groups.Count <= 1)
        {
            return;
        }

        //存在多个结果时，将结果写入Message中
        StringBuilder str = new StringBuilder();
        str.Append(Config.VoteCount)
            .Append("次投票存在")
            .Append(groups.Count)
            .Append("个结果：");
        foreach (var group in groups)
        {
            str.Append('“')
                .Append(group.Key)
                .Append('”')
                .Append(group.Count)
                .Append("次，");
        }

        str.Remove(str.Length - 1, 1);
        item.Message = str.ToString();
        item.VoteResultNotInconsistent = true;
    }
}