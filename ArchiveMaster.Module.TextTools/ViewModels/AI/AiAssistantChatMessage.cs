using System.Text;
using ArchiveMaster.Enums;

namespace ArchiveMaster.ViewModels;

public class AiAssistantChatMessage : AiChatMessage
{
    private string frozenPlainText;
    private string frozenThinkText;
    private bool isThinking;
    private int lineBeginIndex = 0;
    private readonly StringBuilder plainTextBuilder = new StringBuilder();
    
    private readonly StringBuilder thinkTextBuilder = new StringBuilder();
    
    public string PlainText
    {
        get
        {
            if (Sender != AiChatMessageSender.Assistant)
            {
                throw new InvalidOperationException("当前消息不是Assistant，PlainText");
            }

            if (IsFrozen)
            {
                return frozenPlainText;
            }

            throw new InvalidOperationException("当前消息未冻结，PlainText");
        }
    }

    public override AiChatMessageSender Sender => AiChatMessageSender.Assistant;
    
    public string ThinkText
    {
        get
        {
            if (Sender != AiChatMessageSender.Assistant)
            {
                throw new InvalidOperationException("当前消息不是Assistant，无法获取ThinkText");
            }

            if (IsFrozen)
            {
                return frozenThinkText;
            }

            throw new InvalidOperationException("当前消息未冻结，无法获取ThinkText");
        }
    }
    
    public void Append(string message)
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

    protected override void Freeze(bool fold = false, int maxLength = 50)
    {
        if (Sender == AiChatMessageSender.Assistant)
        {
            EndLine();
            frozenPlainText = plainTextBuilder.ToString();
            frozenThinkText = thinkTextBuilder.ToString();
        }

        base.Freeze(fold, maxLength);
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
            FullTextBuilder.AppendLine(lineText);
            plainTextBuilder.AppendLine(string.Concat(formatedInlines.Select(p => p.Text)));
        }
    }
}