using ArchiveMaster.Configs;
using ArchiveMaster.Events;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using Microsoft.Extensions.AI;

namespace ArchiveMaster.Services;

public abstract class AiServiceBase<TConfig>(AppConfig appConfig)
    : ServiceBase<TConfig>(appConfig), IAiService
    where TConfig : ConfigBase
{
    public const int MaxLength = 300_000;
    protected ChatOptions ChatOptions { get; } = null;

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

            Conversation.AddSystemMessage(systemPrompt).Freeze(true);
            Conversation.AddUserMessage(userPrompt).Freeze(true);
            await CallAiWithStreamAsync(Conversation.GetChatMessages(), NeedRemoveThink);
        }
        else
        {
            Conversation.AddUserMessage(Conversation.InputText).Freeze(true);
            await CallAiWithStreamAsync(Conversation.GetChatMessages(), NeedRemoveThink);
        }
    }


    public async Task<string> CallAiWithStreamAsync(IEnumerable<ChatMessage> messages, bool removeThink,
        CancellationToken ct = default)
    {
        AiChatMessage assistantMessage = null;
        if (Conversation != null)
        {
            assistantMessage = Conversation.AddAssistantMessage();
        }

        LlmCallerService s = new LlmCallerService(AI);
        var result = await s.CallWithStreamAsync(messages, ChatOptions, (_, e) =>
        {
            OnAiTextGenerate(e.Value);
            assistantMessage?.AddInline(e.Value);
        }, ct);
        assistantMessage?.Freeze(false);

        if (removeThink)
        {
            result = LlmCallerService.RemoveThink(result);
            Conversation?.LastAssistantMessage.ReplaceWithFinalResponse(result);
        }

        Conversation?.EndResponse();

        return result;
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