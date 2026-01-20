using ArchiveMaster.ViewModels;
using Microsoft.Extensions.AI;

namespace ArchiveMaster.Services;

public static class AiServiceExtensions
{
    extension(IAiService service)
    {
        [Obsolete]
        public async Task<string> CallAiWithStreamAsync(string systemPrompt,
            string userPrompt, ChatOptions options,
            bool removeThink, CancellationToken ct = default)
        {
            AiChatMessage assistantMessage = null;
            if (service.Conversation != null)
            {
                service.Conversation.AddSystemMessage(systemPrompt).Freeze(true);
                service.Conversation.AddUserMessage(userPrompt).Freeze(true);
                assistantMessage = service.Conversation.AddAssistantMessage();
            }

            LlmCallerService s = new LlmCallerService(service.AI);
            var result = await s.CallWithStreamAsync(systemPrompt, userPrompt, options, (_, e) =>
            {
                service.OnAiTextGenerate(e.Value);
                assistantMessage?.AddInline(e.Value);
            }, ct);
            assistantMessage?.Freeze(false);
            
            if (removeThink)
            {
                result = LlmCallerService.RemoveThink(result);
                service.Conversation?.LastAssistantMessage.ReplaceWithFinalResponse(result);
            }

            service.Conversation?.EndResponse();

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