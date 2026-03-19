using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class InlineItem : ObservableObject
{
    public InlineItem()
    {
    }

    public InlineItem(string text, bool isBold = false, IBrush foreground = null, bool isItalic = false,
        int fontSize = 0)
    {
        Text = text;
        IsBold = isBold;
        Foreground = foreground;
        IsItalic = isItalic;
        FontSize = fontSize;
    }

    [ObservableProperty]
    private string text;

    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public int FontSize { get; set; }
    public IBrush Foreground { get; set; }
    
    public static implicit operator InlineItem( string message)
    {
        return new InlineItem(message);
    }
}