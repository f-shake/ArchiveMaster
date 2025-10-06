using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class TextPartResult : ObservableObject
{
    [ObservableProperty]
    private string pattern;

    [ObservableProperty]
    private string sourceParagraph;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Context))]
    private int index;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Context))]
    private int contextStartIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Context))]
    private int contextEndIndex;

    public string Context
    {
        get
        {
            int start = Math.Max(0, ContextStartIndex);
            int end = Math.Min(ContextEndIndex, SourceParagraph.Length);
            return SourceParagraph.Substring(start, end - start);
        }
    }
}