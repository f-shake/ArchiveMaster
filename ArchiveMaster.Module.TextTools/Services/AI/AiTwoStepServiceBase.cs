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

    public AiProviderConfig AI => AppConfig.GetOrCreateConfigWithDefaultKey<AiProvidersConfig>().CurrentProvider;

    public event GenericEventHandler<LlmOutputItem> AiTextGenerate;
    public string AiResult { get; protected set; }

    public void BindConversation(AiConversation conversation)
    {
        Conversation = conversation;
    }

    public AiConversation Conversation { get; private set; }

    public void OnAiTextGenerate(LlmOutputItem e)
    {
        AiTextGenerate?.Invoke(this, new GenericEventArgs<LlmOutputItem>(e));
    }

    public Task<(string SystemPrompt, string UserPrompt)> GetFirstPromptAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }


    protected AppConfig AppConfig { get; } = appConfig;
}