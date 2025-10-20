using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Cryptography;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;
using Serilog;

namespace ArchiveMaster.ViewModels;

public partial class AiProvidersViewModel(ViewModelServices services)
    : ViewModelBase(services)
{
    [ObservableProperty]
    private AiProviderConfig selectedProvider;

    public AiProvidersConfig Config { get; } = services.AppConfig.GetOrCreateConfigWithDefaultKey<AiProvidersConfig>();
    
    [RelayCommand]
    private void AddProvider()
    {
        var config = new AiProviderConfig();
        Config.Providers.Add(config);
        SelectedProvider = config;
    }

    [RelayCommand]
    private void DeleteSelectedProvider()
    {
        if (SelectedProvider != null)
        {
            Config.Providers.Remove(SelectedProvider);
            if (Config.Providers.Count > 0)
            {
                SelectedProvider = Config.Providers[0];
            }
        }
    }
}