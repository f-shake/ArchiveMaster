using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Cryptography;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;
using Serilog;

namespace ArchiveMaster.ViewModels;

public partial class AiProvidersViewModel : ObservableObject
{
    [ObservableProperty] private AiProviderConfigViewModel selectedProvider;

    [ObservableProperty] private ObservableCollection<AiProviderConfigViewModel> providers = new();

    public AiProvidersViewModel(ViewModelServices services)
    {
    }

    public AiProvidersConfig Config { get; } = GlobalConfigs.Instance.AiProviders;

    [RelayCommand]
    private void AddProvider()
    {
        var config = new AiProviderConfigViewModel();
        Providers.Add(config);
        SelectedProvider = config;
    }

    [RelayCommand]
    private void DeleteSelectedProvider()
    {
        if (SelectedProvider != null)
        {
            Providers.Remove(SelectedProvider);
            if (Providers.Count > 0)
            {
                SelectedProvider = Providers[0];
            }
        }
    }

    [RelayCommand]
    private void ViewLoaded()
    {
        Providers.Clear();
        foreach (var (index, provider) in Config.Providers.Index())
        {
            var vmProvider = AiProviderConfigViewModel.FromConfig(provider);
            if (index == Config.CurrentProviderIndex)
            {
                vmProvider.IsDefault = true;
            }

            Providers.Add(vmProvider);
        }

        //确保至少有一个Provider
        if (Providers.Count == 0)
        {
            AddProvider();
        }

        //设置默认值
        if (Providers.All(p => !p.IsDefault))
        {
            Providers[0].IsDefault = true;
        }

        //选中默认配置
        SelectedProvider = Providers.First(p => p.IsDefault);
    }

    [RelayCommand]
    private void ViewUnloaded()
    {
        Config.Providers.Clear();
        foreach (var (index, vmProvider) in Providers.Index())
        {
            Config.Providers.Add(vmProvider.ToConfig());
            if (vmProvider.IsDefault)
            {
                Config.CurrentProviderIndex = index;
            }
        }
    }

    [RelayCommand]
    private void MakeCurrent(AiProviderConfigViewModel provider)
    {
        foreach (var (index, vmProvider) in Providers.Index())
        {
            if (vmProvider == provider)
            {
                Config.CurrentProviderIndex = index;
                vmProvider.IsDefault = true;
            }
            else
            {
                vmProvider.IsDefault = false;
            }
        }
    }
}