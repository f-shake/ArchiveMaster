using ArchiveMaster.Models;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem;

public partial class TaggingPhotoFileInfo(FileInfo file, string topDir) : ImageFileInfo(file, topDir)
{
    [ObservableProperty]
    private List<TagInfo> tags;

    [ObservableProperty]
    private bool hasGenerated;

    public PhotoTag ToPhotoTag() => new PhotoTag(RelativePath, Tags);
    
}