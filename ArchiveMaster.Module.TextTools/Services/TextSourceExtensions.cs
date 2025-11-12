using System.Runtime.CompilerServices;
using System.Text;
using ArchiveMaster.ViewModels;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
// using NPOI.XWPF.UserModel;
using DocumentFormat.OpenXml.Wordprocessing;
using FzLib.Collections;
using FzLib.Text;
using Markdig;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UtfUnknown;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;

namespace ArchiveMaster.Services;

public static class TextSourceExtensions
{
    public static async IAsyncEnumerable<DocFilePart> GetPlainTextAsync(this TextSource source,
        TextSourceReadUnit readUnit = TextSourceReadUnit.PerFile,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        StringBuilder combined = new StringBuilder();
        if (source.FromFile)
        {
            foreach (var file in source.Files)
            {
                ct.ThrowIfCancellationRequested();

                if (file.File == null || !File.Exists(file.File))
                {
                    throw new FileNotFoundException(file.File);
                }

                switch (new FileInfo(file.File).Length)
                {
                    case <= 0:
                        throw new InvalidOperationException($"文件{file.File}的大小为0");
                    case > 1024 * 1024 * 1024:
                        throw new InvalidOperationException($"文件{file.File}的大小超过1GB");
                }

                bool perParagraph = readUnit == TextSourceReadUnit.PerParagraph;
                var lines = Path.GetExtension(file.File) switch
                {
                    ".docx" => file.ReadDocxAsync(perParagraph, ct),
                    ".xlsx" => file.ReadXlsxAsync(perParagraph, ct),
                    // ".doc" => file.ReadDocAsync(ct: ct),
                    ".md" => file.ReadMarkdownAsync(perParagraph, ct),
                    ".pdf" => file.ReadPdfAsync(perParagraph, ct),
                    _ => file.ReadTxtAsync(perParagraph, ct)
                };

                switch (readUnit)
                {
                    case TextSourceReadUnit.PerFile:
                    case TextSourceReadUnit.PerParagraph:
                    {
                        await foreach (var line in lines.WithCancellation(ct))
                        {
                            yield return new DocFilePart(file.File, line);
                        }

                        break;
                    }
                    case TextSourceReadUnit.Combined:
                    {
                        combined.AppendLine(await lines.FirstAsync());

                        break;
                    }

                    default:
                        throw new ArgumentOutOfRangeException(nameof(readUnit), readUnit, null);
                }
            }

            if (readUnit == TextSourceReadUnit.Combined)
            {
                yield return new DocFilePart(null, combined.ToString());
            }
        }
        else
        {
            switch (readUnit)
            {
                case TextSourceReadUnit.PerFile:
                case TextSourceReadUnit.Combined:
                    yield return new DocFilePart(null, source.Text);
                    break;
                case TextSourceReadUnit.PerParagraph:
                    foreach (var line in source.Text.SplitLines())
                    {
                        yield return new DocFilePart(null, line);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(readUnit), readUnit, null);
            }
        }
    }

    private static async Task<Encoding> DetectEncoding(DocFile file, CancellationToken ct)
    {
        try
        {
            var result = await CharsetDetector.DetectFromFileAsync(file.File, ct);
            return result.Detected?.Encoding ?? Encoding.UTF8;
        }
        catch (Exception)
        {
            return Encoding.UTF8;
        }
    }

    private static async IAsyncEnumerable<string> ReadDocxAsync(this DocFile file,
        bool perParagraph,
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

        StringBuilder str = new StringBuilder();
        foreach (var element in body.Elements())
        {
            ct.ThrowIfCancellationRequested();
            if (element is Paragraph para)
            {
                if (perParagraph)
                {
                    yield return para.InnerText;
                }
                else
                {
                    str.AppendLine(para.InnerText);
                }
            }
            else if (element is Table table)
            {
                foreach (var row in table.Elements<TableRow>())
                {
                    string rowText = string.Join('\t', row.Elements<TableCell>().Select(p=>p.InnerText));
                    if (perParagraph)
                    {
                        yield return rowText;
                    }
                    else
                    {
                        str.AppendLine(rowText);
                    }
                }
            }
            else
            {
                if (perParagraph)
                {
                    yield return element.InnerText;
                }
                else
                {
                    str.AppendLine(element.InnerText);
                }
            }
        }

        if (!perParagraph)
        {
            yield return str.ToString();
        }
    }

    private static async IAsyncEnumerable<string> ReadXlsxAsync(this DocFile file,
        bool perParagraph,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(file.File);

        using SpreadsheetDocument excelDoc = SpreadsheetDocument.Open(stream, false);
        if (excelDoc.WorkbookPart == null)
        {
            yield break;
        }

        var sharedStrings = excelDoc.WorkbookPart.SharedStringTablePart?.SharedStringTable;

        ct.ThrowIfCancellationRequested();
        var sheets = excelDoc.WorkbookPart.Workbook.Sheets;
        if (sheets == null)
        {
            yield break;
        }

        ct.ThrowIfCancellationRequested();

        StringBuilder str = new StringBuilder();
        foreach (var sheet in sheets.Elements<Sheet>())
        {
            ct.ThrowIfCancellationRequested();
            if (sheet.Id == null || !sheet.Id.HasValue)
            {
                continue;
            }

            var worksheetPart = (WorksheetPart)excelDoc.WorkbookPart.GetPartById(sheet.Id);
            var rows = worksheetPart.Worksheet.Descendants<Row>();
            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                foreach (var cell in row.Elements<Cell>())
                {
                    AppendCellValue(cell);
                    str.Append('\t');
                }

                str.Remove(str.Length - 1, 1);
                if (perParagraph)
                {
                    yield return str.ToString();
                    str.Clear();
                }
                else
                {
                    str.AppendLine();
                }
            }
        }

        if (!perParagraph)
        {
            yield return str.ToString();
        }

        void AppendCellValue(Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            string value = cell.CellValue?.InnerText;
            if (cell.DataType != null && cell.DataType == CellValues.SharedString)
            {
                if (int.TryParse(value, out int index) && sharedStrings != null)
                {
                    str.Append(sharedStrings.ElementAt(index).InnerText);
                }
            }

            if (value != null)
            {
                str.Append(value);
            }
        }
    }

    private static async IAsyncEnumerable<string> ReadPdfAsync(this DocFile file,
        bool perParagraph,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        var bytes = await File.ReadAllBytesAsync(file.File, ct);
        using var doc = PdfDocument.Open(bytes);
        StringBuilder str = new StringBuilder();
        foreach (var page in doc.GetPages())
        {
            string text = ContentOrderTextExtractor.GetText(page);
            if (perParagraph)
            {
                foreach (var line in text.SplitLines())
                {
                    yield return line;
                }
            }
            else
            {
                str.AppendLine(text);
            }
            // var words = page.GetWords(NearestNeighbourWordExtractor.Instance);
        }

        if (!perParagraph)
        {
            yield return str.ToString();
        }
    }

    private static async IAsyncEnumerable<string> ReadMarkdownAsync(this DocFile file,
        bool perParagraph,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        var encoding = await DetectEncoding(file, ct);
        var markdown = await File.ReadAllTextAsync(file.File, encoding, ct);
        ct.ThrowIfCancellationRequested();
        var text = Markdown.ToPlainText(markdown);
        if (perParagraph)
        {
            foreach (var line in text.SplitLines())
            {
                yield return line;
            }
        }
        else
        {
            yield return text;
        }
    }

    private static async IAsyncEnumerable<string> ReadTxtAsync(this DocFile file,
        bool perParagraph,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        var encoding = await DetectEncoding(file, ct);
        if (perParagraph)
        {
            var lines = await File.ReadAllLinesAsync(file.File, encoding, ct);
            foreach (var line in lines)
            {
                ct.ThrowIfCancellationRequested();
                yield return line;
            }
        }
        else
        {
            var text = await File.ReadAllTextAsync(file.File, encoding, ct);
            yield return text;
        }
    }
}