using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Services;

namespace ArchiveMaster.ViewModels;

public partial class SettingViewModel : ObservableObject
{
    public const string ConfigExtension = "amcfg";

    public SettingViewModel(ViewModelServices services, AiProvidersViewModel aiProvidersViewModel)
    {
        Services = services;
        AiProvidersViewModel = aiProvidersViewModel;
    }

    public GlobalConfigs Configs => GlobalConfigs.Instance;
    public ViewModelServices Services { get; }

    public AiProvidersViewModel AiProvidersViewModel { get; }

    [RelayCommand]
    private async Task ExportConfigsAsync()
    {
        var path = await Services.StorageProvider.CreatePickerBuilder()
            .AddFilter($"{nameof(ArchiveMaster)}配置", ConfigExtension)
            .SuggestedFileName($"{nameof(ArchiveMaster)}配置（{DateTime.Now:yyyyMMddHHmmss}）")
            .SaveFilePickerAndGetPathAsync();
        if (path != null)
        {
            try
            {
                Services.AppConfig.Save(path);
            }
            catch (Exception ex)
            {
                await Services.Dialog.ShowErrorDialogAsync("导出配置失败", ex);
                return;
            }

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

    public void OnSettingPanelOpened()
    {
        AiProvidersViewModel.ViewLoadedCommand.Execute(null);
    }

    public bool OnSettingPanelClosing()
    {
        if (Configs.DeleteMode == DeleteMode.MoveToSpecialFolder)
        {
            if (string.IsNullOrWhiteSpace(Configs.SpecialDeleteFolderName))
            {
                //故意不用异步
                Services.Dialog.ShowErrorDialogAsync("未指定删除文件夹", "请指定删除文件夹");
                return false;
            }

            if (Path.GetInvalidPathChars().Any(p => Configs.SpecialDeleteFolderName.Contains(p)))
            {
                Services.Dialog.ShowErrorDialogAsync("文件夹名称包含非法字符", "文件夹名称包含非法字符，请修改后再保存",
                    $"非法字符包括：{string.Join(",", Path.GetInvalidPathChars())}");
                return false;
            }
        }
        
        AiProvidersViewModel.ViewUnloadedCommand.Execute(null);

        return true;
    }
}