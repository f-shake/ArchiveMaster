using ArchiveMaster.Configs;
using ArchiveMaster.Events;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;

namespace ArchiveMaster.Services;

public abstract class AiServiceBase<TConfig>(AppConfig appConfig)
    : ServiceBase<TConfig>(appConfig), IAiService
    where TConfig : ConfigBase
{
    public const int MaxLength = 300_000;
    public event GenericEventHandler<LlmOutputItem> AiTextGenerate;

    public AiProviderConfig AI => GlobalConfigs.Instance.AiProviders.CurrentProvider;
    public ChatOptions ChatOptions { get; } = null;
    public bool NeedRemoveThink { get; } = true;
    protected AppConfig AppConfig { get; } = appConfig;
    public abstract Task<(string SystemPrompt, string UserPrompt)> GetFirstPromptAsync(CancellationToken ct);

    public void OnAiTextGenerate(LlmOutputItem e)
    {
        AiTextGenerate?.Invoke(this, new GenericEventArgs<LlmOutputItem>(e));
    }

    public abstract void Reset();
}