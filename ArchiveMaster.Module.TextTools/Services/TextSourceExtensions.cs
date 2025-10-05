using System.Text;
using ArchiveMaster.ViewModels;
using NPOI.XWPF.UserModel;

namespace ArchiveMaster.Services;

public static class TextSourceExtensions
{
    public static async Task<string> GetTextAsync(this TextSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.FromFile)
        {
            StringBuilder str = new StringBuilder();
            foreach (var file in source.Files)
            {
                await file.ReadTextAsync(str);
            }

            return str.ToString();
        }
        else
        {
            return source.Text;
        }
    }

    private static async Task ReadTextAsync(this DocFile file, StringBuilder text)
    {
        await using var stream = File.OpenRead(file.File);
        XWPFDocument doc = new XWPFDocument(stream);
        foreach (var para in doc.Paragraphs)
        {
            string v = para.ParagraphText; //获得文本
            if (!string.IsNullOrEmpty(v))
            {
                text.AppendLine(v);
            }
        }
    }
}