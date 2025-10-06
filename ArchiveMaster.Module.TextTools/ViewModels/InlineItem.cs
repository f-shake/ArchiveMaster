using Avalonia.Media;

namespace ArchiveMaster.ViewModels;

public class InlineItem
{
    public InlineItem()
    {
    }

    public InlineItem(string text, bool isBold = false, IBrush foreground = null)
    {
        Text = text;
        IsBold = isBold;
        Foreground = foreground;
    }

    public string Text { get; set; }
    public bool IsBold { get; set; }
    public IBrush Foreground { get; set; }
}