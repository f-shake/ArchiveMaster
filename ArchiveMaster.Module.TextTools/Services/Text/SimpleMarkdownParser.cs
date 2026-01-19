using Avalonia.Media;
using System.Collections.Generic;

namespace ArchiveMaster.ViewModels;

public static class SimpleMarkdownParser
{
    public static IEnumerable<InlineItem> ParseSimpleMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        var lines = text.Replace("\r\n", "\n").Split('\n');

        foreach (var line in lines)
        {
            int fontSize = 12;
            string content = line;

            // ===== 1. 标题解析 =====
            if (line.StartsWith("### "))
            {
                fontSize = 16;
                content = line[4..];
            }
            else if (line.StartsWith("## "))
            {
                fontSize = 18;
                content = line[3..];
            }
            else if (line.StartsWith("# "))
            {
                fontSize = 22;
                content = line[2..];
            }

            // ===== 2. 行内解析（粗体 / 斜体）=====
            foreach (var item in ParseInline(content, fontSize))
            {
                yield return item;
            }

            // 保留换行
            yield return new InlineItem("\n") { FontSize = fontSize };
        }
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
                    yield return new InlineItem(
                        text.Substring(i + 2, end - i - 2),
                        isBold: true,
                        fontSize: fontSize);

                    i = end + 2;
                    continue;
                }
            }

            // 斜体 *
            if (text[i] == '*')
            {
                int end = text.IndexOf('*', i + 1);
                if (end > i)
                {
                    yield return new InlineItem(
                        text.Substring(i + 1, end - i - 1),
                        isItalic: true,
                        fontSize: fontSize);

                    i = end + 1;
                    continue;
                }
            }

            // 普通文本
            int next = FindNextSpecial(text, i);

            if (next <= i)
                next = text.Length;

            yield return new InlineItem(
                text.Substring(i, next - i),
                fontSize: fontSize);

            i = next;
        }
    }

    private static int FindNextSpecial(string text, int start)
    {
        int nextBold = text.IndexOf("**", start);
        int nextItalic = text.IndexOf('*', start);

        int next = -1;

        if (nextBold >= 0 && nextItalic >= 0)
            next = Math.Min(nextBold, nextItalic);
        else if (nextBold >= 0)
            next = nextBold;
        else if (nextItalic >= 0)
            next = nextItalic;

        return next >= 0 ? next : text.Length;
    }
}