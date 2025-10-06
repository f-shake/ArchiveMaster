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
    private ObservableCollection<TextSearchResult> searchResults = new ObservableCollection<TextSearchResult>();

    [ObservableProperty]
    private string aiConclude = "";

    protected override Task OnExecutingAsync(CancellationToken ct)
    {
        Service.AitStreamUpdate += (sender, e) => AiConclude += e.Text;
        Service.SearchResultsUpdate += (sender, e) => SearchResults.Add(e.SearchResult);
        return base.OnExecutingAsync(ct);
    }

    protected override Task OnExecutedAsync(CancellationToken ct)
    {
        SearchResults = [..Service.SearchResults];
        AiConclude = Service.AiConclude;
        return base.OnExecutedAsync(ct);
    }

    protected override void OnReset()
    {
        SearchResults.Clear();
        AiConclude = "";
    }
}