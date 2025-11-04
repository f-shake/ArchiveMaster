using System;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Application.Startup;
using FzLib.Avalonia.Services;

namespace ArchiveMaster.ViewModels;

public partial class SettingViewModel : ObservableObject
{
    public const string ConfigExtension = "amcfg";

    [ObservableProperty]
    private bool isAutoStart;

    public SettingViewModel(ViewModelServices services, IStartupManager startupManager = null)
    {
        Services = services;
        StartupManager = startupManager;
        isAutoStart = startupManager?.IsStartupEnabled() ?? false;
    }

    public GlobalConfigs Configs => GlobalConfigs.Instance;
    public ViewModelServices Services { get; }
    public IStartupManager StartupManager { get; }

    [RelayCommand]
    private async Task ExportConfigsAsync()
    {
        var path = await Services.StorageProvider.CreatePickerBuilder()
            .AddFilter($"{nameof(ArchiveMaster)}配置", ConfigExtension)
            .SuggestedFileName($"{nameof(ArchiveMaster)}配置（{DateTime.Now:yyyyMMddHHmmss}）")
            .SaveFilePickerAndGetPathAsync();
        if (path != null)
        {
            Services.AppConfig.Save(path);
            await Services.Dialog.ShowOkDialogAsync($"配置已导出", $"配置已导出到{path}");
        }
    }

    [RelayCommand]
    private async Task ImportConfigsAsync()
    {
        var path = await Services.StorageProvider.CreatePickerBuilder()
            .AddFilter($"{nameof(ArchiveMaster)}配置", ConfigExtension)
            .OpenFilePickerAndGetFirstAsync();
        if (path != null)
        {
            try
            {
                Services.AppConfig.LoadNextStart(path);
                await Services.Dialog.ShowOkDialogAsync("配置已导入", "配置已导入，下次启动时生效");
            }
            catch (Exception ex)
            {
                await Services.Dialog.ShowErrorDialogAsync($"导入配置文件失败", ex);
            }
        }
    }


    [RelayCommand]
    private void SetAutoStart(bool autoStart)
    {
        if (StartupManager == null)
        {
            return;
        }

        if (autoStart)
        {
            StartupManager.EnableStartup("s");
        }
        else
        {
            StartupManager.DisableStartup();
        }
    }
}