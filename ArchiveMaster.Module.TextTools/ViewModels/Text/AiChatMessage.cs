using System.Text;
using System.Text.RegularExpressions;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using Avalonia.Collections;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class AiChatMessage : ObservableObject
{
    private AiChatMessage chatMessage;

    private string fullText;

    private bool isFrozen;

    [ObservableProperty]
    private AiChatMessageSender sender;

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

    public event EventHandler MessageAppended;


    public string FullText => isFrozen ? fullText : throw new InvalidOperationException("当前消息未冻结，无法获取FullText");
    public AvaloniaList<InlineItem> Inlines { get; } = new AvaloniaList<InlineItem>();

    public bool IsFrozen => isFrozen;

    public void AddInline(InlineItem inline)
    {
        if (isFrozen)
        {
            throw new InvalidOperationException("当前消息已冻结，无法修改");
        }

        Inlines.Add(inline);
        MessageAppended?.Invoke(this, EventArgs.Empty);
    }

    public void AppendMessage(string message)
    {
        if (Sender != AiChatMessageSender.Assistant)
        {
            throw new InvalidOperationException("当前消息不是Assistant，无法追加消息");
        }

        if (Inlines.Count == 0)
        {
            AddInline("");
        }

        var lines = message.Split('\n');
        Inlines[^1].Text += lines[0];
        if (lines.Length == 1)
        {
            return;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            var inlines = SimpleMarkdownParser.ParseSimpleMarkdown(Inlines[^1].Text);
            inlines = inlines.Append("\n");
            Inlines.RemoveAt(Inlines.Count - 1);
            Inlines.AddRange(inlines);
            AddInline(lines[i]);
        }
    }


    public void Freeze(bool removeThink = true, bool fold = false, int maxLength = 50)
    {
        isFrozen = true;

        chatMessage = new AiChatMessage(Sender, string.Concat(Inlines.Select(i => i.Text)));

        fullText = string.Concat(Inlines.Select(p => p.Text)).Trim();
        if (removeThink)
        {
            fullText = RemoveThink(fullText);
        }

        if (fold)
        {
            if (FullText.Length > maxLength)
            {
                Inlines.Clear();
                var text = FullText.Replace("\r", "").Replace("\n", "");
                Inlines.Add(new InlineItem(text[..(maxLength / 2)]));
                Inlines.Add(new InlineItem("  ...  ", foreground: Brushes.Gray));
                Inlines.Add(new InlineItem(text[^(maxLength / 2)..]));
                Inlines.Add(new InlineItem($"（共{FullText.Length}字）"));
            }
        }
        else
        {
            //20260319改为逐行解析
            // if (Sender == AiChatMessageSender.Assistant)
            // {
            //     Inlines.Clear();
            //     var inlines = SimpleMarkdownParser.ParseSimpleMarkdown(FullText);
            //     Inlines.AddRange(inlines);
            // }
        }

        OnPropertyChanged(nameof(IsFrozen));
        OnPropertyChanged(nameof(FullText));
    }

    public static string RemoveThink(string text)
    {
        return ThinkPartRegex().Replace(text, string.Empty);
    }

    [GeneratedRegex(@"^\s*<Think>.*?</Think>\s*$\r?\n?",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline, "zh-CN")]
    private static partial Regex ThinkPartRegex();
}