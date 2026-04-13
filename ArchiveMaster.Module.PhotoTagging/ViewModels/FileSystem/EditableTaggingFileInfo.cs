using ArchiveMaster.Models;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem;

public partial class EditableTaggingFileInfo : ImageFileInfo
{
    public ObservablePhotoTags Tags{ get;  }
    
    public event EventHandler TagsChanged;


    public EditableTaggingFileInfo(PhotoTags tags, string relativePath, string topDir) : base(relativePath, topDir)
    {
        Tags = new ObservablePhotoTags(tags);
        Tags.PropertyChanged += (sender, e) => TagsChanged?.Invoke(this, EventArgs.Empty);
    }

    public EditableTaggingFileInfo(string relativePath, string topDir) : base(relativePath, topDir)
    {
        Tags = new ObservablePhotoTags();
        Tags.PropertyChanged += (sender, e) => TagsChanged?.Invoke(this, EventArgs.Empty);
    }
}