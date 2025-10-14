using System.ClientModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ArchiveMaster.Configs;
using ArchiveMaster.Models;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using ChatResponseFormat = OpenAI.Chat.ChatResponseFormat;

namespace ArchiveMaster.Services;

public class LlmCallerService(AiProviderConfig config)
{
    public AiProviderConfig Config { get; } = config;

    public async Task<string> CallAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var chatClient = GetChatClient();

        var sys = new ChatMessage(ChatRole.System, systemPrompt);
        var user = new ChatMessage(ChatRole.User, userPrompt);
        var response = await chatClient.GetResponseAsync([sys, user], cancellationToken: ct);
        return response.Text;
    }

    public async IAsyncEnumerable<string> CallStreamAsync(string systemPrompt, string userPrompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var chatClient = GetChatClient();

        var sys = new ChatMessage(ChatRole.System, systemPrompt);
        var user = new ChatMessage(ChatRole.User, userPrompt);
        await foreach (ChatResponseUpdate item in
                       chatClient.GetStreamingResponseAsync([sys, user], cancellationToken: ct))
        {
            ct.ThrowIfCancellationRequested();
            yield return item.Text;
        }
    }

    private IChatClient GetChatClient()
    {
        IChatClient chatClient = null;
        switch (Config.Type)
        {
            case AiProviderType.OpenAI:
                chatClient = new OpenAIChatClient(Config.Url, Config.Model, Config.Key);
                break;
            case AiProviderType.Ollama:
                chatClient = new OllamaApiClient(new Uri(Config.Url), Config.Model);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return chatClient;
    }
}