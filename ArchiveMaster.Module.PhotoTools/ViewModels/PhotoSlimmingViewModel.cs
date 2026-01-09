using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Cryptography;
using Mapster;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster.ViewModels;

public partial class PhotoSlimmingViewModel(ViewModelServices services)
    : TwoStepViewModelBase<PhotoSlimmingService, PhotoSlimmingConfig>(services)
{
    [ObservableProperty]
    private bool canCancel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeletingFilesCount),
        nameof(CopyingFilesCount),
        nameof(CompressingFilesCount),
        nameof(SkippingFilesCount),
        nameof(CopyingFilesLength),
        nameof(CompressingFilesLength))]
    private ObservableCollection<SlimmingFilesInfo> files;

    public int DeletingFilesCount => Files?.Count(p => p.SlimmingTaskType == SlimmingTaskType.Delete) ?? 0;
    public int CopyingFilesCount => Files?.Count(p => p.SlimmingTaskType == SlimmingTaskType.Copy) ?? 0;
    public int CompressingFilesCount => Files?.Count(p => p.SlimmingTaskType == SlimmingTaskType.Compress) ?? 0;
    public int SkippingFilesCount => Files?.Count(p => p.SlimmingTaskType == SlimmingTaskType.Skip) ?? 0;

    public long CopyingFilesLength =>
        Files?.Where(p => p.SlimmingTaskType == SlimmingTaskType.Copy)?.Select(p => p.Length)?.Sum() ?? 0;

    public long CompressingFilesLength =>
        Files?.Where(p => p.SlimmingTaskType == SlimmingTaskType.Compress)?.Select(p => p.Length)?.Sum() ?? 0;

    public List<MagickFormat> SupportedImageFormats { get; } = MagickNET.SupportedFormats
        .Where(p => p.SupportsReading)
        .Where(p => p.SupportsWriting)
        .Select(p => p.Format)
        .ToList();

    protected override Task OnInitializedAsync()
    {
        Files = new ObservableCollection<SlimmingFilesInfo>(Service.Files);
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        Files = null;
    }

    [RelayCommand]
    private void SelectCompressFormat(MagickFormat format)
    {
        Config.CompressImageFormat = format;
    }
}