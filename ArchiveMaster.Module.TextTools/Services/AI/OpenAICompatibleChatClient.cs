using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArchiveMaster.Configs;
using Microsoft.Extensions.AI;

namespace ArchiveMaster.Services;

public class OpenAICompatibleChatClient(IOpenAIAiProvider config) : BaseChatClient<IOpenAIAiProvider>(config, "/v1/")
{
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions options = null, CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(messages, options, false);
        var response = await HttpClient.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken: cancellationToken);
        return new ChatResponse([
            new ChatMessage(ChatRole.Assistant, result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty)
        ]);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(messages, options, true);
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            { Content = JsonContent.Create(payload) };
        using var response =
            await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line) || line == "data: [DONE]") continue;
            if (line.StartsWith("data: "))
            {
                var chunk = JsonSerializer.Deserialize<OpenAIStreamResponse>(line["data: ".Length..]);
                var content = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
                if (!string.IsNullOrEmpty(content)) yield return new ChatResponseUpdate(ChatRole.Assistant, content);
            }
        }
    }

    private object BuildPayload(IEnumerable<ChatMessage> messages, ChatOptions options, bool isStream)
    {
        var root = CreateBasePayload(messages, isStream);
        if ((options?.Temperature ?? Config.Temperature) is double t) root["temperature"] = t;
        if ((options?.TopP ?? Config.TopP) is double p) root["top_p"] = p;
        if ((options?.MaxOutputTokens ?? Config.MaxTokens) is int m) root["max_tokens"] = m;

        ApplyExtraParams(root, (k, v) => root[k] = v);
        return root;
    }


    #region OpenAI 数据类

    private class OpenAIResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; }
    }

    private class OpenAIStreamResponse
    {
        [JsonPropertyName("choices")]
        public List<StreamChoice> Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public OpenAIMessage Message { get; set; }
    }

    private class StreamChoice
    {
        [JsonPropertyName("delta")]
        public OpenAIMessage Delta { get; set; }
    }

    private class OpenAIMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    #endregion
}