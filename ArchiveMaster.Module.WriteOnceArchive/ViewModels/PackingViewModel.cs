using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class PackingViewModel(AppConfig appConfig, IDialogService dialogService)
    : TwoStepViewModelBase<PackingService, PackingConfig>(appConfig, dialogService,
        WriteOnceArchiveModuleInfo.CONFIG_GROUP)
{
    public static readonly (double sizeGB, string desc)[] PresetPackageSizes =
    [
        (0.63, "CD 650MB"),
        (0.68, "CD 700MB"),
        (4.3, "DVD5"),
        (7.9, "DVD9"),
        (8.7, "DVD10"),
        (15.8, "DVD18"),
        (23, "BD25"),
        (46, "BD50"),
        (93, "BD100"),
        (119, "BD128"),
    ];

    [ObservableProperty]
    private DateTime earliestDateTime = new DateTime(1, 1, 1);

    [ObservableProperty]
    private FileSystem.WriteOncePackage selectedPackage;

    [ObservableProperty]
    private List<FileSystem.WriteOncePackage> writeOnceFilePackages;

    protected override Task OnExecutingAsync(CancellationToken token)
    {
        if (!WriteOnceFilePackages.Any(p => p.IsChecked))
        {
            throw new Exception("没有任何被选中的文件包");
        }
        return base.OnExecutingAsync(token);
    }

    protected override Task OnInitializedAsync()
    {
        var pkgs = Service.Packages.Packages;
        if (Service.Packages.OutOfSizeFiles.Count > 0)
        {
            pkgs.Add(new FileSystem.WriteOncePackage()
            {
                Index = -1,
            });
            pkgs[^1].Files.AddRange(Service.Packages.OutOfSizeFiles);
        }

        WriteOnceFilePackages = pkgs;
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        WriteOnceFilePackages = null;
    }

    [RelayCommand]
    private void SelectAll()
    {
        WriteOnceFilePackages?.ForEach(p => p.IsChecked = true);
    }

    [RelayCommand]
    private void SelectNone()
    {
        WriteOnceFilePackages?.ForEach(p => p.IsChecked = false);
    }

    [RelayCommand]
    private void SetPackageSize(double size)
    {
        Config.PackageSizeGB = size;
    }
}