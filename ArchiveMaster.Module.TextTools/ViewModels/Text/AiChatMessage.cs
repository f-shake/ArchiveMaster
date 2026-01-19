using System.Text;
using ArchiveMaster.Enums;
using Avalonia.Collections;
using Avalonia.Media;
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

    public AiChatMessage(AiChatMessageSender sender, string message)
    {
        Sender = sender;
        AddInline(message);
    }

    [ObservableProperty]
    private AiChatMessageSender sender;

    [ObservableProperty]
    private AvaloniaList<InlineItem> inlines = new AvaloniaList<InlineItem>();

    public void AddInline(InlineItem inline)
    {
        if (isFrozen)
        {
            throw new InvalidOperationException("当前消息已冻结，无法修改");
        }

        Inlines.Add(inline);
    }

    private bool isFrozen;

    public void Freeze(bool fold, int maxLength = 50)
    {
        isFrozen = true;
        if (fold)
        {
            StringBuilder str = new StringBuilder();
            foreach (var inline in Inlines)
            {
                str.Append(inline.Text.Replace("\r", "").Replace("\n", ""));
            }

            Inlines.Clear();
            if (str.Length > maxLength)
            {
                //前半部分
                Inlines.Add(new InlineItem(str.ToString(0, maxLength / 2)));
                Inlines.Add(new InlineItem("  ...  ", foreground: Brushes.Gray));
                Inlines.Add(new InlineItem(str.ToString(str.Length - maxLength / 2, maxLength / 2)));
            }
            else
            {
                Inlines.Add(new InlineItem(str.ToString()));
            }
        }
    }

    public void AddInline(string message)
    {
        if (message.Contains('\r'))
        {
            message = message.Replace("\r", "");
        }

        Inlines.Add(new InlineItem(message));
    }

    public void ReplaceWithFinalResponse(string text)
    {
        Inlines.Clear();
        var inlines = SimpleMarkdownParser.ParseSimpleMarkdown(text);
        Inlines.AddRange(inlines);
    }
}