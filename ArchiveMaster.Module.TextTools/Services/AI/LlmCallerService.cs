using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Events;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using Serilog;

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

        Config = config;
    }

    public AiProviderConfig Config { get; }

    public async Task<string> CallAsync(string systemPrompt, string userPrompt, ChatOptions options = null,
        CancellationToken ct = default)
    {
        var sys = AiChatMessage.CreateSystemMessage(systemPrompt, false, 0);
        var user = AiChatMessage.CreateUserMessage(userPrompt, false, 0);
        return await GetResponseAsync([sys, user], options, ct);
    }

    private async Task<string> GetResponseAsync(IEnumerable<AiChatMessage> messages, ChatOptions options = null,
        CancellationToken ct = default)
    {
        var chatClient = GetChatClient();
        string response;
        try
        {
            response = await chatClient.GetResponseAsync(messages, options, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Exception($"AI模型调用失败（{ex.Message}）", ex);
        }

        Debug.WriteLine($"AI回答：{response}");
        Log.Logger.Information("AI回答：{ResponseText}", response);
        Debug.WriteLine("AI调用完成");
        return response;
    }

    public async Task<string> CallWithStreamAsync(string systemPrompt, string userPrompt,
        ChatOptions options = null,
        GenericEventHandler<LlmOutputItem> onStreamUpdate = null,
        CancellationToken ct = default)
    {
        List<string> result = new List<string>();
        await foreach (var part in CallStreamAsync(systemPrompt, userPrompt, options, ct))
        {
            onStreamUpdate?.Invoke(this, new GenericEventArgs<LlmOutputItem>(part));
            result.Add(part);
        }

        return string.Concat(result);
    }

    public async Task<string> CallWithStreamAsync(IEnumerable<AiChatMessage> messages,
        ChatOptions options = null,
        GenericEventHandler<LlmOutputItem> onStreamUpdate = null,
        bool throwOnCancel = false,
        CancellationToken ct = default)
    {
        List<string> result = new List<string>();
        try
        {
            await foreach (var part in CallStreamAsync(messages, options, ct))
            {
                onStreamUpdate?.Invoke(this, new GenericEventArgs<LlmOutputItem>(part));
                result.Add(part);
            }
        }
        catch (OperationCanceledException)
        {
            if (throwOnCancel)
            {
                throw;
            }
        }

        return string.Concat(result);
    }

    public async IAsyncEnumerable<string> CallStreamAsync(string systemPrompt, string userPrompt,
        ChatOptions options = null,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        // LogPrompt(systemPrompt, userPrompt, true);
        var sys = AiChatMessage.CreateSystemMessage(systemPrompt, false, 0);
        var user = AiChatMessage.CreateUserMessage(userPrompt, false, 0);
        await foreach (var p in GetStreamResponseAsync([sys, user], options, ct))
        {
            yield return p;
        }
    }


    public async IAsyncEnumerable<string> CallStreamAsync(IEnumerable<AiChatMessage> messages,
        ChatOptions options = null,
        CancellationToken ct = default)
    {
        await foreach (var p in GetStreamResponseAsync(messages, options, ct))
        {
            yield return p;
        }
    }

    private async IAsyncEnumerable<string> GetStreamResponseAsync(IEnumerable<AiChatMessage> messages,
        ChatOptions options = null, CancellationToken ct = default)
    {
        var chatClient = GetChatClient();

        Debug.WriteLine("AI开始回答");
        StringBuilder str = new StringBuilder("AI回答：");

        IAsyncEnumerable<string> items = null;
        try
        {
            items = chatClient.GetStreamingResponseAsync(messages, options, ct);
        }
        catch (Exception ex)
        {
            throw new Exception($"AI模型调用失败（{ex.Message}）", ex);
        }

        await foreach (string item in items)
        {
            Debug.Write(item);
            str.Append(item);
            ct.ThrowIfCancellationRequested();
            yield return item;
        }

        Log.Logger.Information(str.ToString());
        Debug.WriteLine("AI流式调用完成");
    }

    // private void LogPrompt(string systemPrompt, string userPrompt, bool stream)
    // {
    //     var str = new StringBuilder().Append("调用AI")
    //         .AppendLine(stream ? "（流式）" : "")
    //         .AppendLine("##系统提示##")
    //         .AppendLine(GetPreAndSuffixes(systemPrompt))
    //         .AppendLine("##用户提示##")
    //         .Append(GetPreAndSuffixes(userPrompt))
    //         .ToString();
    //     Debug.WriteLine(str);
    //     Log.Logger.Information(str);
    //
    //     string GetPreAndSuffixes(string str)
    //     {
    //         str = str.Replace('\n', ' ').Replace('\r', ' ');
    //         if (str.Length < 100)
    //         {
    //             return str;
    //         }
    //
    //         return $"{str[..50]}...{str[^50..]} (长度为{str.Length})";
    //     }
    // }

    private IChatClient GetChatClient()
    {
        IChatClient chatClient = Config.Type switch
        {
            AiProviderType.OpenAI => new OpenAICompatibleChatClient(Config),
            AiProviderType.Ollama => new OllamaChatClient(Config),
            // AiProviderType.OpenAICompatible => new OpenAICompatibleChatClient(Config),
            _ => throw new ArgumentOutOfRangeException()
        };

        return chatClient;
    }
}