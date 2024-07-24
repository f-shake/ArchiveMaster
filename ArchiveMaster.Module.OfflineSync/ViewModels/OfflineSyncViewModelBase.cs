﻿using ArchiveMaster.Enums;
using ArchiveMaster.Messages;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using FzLib;
using FzLib.Avalonia.Messages;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ArchiveMaster.ViewModels
{
    public partial class OfflineSyncViewModelBase<T> : ObservableObject where T : FileInfoWithStatus
    {
        [ObservableProperty]
        private bool canAnalyze = true;

        [ObservableProperty]
        private bool canEditConfigs = true;

        [ObservableProperty]
        private bool canProcess = false;

        [ObservableProperty]
        private bool canStop = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AddedFileLength),
                nameof(AddedFileCount),
                nameof(ModifiedFileCount),
                nameof(ModifiedFileLength),
                nameof(DeletedFileCount),
                nameof(MovedFileCount),
                nameof(CheckedFileCount))]
        private ObservableCollection<T> files = new ObservableCollection<T>();

        [ObservableProperty]
        private string message = "就绪";

        [ObservableProperty]
        private double progress;

        [ObservableProperty]
        private bool progressIndeterminate;

        [ObservableProperty]
        private double progressMax;

        public long AddedFileCount => Files?.Cast<SyncFileInfo>().Where(p => p.UpdateType == FileUpdateType.Add && p.IsChecked)?.Count() ?? 0;


        public long AddedFileLength => Files?.Cast<SyncFileInfo>().Where(p => p.UpdateType == FileUpdateType.Add && p.IsChecked)?.Sum(p => p.Length) ?? 0;


        public int CheckedFileCount => Files?.Where(p => p.IsChecked)?.Count() ?? 0;


        public int DeletedFileCount => Files?.Cast<SyncFileInfo>().Where(p => p.UpdateType == FileUpdateType.Delete && p.IsChecked)?.Count() ?? 0;

        public long ModifiedFileCount => Files?.Cast<SyncFileInfo>().Where(p => p.UpdateType == FileUpdateType.Modify && p.IsChecked)?.Count() ?? 0;

        public long ModifiedFileLength => Files?.Cast<SyncFileInfo>().Where(p => p.UpdateType == FileUpdateType.Modify && p.IsChecked)?.Sum(p => p.Length) ?? 0;

        public int MovedFileCount => Files?.Cast<SyncFileInfo>().Where(p => p.UpdateType == FileUpdateType.Move && p.IsChecked)?.Count() ?? 0;

        public void UpdateStatus(StatusType status)
        {
            CanStop = status is StatusType.Analyzing or StatusType.Processing;
            CanAnalyze = status is StatusType.Ready or StatusType.Analyzed;
            CanProcess = status is StatusType.Analyzed;
            CanEditConfigs = status is StatusType.Ready or StatusType.Analyzed;
            Message = status is StatusType.Ready or StatusType.Analyzed ? "就绪" : "处理中";
            Progress = 0;
            ProgressIndeterminate = status is StatusType.Analyzing or StatusType.Processing or StatusType.Stopping;
        }

        private void AddFileCheckedNotify(FileInfoWithStatus file)
        {
            file.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FileInfoWithStatus.IsChecked))
                {
                    this.Notify(nameof(CheckedFileCount));
                    if (s is SyncFileInfo syncFile)
                    {
                        switch (syncFile.UpdateType)
                        {
                            case FileUpdateType.Add:
                                this.Notify(nameof(AddedFileCount), nameof(AddedFileLength));
                                break;

                            case FileUpdateType.Modify:
                                this.Notify(nameof(ModifiedFileCount), nameof(ModifiedFileLength));
                                break;

                            case FileUpdateType.Delete:
                                this.Notify(nameof(DeletedFileCount));
                                break;

                            case FileUpdateType.Move:
                                this.Notify(nameof(MovedFileCount));
                                break;

                            default:
                                break;
                        }
                    }
                }
            };
        }

        partial void OnFilesChanged(ObservableCollection<T> value)
        {
            value.ForEach(p => AddFileCheckedNotify(p));
            value.CollectionChanged += (s, e) => throw new NotSupportedException("不允许对集合进行修改");
        }

        partial void OnProgressChanged(double value)
        {
            ProgressIndeterminate = false;
        }

        protected Task ShowErrorAsync(string title, Exception exception)
        {
            return WeakReferenceMessenger.Default.Send(new CommonDialogMessage()
            {
                Type = CommonDialogMessage.CommonDialogType.Error,
                Title = title,
                Exception = exception
            }).Task;
        }
    }
}