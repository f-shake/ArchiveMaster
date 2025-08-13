using ArchiveMaster.Configs;
using ArchiveMaster.Messages;
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
using System.Threading.Tasks;
using ArchiveMaster.Models;
using ArchiveMaster.Services;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster
{
    public class OfflineSyncModuleInfo : IModuleInfo
    {
        public const string CONFIG_GRROUP = "OfflineSync";

        private readonly string baseUrl = "avares://ArchiveMaster.Module.OfflineSync/Assets/";
        public IList<Type> BackgroundServices { get; }

        public IList<ConfigMetadata> Configs =>
        [
            new ConfigMetadata(typeof(OfflineSyncStep1Config), CONFIG_GRROUP),
            new ConfigMetadata(typeof(OfflineSyncStep2Config), CONFIG_GRROUP),
            new ConfigMetadata(typeof(OfflineSyncStep3Config), CONFIG_GRROUP),
        ];

        public string ModuleName => "异地备份";
        public string ModuleDescription => "解决在无法通过网络或实地全量同步的情况下，进行增量同步和备份的需求";

        public int Order => 3;
        public IList<Type> SingletonServices { get; }

        public IList<Type> TransientServices { get; } =
            [typeof(Step1Service), typeof(Step2Service), typeof(Step3Service)];

        public ToolPanelGroupInfo Views => new ToolPanelGroupInfo()
        {
            Panels =
            {
                new ToolPanelInfo(typeof(Step1Panel), typeof(Step1ViewModel), "制作异地快照", "在异地计算机创建所需要的目录快照",
                    baseUrl + "snapshot.svg"),
                new ToolPanelInfo(typeof(Step2Panel), typeof(Step2ViewModel), "本地生成补丁", "在本地计算机生成与异地的差异文件的补丁包",
                    baseUrl + "patch.svg"),
                new ToolPanelInfo(typeof(Step3Panel), typeof(Step3ViewModel), "异地同步", "在异地应用补丁包，实现数据同步",
                    baseUrl + "update.svg")
            },
            GroupName = ModuleName,
            GroupDescription = ModuleDescription,
            MenuItems =
            {
                new ModuleMenuItemInfo("生成测试数据", new AsyncRelayCommand(async () =>
                {
                    var folders = await HostServices.GetRequiredService<IStorageProviderService>()
                        .OpenFolderPickerAsync(new FolderPickerOpenOptions());
                    if (folders.Count > 0)
                    {
                        var folder = folders[0].TryGetLocalPath();
                        WeakReferenceMessenger.Default.Send(new LoadingMessage(true));
                        try
                        {
                            await TestService.CreateSyncTestFilesAsync(folder);
                        }
                        finally
                        {
                            WeakReferenceMessenger.Default.Send(new LoadingMessage(false));
                        }
                    }
                })),
                new ModuleMenuItemInfo("自动化测试", new AsyncRelayCommand(async () =>
                {
                    try
                    {
                        WeakReferenceMessenger.Default.Send(new LoadingMessage(true));
                        try
                        {
                            await TestService.TestAllAsync();
                        }
                        finally
                        {
                            WeakReferenceMessenger.Default.Send(new LoadingMessage(false));
                        }

                        await HostServices.GetRequiredService<IDialogService>().ShowOkDialogAsync("自动化测试", "通过测试");
                    }
                    catch (Exception ex)
                    {
                        await HostServices.GetRequiredService<IDialogService>().ShowErrorDialogAsync("自动化测试",  ex);
                    }
                }))
            }
        };
    }
}