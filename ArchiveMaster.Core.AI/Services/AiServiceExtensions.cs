using ArchiveMaster.ViewModels;

namespace ArchiveMaster.Services;

public static class AiServiceExtensions
{
    extension(IAiService service)
    {
        public async Task<string> CallAiWithStreamAsync(IEnumerable<AiChatMessage> messages,
            AiAssistantChatMessage assistantMessage, CancellationToken ct = default)
        {
            LlmCallerService s = new LlmCallerService(service.AI);
            try
            {
                string result = await s.CallWithStreamAsync(messages, service.ChatOptions, (_, e) =>
                {
                    service.OnAiTextGenerate(e.Value);
                    assistantMessage?.Append(e.Value);
                }, ct: ct);
                return result;
            }
            finally
            {
                assistantMessage?.Complete();
            }
        }

        public void CheckTextSource(string text, int maxLength, string name)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException($"{name}为空");
            }

            if (text.Length > maxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(text), $"{name}长度超过限制（{maxLength}），请缩减文本源长度。");
            }
        }
    }
}