using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem;

public partial class FileReadabilityInfo(FileInfo fileInfo, string topDir) : SimpleFileInfo(fileInfo, topDir)
{
    [ObservableProperty]
    private bool? isReadable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Ratio))]
    private long? bitOneCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Ratio))]
    private long? bitZeroCount;

    public string Ratio
    {
        get
        {
            if (!BitOneCount.HasValue || !BitZeroCount.HasValue)
            {
                return null;
            }

            long count = BitOneCount.Value + BitZeroCount.Value;
            return $"{BitZeroCount.Value / (double)count:P2} : {BitOneCount.Value / (double)count:P2}";
        }
    }
}