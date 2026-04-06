using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels;
using ArchiveMaster.Views;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster
{
    public class PhotoTaggingModuleInfo : IModuleInfo
    {
        private readonly string baseUrl = "avares://ArchiveMaster.Module.PhotoTagging/Assets/";
        public IList<Type> BackgroundServices { get; }

        public IList<ConfigMetadata> Configs =>
        [
            new ConfigMetadata(typeof(PhotoTagGeneratorConfig)),
            new ConfigMetadata(typeof(PhotoTagSearcherConfig)),
        ];

        public IList<JsonConverter> JsonConverters { get; }

        public string ModuleName => "照片标签工具";
        public string ModuleDescription => "通过AI生成照片的文字标签，方便对大量照片的搜索和管理";
        public string HelpFileName { get; } 
        public IList<Type> SingletonServices { get; }

        public IList<Type> TransientServices { get; } =
        [
            typeof(PhotoTagGeneratorService),
        ];

        public ToolPanelGroupInfo Views => new ToolPanelGroupInfo()
        {
            Panels =
            {
                new ToolPanelInfo(typeof(PhotoTagGeneratorPanel), typeof(PhotoTagGeneratorViewModel), "照片标签生成",
                    "通过AI，自动生成照片的关键词标签", baseUrl + "tag.svg", true),
                new ToolPanelInfo(typeof(PhotoTagSearcherPanel), typeof(PhotoTagSearcherViewModel), "根据关键词查找",
                    "根据关键词，查找指定标签的图像", baseUrl + "pic_search.svg")
            },
            GroupName = ModuleName,
            GroupDescription = ModuleDescription
        };
    }
}