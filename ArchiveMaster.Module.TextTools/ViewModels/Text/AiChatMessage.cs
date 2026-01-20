using System.Text;
using ArchiveMaster.Enums;
using Avalonia.Collections;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;

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

    public event EventHandler MessageAppended;

    [ObservableProperty]
    private AvaloniaList<InlineItem> inlines = new AvaloniaList<InlineItem>();

    public void AddInline(InlineItem inline)
    {
        if (isFrozen)
        {
            throw new InvalidOperationException("当前消息已冻结，无法修改");
        }

        Inlines.Add(inline);
        MessageAppended?.Invoke(this, EventArgs.Empty);
    }

    private bool isFrozen;

    private ChatMessage chatMessage;

    public ChatMessage ChatMessage =>
        !isFrozen ? throw new InvalidOperationException("当前消息未冻结，无法获取ChatMessage") : chatMessage;

    public void Freeze(bool fold, int maxLength = 50)
    {
        isFrozen = true;

        var role = Sender switch
        {
            AiChatMessageSender.System => ChatRole.System,
            AiChatMessageSender.User => ChatRole.User,
            AiChatMessageSender.Assistant => ChatRole.Assistant,
            _ => throw new ArgumentOutOfRangeException(nameof(Sender), Sender, null)
        };
        chatMessage = new ChatMessage(role, string.Concat(Inlines.Select(i => i.Text)));


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

        AddInline(new InlineItem(message));
    }

    public void ReplaceWithFinalResponse(string text)
    {
        Inlines.Clear();
        var inlines = SimpleMarkdownParser.ParseSimpleMarkdown(text);
        Inlines.AddRange(inlines);
        Freeze(false);
    }
}