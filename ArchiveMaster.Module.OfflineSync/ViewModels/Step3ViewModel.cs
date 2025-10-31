using CommunityToolkit.Mvvm.ComponentModel;
using FzLib;
using ArchiveMaster.Views;
using System.Collections;
using System.Collections.ObjectModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveMaster.ViewModels
{
    public partial class Step3ViewModel(ViewModelServices services)
        : OfflineSyncViewModelBase<Step3Service, OfflineSyncStep3Config, FileSystem.SyncFileInfo>(services)
    {
        public IEnumerable DeleteModes => Enum.GetValues<DeleteMode>();

        protected override Task OnInitializedAsync()
        {
            Files = new ObservableCollection<FileSystem.SyncFileInfo>(Service.UpdateFiles);
            return base.OnInitializedAsync();
        }

        protected override async Task OnExecutedAsync(CancellationToken ct)
        {
            if (Service.DeletingDirectories.Count != 0)
            {
                var result = await Services.Dialog.ShowYesNoDialogAsync("删除空目录",
                    $"有{Service.DeletingDirectories.Count}个已不存在于本地的空目录，是否删除？",
                    string.Join(Environment.NewLine, Service.DeletingDirectories.Select(p => p.Path)));

                if (true.Equals(result))
                {
                    await Service.DeleteEmptyDirectoriesAsync();
                }
            }
        }
    }
}