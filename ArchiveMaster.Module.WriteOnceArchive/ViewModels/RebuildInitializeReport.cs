using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class RebuildInitializeReport : ObservableObject
{
    public int TotalFileCount { get; set; }
    public long TotalFileLength { get; set; }
    public int MatchedFileCount { get; set; }
    public long MatchedFileLength { get; set; }
    public double MatchCountPercent => 1.0 * MatchedFileCount / TotalFileCount;
    public double MatchLengthPercent => 1.0 * MatchedFileLength / TotalFileLength;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MeanReadSpeedBytePerSecond))]
    private double? totalReadTimeCostSecond;

    public double? MeanReadSpeedBytePerSecond =>
        TotalReadTimeCostSecond.HasValue ? MatchedFileLength / TotalReadTimeCostSecond : null;
}