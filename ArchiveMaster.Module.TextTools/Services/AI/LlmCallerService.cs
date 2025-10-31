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
using Serilog;
using ChatResponseFormat = OpenAI.Chat.ChatResponseFormat;

namespace ArchiveMaster.Services;

public class LlmCallerService
{
    public LlmCallerService(AiProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Url))
        {
            throw new ArgumentException($"AI提供商{config.Name}的地址为空");
        }

        if (string.IsNullOrWhiteSpace(config.Model))
        {
            throw new ArgumentException($"AI提供商{config.Name}的模型名为空");
        }

        if (config.Type == AiProviderType.OpenAI &&
            (string.IsNullOrWhiteSpace(config.Key) || string.IsNullOrWhiteSpace(config.Key.Password)))
        {
            throw new ArgumentException($"AI提供商{config.Name}的密钥为空");
        }

        Config = config;
    }

    public AiProviderConfig Config { get; }

    public async Task<string> CallAsync(string systemPrompt, string userPrompt, ChatOptions options = null,
        CancellationToken ct = default)
    {
        LogPrompt(systemPrompt, userPrompt, false);
        var chatClient = GetChatClient();

        var sys = new ChatMessage(ChatRole.System, systemPrompt);
        var user = new ChatMessage(ChatRole.User, userPrompt);
        ChatResponse response = null;
        try
        {
            response = await chatClient.GetResponseAsync([sys, user], options, ct);
        }
        catch (Exception ex)
        {
            throw new Exception($"AI模型调用失败（{ex.Message}）", ex);
        }

        Debug.WriteLine($"AI回答：{response.Text}");
        Log.Logger.Information("AI回答：{ResponseText}", response.Text);
        return response.Text;
    }

    public async IAsyncEnumerable<string> CallStreamAsync(string systemPrompt, string userPrompt,
        ChatOptions options = null,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        LogPrompt(systemPrompt, userPrompt, true);
        var chatClient = GetChatClient();

        var sys = new ChatMessage(ChatRole.System, systemPrompt);
        var user = new ChatMessage(ChatRole.User, userPrompt);
        Debug.WriteLine("AI开始回答");
        StringBuilder str = new StringBuilder("AI回答：");

        IAsyncEnumerable<ChatResponseUpdate> items = null;
        try
        {
            items = chatClient.GetStreamingResponseAsync([sys, user], options, ct);
        }
        catch (Exception ex)
        {
            throw new Exception($"AI模型调用失败（{ex.Message}）", ex);
        }

        await foreach (ChatResponseUpdate item in items)
        {
            Debug.Write(item.Text);
            str.Append(item.Text);
            ct.ThrowIfCancellationRequested();
            yield return item.Text;
        }

        Log.Logger.Information(str.ToString());
    }

    private void LogPrompt(string systemPrompt, string userPrompt, bool stream)
    {
        var str = new StringBuilder().Append("调用AI")
            .AppendLine(stream ? "（流式）" : "")
            .AppendLine("##系统提示##")
            .AppendLine(GetPreAndSuffixes(systemPrompt))
            .AppendLine("##用户提示##")
            .Append(GetPreAndSuffixes(userPrompt))
            .ToString();
        Debug.WriteLine(str);
        Log.Logger.Information(str);

        string GetPreAndSuffixes(string str)
        {
            str = str.Replace('\n', ' ').Replace('\r', ' ');
            if (str.Length < 100)
            {
                return str;
            }

            return $"{str[..50]}...{str[^50..]} (长度为{str.Length})";
        }
    }

    private IChatClient GetChatClient()
    {
        IChatClient chatClient;
        switch (Config.Type)
        {
            case AiProviderType.OpenAI:
                chatClient = new OpenAIChatClient(Config.Url, Config.Model, Config.Key);
                break;
            case AiProviderType.Ollama:
                var httpClient = new HttpClient
                {
                    BaseAddress = new Uri(Config.Url),
                    Timeout = TimeSpan.FromHours(1)
                };
                chatClient = new OllamaApiClient(httpClient, Config.Model);
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