using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Events;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.InkML;
using Microsoft.Extensions.AI;

namespace ArchiveMaster.Services;

public class TextRewriterService(AppConfig appConfig)
    : AiTwoStepServiceBase<TextRewriterConfig>(appConfig)
{
    public const int MAX_LENGTH = 300_000;

    public event GenericEventHandler<LlmOutputItem> TextGenerated;

    public override async Task ExecuteAsync(CancellationToken ct = default)
    {
        StringBuilder str = new StringBuilder();

        await Task.Run(async () =>
        {
            string text = null;
            NotifyMessage("正在读取文本源");
            await foreach (var part in Config.Source.GetPlainTextAsync(TextSourceReadUnit.Combined, ct))
            {
                //肯定只有一个
                text = part.Text;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("文本源为空");
            }

            NotifyMessage("正在调用AI进行处理");

            var prompt = GetSystemPrompt(Config.Type);
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

    private string GetSystemPrompt(TextRewriterType type)
    {
        var prompt =
            "你是一个文本处理机器人。你需要根据以下要求，对用户输入的语段进行修改。" +
            "要求输出的时候，仅输出修改后的文本，不要输出其他内容。" +
            "输出格式上，要完全符合用户输入的语段，不要添加额外的内容，绝对不要输出MarkDown格式（用户输入Markdown除外）。" +
            "以下是具体要求：";
        prompt += type switch
        {
            TextRewriterType.Refinement => "请优化语言表达，使文本更流畅和优美。",
            TextRewriterType.Simplification => "请简化文本，保留基本意思，缩短语句。",
            TextRewriterType.Elongation => "请扩展文本，增加细节和描述，丰富内容。",
            TextRewriterType.Formalization => "请将文本转化为更正式的表达方式。",
            TextRewriterType.Casualization => "请将文本转化为更口语化的表达方式。",
            TextRewriterType.Reconstruction => "请用不同的方式表达同样的意思。",
            TextRewriterType.Translation => string.IsNullOrWhiteSpace(Config.TranslationTargetLanguage)
                ? throw new ArgumentNullException(nameof(Config.TranslationTargetLanguage), "请指定翻译目标语言。")
                : $"请将文本翻译成{Config.TranslationTargetLanguage}。",
            TextRewriterType.Summary => "请对文本进行摘要，形成一段连续完整的话，保留原文的主要意思。",
            TextRewriterType.Custom => string.IsNullOrWhiteSpace(Config.CustomPrompt)
                ? throw new ArgumentNullException(nameof(Config.CustomPrompt), "请指定自定义提示。")
                : Config.CustomPrompt,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        if (type != TextRewriterType.Custom && !string.IsNullOrWhiteSpace(Config.ExtraAiPrompt))
        {
            prompt += $"{Environment.NewLine}用户的额外要求：{Config.ExtraAiPrompt}";
        }

        return prompt;
    }
}