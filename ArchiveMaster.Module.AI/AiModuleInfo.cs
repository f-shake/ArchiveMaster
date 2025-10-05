using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels;
using ArchiveMaster.Views;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster
{
    public class AiModuleInfo : IModuleInfo
    {
        private readonly string baseUrl = "avares://ArchiveMaster.Module.AI/Assets/";
        public IList<Type> BackgroundServices { get; }

        public IList<ConfigMetadata> Configs =>
        [
            new ConfigMetadata(typeof(SmartDocSearchConfig)),
        ];

        public string ModuleName => "AI赋能";
        public string ModuleDescription => "解决通过AI智能辅助文本文件管理的需求";

#if DEBUG
        public int Order => -1;
#else
        public int Order => 3;
#endif
        public IList<Type> SingletonServices { get; }

        public IList<Type> TransientServices { get; } =
        [
            typeof(SmartDocSearchService),
        ];

        public ToolPanelGroupInfo Views => new ToolPanelGroupInfo()
        {
            Panels =
            {
                new ToolPanelInfo(typeof(SmartDocSearchPanel), typeof(SmartDocSearchViewModel), "文档智能搜索",
                    "从多个文档中搜索关键词，并通过AI进行总结归纳", baseUrl + "docSearch.svg"),
            },
            GroupName = ModuleName,
            GroupDescription = ModuleDescription
        };
    }
}