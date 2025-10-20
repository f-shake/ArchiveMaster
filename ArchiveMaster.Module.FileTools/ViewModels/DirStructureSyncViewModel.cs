using System.Collections.ObjectModel;
using System.ComponentModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib;
using FzLib.Avalonia.Dialogs;
using FzLib.Programming;

namespace ArchiveMaster.ViewModels;

public partial class DirStructureSyncViewModel(ViewModelServices services)
    : TwoStepViewModelBase<DirStructureSyncService, DirStructureSyncConfig>(services)
{
    [ObservableProperty]
    private int checkedFilesCount = 0;

    [ObservableProperty]
    private bool displayMultipleMatches = true;

    [ObservableProperty]
    private bool displayRightPosition = false;

    [ObservableProperty]
    private ObservableCollection<FileSystem.MatchingFileInfo> files;

    [ObservableProperty]
    private int filesCount = 0;

    protected override Task OnInitializedAsync()
    {
        UpdateList();
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        Files = null;
        FilesCount = 0;
        CheckedFilesCount = 0;
    }

    partial void OnDisplayMultipleMatchesChanged(bool value)
    {
        UpdateList();
    }

    partial void OnDisplayRightPositionChanged(bool value)
    {
        UpdateList();
    }

    [RelayCommand]
    private void SelectAll()
    {
        Files?.ForEach(p => p.IsChecked = true);
    }

    [RelayCommand]
    private void SelectNone()
    {
        Files?.ForEach(p => p.IsChecked = true);
    }

    private void UpdateList()
    {
        if (Service == null)
        {
            return;
        }

        if (Service.WrongPositionFiles == null || Service.RightPositionFiles == null)
        {
            Files = new ObservableCollection<FileSystem.MatchingFileInfo>();
            return;
        }

        IEnumerable<FileSystem.MatchingFileInfo> files = Service.WrongPositionFiles;
        if (DisplayRightPosition)
        {
            files = files.Concat(Service.RightPositionFiles);
        }

        if (!DisplayMultipleMatches)
        {
            files = files.Where(p => p.MultipleMatches == false);
        }

        files = files.OrderBy(p => p.Path);
        Files = new ObservableCollection<FileSystem.MatchingFileInfo>(files);
        Service.ExecutingFiles = Files;
        FilesCount = Files.Count;
        CheckedFilesCount = Files.Count(p => p.IsChecked);
    }
}