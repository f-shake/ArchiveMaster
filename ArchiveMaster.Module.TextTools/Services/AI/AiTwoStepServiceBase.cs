using ArchiveMaster.Configs;
using ArchiveMaster.Events;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using Microsoft.Extensions.AI;

namespace ArchiveMaster.Services;

public abstract class AiTwoStepServiceBase<TConfig>(AppConfig appConfig)
    : TwoStepServiceBase<TConfig>(appConfig)
    where TConfig : ConfigBase
{
    public const int MaxLength = 300_000;

    public AiProviderConfig AI => AppConfig.GetOrCreateConfigWithDefaultKey<AiProvidersConfig>().CurrentProvider;

    public event GenericEventHandler<LlmOutputItem> AiTextGenerate;

    protected AppConfig AppConfig { get; } = appConfig;

    protected async Task<string> CallAiWithStreamAsync(string systemPrompt, string userPrompt, ChatOptions options,
        bool removeThink, CancellationToken ct = default)
    {
        LlmCallerService s = new LlmCallerService(AI);
        var result = await s.CallWithStreamAsync(systemPrompt, userPrompt, options, AiTextGenerate, ct);
        if (removeThink)
        {
            result = LlmCallerService.RemoveThink(result);
        }

        return result;
    }

    protected void CheckTextSource(string text, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"{name}为空");
        }

        if (text.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(TextSource), $"{name}长度超过限制（{maxLength}），请缩减文本源长度。");
        }
    }
}