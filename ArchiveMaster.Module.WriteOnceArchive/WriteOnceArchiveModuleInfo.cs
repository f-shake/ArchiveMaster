using ArchiveMaster.Configs;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels;
using ArchiveMaster.Views;

namespace ArchiveMaster
{
    public class WriteOnceArchiveModuleInfo : IModuleInfo
    {
        public const string CONFIG_GROUP = "WriteOnceArchive";
        private readonly string baseUrl = "avares://ArchiveMaster.Module.WriteOnceArchive/Assets/";
        public IList<Type> BackgroundServices { get; }

        public IList<ConfigMetadata> Configs =>
        [
            new ConfigMetadata(typeof(PackingConfig), CONFIG_GROUP),
            new ConfigMetadata(typeof(RebuildConfig), CONFIG_GROUP),
        ];

        public string ModuleName => "动态固存备份";
        public string ModuleDescription => "解决将动态更新目录中的定期文件备份到多个容量有限、写入后不可修改介质（如光盘、一次性磁带）的需求";

        public int Order => 4;
        public IList<Type> SingletonServices { get; }

        public IList<Type> TransientServices { get; } =
        [
            typeof(PackingService),
            typeof(RebuildService)
        ];

        public ToolPanelGroupInfo Views => new ToolPanelGroupInfo()
        {
            Panels =
            {
                new ToolPanelInfo(typeof(PackingPanel), typeof(PackingViewModel), "打包",
                    "扫描文件特征，制作文件归档包", baseUrl + "disc.svg"),
                new ToolPanelInfo(typeof(RebuildPanel), typeof(RebuildViewModel), "重建", "从备份的文件包中提取文件并恢复为原始目录结构",
                    baseUrl + "rebuild.svg"),
            },
            GroupName = ModuleName,
            GroupDescription = ModuleDescription
        };
    }
}