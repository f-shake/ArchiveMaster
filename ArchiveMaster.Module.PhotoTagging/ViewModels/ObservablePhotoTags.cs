using ArchiveMaster.Models;
using FzLib.Text;

namespace ArchiveMaster.ViewModels;

public class ObservablePhotoTags
{
    public ObservablePhotoTags()
    {
        
    }

    public ObservablePhotoTags(PhotoTags tags)
    {
        ObjectTags = new ObservableStringList(tags.ObjectTags);
        SceneTags = new ObservableStringList(tags.SceneTags);
        MoodTags = new ObservableStringList(tags.MoodTags);
        ColorTags = new ObservableStringList(tags.ColorTags);
        TechniqueTags = new ObservableStringList(tags.TechniqueTags);
        TextTags = new ObservableStringList(tags.TextTags);
        Description = tags.Description;
    }
    
    public ObservableStringList ObjectTags { get; init; } = new();
    public ObservableStringList SceneTags { get; init; } = new();
    public ObservableStringList MoodTags { get; init; } = new();
    public ObservableStringList ColorTags { get; init; } = new();
    public ObservableStringList TechniqueTags { get; init; } = new();
    public ObservableStringList TextTags { get; init; } = new();
    public string Description { get; set; }

    
    public PhotoTags ToPhotoTags()
    {
        return new PhotoTags(
            ObjectTags.Trimmed.ToList(),
            SceneTags.Trimmed.ToList(),
            MoodTags.Trimmed.ToList(),
            ColorTags.Trimmed.ToList(),
            TechniqueTags.Trimmed.ToList(),
            TextTags.Trimmed.ToList(),
            Description);
    }
}