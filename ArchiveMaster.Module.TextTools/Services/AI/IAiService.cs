using ArchiveMaster.Configs;
using ArchiveMaster.Events;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;

namespace ArchiveMaster.Services;

public interface IAiService
{
    public AiProviderConfig AI { get; }
    public event GenericEventHandler<LlmOutputItem> AiTextGenerate;
    public void BindConversation(AiConversation conversation);
    public AiConversation Conversation { get; }
    internal void OnAiTextGenerate(LlmOutputItem e);
}