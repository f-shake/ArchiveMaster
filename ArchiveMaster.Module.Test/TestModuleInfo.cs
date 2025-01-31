﻿using ArchiveMaster.Configs;
using ArchiveMaster.Messages;
using ArchiveMaster.ViewModels;
using ArchiveMaster.Views;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Messages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster
{
    public class TestModuleInfo : IModuleInfo
    {
        private readonly string baseUrl = "avares://ArchiveMaster.Module.Test/Assets/";
        public IList<Type> BackgroundServices { get; }
        public IList<ConfigMetadata> Configs =>
        [
        ];

        public string ModuleName => "测试";
        public int Order => -100;
        public IList<Type> SingletonServices { get; }

        public IList<Type> TransientServices { get; }

        public ToolPanelGroupInfo Views => new ToolPanelGroupInfo()
        {
            Panels =
            {
                new ToolPanelInfo(typeof(FileFilterTestPanel), typeof(FileFilterTestViewModel), "文件筛选测试",
                    "测试FileFilter功能", baseUrl + "test.svg"),
            },
            GroupName = ModuleName,
        };
    }
}