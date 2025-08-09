using System.Collections;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class PackingViewModel(AppConfig appConfig, IDialogService dialogService)
    : TwoStepViewModelBase<PackingService, PackingConfig>(appConfig, dialogService, WriteOnceArchiveModuleInfo.CONFIG_GROUP)
{
    [ObservableProperty]
    private List<FileSystem.WriteOncePackage> writeOnceFilePackages;

    [ObservableProperty]
    private DateTime earliestDateTime = new DateTime(1, 1, 1);

    [ObservableProperty]
    private FileSystem.WriteOncePackage selectedPackage;

    public int[] PackageSizes { get; } = [700, 4480, 8500, 23500];

    [RelayCommand]
    private void SetPackageSize(int size)
    {
        Config.PackageSizeMB = size;
    }

    protected override Task OnInitializedAsync()
    {
        var pkgs = Service.Packages.Packages;
        if (Service.Packages.OutOfSizeFiles.Count > 0)
        {
            pkgs.Add(new FileSystem.WriteOncePackage()
            {
                Index = -1
            });
            pkgs[^1].Files.AddRange(Service.Packages.OutOfSizeFiles);
        }

        WriteOnceFilePackages = pkgs;
        return base.OnInitializedAsync();
    }

    protected override async Task OnExecutingAsync(CancellationToken token)
    {
        if (!WriteOnceFilePackages.Any(p => p.IsChecked))
        {
            throw new Exception("没有任何被选中的文件包");
        }

        if (Directory.Exists(Config.TargetDir) && Directory.EnumerateFileSystemEntries(Config.TargetDir).Any())
        {
            var result = await DialogService.ShowYesNoDialogAsync("清空目录",
                $"目录{Config.TargetDir}不为空，{Environment.NewLine}导出前将清空部分目录。{Environment.NewLine}是否继续？");
            if (true.Equals(result))
            {
                try
                {
                    foreach (var index in Service.Packages.Packages.Where(p => p.IsChecked)
                                 .Select(p => p.Index))
                    {
                        var dir = Path.Combine(Config.TargetDir, index.ToString());
                        if (Directory.Exists(dir))
                        {
                            FileHelper.DeleteByConfig(dir);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("清空目录失败", ex);
                }
            }
            else
            {
                throw new OperationCanceledException();
            }
        }
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
}