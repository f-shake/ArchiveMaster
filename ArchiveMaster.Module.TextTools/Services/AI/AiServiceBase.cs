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

    protected AppConfig AppConfig { get; } = appConfig;
}