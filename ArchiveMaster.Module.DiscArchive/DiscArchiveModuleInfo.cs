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
    public class DiscArchiveModuleInfo : IModuleInfo
    {
        public const string CONFIG_GRROUP = "DiscArchive";
        private readonly string baseUrl = "avares://ArchiveMaster.Module.DiscArchive/Assets/";

        public IList<ConfigMetadata> Configs =>
        [
            new ConfigMetadata(typeof(PackingConfig), CONFIG_GRROUP),
            new ConfigMetadata(typeof(RebuildConfig), CONFIG_GRROUP),
        ];

        public string ModuleName => "光盘归档工具";

        public int Order => 4;
        public IList<Type> SingletonServices { get; }

        public IList<Type> TransientServices { get; } = [typeof(PackingService), typeof(RebuildService)];

        public ToolPanelGroupInfo Views => new ToolPanelGroupInfo()
        {
            Panels =
            {
                new ToolPanelInfo(typeof(PackingPanel), typeof(PackingViewModel), "打包到光盘",
                    "将文件按照修改时间顺序，根据光盘最大容量制作成若干文件包", baseUrl + "disc.svg"),
                new ToolPanelInfo(typeof(RebuildPanel), typeof(RebuildViewModel), "从光盘重建", "从备份的光盘中提取文件并恢复为原始目录结构",
                    baseUrl + "rebuild.svg"),
            },
            GroupName = ModuleName
        };
    }
}