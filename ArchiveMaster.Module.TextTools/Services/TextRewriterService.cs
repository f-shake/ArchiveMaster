using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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


    public override bool ProvideFirstUserPrompt { get; } = false;

    public override ValueTask<string> GetFirstUserPromptAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public override async ValueTask<string> GetSystemPromptAsync(CancellationToken ct)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("你是一个文本处理机器人。以下是具体要求：");
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

    public override string PostProcessLine(string text)
    {
        text = base.PostProcessLine(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        // 处理中英数之间的多余空格
        // 匹配：汉字 + 一个或多个空格 + 英文字母/数字
        text = Regex.Replace(text, @"([\u4e00-\u9fa5]+)\s+([a-zA-Z0-9]+)", "$1$2");
        // 匹配：英文字母/数字 + 一个或多个空格 + 汉字
        text = Regex.Replace(text, @"([a-zA-Z0-9]+)\s+([\u4e00-\u9fa5]+)", "$1$2");

        // 清理可能因为大模型抽风产生的首尾空行
        text = text.Trim('\r', '\n'); //.Replace("\r\n\r\n", "\r\n").Replace("\n\n", "\n");

        return text;
    }

    public override void Reset()
    {
    }
}