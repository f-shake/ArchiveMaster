using Avalonia.Media;
using System.Collections.Generic;
using System.Linq;

namespace ArchiveMaster.ViewModels;

public static class SimpleMarkdownParser
{
    public static IEnumerable<InlineItem> ParseSimpleMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        var lines = text.Replace("\r\n", "\n").Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int fontSize = 0;
            IBrush foreground = null;
            string content = line;

            // 1. 处理分隔符 ---
            if (line.Trim() == "---")
            {
                yield return new InlineItem("────────────────────────────────", foreground: Brushes.Gray);
                goto EndLine;
            }

            // 2. 处理引用 > (Blockquote)
            // 支持 "> 内容" 或 ">> 内容"
            if (line.TrimStart().StartsWith(">"))
            {
                // 计算引用深度或简单处理
                int quoteLevel = line.TakeWhile(c => c == '>' || c == ' ').Count(c => c == '>');
                foreground = Brushes.Gray; // 引用通常颜色较浅
                // 剥离所有的 > 和紧跟的一个空格
                content = line.TrimStart().TrimStart('>').TrimStart(' ');
                // 模拟引用符
                yield return new InlineItem(new string('▌', 1) + " ", foreground: Brushes.Gray);
            }

            // 3. 处理无序列表 * (Unordered List)
            // 注意：要检查 * 后面是否有空格，防止把加粗 ** 的第一个星号截断
            var trimmedLine = content.TrimStart();
            if (trimmedLine.StartsWith("* ") || trimmedLine.StartsWith("- "))
            {
                // 模拟列表点
                yield return new InlineItem(" • ");
                content = trimmedLine[2..];
            }

            // 4. 标题解析 (对 content 进行操作，这样可以支持 "> # 标题")
            if (content.StartsWith("#### "))
            {
                fontSize = 16;
                content = content[5..];
            }
            else if (content.StartsWith("### "))
            {
                fontSize = 18;
                content = content[4..];
            }
            else if (content.StartsWith("## "))
            {
                fontSize = 20;
                content = content[3..];
            }
            else if (content.StartsWith("# "))
            {
                fontSize = 24;
                content = content[2..];
            }

            // 5. 行内解析
            foreach (var item in ParseInline(content, fontSize))
            {
                if (foreground != null && item.Foreground == null)
                    item.Foreground = foreground;
                yield return item;
            }

            EndLine:
            if (i < lines.Length - 1)
            {
                yield return new InlineItem("\n") { FontSize = fontSize };
            }
        }
    }

    private static int FindNextSpecial(string text, int start)
    {
        int nextBold = text.IndexOf("**", start);
        int nextItalic = text.IndexOf('*', start);

        if (nextBold >= 0 && nextItalic >= 0)
            return Math.Min(nextBold, nextItalic);

        if (nextBold >= 0) return nextBold;
        if (nextItalic >= 0) return nextItalic;

        return text.Length;
    }

    private static IEnumerable<InlineItem> ParseInline(string text, int fontSize)
    {
        int i = 0;
        while (i < text.Length)
        {
            // 粗体 **
            if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
            {
                int end = text.IndexOf("**", i + 2);
                if (end > i)
                {
                    yield return new InlineItem(text.Substring(i + 2, end - i - 2), isBold: true, fontSize: fontSize);
                    i = end + 2;
                    continue;
                }
            }

            // 斜体 * (增加空格判断：如果星号后面是空格，通常不是斜体)
            if (text[i] == '*')
            {
                if (i + 1 < text.Length && text[i + 1] != ' ')
                {
                    int end = text.IndexOf('*', i + 1);
                    if (end > i)
                    {
                        yield return new InlineItem(text.Substring(i + 1, end - i - 1), isItalic: true,
                            fontSize: fontSize);
                        i = end + 1;
                        continue;
                    }
                }
            }

            // 普通文本
            int next = FindNextSpecial(text, i);
            if (next <= i) next = text.Length;

            yield return new InlineItem(text.Substring(i, next - i), fontSize: fontSize);
            i = next;
        }
    }
}