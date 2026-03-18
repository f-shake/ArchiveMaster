using ArchiveMaster.Configs;
using ArchiveMaster.Events;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using Avalonia.Media;

namespace ArchiveMaster.Services;

public abstract class AiTwoStepServiceBase<TConfig>(AppConfig appConfig)
    : TwoStepServiceBase<TConfig>(appConfig), IAiService
    where TConfig : ConfigBase
{
    public const int MaxLength = 300_000;

    public event GenericEventHandler<LlmOutputItem> AiTextGenerate;

    public AiProviderConfig AI => AppConfig.GetOrCreateConfigWithDefaultKey<AiProvidersConfig>().CurrentProvider;

    public ChatOptions ChatOptions { get; }


    public bool NeedRemoveThink { get; }

    protected AppConfig AppConfig { get; } = appConfig;


    public Task<(string SystemPrompt, string UserPrompt)> GetFirstPromptAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public void OnAiTextGenerate(LlmOutputItem e)
    {
        AiTextGenerate?.Invoke(this, new GenericEventArgs<LlmOutputItem>(e));
    }
}