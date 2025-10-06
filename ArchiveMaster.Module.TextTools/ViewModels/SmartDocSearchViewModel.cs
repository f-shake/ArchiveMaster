using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Cryptography;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class SmartDocSearchViewModel(AppConfig appConfig, IDialogService dialogService)
    : TwoStepViewModelBase<SmartDocSearchService, SmartDocSearchConfig>(appConfig, dialogService)
{
    public override bool EnableInitialize => false;

    [ObservableProperty]
    private ObservableCollection<TextPartResult> searchResults = new ObservableCollection<TextPartResult>();

    protected override Task OnInitializedAsync()
    {
        SearchResults = [..Service.SearchResults];
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
    }
}