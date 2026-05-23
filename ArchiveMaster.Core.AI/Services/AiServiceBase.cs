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
    public abstract bool ProvideFirstUserPrompt { get; }

    protected AppConfig AppConfig { get; } = appConfig;

    public abstract ValueTask<string> GetFirstUserPromptAsync(CancellationToken ct);

    public abstract ValueTask<string> GetSystemPromptAsync(CancellationToken ct);
    public void OnAiTextGenerate(LlmOutputItem e)
    {
        AiTextGenerate?.Invoke(this, new GenericEventArgs<LlmOutputItem>(e));
    }
    public virtual string PostProcessLine(string text)
    {
        return text;
    }

    public abstract void Reset();
}