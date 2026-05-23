using ArchiveMaster.Configs;
using ArchiveMaster.Events;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;

namespace ArchiveMaster.Services;

public interface IAiService
{
    public AiProviderConfig AI { get; }
    public ChatOptions ChatOptions { get; }

    public bool ProvideFirstUserPrompt { get; }

    public ValueTask<string> GetFirstUserPromptAsync(CancellationToken ct);

    public ValueTask<string> GetSystemPromptAsync(CancellationToken ct);
    string PostProcessLine(string text);

    internal void OnAiTextGenerate(LlmOutputItem e);
}