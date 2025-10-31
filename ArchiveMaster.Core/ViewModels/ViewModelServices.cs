using ArchiveMaster.Configs;
using FzLib.Avalonia.Controls;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Services;

namespace ArchiveMaster.ViewModels;

public class ViewModelServices(
    IDialogService dialog,
    IStorageProviderService storageProvider,
    IProgressOverlayService progressOverlay,
    IClipboardService clipboard,
    AppConfig appConfig)
{
    public IDialogService Dialog { get; } = dialog;
    public IStorageProviderService StorageProvider { get; } = storageProvider;
    public IProgressOverlayService ProgressOverlay { get; } = progressOverlay;
    public IClipboardService Clipboard { get; } = clipboard;
    public AppConfig AppConfig { get; } = appConfig;
}