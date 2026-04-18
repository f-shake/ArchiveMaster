using System.Collections.ObjectModel;
using System.ComponentModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class VideoInfoViewModel(ViewModelServices services)
    : TwoStepViewModelBase<VideoInfoService, VideoInfoConfig>(services)
{
    [ObservableProperty]
    private ObservableCollection<VideoInfoFileInfo> files;

    protected override Task OnInitializedAsync()
    {
        Files = new ObservableCollection<VideoInfoFileInfo>(Service.Files);
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        base.OnReset();
        Files = null;
    }
}