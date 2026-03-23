using System.Collections.ObjectModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class TimeAssViewModel(ViewModelServices services)
    : TwoStepViewModelBase<TimeAssService, TimeAssConfig>(services)
{

    protected override Task OnInitializedAsync()
    {
        return base.OnInitializedAsync();
    }
}