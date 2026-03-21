using System.Collections.Concurrent;
using System.Text;
using ArchiveMaster.Enums;

namespace ArchiveMaster.ViewModels;

public class AiAssistantChatMessage : AiChatMessage
{
    private readonly StringBuilder plainTextBuilder = new StringBuilder();

    private readonly StringBuilder thinkTextBuilder = new StringBuilder();

    private string frozenPlainText;

    private string frozenThinkText;

    private bool isThinking;

    private int lineBeginIndex = 0;

    private ConcurrentQueue<string> queueMessage = new ConcurrentQueue<string>();

    private PeriodicTimer timer;

    private CancellationTokenSource timerCts = new();

    public AiAssistantChatMessage()
    {
        _ = InitializeTimerAsync();
    }
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
        queueMessage.Enqueue(message);
    }

    protected override void Freeze(bool fold = false, int maxLength = 50)
    {
        timerCts.Cancel();
        timer.Dispose();
        OnTimerTick();

        EndLine();
        frozenPlainText = plainTextBuilder.ToString();
        frozenThinkText = thinkTextBuilder.ToString();

        base.Freeze(fold, maxLength);
    }

    private void AppendImmediately(string message)
    {
        //没有换行
        if (!message.Contains('\n'))
        {
            AddInline(message);
            return;
        }

        var lines = message.Split('\n');

        //至少2行
        //第一行，和之前的合并（如果第一行为空则不处理），并处理行尾
        if (lines[0].Length > 0)
        {
            AddInline(lines[0]);
        }

        EndLine();

        //第2~n-1行直接处理，最后一行
        for (int i = 1; i < lines.Length - 1; i++)
        {
            EndLine(lines[i]);
        }

        //最后一行
        AddInline(lines[^1]);
    }

    private void EndLine(string lineText = null)
    {
        if (lineText == null)
        {
            var inlines = Inlines.GetRange(lineBeginIndex, Inlines.Count - lineBeginIndex);
            lineText = string.Concat(inlines.Select(i => i.Text));
            Inlines.RemoveRange(lineBeginIndex, Inlines.Count - lineBeginIndex);
        }

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
        var formatedInlines = SimpleMarkdownParser.ParseSimpleMarkdown(lineText).ToList();
        Inlines.AddRange(formatedInlines);
        Inlines.Add("\n");
        lineBeginIndex = Inlines.Count;

        //加入StringBuilder
        if (isThinking)
        {
            thinkTextBuilder.AppendLine(lineText);
        }
        else
        {
            FullTextBuilder.AppendLine(lineText);
            plainTextBuilder.AppendLine(string.Concat(formatedInlines.Select(p => p.Text))); //结尾已经加了\n，不需要换行了
        }
    }

    private async Task InitializeTimerAsync()
    {
        using (timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100)))
        {
            try
            {
                // 传入 Token
                while (await timer.WaitForNextTickAsync(timerCts.Token))
                {
                    OnTimerTick();
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，无需处理
            }
            catch (ObjectDisposedException)
            {
                // 计时器已释放，正常退出
            }
        }
    }
    private void OnTimerTick()
    {
        var items = new List<string>(queueMessage.Count + 2);
        while (queueMessage.TryDequeue(out string item))
        {
            items.Add(item);
        }

        if (items.Count > 0)
        {
            AppendImmediately(string.Concat(items));
        }
    }
}