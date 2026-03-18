using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ArchiveMaster.Configs;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;

namespace ArchiveMaster.Services;

public class OpenAICompatibleChatClient(IOpenAIAiProvider config) : BaseChatClient<IOpenAIAiProvider>(config, "/v1/")
{
    public override async Task<string> GetResponseAsync(IEnumerable<AiChatMessage> messages,
        ChatOptions options = null, CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(messages, options, false);
        var response = await HttpClient.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken: cancellationToken);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    public override IAsyncEnumerable<string> GetStreamingResponseAsync(
        IEnumerable<AiChatMessage> messages, ChatOptions options = null,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        return GetStreamingResponseAsync(messages, options, "chat/completions",
            line =>
            {
                if (string.IsNullOrWhiteSpace(line) || line == "data: [DONE]")
                {
                    return null;
                }

                if (line.StartsWith("data: "))
                {
                    var chunk = JsonSerializer
                        .Deserialize<OpenAIStreamResponse>(line["data: ".Length..]);
                    var content = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
                    if (!string.IsNullOrEmpty(content))
                    {
                        return content;
                    }
                }

                return null;
            }, ct);
    }

    protected override JsonObject BuildPayload(IEnumerable<AiChatMessage> messages, ChatOptions options, bool isStream)
    {
        var root = CreateBasePayload(messages, isStream);
        if ((options?.Temperature ?? Config.Temperature) is double t)
        {
            root["temperature"] = t;
        }

        if ((options?.TopP ?? Config.TopP) is double p)
        {
            root["top_p"] = p;
        }

        if ((options?.MaxOutputTokens ?? Config.MaxTokens) is int m)
        {
            root["max_tokens"] = m;
        }

        if (options?.OutputJson == true)
        {
            root["response_format"] = new JsonObject { ["type"] = "json" };
        }

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