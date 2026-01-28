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

public partial class SmartDocSearchViewModel(ViewModelServices services)
    : AiChatViewModelBase<SmartDocSearchService, SmartDocSearchConfig>(services)
{
    [ObservableProperty]
    private List<TextSearchResult> searchResults;

    [RelayCommand]
    private void Reset()
    {
        SearchResults = null;
        Service.Reset();
        AiConversation.Reset();
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task SearchAsync(CancellationToken ct)
    {
        return Services.ProgressOverlay.WithOverlayAsync(
            async () =>
            {
                await Service.SearchAsync(ct);
                SearchResults = Service.SearchResults;
            },
            () =>
            {
                SearchCancelCommand.Execute(null);
                Services.ProgressOverlay.SetVisible(false);
                return Task.CompletedTask;
            },
            async ex =>
            {
                if (ex is OperationCanceledException)
                {
                    return;
                }

                await Services.Dialog.ShowErrorDialogAsync("搜索失败", ex);
            }, "正在搜索");
    }
}