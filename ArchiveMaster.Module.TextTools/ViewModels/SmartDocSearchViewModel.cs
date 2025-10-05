using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Cryptography;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class SmartDocSearchViewModel(AppConfig appConfig, IDialogService dialogService)
    : TwoStepViewModelBase<SmartDocSearchService, SmartDocSearchConfig>(appConfig, dialogService)
{
    protected override async Task OnInitializingAsync()
    {
        //Test
        var text = await Config.Source.GetTextAsync();
    }

    protected override void OnReset()
    {
    }
}