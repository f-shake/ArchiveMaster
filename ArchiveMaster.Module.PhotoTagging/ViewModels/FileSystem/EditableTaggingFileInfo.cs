using ArchiveMaster.Models;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem;

public partial class EditableTaggingFileInfo : ImageFileInfo
{
    [ObservableProperty]
    private ObservablePhotoTags tags;


    public EditableTaggingFileInfo(PhotoTags tags, string relativePath, string topDir) : base(relativePath, topDir)
    {
        Tags = new ObservablePhotoTags(tags);
    }

    public EditableTaggingFileInfo(string relativePath, string topDir) : base(relativePath, topDir)
    {
        Tags = new ObservablePhotoTags();
    }
}