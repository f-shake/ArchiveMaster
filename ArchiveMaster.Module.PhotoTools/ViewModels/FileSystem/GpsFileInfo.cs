using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem;

public partial class GpsFileInfo : SimpleFileInfo
{
    public GpsFileInfo(FileInfo file, string topDir) : base(file, topDir)
    {
    }

    [ObservableProperty]
    private double? longitude;

    [ObservableProperty]
    private double? latitude;

    [ObservableProperty]
    private DateTime? exifTime;

    [ObservableProperty]
    private DateTime? gpsTime;
    
    [ObservableProperty]
    private bool alreadyHasGps;

    [ObservableProperty]
    private string region;
}