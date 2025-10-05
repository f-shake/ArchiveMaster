using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using ChatResponseFormat = OpenAI.Chat.ChatResponseFormat;
using OChatMessage = OpenAI.Chat.ChatMessage;
using MChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatMessage = object;

namespace ArchiveMaster.Services;

public class OpenAIChatClient : IChatClient
{
    private readonly OpenAIClient client;
    private readonly string model;

    public OpenAIChatClient(string endpoint, string model, string apiKey)
    {
        this.model = model ?? throw new ArgumentNullException(nameof(model));
        if (string.IsNullOrEmpty(apiKey)) throw new ArgumentNullException(nameof(apiKey));

        client = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint)
            });
    }

    private (IEnumerable<OChatMessage>, ChatCompletionOptions) GetMessagesAndOptions(IEnumerable<MChatMessage> messages,
        ChatOptions options)
    {
        var oMessages = new List<OChatMessage>();
        foreach (var message in messages)
        {
            if (message.Role.Value == ChatRole.System.Value)
            {
                oMessages.Add(OChatMessage.CreateSystemMessage(message.Text));
            }
            else if (message.Role.Value == ChatRole.User.Value)
            {
                oMessages.Add(OChatMessage.CreateUserMessage(message.Text));
            }
            else if (message.Role.Value == ChatRole.Assistant.Value)
            {
                oMessages.Add(OChatMessage.CreateAssistantMessage(message.Text));
            }
            else if (message.Role.Value == ChatRole.Tool.Value)
            {
                oMessages.Add(OChatMessage.CreateToolMessage(message.Text));
            }
            else
            {
                throw new NotSupportedException("不支持的角色类型: " + message.Role.Value);
            }
        }

        var cco = new ChatCompletionOptions()
        {
        };

        return (oMessages, cco);
    }

    /// <summary>一次性返回完整响应</summary>
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<MChatMessage> messages,
        ChatOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var (oMessages, cco) = GetMessagesAndOptions(messages, options);

        var result = await client.GetChatClient(model).CompleteChatAsync(oMessages, cco, cancellationToken);

        string fullText = string.Join(Environment.NewLine, result.Value.Content.Select(p => p.Text));

        return new ChatResponse([new MChatMessage(ChatRole.Assistant, fullText)]);
    }

    /// <summary>流式返回响应</summary>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<MChatMessage> messages,
        ChatOptions options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var (oMessages, cco) = GetMessagesAndOptions(messages, options);

        await foreach (var chunk in client.GetChatClient(model)
                           .CompleteChatStreamingAsync(oMessages, cco, cancellationToken))
        {
            var text = string.Concat(chunk.ContentUpdate.Select(p => p.Text));
            yield return new ChatResponseUpdate(ChatRole.Assistant, text);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}