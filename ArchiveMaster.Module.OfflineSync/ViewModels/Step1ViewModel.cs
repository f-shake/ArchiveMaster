using ArchiveMaster.Configs;
using ArchiveMaster.Messages;
using ArchiveMaster.Services;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib;
using System.Collections.ObjectModel;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster.ViewModels
{
    public partial class Step1ViewModel(
        AppConfig appConfig,
        IDialogService dialogService,
        IStorageProviderService storage)
        : OfflineSyncViewModelBase<Step1Service, OfflineSyncStep1Config, SimpleFileInfo>(appConfig, dialogService,
            storage)
    {
        [ObservableProperty]
        private string selectedSyncDir;

        public override bool EnableInitialize => false;
        public string SnapshotSuggestedFileName => $"异地备份（{DateTime.Now:yyyyMMdd-HHmmss}）";

        private void AddSyncDir(string path)
        {
            DirectoryInfo newDirInfo = new DirectoryInfo(path);

            if (!newDirInfo.Exists)
            {
                throw new DirectoryNotFoundException("指定的目录不存在");
            }

            if (Config.SyncDirs == null)
            {
                Config.SyncDirs = new ObservableCollection<string>();
            }

            // 检查新目录与现有目录是否相同
            foreach (string existingPath in Config.SyncDirs)
            {
                DirectoryInfo existingDirInfo = new DirectoryInfo(existingPath);

                if (existingDirInfo.FullName.Equals(newDirInfo.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"目录 '{path}' 已经存在，不能重复添加。");
                }
            }

            // 检查新目录是否是现有目录的子目录或父目录
            foreach (string existingPath in Config.SyncDirs)
            {
                DirectoryInfo existingDirInfo = new DirectoryInfo(existingPath);

                // 检查新目录是否是现有目录的子目录
                DirectoryInfo temp = newDirInfo;
                while (temp.Parent != null)
                {
                    if (temp.Parent.FullName.Equals(existingDirInfo.FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"新目录 '{path}' 是现有目录 '{existingPath}' 的子目录，不能添加。");
                    }

                    temp = temp.Parent;
                }

                // 检查新目录是否是现有目录的父目录
                temp = existingDirInfo;
                while (temp.Parent != null)
                {
                    if (temp.Parent.FullName.Equals(newDirInfo.FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"新目录 '{path}' 是现有目录 '{existingPath}' 的父目录，不能添加。");
                    }

                    temp = temp.Parent;
                }
            }

            Config.SyncDirs.Add(path);
        }

        [RelayCommand]
        private async Task BrowseDirAsync()
        {
            var files = await Storage.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                AllowMultiple = true,
            });
            if (files.Count > 0)
            {
                try
                {
                    foreach (var file in files)
                    {
                        var path = file.TryGetLocalPath();
                        AddSyncDir(path);
                    }
                }
                catch (Exception ex)
                {
                    await DialogService.ShowErrorDialogAsync("加入失败", ex);
                }
            }
        }

        protected override async Task OnExecutingAsync(CancellationToken ct)
        {
            var dirs = (Config.SyncDirs ?? []).ToHashSet();
            if (dirs.Count == 0)
            {
                throw new Exception("未选择任何目录");
            }

            if (string.IsNullOrWhiteSpace(Config.OutputFile))
            {
                var result = await Storage.SaveFilePickerAndGetPathAsync(new FilePickerSaveOptions()
                {
                    FileTypeChoices =
                    [
                        new FilePickerFileType("异地快照文件") { Patterns = ["*.os1"] }
                    ],
                });
                if (result != null)
                {
                    Config.OutputFile = result;
                }
            }
        }

        [RelayCommand]
        private async Task InputDirAsync()
        {
            try
            {
                var result = await DialogService.ShowInputTextDialogAsync("输入目录", "请输入欲加入的目录地址");
                if (!string.IsNullOrWhiteSpace(result))
                {
                    AddSyncDir(result);
                }
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorDialogAsync("加入失败", ex);
            }
        }

        [RelayCommand]
        private void RemoveAll()
        {
            Config.SyncDirs.Clear();
        }

        [RelayCommand]
        private void RemoveSelected()
        {
            if (SelectedSyncDir != null)
            {
                Config.SyncDirs.Remove(SelectedSyncDir);
            }
        }
    }
}