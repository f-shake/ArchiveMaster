using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
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

public class TextRewriterService(AppConfig appConfig)
    : AiTwoStepServiceBase<TextRewriterConfig>(appConfig)
{
    public const int MaxRefLength = 10_000;

    public event GenericEventHandler<LlmOutputItem> TextGenerated;

    public override async Task ExecuteAsync(CancellationToken ct = default)
    {
        StringBuilder str = new StringBuilder();

        await Task.Run(async () =>
        {
            NotifyMessage("正在读取文本源");
            string text = (await Config.Source.GetPlainTextAsync(TextSourceReadUnit.Combined, ct)
                .FirstOrDefaultAsync()).Text;
            CheckTextSource(text, MaxLength, "文本源");

            NotifyMessage("正在调用AI进行处理");

            var prompt = await GetSystemPromptAsync(Config.Category, ct);
            var ai = new LlmCallerService(AI);
            await foreach (var output in ai.CallStreamAsync(prompt, text, ct: ct))
            {
                TextGenerated?.Invoke(this, new GenericEventArgs<LlmOutputItem>(output));
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

    private async Task<string> GetSystemPromptAsync(TextGenerationCategory type, CancellationToken ct)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("你是一个文本处理机器人。");
        (var attr, var e) = Config.GetCurrentAgent();

        var tempPrompt = attr.SystemPrompt;

        //处理参考文本（如仿写中的参考文本）
        if (attr.NeedReferenceText)
        {
            string referenceText = (await Config.ReferenceSource.GetPlainTextAsync(TextSourceReadUnit.Combined, ct)
                .FirstOrDefaultAsync()).Text;
            CheckTextSource(referenceText, MaxRefLength, "参考文本");
            tempPrompt = tempPrompt.Replace(AiAgentAttribute.ReferenceTextPlaceholder, referenceText);
        }

        //处理额外信息（如翻译中的目标语言）
        if (attr.NeedExtraInformation)
        {
            if (string.IsNullOrWhiteSpace(Config.ExtraInformation))
            {
                throw new ArgumentNullException(nameof(Config.ExtraInformation), $"请指定{attr.ExtraInformationLabel}");
            }

            tempPrompt = tempPrompt.Replace(AiAgentAttribute.ExtraInformationPlaceholder, Config.ExtraInformation);
        }

        prompt.AppendLine(tempPrompt);

        //处理额外提示
        if (type != TextGenerationCategory.Custom && !string.IsNullOrWhiteSpace(Config.ExtraAiPrompt))
        {
            prompt.AppendLine($"用户的额外要求：{Config.ExtraAiPrompt}");
        }

        //增加其他要求
        prompt.AppendLine("要求输出的时候，仅输出结果，不要输出其他内容。");
        prompt.AppendLine("输出格式上，要完全符合用户输入的语段，不要添加额外的内容，绝对不要输出MarkDown格式（用户输入Markdown除外）。");
        return prompt.ToString();
    }
}