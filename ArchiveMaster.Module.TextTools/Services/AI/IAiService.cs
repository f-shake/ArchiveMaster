using ArchiveMaster.Configs;
using ArchiveMaster.Events;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using Microsoft.Extensions.AI;

namespace ArchiveMaster.Services;

public interface IAiService
{
    public event GenericEventHandler<LlmOutputItem> AiTextGenerate;

    public AiProviderConfig AI { get; }
    public ChatOptions ChatOptions { get; }

    public bool NeedRemoveThink { get; }

    public Task<(string SystemPrompt, string UserPrompt)> GetFirstPromptAsync(CancellationToken ct);

    internal void OnAiTextGenerate(LlmOutputItem e);
}