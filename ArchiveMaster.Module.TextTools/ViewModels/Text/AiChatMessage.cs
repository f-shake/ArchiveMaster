using ArchiveMaster.Enums;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class AiChatMessage : ObservableObject
{
    public AiChatMessage()
    {
    }

    public AiChatMessage(AiChatMessageSender sender)
    {
        Sender = sender;
    }

    [ObservableProperty]
    private AiChatMessageSender sender;

    [ObservableProperty]
    private AvaloniaList<InlineItem> inlines = new AvaloniaList<InlineItem>();

    public void AddInline(InlineItem inline)
    {
        Inlines.Add(inline);
    }

    public void AddInline(string message)
    {
        if (message.Contains('\r'))
        {
            message = message.Replace("\r", "");
        }

        Inlines.Add(new InlineItem(message));
    }
}