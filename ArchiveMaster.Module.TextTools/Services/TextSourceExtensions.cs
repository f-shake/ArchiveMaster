using System.Runtime.CompilerServices;
using System.Text;
using ArchiveMaster.Enums;
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
    extension(TextSource source)
    {
        public async Task<string> GetCombinedPlainTextAsync(CancellationToken ct = default)
        {
            return (await source.GetPlainTextAsync(TextSourceReadUnit.Combined, ct).FirstOrDefaultAsync()).Text;
        }

        public async IAsyncEnumerable<DocFilePart> GetPlainTextAsync(TextSourceReadUnit readUnit = TextSourceReadUnit.PerFile,
            [EnumeratorCancellation]
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(source);

            StringBuilder combined = new StringBuilder();
            if (source.FromFile)
            {
                await foreach (var item in source.Files.GetPlainTextAsync(readUnit, ct))
                {
                    yield return item;
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
    }
}