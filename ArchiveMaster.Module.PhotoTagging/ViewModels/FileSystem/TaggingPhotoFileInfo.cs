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
    
    public TaggingPhotoFileInfo(PhotoTagItem tagItem, string topDir) 
        : base(new FileInfo(System.IO.Path.Combine(topDir,tagItem.RelativePath)), topDir)
    {
        HasGenerated = true;
        Tags = tagItem.Tags;
    }
}