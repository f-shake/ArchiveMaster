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
}