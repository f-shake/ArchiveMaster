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
    private AiProviderConfigViewModel selectedProvider;

    [ObservableProperty]
    private ObservableCollection<AiProviderConfigViewModel> providers = new();

    public AiProvidersConfig Config { get; } = services.AppConfig.GetOrCreateConfigWithDefaultKey<AiProvidersConfig>();

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

    public override void OnEnter()
    {
        base.OnEnter();
        //转换到ViewModel的配置
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

    public override Task OnExitAsync(CancelEventArgs args)
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

        return base.OnExitAsync(args);
    }

    [RelayCommand]
    private void MakeCurrent()
    {
        foreach (var (index, vmProvider) in Providers.Index())
        {
            if (vmProvider == SelectedProvider)
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