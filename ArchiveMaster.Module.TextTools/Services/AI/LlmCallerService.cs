using System.ClientModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    public async Task<string> CallAsync(string systemPrompt, string userPrompt, ChatOptions options = null,
        CancellationToken ct = default)
    {
        var chatClient = GetChatClient();

        var sys = new ChatMessage(ChatRole.System, systemPrompt);
        var user = new ChatMessage(ChatRole.User, userPrompt);
        var response = await chatClient.GetResponseAsync([sys, user], options, ct);
        return response.Text;
    }

    public async IAsyncEnumerable<string> CallStreamAsync(string systemPrompt, string userPrompt,
        ChatOptions options = null,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        Debug.WriteLine(
            $"调用AI，系统提示：{systemPrompt}，用户提示：{(userPrompt.Length > 100 ? userPrompt[..100] + "..." : userPrompt)}");
        var chatClient = GetChatClient();

        var sys = new ChatMessage(ChatRole.System, systemPrompt);
        var user = new ChatMessage(ChatRole.User, userPrompt);
        await foreach (ChatResponseUpdate item in
                       chatClient.GetStreamingResponseAsync([sys, user], options, ct))
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

    public static string RemoveThink(string text)
    {
        return Regex.Replace(text, @"^\s*<Think>.*?</Think>\s*$\r?\n?", string.Empty,
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
    }
}