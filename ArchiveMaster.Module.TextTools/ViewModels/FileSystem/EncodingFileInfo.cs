using CommunityToolkit.Mvvm.ComponentModel;
using UtfUnknown;

namespace ArchiveMaster.ViewModels.FileSystem;

public partial class EncodingFileInfo : SimpleFileInfo
{
    [ObservableProperty]
    private IList<DetectionDetail> details;

    [ObservableProperty]
    private DetectionDetail encoding;

    public EncodingFileInfo(FileInfo file, string topDir) : base(file, topDir)
    {
    }
}