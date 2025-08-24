using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;


public record FileCountLength(int Count, long TotalLength);

public partial class RebuildInitializeReport : ObservableObject
{
    [ObservableProperty]
    private FileCountLength totalFiles;

    [ObservableProperty]
    private FileCountLength packageFiles;

    [ObservableProperty]
    private FileCountLength foundPhysicalFiles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MeanReadSpeedBytePerSecond))]
    private double? totalReadTimeCostSecond;

    public double? MeanReadSpeedBytePerSecond =>
        TotalReadTimeCostSecond.HasValue ? FoundPhysicalFiles.TotalLength / TotalReadTimeCostSecond : null;

    [ObservableProperty]
    private DateTime packageTime;

    [ObservableProperty]
    private FileCountLength lostFiles;
    
    [ObservableProperty]
    private FileCountLength unreferencedFiles;
}