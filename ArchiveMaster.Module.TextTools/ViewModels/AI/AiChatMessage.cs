using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using Avalonia.Collections;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public abstract partial class AiChatMessage : INotifyPropertyChanged
{
    private string frozenFullText;
    private readonly StringBuilder fullTextBuilder = new StringBuilder();
    private bool isFrozen;
    protected AiChatMessage()
    {
    }

    public event EventHandler MessageAppended;

    public event PropertyChangedEventHandler PropertyChanged;

    public string FullText => !isFrozen
        ? throw new InvalidOperationException("当前消息未冻结，无法获取FullText")
        : frozenFullText ?? throw new InvalidOperationException("当前消息已冻结，但全文内容尚未初始化");

    public AvaloniaList<InlineItem> Inlines { get; } = new AvaloniaList<InlineItem>();
    public bool IsFrozen => isFrozen;
    public abstract AiChatMessageSender Sender { get; }
    protected StringBuilder FullTextBuilder => fullTextBuilder;
    public static AiAssistantChatMessage CreateAssistantMessage()
    {
        var message = new AiAssistantChatMessage();
        return message;
    }

    public static AiSystemChatMessage CreateSystemMessage(string systemPrompt, bool fold, int maxLength)
    {
        var message = new AiSystemChatMessage();
        message.AddInline(systemPrompt);
        message.fullTextBuilder.Append(systemPrompt);
        message.Freeze(fold, maxLength);
        return message;
    }

    public static AiUserChatMessage CreateUserMessage(string userPrompt, bool fold, int maxLength)
    {
        var message = new AiUserChatMessage();
        message.AddInline(userPrompt);
        message.fullTextBuilder.Append(userPrompt);
        message.Freeze(fold, maxLength);
        return message;
    }
    public static string RemoveThink(string text)
    {
        return ThinkPartRegex().Replace(text, string.Empty);
    }

    public void FreezeAssistantMessage()
    {
        if (Sender != AiChatMessageSender.Assistant)
        {
            throw new InvalidOperationException("当前消息不是Assistant，无法冻结");
        }

        Freeze(false, 0);
    }

    protected void AddInline(InlineItem inline)
    {
        if (isFrozen)
        {
            throw new InvalidOperationException("当前消息已冻结，无法修改");
        }

        Inlines.Add(inline);
        MessageAppended?.Invoke(this, EventArgs.Empty);
    }
    protected virtual void Freeze(bool fold = false, int maxLength = 50)
    {
        isFrozen = true;
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
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    [GeneratedRegex(@"^\s*<Think>.*?</Think>\s*$\r?\n?",
                    RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline, "zh-CN")]
    private static partial Regex ThinkPartRegex();
}