using System.Diagnostics;
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
    private StringBuilder fullTextBuilder=new StringBuilder();
    private string frozenFullText;

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


    public string FullText => !isFrozen
        ? throw new InvalidOperationException("当前消息未冻结，无法获取FullText")
        : frozenFullText ?? throw new InvalidOperationException("当前消息已冻结，但全文内容尚未初始化");

    private int lineBeginIndex = 0;
    public AvaloniaList<InlineItem> Inlines { get; } = new AvaloniaList<InlineItem>();

    public bool IsFrozen => isFrozen;

    public void AddInline(InlineItem inline)
    {
        if (isFrozen)
        {
            throw new InvalidOperationException("当前消息已冻结，无法修改");
        }

        Inlines.Add(inline);
        fullTextBuilder.Append(inline.Text);
        MessageAppended?.Invoke(this, EventArgs.Empty);
    }

    private void EndLine()
    {
        var inlines = Inlines.GetRange(lineBeginIndex, Inlines.Count - lineBeginIndex).ToList();
        var text = string.Concat(inlines.Select(i => i.Text));
        Inlines.RemoveRange(lineBeginIndex, Inlines.Count - lineBeginIndex);
        var formatedInlines = SimpleMarkdownParser.ParseSimpleMarkdown(text);
        Inlines.AddRange(formatedInlines.Append("\n"));
        fullTextBuilder.AppendLine();
        lineBeginIndex = Inlines.Count;
    }

    public void AppendMessage(string message)
    {
        if (Sender != AiChatMessageSender.Assistant)
        {
            throw new InvalidOperationException("当前消息不是Assistant，无法追加消息");
        }

        var lines = message.Split('\n');
        //没有换行
        if (lines.Length == 1)
        {
            AddInline(message);
            return;
        }

        //换行了
        AddInline(lines[0]);
        for (int i = 1; i < lines.Length; i++)
        {
            EndLine();
            AddInline(lines[i]);
        }
    }


    public void Freeze(bool removeThink = true, bool fold = false, int maxLength = 50)
    {
        isFrozen = true;

        if (Sender == AiChatMessageSender.Assistant)
        {
            EndLine();
        }

        var fullText = fullTextBuilder.ToString();
        if (removeThink)
        {
            fullText = RemoveThink(fullText);
        }

        frozenFullText = fullText;
        fullTextBuilder = null;

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