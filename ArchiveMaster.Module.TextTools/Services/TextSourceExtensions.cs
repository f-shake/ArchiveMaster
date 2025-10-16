using System.Runtime.CompilerServices;
using System.Text;
using ArchiveMaster.ViewModels;
using DocumentFormat.OpenXml.Packaging;
using NPOI.XWPF.UserModel;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using UtfUnknown;

namespace ArchiveMaster.Services;

public static class TextSourceExtensions
{
    public static async IAsyncEnumerable<DocFilePart> GetPlainTextAsync(this TextSource source,
        TextSourceReadMode readMode = TextSourceReadMode.PerFile,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        StringBuilder combined = new StringBuilder();
        if (source.FromFile)
        {
            foreach (var file in source.Files)
            {
                var lines = Path.GetExtension(file.File) switch
                {
                    ".docx" => file.ReadDocxAsync(ct: ct),
                    ".doc" => file.ReadDocAsync(ct: ct),
                    ".md" => file.ReadMarkdownAsync(ct: ct),
                    _ => file.ReadTxtAsync(ct: ct)
                };

                switch (readMode)
                {
                    case TextSourceReadMode.PerFile:
                    {
                        StringBuilder str = new StringBuilder();
                        await foreach (var line in lines.WithCancellation(ct))
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

                        yield return new DocFilePart(file.File, str.ToString());
                        break;
                    }
                    case TextSourceReadMode.PerParagraph:
                    {
                        await foreach (var line in lines.WithCancellation(ct))
                        {
                            yield return new DocFilePart(file.File, line);
                        }

                        break;
                    }
                    case TextSourceReadMode.Combined:
                    {
                        await foreach (var line in lines.WithCancellation(ct))
                        {
                            combined.AppendLine(line);
                        }

                        break;
                    }
                    
                    default:
                        throw new ArgumentOutOfRangeException(nameof(readMode), readMode, null);
                }
            }
            
            if (readMode == TextSourceReadMode.Combined)
            {
                yield return new DocFilePart(null, combined.ToString());
            }
        }
        else
        {
            switch (readMode)
            {
                case TextSourceReadMode.PerFile:
                case TextSourceReadMode.Combined:
                    yield return new DocFilePart(null, source.Text);
                    break;
                case TextSourceReadMode.PerParagraph:
                    foreach (var line in source.Text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
                    {
                        yield return new DocFilePart(null, line);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(readMode), readMode, null);
            }
        }
    }

    private static async Task<Encoding> DetectEncoding(DocFile file, CancellationToken ct)
    {
        var result = await CharsetDetector.DetectFromFileAsync(file.File, ct);
        var encoding = result.Detected?.Encoding ?? Encoding.UTF8;
        return encoding;
    }

    private static async IAsyncEnumerable<string> ReadDocAsync(this DocFile file,
            [EnumeratorCancellation]
        CancellationToken ct = default)
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

    private static async IAsyncEnumerable<string> ReadDocxAsync(this DocFile file,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(file.File);

        using WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false);
        var body = wordDoc.MainDocumentPart?.Document.Body;

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

    private static async IAsyncEnumerable<string> ReadMarkdownAsync(this DocFile file,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        var encoding = await DetectEncoding(file, ct);
        var markdown = await File.ReadAllTextAsync(file.File, encoding, ct);
        ct.ThrowIfCancellationRequested();
        var text = Markdown.ToPlainText(markdown);
        yield return text;
    }

    private static async IAsyncEnumerable<string> ReadTxtAsync(this DocFile file,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        var encoding = await DetectEncoding(file, ct);
        var lines = await File.ReadAllLinesAsync(file.File, encoding, ct);
        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();
            yield return line;
        }
    }
}