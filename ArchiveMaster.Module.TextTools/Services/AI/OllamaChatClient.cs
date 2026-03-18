using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArchiveMaster.Configs;
using Microsoft.Extensions.AI;

namespace ArchiveMaster.Services;
public class OllamaChatClient(IOllamaAiProvider config) : BaseChatClient<IOllamaAiProvider>(config, "/")
{
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options = null, CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(messages, options, false);
        var response = await HttpClient.PostAsJsonAsync("api/chat", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OllamaFullResponse>(cancellationToken);
        return new ChatResponse([new ChatMessage(ChatRole.Assistant, result?.Message?.Content ?? string.Empty)]);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(messages, options, true);
        using var response = await HttpClient.PostAsJsonAsync("api/chat", payload, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var chunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line);
            if (chunk?.Message?.Content != null) yield return new ChatResponseUpdate(ChatRole.Assistant, chunk.Message.Content);
            if (chunk?.Done == true) break;
        }
    }

    private object BuildPayload(IEnumerable<ChatMessage> messages, ChatOptions options, bool isStream)
    {
        var root = CreateBasePayload(messages, isStream);
        var ollamaOptions = new Dictionary<string, object>();

        if ((options?.Temperature ?? Config.Temperature) is double t) ollamaOptions["temperature"] = t;
        if ((options?.TopP ?? Config.TopP) is double p) ollamaOptions["top_p"] = p;
        if ((options?.MaxOutputTokens ?? Config.MaxTokens) is int m) ollamaOptions["num_predict"] = m;

        if (ollamaOptions.Count > 0) root["options"] = ollamaOptions;

        var commonOptions = new[] { "temperature", "top_p", "top_k", "num_predict", "num_ctx", "repeat_penalty" };
        ApplyExtraParams(root, (k, v) => 
        {
            if (commonOptions.Contains(k)) 
            {
                var opt = root.TryGetValue("options", out var o) ? (Dictionary<string, object>)o : (Dictionary<string, object>)(root["options"] = new Dictionary<string, object>());
                opt[k] = v;
            }
            else root[k] = v;
        });
        return root;
    }

    #region 辅助数据类
    private class OllamaFullResponse
    {
        [JsonPropertyName("message")] public OllamaMessage Message { get; set; }
    }
    private class OllamaStreamChunk
    {
        [JsonPropertyName("message")] public OllamaMessage Message { get; set; }
        [JsonPropertyName("done")] public bool Done { get; set; }
    }
    private class OllamaMessage
    {
        [JsonPropertyName("content")] public string Content { get; set; }
    }
    #endregion
}