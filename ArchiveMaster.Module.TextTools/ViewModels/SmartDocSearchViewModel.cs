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

public partial class SmartDocSearchViewModel(AppConfig appConfig, IDialogService dialogService)
    : AiTwoStepViewModelBase<SmartDocSearchService, SmartDocSearchConfig>(appConfig, dialogService)
{
    [ObservableProperty]
    private string aiConclude = "";

    [ObservableProperty]
    private ObservableCollection<TextSearchResult> searchResults = new ObservableCollection<TextSearchResult>();

    public override bool EnableRepeatExecute => true;

    protected override Task OnExecutedAsync(CancellationToken ct)
    {
        AiConclude = Service.AiConclude;
        return base.OnExecutedAsync(ct);
    }

    protected override Task OnExecutingAsync(CancellationToken ct)
    {
        AiConclude = "";
        Service.AitStreamUpdate += (sender, e) => AiConclude += e.Value;
        return base.OnExecutingAsync(ct);
    }

    protected override Task OnInitializedAsync()
    {
        SearchResults = [..Service.SearchResults];
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        SearchResults.Clear();
        AiConclude = "";
    }
}