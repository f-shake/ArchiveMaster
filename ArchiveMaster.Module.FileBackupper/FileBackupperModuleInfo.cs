using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels;
using ArchiveMaster.Views;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArchiveMaster
{
    public class FileBackupperModuleInfo : IModuleInfo
    {
        private readonly string baseUrl = "avares://ArchiveMaster.Module.FileBackupper/Assets/";
        public IList<Type> BackgroundServices { get; } = [typeof(BackupBackgroundService)];
        public IList<ConfigMetadata> Configs =>
        [
            new ConfigMetadata(typeof(FileBackupperConfig)),
        ];

        public string ModuleName => "实时备份服务";
        public string ModuleDescription => "解决定时备份目录文件到其他物理磁盘，实现文件历史版本控制的需求";
        
        public string HelpFileName { get; } = "backup.md";

        public int Order => 5;
        public IList<Type> SingletonServices { get; } = [typeof(BackupService)];

        public IList<Type> TransientServices { get; }

        public ToolPanelGroupInfo Views => new ToolPanelGroupInfo()
        {
            Panels =
            {
                new ToolPanelInfo(typeof(BackupTasksPanel), typeof(BackupTasksViewModel), "备份任务配置", "备份任务的管理以及日志查看",
                    baseUrl + "backup.svg"),
                new ToolPanelInfo(typeof(BackupManageCenterPanel), typeof(BackupManageCenterViewModel), "备份管理中心",
                    "控制后台备份，查看当前状态，查看任务日志", baseUrl + "configuration.svg"),
            },
            GroupName = ModuleName,
            GroupDescription = ModuleDescription
        };
        public void RegisterMessages(Visual visual)
        {
        }
    }
}