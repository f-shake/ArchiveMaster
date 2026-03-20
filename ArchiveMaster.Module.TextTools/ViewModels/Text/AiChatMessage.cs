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
    private StringBuilder fullTextBuilder = new StringBuilder();
    private StringBuilder plainTextBuilder = new StringBuilder();
    private StringBuilder thinkTextBuilder = new StringBuilder();
    private bool isThinking;
    private string frozenFullText;
    private string frozenPlainText;

    private bool isFrozen;

    [ObservableProperty]
    private AiChatMessageSender sender;

    private AiChatMessage()
    {
    }

    public static AiChatMessage CreateSystemMessage(string systemPrompt, bool fold, int maxLength)
    {
        var message = new AiChatMessage { Sender = AiChatMessageSender.System };
        message.AddInline(systemPrompt);
        message.fullTextBuilder.Append(systemPrompt);
        message.Freeze(fold, maxLength);
        return message;
    }

    public static AiChatMessage CreateUserMessage(string userPrompt, bool fold, int maxLength)
    {
        var message = new AiChatMessage { Sender = AiChatMessageSender.User };
        message.AddInline(userPrompt);
        message.fullTextBuilder.Append(userPrompt);
        message.Freeze(fold, maxLength);
        return message;
    }

    public static AiChatMessage CreateAssistantMessage()
    {
        var message = new AiChatMessage { Sender = AiChatMessageSender.Assistant };
        return message;
    }

    public event EventHandler MessageAppended;

    private string frozenThinkText;

    public string FullText => !isFrozen
        ? throw new InvalidOperationException("当前消息未冻结，无法获取FullText")
        : frozenFullText ?? throw new InvalidOperationException("当前消息已冻结，但全文内容尚未初始化");

    public string ThinkText
    {
        get
        {
            if (Sender != AiChatMessageSender.Assistant)
            {
                throw new InvalidOperationException("当前消息不是Assistant，无法获取ThinkText");
            }

            if (isFrozen)
            {
                return frozenThinkText;
            }

            throw new InvalidOperationException("当前消息未冻结，无法获取ThinkText");
        }
    }

    public string PlainText
    {
        get
        {
            if (Sender != AiChatMessageSender.Assistant)
            {
                throw new InvalidOperationException("当前消息不是Assistant，PlainText");
            }

            if (isFrozen)
            {
                return frozenPlainText;
            }

            throw new InvalidOperationException("当前消息未冻结，PlainText");
        }
    }

    private int lineBeginIndex = 0;
    public AvaloniaList<InlineItem> Inlines { get; } = new AvaloniaList<InlineItem>();

    public bool IsFrozen => isFrozen;

    private void AddInline(InlineItem inline)
    {
        if (isFrozen)
        {
            throw new InvalidOperationException("当前消息已冻结，无法修改");
        }

        Inlines.Add(inline);
        MessageAppended?.Invoke(this, EventArgs.Empty);
    }

    private void EndLine()
    {
        var inlines = Inlines.GetRange(lineBeginIndex, Inlines.Count - lineBeginIndex);
        var lineText = string.Concat(inlines.Select(i => i.Text));
        Inlines.RemoveRange(lineBeginIndex, Inlines.Count - lineBeginIndex);
        if (lineText.Trim().Equals("<think>", StringComparison.OrdinalIgnoreCase))
        {
            isThinking = true;
            return;
        }

        if (isThinking && lineText.Trim().Equals("</think>", StringComparison.OrdinalIgnoreCase))
        {
            //思考模式，清空Inlines
            isThinking = false;
            Inlines.Clear();
            lineBeginIndex = 0;
            return;
        }

        //将Markdown格式化为InlineItem
        var formatedInlines = SimpleMarkdownParser.ParseSimpleMarkdown(lineText)
            .Append("\n")
            .ToList();
        Inlines.AddRange(formatedInlines);
        lineBeginIndex = Inlines.Count;

        //加入StringBuilder
        if (isThinking)
        {
            thinkTextBuilder.AppendLine(lineText);
        }
        else
        {
            fullTextBuilder.AppendLine(lineText);
            plainTextBuilder.AppendLine(string.Concat(formatedInlines.Select(p => p.Text)));
        }

    }

    public void AppendAssistantMessage(string message)
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
        if (lines[0].Length > 0)
        {
            //如果\n开头，第一个元素将为空
            AddInline(lines[0]);
        }

        for (int i = 1; i < lines.Length; i++)
        {
            EndLine();
            AddInline(lines[i]);
        }
    }


    public void FreezeAssistantMessage()
    {
        if (Sender != AiChatMessageSender.Assistant)
        {
            throw new InvalidOperationException("当前消息不是Assistant，无法冻结");
        }

        Freeze(false, 0);
    }

    private void Freeze(bool fold = false, int maxLength = 50)
    {
        isFrozen = true;

        if (Sender == AiChatMessageSender.Assistant)
        {
            EndLine();
            frozenPlainText = plainTextBuilder.ToString();
            frozenThinkText = thinkTextBuilder.ToString();
        }

        frozenFullText = fullTextBuilder.ToString();

        if (fold)
        {
            if (FullText.Length > maxLength)
            {
                Inlines.Clear();
                var text = FullText.Replace("\r", "").Replace("\n", "");
                Inlines.Add(new InlineItem(text[..(maxLength / 2)]));
                Inlines.Add(new InlineItem("  ...  ", false, Brushes.Gray));
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