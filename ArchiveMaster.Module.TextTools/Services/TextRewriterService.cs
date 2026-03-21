using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using ArchiveMaster.AiAgents;
using ArchiveMaster.Attributes;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using FzLib.Collections;

namespace ArchiveMaster.Services;

public class TextRewriterService(AppConfig appConfig)
    : AiServiceBase<TextRewriterConfig>(appConfig)
{
    public const int MaxRefLength = 10_000;

    public AiAgentBase AiAgent { get; set; }


    public override async Task<(string SystemPrompt, string UserPrompt)> GetFirstPromptAsync(CancellationToken ct)
    {
        if (AiAgent == null)
        {
            throw new InvalidOperationException("请设置AI智能体");
        }

        StringBuilder str = new StringBuilder();

        NotifyMessage("正在读取文本源");
        string text = (await Config.Source.GetPlainTextAsync(TextSourceReadUnit.Combined, ct)
            .FirstOrDefaultAsync()).Text;
        this.CheckTextSource(text, MaxLength, "文本源");

        NotifyMessage("正在调用AI进行处理");

        var prompt = await GetSystemPromptAsync(ct);
        return (prompt, text);
    }

    public override void Reset()
    {
    }

    private async Task<string> GetSystemPromptAsync(CancellationToken ct)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("你是一个文本处理机器人。");
        prompt.AppendLine(await AiAgent.BuildSystemPromptAsync(ct));


        //处理额外提示
        if (AiAgent.CanUserSetExtraPrompt && !string.IsNullOrWhiteSpace(AiAgent.ExtraPrompt))
        {
            prompt.AppendLine($"额外要求：{AiAgent.ExtraPrompt}");
        }

        //增加其他要求
        prompt.AppendLine("要求输出的时候，仅输出结果，不要输出其他内容。");
        prompt.AppendLine("输出格式上，要完全符合用户输入的语段，不要添加额外的内容。" +
                          "若有必要输出MarkDown，样式应当简单，不要输出表格。");
        return prompt.ToString();
    }
}