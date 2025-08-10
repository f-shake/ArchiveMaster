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

        public string ModuleName => "一次性写入介质归档";

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
            GroupName = ModuleName
        };
    }
}