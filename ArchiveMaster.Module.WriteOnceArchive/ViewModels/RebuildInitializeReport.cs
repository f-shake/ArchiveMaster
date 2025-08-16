namespace ArchiveMaster.ViewModels;

public class RebuildInitializeReport
{
    public int TotalFileCount { get; set; }
    public long TotalFileLength { get; set; }
    public int MatchedFileCount { get; set; }
    public long MatchedFileLength { get; set; }
    public double MatchCountPercent => 1.0 * MatchedFileCount / TotalFileCount;
    public double MatchLengthPercent => 1.0 * MatchedFileLength / TotalFileLength;
}