using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using ArchiveMaster.Enums;
using FzLib.Avalonia.Dialogs;


namespace ArchiveMaster.ViewModels
{
    public partial class BackupTasksViewModel : ViewModelBase
    {
        private readonly BackupService backupService;

        [ObservableProperty]
        private BackupTask selectedTask;

        [ObservableProperty]
        private ObservableCollection<BackupTask> tasks;

        public BackupTasksViewModel(AppConfig appConfig, BackupService backupService, IDialogService dialogService) :
            base(dialogService)
        {
            this.backupService = backupService;
            Config = appConfig.GetOrCreateConfigWithDefaultKey<FileBackupperConfig>();
            AppConfig = appConfig;
#if DEBUG
            if (Config.Tasks.Count == 0)
            {
                Config.Tasks.Add(new BackupTask()
                {
                    Name = "任务名",
                    SourceDir = @"C:\Users\autod\Desktop\备份源目录",
                    BackupDir = @"C:\Users\autod\Desktop\备份文件夹",
                });
            }

            appConfig.Save();
#endif
        }

        public AppConfig AppConfig { get; }

        public FileBackupperConfig Config { get; }

        public override async void OnEnter()
        {
            base.OnEnter();

            while (backupService.IsBackingUp)
            {
                var result =
                    await DialogService.ShowErrorDialogAsync("正在备份", "有任务正在备份，无法进行任务配置，请前往管理中心停止备份或重试",
                        retryButton: true);

                if (false.Equals(result))
                {
                    // Exit();
                    return;
                }
            }

            Tasks = new ObservableCollection<BackupTask>(Config.Tasks);
            await Tasks.UpdateStatusAsync();
        }

        [RelayCommand]
        private void AddTask()
        {
            var task = new BackupTask();
            Tasks.Add(task);
            SelectedTask = task;
        }

        [RelayCommand]
        private void DeleteSelectedTask()
        {
            Debug.Assert(SelectedTask != null);
            Tasks.Remove(SelectedTask);
        }

        [RelayCommand]
        private void Save()
        {
            Config.Tasks = Tasks.Select(p => p.Clone() as BackupTask).ToList();
            AppConfig.Save();
        }
    }
}