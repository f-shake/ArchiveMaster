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

    public AvaloniaList<InlineItem> Inlines { get; } = new AvaloniaList<InlineItem>();

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
    private string fullText;

    public bool IsFrozen => isFrozen;

    public string FullText => isFrozen ? fullText : throw new InvalidOperationException("当前消息未冻结，无法获取FullText");

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

        fullText = string.Concat(Inlines.Select(p => p.Text)).Trim();

        if (fold)
        {
            Inlines.Clear();
            var text = FullText.Replace("\r", "").Replace("\n", "");
            if (text.Length > maxLength)
            {
                //前半部分
                Inlines.Add(new InlineItem(text[..(maxLength / 2)]));
                Inlines.Add(new InlineItem("  ...  ", foreground: Brushes.Gray));
                Inlines.Add(new InlineItem(text[^(maxLength / 2)..]));
            }
            else
            {
                Inlines.Add(new InlineItem(text));
            }
        }
        else
        {
            if (Sender == AiChatMessageSender.Assistant)
            {
                Inlines.Clear();
                var inlines = SimpleMarkdownParser.ParseSimpleMarkdown(FullText);
                Inlines.AddRange(inlines);
            }
        }

        OnPropertyChanged(nameof(IsFrozen));
        OnPropertyChanged(nameof(FullText));
    }

    public void AddInline(string message)
    {
        if (message.Contains('\r'))
        {
            message = message.Replace("\r", "");
        }

        AddInline(new InlineItem(message));
    }

    // public void ReplaceWithFinalResponse(string text)
    // {
    //     Inlines.Clear();
    //     Inlines.AddRange(inlines);
    //     Freeze(false);
    // }
}