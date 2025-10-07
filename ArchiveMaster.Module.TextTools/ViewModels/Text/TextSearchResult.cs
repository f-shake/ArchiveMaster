using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class TextSearchResult : ObservableObject
{
    private string context;

    [ObservableProperty]
    private int contextEndIndex;

    [ObservableProperty]
    private int contextStartIndex;

    [ObservableProperty]
    private List<int> indexes;

    private IEnumerable<InlineItem> inlines;

    [ObservableProperty]
    private HashSet<string> keywords = new HashSet<string>();

    [ObservableProperty]
    private string source;
    [ObservableProperty]
    private string sourceParagraph;

    public string Context =>
        context ?? throw new InvalidOperationException($"请先调用{nameof(GenerateContext)}方法");

    public IEnumerable<InlineItem> Inlines =>
        inlines ?? throw new InvalidOperationException($"请先调用{nameof(GenerateContext)}方法");

    public string KeyWordsString => string.Join("、", Keywords);
    public void GenerateContext()
    {
        int start = Math.Max(0, ContextStartIndex);
        int end = Math.Min(ContextEndIndex, SourceParagraph.Length);
        context = SourceParagraph.Substring(start, end - start);

        if (string.IsNullOrEmpty(Context) || Keywords?.Count is null or 0)
        {
            // 如果没有关键词，返回普通文本
            inlines = [new InlineItem { Text = Context }];
        }
        else
        {
            var result = new List<InlineItem>();
            var remainingText = Context;
            var sortedKeywords = Keywords
                .Where(k => !string.IsNullOrEmpty(k))
                .OrderByDescending(k => k.Length)
                .ToList();

            while (!string.IsNullOrEmpty(remainingText))
            {
                var foundKeyword = sortedKeywords
                    .FirstOrDefault(k => remainingText.StartsWith(k));

                if (foundKeyword != null)
                {
                    // 添加加粗的关键词
                    result.Add(new InlineItem
                    {
                        Text = foundKeyword,
                        IsBold = true,
                        Foreground = Brushes.Red
                    });
                    remainingText = remainingText[foundKeyword.Length..];
                }
                else
                {
                    // 添加普通文本
                    var nextKeywordPositions = sortedKeywords
                        .Select(k => remainingText.IndexOf(k, StringComparison.Ordinal))
                        .Where(pos => pos > 0)
                        .ToList();

                    var nextPos = nextKeywordPositions.Any()
                        ? nextKeywordPositions.Min()
                        : remainingText.Length;

                    result.Add(new InlineItem
                    {
                        Text = remainingText[..nextPos]
                    });
                    remainingText = remainingText[nextPos..];
                }
            }

            inlines = result;
        }
    }
}