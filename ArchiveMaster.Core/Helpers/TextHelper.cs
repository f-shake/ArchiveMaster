using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Services;

namespace ArchiveMaster.Helpers;

public partial class TextHelper(IClipboardService clipboardService) : ObservableObject
{
    private readonly IClipboardService ClipboardService = clipboardService;

    public static TextHelper Instance { get; } = HostServices.GetRequiredService<TextHelper>();

    [RelayCommand]
    private Task CopyTextAsync(string text)
    {
        return ClipboardService.SetTextAsync(text);
    }
}