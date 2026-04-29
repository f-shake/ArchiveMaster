using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Services;
using Serilog;

namespace ArchiveMaster.ViewModels;

public partial class GlobalCommands(IClipboardService clipboardService, IDialogService dialogService) : ObservableObject
{
    public static GlobalCommands Instance { get; } = HostServices.GetRequiredService<GlobalCommands>();

    [RelayCommand]
    private Task CopyTextAsync(string text)
    {
        return clipboardService.SetTextAsync(text);
    }

    [RelayCommand]
    private async Task OpenFileAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (File.Exists(path) || Directory.Exists(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                await dialogService.ShowErrorDialogAsync("打开文件失败", ex);
                Log.Error(ex, "打开文件失败");
            }
        }
        else
        {
            await dialogService.ShowErrorDialogAsync("打开文件失败", $"文件或目录{path}不存在");
        }
    }

    [RelayCommand]
    private async Task OpenParentDirAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (File.Exists(path) || Directory.Exists(path))
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe")
                    {
                        Arguments = $"/select, \"{path}\""
                    });
                }
                catch (Exception ex)
                {
                    await dialogService.ShowErrorDialogAsync("打开文件失败", ex);
                    Log.Error(ex, "打开文件失败");
                }

                return;
            }

            path = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(path))
            {
                await dialogService.ShowErrorDialogAsync("打开文件失败", "文件路径无效");
            }

            try
            {
                Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                await dialogService.ShowErrorDialogAsync("打开文件失败", ex);
                Log.Error(ex, "打开文件失败");
            }
        }
        else
        {
            await dialogService.ShowErrorDialogAsync("打开文件失败", $"文件或目录{path}不存在");
        }
    }
}