using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels;
using ArchiveMaster.Views;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Avalonia.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster
{
    public class VideoToolsModuleInfo : IModuleInfo
    {
        private readonly string baseUrl = "avares://ArchiveMaster.Module.VideoTools/Assets/";
        public IList<Type> BackgroundServices { get; }

        public IList<ConfigMetadata> Configs =>
        [
            new ConfigMetadata(typeof(TimeAssConfig)),
            new ConfigMetadata(typeof(VideoInfoConfig))
        ];

        public IList<JsonConverter> JsonConverters { get; }

        public string ModuleName => "视频工具";
        public string ModuleDescription => "关于视频的一些处理工作";
        public string HelpFileName { get; } = "video.md";
        public IList<Type> SingletonServices { get; }

        public IList<Type> TransientServices { get; } =
        [
            typeof(TimeAssService),
            typeof(VideoInfoService),
        ];

        public ToolPanelGroupInfo Views => new ToolPanelGroupInfo()
        {
            Panels =
            {
                new ToolPanelInfo(typeof(TimeAssPanel), typeof(TimeAssViewModel), "视频时间戳字幕",
                    "为视频添加录制时间的字幕", baseUrl + "time_ass.svg"),
                new ToolPanelInfo(typeof(VideoInfoPanel), typeof(VideoInfoViewModel), "视频信息提取",
                    "为一系列视频批量提取结构化的信息", baseUrl + "videos.svg"),
            },
            GroupName = ModuleName,
            GroupDescription = ModuleDescription
        };
    }
}