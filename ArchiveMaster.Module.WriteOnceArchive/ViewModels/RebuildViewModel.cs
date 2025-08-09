using System.Collections.ObjectModel;
using System.ComponentModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;
//
// public partial class RebuildViewModel(AppConfig appConfig,IDialogService dialogService)
//     : TwoStepViewModelBase<RebuildService, RebuildConfig>(appConfig,dialogService, WriteOnceArchiveModuleInfo.CONFIG_GROUP)
// {
//     [ObservableProperty]
//     private TreeFileDirInfo fileTree;
//
//     [ObservableProperty]
//     private IReadOnlyList<RebuildError> rebuildErrors;
//
//     protected override Task OnInitializedAsync()
//     {
//         FileTree = Service.FileTree;
//         return base.OnInitializedAsync();
//     }
//
//     protected override Task OnExecutingAsync(CancellationToken token)
//     {
//         if (FileTree.Count == 0 && FileTree.Files.Count == 0)
//         {
//             throw new Exception("没有任何需要重建的文件");
//         }
//
//         return base.OnExecutingAsync(token);
//     }
//
//     protected override Task OnExecutedAsync(CancellationToken token)
//     {
//         RebuildErrors = Service.RebuildErrors;
//         return base.OnExecutedAsync(token);
//     }
// }