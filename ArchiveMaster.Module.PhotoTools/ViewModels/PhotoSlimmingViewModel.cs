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
using ArchiveMaster.Helpers;
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

    public List<MagickFormat> SupportedImageFormats { get; } = BuildSupportedImageFormats();

    /// <summary>
    /// 随包的 libheif 是否可用。不可用时不在界面提供 HEIC 选项（例如缺少原生库的平台）。
    /// </summary>
    public bool IsHeifEncoderAvailable => HeifEncoder.IsAvailable;

    private static List<MagickFormat> BuildSupportedImageFormats()
    {
        var formats = MagickNET.SupportedFormats
            .Where(p => p.SupportsReading)
            .Where(p => p.SupportsWriting)
            .Select(p => p.Format)
            .ToList();

        // 即便 Magick.NET 将来把 Heif 列为可写也要剔除：本工具只用 .heic（相册兼容性更好），
        // 而 Heif/Heic 是同一套编码路径，留着只会让用户选出次优选项
        formats.Remove(MagickFormat.Heif);

        // Magick.NET 无法编码 HEIC（其 libheif 未编入 HEVC 编码器），改由随包的 libheif 完成，
        // 因此这里不能只依赖 MagickNET.SupportedFormats：原生库可用时才把 HEIC 补进列表。
        // 只提供 .heic 而不提供 .heif：实测两者的相册兼容性以 .heic 更好。
        if (HeifEncoder.IsAvailable && !formats.Contains(MagickFormat.Heic))
        {
            formats.Insert(0, MagickFormat.Heic);
        }

        return formats;
    }

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