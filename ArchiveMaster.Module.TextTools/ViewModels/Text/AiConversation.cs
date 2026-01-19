using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class AiConversation : ObservableObject
{
    public AvaloniaList<AiChatMessage> Messages { get; } = new AvaloniaList<AiChatMessage>();
}