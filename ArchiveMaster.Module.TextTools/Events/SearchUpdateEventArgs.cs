using ArchiveMaster.ViewModels;

namespace ArchiveMaster.Events;

public class SearchUpdateEventArgs(TextSearchResult searchResult) : EventArgs
{
    public TextSearchResult SearchResult { get; } = searchResult;
}