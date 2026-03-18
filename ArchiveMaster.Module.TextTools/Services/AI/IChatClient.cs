using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;

public interface IChatClient
{
    public string Model { get; }

    public abstract Task<string> GetResponseAsync(IEnumerable<AiChatMessage> messages, ChatOptions options = null,
        CancellationToken cancellationToken = default);

    public abstract IAsyncEnumerable<string> GetStreamingResponseAsync(IEnumerable<AiChatMessage> messages,
        ChatOptions options = null, CancellationToken ct = default);
}