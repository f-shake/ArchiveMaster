using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchiveMaster.Configs;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;

public abstract class BaseChatClient<TConfig> : IChatClient where TConfig : IAiProvider
{
    protected readonly HttpClient HttpClient;
    protected readonly TConfig Config;
    public string Model { get; }

    protected BaseChatClient(TConfig config, string urlSuffix = "")
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Model = config.Model ?? throw new ArgumentException("未指定模型");

        HttpClient = new HttpClient
        {
            BaseAddress = new Uri(config.Url.TrimEnd('/') + urlSuffix),
            Timeout = TimeSpan.FromHours(1)
        };

        if (config is IOpenAIAiProvider { Key: not null } openAiConfig &&
            !string.IsNullOrEmpty(openAiConfig.Key.Password))
        {
            HttpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openAiConfig.Key.Password);
        }
    }

    public abstract Task<string> GetResponseAsync(IEnumerable<AiChatMessage> messages, ChatOptions options = null,
        CancellationToken cancellationToken = default);

    public abstract IAsyncEnumerable<string> GetStreamingResponseAsync(IEnumerable<AiChatMessage> messages,
        ChatOptions options = null, CancellationToken ct = default);

    protected abstract JsonObject BuildPayload(IEnumerable<AiChatMessage> messages, ChatOptions options, bool isStream);

    protected async IAsyncEnumerable<string> GetStreamingResponseAsync(IEnumerable<AiChatMessage> messages,
        ChatOptions options, string requestUrl,
        Func<string, string> processEachLine,
        [EnumeratorCancellation]
        CancellationToken ct)
    {
        var payload = BuildPayload(messages, options, true);

        // 1. 手动构建 Request 以便控制 CompletionOption
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = JsonContent.Create(payload)
        };
        using var response =
            await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var result = processEachLine(line);
            if (result != null)
            {
                yield return result;
            }
        }
    }

    protected JsonObject CreateBasePayload(IEnumerable<AiChatMessage> messages, bool isStream)
    {
        var messageArray = new JsonArray();

        foreach (var m in messages)
        {
            messageArray.Add(new JsonObject
            {
                ["role"] = m.Sender.ToString().ToLower(),
                ["content"] = m.FullText
            });
        }

        return new JsonObject
        {
            ["model"] = Model,
            ["messages"] = messageArray,
            ["stream"] = isStream
        };
    }

    protected void WriteOptions(JsonObject container, ChatOptions options, string temperatureKey, string topPKey,
        string maxTokensKey)
    {
        ArgumentNullException.ThrowIfNull(container);
        if ((options?.Temperature ?? Config.Temperature) is double t)
        {
            container[temperatureKey] = t;
        }

        if ((options?.TopP ?? Config.TopP) is double p)
        {
            container[topPKey] = p;
        }

        if ((options?.MaxOutputTokens ?? Config.MaxTokens) is int m)
        {
            container[maxTokensKey] = m;
        }
    }

    protected void MergeExtraParams(JsonObject container)
    {
        if (string.IsNullOrWhiteSpace(Config.ExtraParamsJson))
        {
            return;
        }

        try
        {
            var extraNode = JsonNode.Parse(Config.ExtraParamsJson);

            if (extraNode is JsonObject extraObj)
            {
                foreach (var property in extraObj)
                {
                    container[property.Key] = property.Value?.DeepClone();
                }
            }
        }
        catch (JsonException ex)
        {
            throw new Exception($"AI提供者的额外JSON参数解析失败: {ex.Message}");
        }
    }

    public void Dispose() => HttpClient.Dispose();
}