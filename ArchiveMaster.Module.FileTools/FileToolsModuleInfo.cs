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
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster
{
    public class FileToolsModuleInfo : IModuleInfo
    {
        private readonly string baseUrl = "avares://ArchiveMaster.Module.FileTools/Assets/";
        public IList<Type> BackgroundServices { get; }
        public IList<ConfigMetadata> Configs =>
        [
            new ConfigMetadata(typeof(EncryptorConfig)),
            new ConfigMetadata(typeof(DirStructureSyncConfig)),
            new ConfigMetadata(typeof(DirStructureCloneConfig)),
            new ConfigMetadata(typeof(RenameConfig)),
            new ConfigMetadata(typeof(DuplicateFileCleanupConfig)),
            new ConfigMetadata(typeof(TimeClassifyConfig)),
            new ConfigMetadata(typeof(TwinFileCleanerConfig)),
            new ConfigMetadata(typeof(BatchCommandLineConfig)),
            new ConfigMetadata(typeof(LinkDeduplicationConfig)),
        ];

        public string ModuleName => "文件目录工具";
        public string ModuleDescription => "解决对文件和目录进行批量操作的需求";

        public int Order => 1;
        public IList<Type> SingletonServices { get; }

        public IList<Type> TransientServices { get; } =
        [
            typeof(EncryptorService),
            typeof(DirStructureCloneService),
            typeof(DirStructureSyncService),
            typeof(RenameService),
            typeof(DuplicateFileCleanupService),
            typeof(TwinFileCleanerService),
            typeof(TimeClassifyService),
            typeof(BatchCommandLineService),
            typeof(LinkDeduplicationService)
        ];

        public ToolPanelGroupInfo Views => new ToolPanelGroupInfo()
        {
            Panels =
            {
                new ToolPanelInfo(typeof(EncryptorPanel), typeof(EncryptorViewModel), "文件加密解密", "使用AES加密方法，对文件进行加密或解密",
                    baseUrl + "encrypt.svg"),
                new ToolPanelInfo(typeof(DirStructureSyncPanel), typeof(DirStructureSyncViewModel), "目录结构同步",
                    "以一个目录为模板，将另一个目录中的文件同步到与模板内相同文件一直的位置", baseUrl + "sync.svg"),
                new ToolPanelInfo(typeof(DirStructureClonePanel), typeof(DirStructureCloneViewModel), "目录结构克隆",
                    "以一个目录为模板，导出一个新的稀疏文件目录，或包含文件结构的JSON文件", baseUrl + "directory.svg"),
                new ToolPanelInfo(typeof(RenamePanel), typeof(RenameViewModel), "批量重命名", "批量对一个目录中的文件或文件夹按规则进行重命名操作",
                    baseUrl + "rename.svg"),
                new ToolPanelInfo(typeof(DuplicateFileCleanupPanel), typeof(DuplicateFileCleanupViewModel), "重复文件清理",
                    "清理一个目录内的重复文件，或已包含在另一个目录中的相同文件", baseUrl + "cleanup.svg"),
                new ToolPanelInfo(typeof(TimeClassifyPanel), typeof(TimeClassifyViewModel), "根据时间段归档",
                    "识别目录中相同时间段的文件，将它们移动到相同的新目录中",
                    baseUrl + "archive.svg"),
                new ToolPanelInfo(typeof(TwinFileCleanerPanel), typeof(TwinFileCleanerViewModel), "附属文件清理",
                    "当目录中存在某后缀文件（如.dng）时，自动删除同名不同后缀的关联文件（如.jpg）", baseUrl + "jpg.svg"),
                new ToolPanelInfo(typeof(BatchCommandLinePanel), typeof(BatchCommandLineViewModel), "批量命令行执行",
                    "以文件或目录为元素，批量执行命令行", baseUrl + "cmd.svg"), 
                new ToolPanelInfo(typeof(LinkDeduplicationPanel), typeof(LinkDeduplicationViewModel), "硬链接去重",
                    "通过硬链接替换重复文件，节省磁盘空间", baseUrl + "link.svg"),

            },
            GroupName = ModuleName,
            GroupDescription = ModuleDescription
        };
    }
}