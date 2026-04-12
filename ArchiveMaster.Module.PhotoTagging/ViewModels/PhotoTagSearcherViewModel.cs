using System.Collections.ObjectModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class PhotoTagSearcherViewModel(ViewModelServices services)
    : TwoStepViewModelBase<PhotoTagSearcherService, PhotoTagSearcherConfig>(services)
{
    [ObservableProperty]
    private List<TaggingPhotoFileInfo> files;

    [ObservableProperty]
    private bool hasLoaded = false;

    [NotifyPropertyChangedFor(nameof(TagTypes))]
    [ObservableProperty]
    private bool partial = false;

    [ObservableProperty]
    private string searchKeyword = "";

    [NotifyPropertyChangedFor(nameof(Tags))]
    [ObservableProperty]
    private TagType tagType = TagType.All;

    public override bool EnableInitialize => false;
    public TagType[] TagTypes => Partial
        ? Enum.GetValues<TagType>()
        :
        [
            TagType.All,
            TagType.Object,
            TagType.Scene,
            TagType.Mood,
            TagType.Color,
            TagType.Technique
        ];
    private List<TagAndCount> Tags
    {
        get
        {
            if (Service == null)
            {
                return null;
            }

            return TagType switch
            {
                TagType.All => Service.AllTags,
                TagType.Object => Service.ObjectTags,
                TagType.Scene => Service.SceneTags,
                TagType.Color => Service.ColorTags,
                TagType.Mood => Service.MoodTags,
                TagType.Technique => Service.TechniqueTags,
                _ => []
            };
        }
    }
    protected override Task OnExecutedAsync(CancellationToken ct)
    {
        Files = Service.AllFiles;
        OnPropertyChanged(nameof(Tags));
        HasLoaded = true;
        return base.OnExecutedAsync(ct);
    }

    protected override void OnReset()
    {
        Files = null;
        SearchKeyword = "";
        HasLoaded = false;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        Files = await Service.SearchAsync(TagType, SearchKeyword, Partial);
    }
}