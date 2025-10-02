using ArchiveMaster.Configs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Application.Startup;

namespace ArchiveMaster.ViewModels;

public partial class SettingViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isAutoStart;

    public SettingViewModel(IStartupManager startupManager = null)
    {
        StartupManager = startupManager;
        isAutoStart = startupManager?.IsStartupEnabled() ?? false;
    }

    public GlobalConfigs Configs => GlobalConfigs.Instance;
    public IStartupManager StartupManager { get; }
    
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