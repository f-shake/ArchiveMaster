using System.Text;
using System.Text.RegularExpressions;
using ArchiveMaster.ViewModels;
using DocumentFormat.OpenXml.Packaging;
using NPOI.XWPF.UserModel;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;

namespace ArchiveMaster.Services;

public static class TextSourceExtensions
{
    public static async IAsyncEnumerable<string> GetPlainTextAsync(this TextSource source, bool combinePerFile = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.FromFile)
        {
            foreach (var file in source.Files)
            {
                var lines = Path.GetExtension(file.File) switch
                {
                    ".docx" => file.ReadDocxAsync(),
                    ".doc" => file.ReadDocAsync(),
                    ".md" => file.ReadMarkdownAsync(),
                    _ => file.ReadTxtAsync()
                };

                if (combinePerFile)
                {
                    StringBuilder str = new StringBuilder();
                    await foreach (var line in lines)
                    {
                        if (source.IgnoreLineBreak)
                        {
                            str.Append(line);
                        }
                        else
                        {
                            str.AppendLine(line);
                        }
                    }

                    yield return str.ToString();
                }
                else
                {
                    await foreach (var line in lines)
                    {
                        yield return line;
                    }
                }
            }
        }
        else
        {
            yield return source.Text;
        }
    }

    private static async IAsyncEnumerable<string> ReadDocAsync(this DocFile file, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(file.File);
        XWPFDocument doc = new XWPFDocument(stream);
        foreach (var para in doc.Paragraphs)
        {
            ct.ThrowIfCancellationRequested();
            string v = para.ParagraphText; //获得文本
            if (!string.IsNullOrEmpty(v))
            {
                yield return v;
            }
        }
    }

    private static async IAsyncEnumerable<string> ReadDocxAsync(this DocFile file, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(file.File);

        using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;

        if (body == null)
        {
            yield break;
        }

        foreach (var para in body.Elements<Paragraph>())
        {
            ct.ThrowIfCancellationRequested();
            yield return para.InnerText;
        }
    }

    private static async IAsyncEnumerable<string> ReadMarkdownAsync(this DocFile file, CancellationToken ct = default)
    {
        var markdown = await File.ReadAllTextAsync(file.File, ct);
        ct.ThrowIfCancellationRequested();
        var text = Markdown.ToPlainText(markdown);
        yield return text;
    }

    private static async IAsyncEnumerable<string> ReadTxtAsync(this DocFile file, CancellationToken ct = default)
    {
        var lines = await File.ReadAllLinesAsync(file.File, ct);
        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();
            yield return line;
        }
    }
}