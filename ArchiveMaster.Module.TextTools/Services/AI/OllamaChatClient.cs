using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ArchiveMaster.Configs;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;

namespace ArchiveMaster.Services;

public class OllamaChatClient(IOllamaAiProvider config) : BaseChatClient<IOllamaAiProvider>(config, "/")
{
    public override async Task<string> GetResponseAsync(IEnumerable<AiChatMessage> messages, ChatOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(messages, options, false);
        var response = await HttpClient.PostAsJsonAsync("api/chat", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OllamaFullResponse>(cancellationToken);
        return result?.Message?.Content ?? string.Empty;
    }

    public override IAsyncEnumerable<string> GetStreamingResponseAsync(
        IEnumerable<AiChatMessage> messages,
        ChatOptions options = null,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        return GetStreamingResponseAsync(messages, options, "api/chat",
            line =>
            {
                string result = null;
                try
                {
                    var chunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line);
                    if (chunk?.Message?.Content != null)
                    {
                        return chunk.Message.Content;
                    }

                    if (chunk?.Done == true)
                    {
                        return null;
                    }
                }
                catch (JsonException)
                {
                    return null;
                }

                return null;
            }, ct);
    }


    protected override JsonObject BuildPayload(IEnumerable<AiChatMessage> messages, ChatOptions options, bool isStream)
    {
        // 假设 CreateBasePayload 也修改为返回 JsonObject
        var root = CreateBasePayload(messages, isStream);
        if (options?.OutputJson == true)
        {
            root["format"] = "json";
        }

        var ollamaOptions = new JsonObject();

        // 使用模式匹配简化赋值逻辑
        if ((options?.Temperature ?? Config.Temperature) is double t)
            ollamaOptions["temperature"] = t;

        if ((options?.TopP ?? Config.TopP) is double p)
            ollamaOptions["top_p"] = p;

        if ((options?.MaxOutputTokens ?? Config.MaxTokens) is int m)
            ollamaOptions["num_predict"] = m;

        // 如果有配置项，直接挂载到 root
        if (ollamaOptions.Count > 0)
        {
            root["options"] = ollamaOptions;
        }

        return root;
    }

    #region 辅助数据类

    private class OllamaFullResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage Message { get; set; }
    }

    private class OllamaStreamChunk
    {
        [JsonPropertyName("message")]
        public OllamaMessage Message { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }
    }

    private class OllamaMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    #endregion
}