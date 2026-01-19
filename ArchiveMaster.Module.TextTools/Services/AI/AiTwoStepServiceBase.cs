using ArchiveMaster.Configs;
using ArchiveMaster.Events;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using Avalonia.Media;
using Microsoft.Extensions.AI;

namespace ArchiveMaster.Services;

public static class AiServiceExtensions
{
    extension(IAiService service)
    {
        public async Task<string> CallAiWithStreamAsync(string systemPrompt,
            string userPrompt, ChatOptions options,
            bool removeThink, CancellationToken ct = default)
        {
            if (service.Conversation != null)
            {
                service.Conversation.AddSystemMessage(systemPrompt).Freeze(true);
                service.Conversation.AddUserMessage(userPrompt).Freeze(true);
                service.Conversation.AddAssistantMessage();
            }

            LlmCallerService s = new LlmCallerService(service.AI);
            var result = await s.CallWithStreamAsync(systemPrompt, userPrompt, options, (s, e) =>
            {
                service.OnAiTextGenerate(e.Value);
                service.Conversation?.LastAssistantMessage.AddInline(e.Value);
            }, ct);
            if (removeThink)
            {
                result = LlmCallerService.RemoveThink(result);
                if (service.Conversation != null)
                {
                    service.Conversation.LastAssistantMessage.ReplaceWithFinalResponse(result);
                }
            }

            if (service.Conversation != null)
            {
                service.Conversation.CanUserInput = true;
            }

            return result;
        }

        public void CheckTextSource(string text, int maxLength, string name)
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
}

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

    protected AppConfig AppConfig { get; } = appConfig;
}