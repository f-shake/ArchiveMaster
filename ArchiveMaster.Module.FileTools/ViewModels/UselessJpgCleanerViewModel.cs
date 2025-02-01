﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.ViewModels;

public partial class TwinFileCleanerViewModel(AppConfig appConfig)
    : TwoStepViewModelBase<TwinFileCleanerService, TwinFileCleanerConfig>(appConfig)
{
    [ObservableProperty]
    private List<SimpleFileInfo> deletingJpgFiles;

    protected override Task OnInitializedAsync()
    {
        DeletingJpgFiles = Service.DeletingJpgFiles;
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        DeletingJpgFiles = null;
    }
}