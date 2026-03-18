using System.Text.Json;
using ArchiveMaster.Configs;
using Microsoft.Extensions.AI;

public abstract class BaseChatClient<TConfig> : IChatClient where TConfig : IAiProvider
{
    protected readonly HttpClient HttpClient;
    protected readonly TConfig Config;
    protected readonly string Model;

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

    public abstract Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options = null,
        CancellationToken cancellationToken = default);

    public abstract IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions options = null, CancellationToken cancellationToken = default);

    // 公共的 Payload 基础构建逻辑
    protected Dictionary<string, object> CreateBasePayload(IEnumerable<ChatMessage> messages, bool isStream)
    {
        return new Dictionary<string, object>
        {
            ["model"] = Model,
            ["messages"] = messages.Select(m => new { role = m.Role.Value.ToLower(), content = m.Text }),
            ["stream"] = isStream
        };
    }

    // 公共的 ExtraParams 合并逻辑
    protected void ApplyExtraParams(Dictionary<string, object> rootPayload, Action<string, object> applyAction)
    {
        if (string.IsNullOrWhiteSpace(Config.ExtraParamsJson)) return;
        try
        {
            var extraParams = JsonSerializer.Deserialize<Dictionary<string, object>>(Config.ExtraParamsJson);
            if (extraParams == null) return;
            foreach (var kvp in extraParams)
            {
                applyAction(kvp.Key, kvp.Value);
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"ExtraParamsJson 解析错误: {ex.Message}");
        }
    }

    public object GetService(Type serviceType, object serviceKey = null) => null;
    public void Dispose() => HttpClient.Dispose();
}