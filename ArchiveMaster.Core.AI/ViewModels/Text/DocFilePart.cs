using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public struct DocFilePart
{
    public DocFilePart()
    {
        
    }

    public DocFilePart(string source, string text)
    {
        Source = source;
        Text = text;
    }
    
    public string Source { get; init; }
    
    public string Text { get; init; }
}