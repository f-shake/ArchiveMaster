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

            var prompt = GetSystemPrompt(Config.Category);
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

    private string GetSystemPrompt(TextGenerationCategory type)
    {
        var prompt =
            "你是一个文本处理机器人。";
        prompt += type switch
        {
            TextGenerationCategory.ExpressionOptimization => GetPrompt(Config.ExpressionOptimizationType),
            TextGenerationCategory.StructuralAdjustment => GetPrompt(Config.StructuralAdjustmentType),
            TextGenerationCategory.ContentTransformation => GetPrompt(Config.ContentTransformationType),
            TextGenerationCategory.TextEvaluation => GetPrompt(Config.TextEvaluationType),
            TextGenerationCategory.Custom => string.IsNullOrWhiteSpace(Config.CustomPrompt)
                ? throw new ArgumentNullException(nameof(Config.CustomPrompt), "请指定自定义提示。")
                : Config.CustomPrompt,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        
        //对翻译类任务单独处理
        if (type == TextGenerationCategory.ContentTransformation
            && Config.ContentTransformationType == ContentTransformationType.Translation)
        {
            prompt += $"请将文本翻译成{Config.TranslationTargetLanguage}。";
        }

        //处理额外提示
        if (type != TextGenerationCategory.Custom && !string.IsNullOrWhiteSpace(Config.ExtraAiPrompt))
        {
            prompt += $"{Environment.NewLine}用户的额外要求：{Config.ExtraAiPrompt}";
        }

        prompt +=
            "要求输出的时候，仅输出结果，不要输出其他内容。" +
            "输出格式上，要完全符合用户输入的语段，不要添加额外的内容，绝对不要输出MarkDown格式（用户输入Markdown除外）。";
        return prompt;
    }


    private static string GetPrompt(Enum type)
    {
        var field = type.GetType().GetField(type.ToString());
        var attr = field?.GetCustomAttributes(typeof(AiPromptAttribute), false);
        return attr is { Length: > 0 }
            ? ((AiPromptAttribute)attr[0]).SystemPrompt
            : throw new ArgumentException("未找到提示");
    }
}