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

    public bool NeedRemoveThink { get; } = true;
    public event GenericEventHandler<LlmOutputItem> AiTextGenerate;
    public string AiResult { get; protected set; }

    public void BindConversation(AiConversation conversation)
    {
        Conversation = conversation;
        Conversation.SendMessageRequested += ConversationOnSendMessageRequested;
    }

    private async void ConversationOnSendMessageRequested(object sender, EventArgs e)
    {
        if (isFirstCall)
        {
            isFirstCall = false;
            (var systemPrompt, var userPrompt) = await GetFirstPromptAsync(CancellationToken.None);
            await this.CallAiWithStreamAsync(systemPrompt, userPrompt, null, NeedRemoveThink);
        }
        else
        {
        }
    }

    private bool isFirstCall = true;

    public void Reset()
    {
        isFirstCall = false;
    }

    public AiConversation Conversation { get; private set; }

    public void OnAiTextGenerate(LlmOutputItem e)
    {
        AiTextGenerate?.Invoke(this, new GenericEventArgs<LlmOutputItem>(e));
    }

    public abstract Task<(string SystemPrompt, string UserPrompt)> GetFirstPromptAsync(CancellationToken ct);


    protected AppConfig AppConfig { get; } = appConfig;
}