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
    [NotifyPropertyChangedFor(nameof(Tags))]
    [ObservableProperty]
    private bool enableColorTags = true;

    [NotifyPropertyChangedFor(nameof(Tags))]
    [ObservableProperty]
    private bool enableDescriptionTags = true;

    [NotifyPropertyChangedFor(nameof(Tags))]
    [ObservableProperty]
    private bool enableMoodTags = true;

    [NotifyPropertyChangedFor(nameof(Tags))]
    [ObservableProperty]
    private bool enableObjectTags = true;

    [NotifyPropertyChangedFor(nameof(Tags))]
    [ObservableProperty]
    private bool enableSceneTags = true;

    [NotifyPropertyChangedFor(nameof(Tags))]
    [ObservableProperty]
    private bool enableTechniqueTags = true;

    [NotifyPropertyChangedFor(nameof(Tags))]
    [ObservableProperty]
    private bool enableTextTags = true;

    [ObservableProperty]
    private List<TaggingPhotoFileInfo> files;

    [ObservableProperty]
    private bool hasLoaded = false;

    [ObservableProperty]
    private bool partial = true;

    [ObservableProperty]
    private string searchKeyword = "";

    public override bool EnableInitialize => false;

    private List<TagAndCount> Tags
    {
        get
        {
            if (Service == null)
            {
                return null;
            }

            List<List<TagAndCount>> tags = new List<List<TagAndCount>>();

            if (EnableObjectTags)
            {
                tags.Add(Service.ObjectTags);
            }

            if (EnableSceneTags)
            {
                tags.Add(Service.SceneTags);
            }

            if (EnableMoodTags)
            {
                tags.Add(Service.MoodTags);
            }

            if (EnableColorTags)
            {
                tags.Add(Service.ColorTags);
            }

            if (EnableTechniqueTags)
            {
                tags.Add(Service.TechniqueTags);
            }

            return tags.SelectMany(p => p).ToList();
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

    partial void OnPartialChanged(bool value)
    {
        if (!value)
        {
            EnableTextTags = false;
            EnableDescriptionTags = false;
        }
    }
    [RelayCommand]
    private async Task SearchAsync()
    {
        var tagType = TagType.None;
        if (EnableObjectTags)
        {
            tagType |= TagType.Object;
        }

        if (EnableSceneTags)
        {
            tagType |= TagType.Scene;
        }

        if (EnableMoodTags)
        {
            tagType |= TagType.Mood;
        }

        if (EnableColorTags)
        {
            tagType |= TagType.Color;
        }

        if (EnableTechniqueTags)
        {
            tagType |= TagType.Technique;
        }

        if (EnableTextTags)
        {
            tagType |= TagType.Text;
        }

        if (EnableDescriptionTags)
        {
            tagType |= TagType.Description;
        }

        Files = await Service.SearchAsync(tagType, SearchKeyword, Partial);
    }
}