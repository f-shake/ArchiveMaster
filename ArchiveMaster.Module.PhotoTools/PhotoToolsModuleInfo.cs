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
    public class PhotoToolsModuleInfo : IModuleInfo
    {
        private readonly string baseUrl = "avares://ArchiveMaster.Module.PhotoTools/Assets/";
        public IList<Type> BackgroundServices { get; }

        public IList<ConfigMetadata> Configs =>
        [
            new ConfigMetadata(typeof(RepairModifiedTimeConfig)),
            new ConfigMetadata(typeof(PhotoSlimmingConfig)),
            new ConfigMetadata(typeof(PhotoGeoTaggingConfig)),
            new ConfigMetadata(typeof(PhotoGeoSorterConfig)),
        ];

        public string ModuleName => "照片工具";
        public string ModuleDescription => "解决根据照片元数据分类或更新照片元数据的需求";

        public int Order => 2;
        public IList<Type> SingletonServices { get; }

        public IList<Type> TransientServices { get; } =
        [
            typeof(PhotoSlimmingService),
            typeof(RepairModifiedTimeService),
            typeof(PhotoGeoTaggingService),
            typeof(PhotoGeoSorterService),
        ];

        public ToolPanelGroupInfo Views => new ToolPanelGroupInfo()
        {
            Panels =
            {
                new ToolPanelInfo(typeof(RepairModifiedTimePanel), typeof(RepairModifiedTimeViewModel), "修复照片修改时间",
                    "寻找EXIF信息中的拍摄时间与照片修改时间不同的文件，将修改时间更新闻EXIF时间", baseUrl + "time.svg"),
                new ToolPanelInfo(typeof(PhotoSlimmingPanel), typeof(PhotoSlimmingViewModel), "照片瘦身",
                    "复制或压缩照片，用于生成更小的照片集副本", baseUrl + "zip.svg"),
                new ToolPanelInfo(typeof(PhotoGeoTaggingPanel), typeof(PhotoGeoTaggingViewModel), "照片地理信息写入",
                    "将GPX轨迹中的GPS位置信息，根据拍摄时间自动匹配并写入照片Exif", baseUrl + "location.svg"),
                new ToolPanelInfo(typeof(PhotoGeoSorterPanel), typeof(PhotoGeoSorterViewModel), "照片位置分类",
                    "根据照片的GPS位置信息，将照片分为不同地理区域的多个目录", baseUrl + "map.svg"),
            },
            GroupName = ModuleName,
            GroupDescription = ModuleDescription
        };
    }
}