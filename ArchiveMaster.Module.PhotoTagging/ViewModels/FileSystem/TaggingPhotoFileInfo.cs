using ArchiveMaster.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem;

public partial class TaggingPhotoFileInfo : SimpleFileInfo
{
    public TaggingPhotoFileInfo(FileInfo file, string topDir) : base(file, topDir)
    {
    }

    [ObservableProperty]
    private bool hasGenerated;

    [ObservableProperty]
    public List<TagInfo> tags;
}