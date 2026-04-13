using ArchiveMaster.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.Text;

namespace ArchiveMaster.ViewModels;

public partial class ObservablePhotoTags : ObservableObject
{
    public ObservablePhotoTags()
    {
        SetOnCollectionChanged();
    }

    public ObservablePhotoTags(PhotoTags tags)
    {
        ObjectTags = new ObservableStringList(tags.ObjectTags);
        SceneTags = new ObservableStringList(tags.SceneTags);
        MoodTags = new ObservableStringList(tags.MoodTags);
        ColorTags = new ObservableStringList(tags.ColorTags);
        TechniqueTags = new ObservableStringList(tags.TechniqueTags);
        OcrText = tags.OcrText;
        Description = tags.Description;
        SetOnCollectionChanged();
    }

    private void SetOnCollectionChanged()
    {
        ObjectTags.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(Count));
        SceneTags.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(Count));
        MoodTags.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(Count));
        ColorTags.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(Count));
        TechniqueTags.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(Count));
    }

    public ObservableStringList ObjectTags { get; } = new();
    public ObservableStringList SceneTags { get; } = new();
    public ObservableStringList MoodTags { get; } = new();
    public ObservableStringList ColorTags { get; } = new();
    public ObservableStringList TechniqueTags { get; } = new();

    [ObservableProperty]
    private string ocrText;
    
    [ObservableProperty]
    private string description;

    public int Count => ObjectTags.Trimmed.Count()
                        + SceneTags.Trimmed.Count()
                        + MoodTags.Trimmed.Count()
                        + ColorTags.Trimmed.Count()
                        + TechniqueTags.Trimmed.Count();

    public PhotoTags ToPhotoTags()
    {
        return new PhotoTags(
            ObjectTags.Trimmed.Distinct().ToList(),
            SceneTags.Trimmed.Distinct().ToList(),
            MoodTags.Trimmed.Distinct().ToList(),
            ColorTags.Trimmed.Distinct().ToList(),
            TechniqueTags.Trimmed.Distinct().ToList(),
            OcrText,
            Description);
    }
}