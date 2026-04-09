using ArchiveMaster.Models;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem;

public partial class TaggingPhotoFileInfo : ImageFileInfo
{
    [ObservableProperty]
    private bool hasGenerated;

    [ObservableProperty]
    private PhotoTags tags;

    public TaggingPhotoFileInfo(FileInfo file, string topDir) : base(file, topDir)
    {
    }

    public TaggingPhotoFileInfo(FileInfo file, PhotoTags tags, string topDir) : base(file, topDir)
    {
        HasGenerated = true;
        Tags = tags;
    }

    public TaggingPhotoFileInfo(TaggedPhoto tagItem, string topDir) : base(tagItem.RelativePath, topDir)
    {
        HasGenerated = true;
        Tags = tagItem.Tags;
    }
}