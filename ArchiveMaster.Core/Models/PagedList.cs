namespace ArchiveMaster.Models;

public class PagedList<T>
{
    public PagedList(IList<T> items, int pageIndex, int pageSize, int totalCount)
    {
        Items = items;
        PageIndex = pageIndex;
        PageSize = pageSize;
        TotalCount = totalCount;
        PageCount = (int)Math.Ceiling(1.0 * totalCount / pageSize);
    }

    public int PageIndex { get; }
    
    public int PageSize { get; }
    
    public int TotalCount { get; }
    
    public IList<T> Items { get; }

    public int PageCount { get; }
}