using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FzLib;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Services;
using FzLib.Programming;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster.ViewModels
{
    public abstract partial class
        OfflineSyncViewModelBase<TService, TConfig, TFile>(ViewModelServices services)
        : TwoStepViewModelBase<TService, TConfig>(services, OfflineSyncModuleInfo.CONFIG_GRROUP)
        where TService : TwoStepServiceBase<TConfig>
        where TConfig : ConfigBase, new()
        where TFile : SimpleFileInfo
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AddedFileLength),
            nameof(AddedFileCount),
            nameof(ModifiedFileCount),
            nameof(ModifiedFileLength),
            nameof(DeletedFileCount),
            nameof(MovedFileCount),
            nameof(CheckedFileCount))]
        private ObservableCollection<TFile> files = new ObservableCollection<TFile>();

        public long AddedFileCount => Files?.Cast<FileSystem.SyncFileInfo>()
            .Where(p => p.UpdateType == FileUpdateType.Add && p.IsChecked)?.Count() ?? 0;

        public long AddedFileLength => Files?.Cast<FileSystem.SyncFileInfo>()
            .Where(p => p.UpdateType == FileUpdateType.Add && p.IsChecked)?.Sum(p => p.Length) ?? 0;

        public int CheckedFileCount => Files?.Where(p => p.IsChecked)?.Count() ?? 0;

        public int DeletedFileCount => Files?.Cast<FileSystem.SyncFileInfo>()
            .Where(p => p.UpdateType == FileUpdateType.Delete && p.IsChecked)?.Count() ?? 0;

        public char DirectorySeparatorChar => Path.DirectorySeparatorChar;

        public long ModifiedFileCount => Files?.Cast<FileSystem.SyncFileInfo>()
            .Where(p => p.UpdateType == FileUpdateType.Modify && p.IsChecked)?.Count() ?? 0;

        public long ModifiedFileLength => Files?.Cast<FileSystem.SyncFileInfo>()
            .Where(p => p.UpdateType == FileUpdateType.Modify && p.IsChecked)?.Sum(p => p.Length) ?? 0;

        public int MovedFileCount => Files?.Cast<FileSystem.SyncFileInfo>()
            .Where(p => p.UpdateType == FileUpdateType.Move && p.IsChecked)?.Count() ?? 0;


        private void AddFileCheckedNotify(SimpleFileInfo file)
        {
            file.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != nameof(SimpleFileInfo.IsChecked))
                {
                    return;
                }

                OnPropertyChanged(nameof(CheckedFileCount));
                if (s is not FileSystem.SyncFileInfo syncFile)
                {
                    return;
                }

                switch (syncFile.UpdateType)
                {
                    case FileUpdateType.Add:
                        OnPropertyChanged(nameof(AddedFileCount));
                        OnPropertyChanged(nameof(AddedFileLength));
                        break;

                    case FileUpdateType.Modify:
                        OnPropertyChanged(nameof(ModifiedFileCount));
                        OnPropertyChanged(nameof(ModifiedFileLength));
                        break;

                    case FileUpdateType.Delete:
                        OnPropertyChanged(nameof(DeletedFileCount));
                        break;

                    case FileUpdateType.Move:
                        OnPropertyChanged(nameof(MovedFileCount));
                        break;

                    case FileUpdateType.None:
                    default:
                        break;
                }
            };
        }


        partial void OnFilesChanged(ObservableCollection<TFile> value)
        {
            if (value == null)
            {
                return;
            }

            value.ForEach(p => AddFileCheckedNotify(p));
            value.CollectionChanged += (s, e) => throw new NotSupportedException("不允许对集合进行修改");
        }


        [RelayCommand]
        private void SelectAll()
        {
            Files?.ForEach(p => p.IsChecked = true);
        }

        [RelayCommand]
        private void SelectNone()
        {
            Files?.ForEach(p => p.IsChecked = false);
        }

        protected override void OnReset()
        {
            Files = new ObservableCollection<TFile>();
        }
    }
}