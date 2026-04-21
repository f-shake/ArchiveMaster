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

    [ObservableProperty]
    private VideoInfoFileInfo selectedFile;

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

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var file = await Services.StorageProvider.CreatePickerBuilder()
            .AddFilter("CSV表格", "csv")
            .SuggestedFileName("视频清单.csv")
            .SaveFilePickerAndGetPathAsync();
        if (file is null)
        {
            return;
        }

        await Services.ProgressOverlay.WithOverlayAsync(async () => { await Service.ExportCsvAsync(file); },
            async ex => { await Services.Dialog.ShowErrorDialogAsync("导出CSV失败", ex); }, "正在导出CSV表格");
    }
}