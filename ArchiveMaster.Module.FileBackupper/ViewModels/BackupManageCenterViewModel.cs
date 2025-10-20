using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Services;

namespace ArchiveMaster.ViewModels
{
    public partial class BackupManageCenterViewModel : ViewModelBase
    {
        private readonly BackupService backupService;

        [ObservableProperty]
        private int selectedTabIndex;

        public BackupManageCenterViewModel(ViewModelServices services, BackupService backupService)
            : base(services)
        {
            Config = services.AppConfig.GetOrCreateConfigWithDefaultKey<FileBackupperConfig>();
            this.backupService = backupService;
            BackupService.NewLog += (s, e) =>
            {
                if (e.Task == SelectedTask)
                {
                    LastLog = e.Log;
                }
            };
        }

        public FileBackupperConfig Config { get; }

        public override async void OnEnter()
        {
            base.OnEnter();
            await LoadTasksAsync();
        }

        private void ThrowIfIsBackingUp()
        {
            if (backupService.IsBackingUp)
            {
                throw new InvalidOperationException("有任务正在备份，无法进行操作");
            }
        }
    }
}