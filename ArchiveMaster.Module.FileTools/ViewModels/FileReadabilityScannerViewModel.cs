using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class FileReadabilityScannerViewModel(ViewModelServices services)
    : TwoStepViewModelBase<FileReadabilityScannerService, FileReadabilityScannerConfig>(services)
{
    [ObservableProperty]
    private List<FileReadabilityInfo> files;

    public static IValueConverter FileReadableBrushConverter =>
        new FuncValueConverter<bool?, IBrush>(b =>
            b.HasValue ? (b.Value ? Brushes.Green : Brushes.Red) : Brushes.Transparent);

    protected override Task OnInitializedAsync()
    {
        Files = Service.Files;
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        Files = null;
    }
}