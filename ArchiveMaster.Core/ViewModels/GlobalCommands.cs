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
        return HostServices.GetRequiredService<IClipboardService>().SetTextAsync(text);
    }

    [RelayCommand]
    private void OpenFile(string path)
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
            Log.Error(ex, "打开文件失败");
        }
    }
}