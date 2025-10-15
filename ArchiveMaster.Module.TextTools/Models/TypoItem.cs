namespace ArchiveMaster.Models;
public class TypoItem : ICheckItem
{
    public string Context { get; init; }
    public string Original { get; init; }
    public string Corrected { get; init; }
    public string FixedSegment { get; init; }
    public string Explanation { get; init; }
}
