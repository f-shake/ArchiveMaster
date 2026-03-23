using System.Collections.ObjectModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class TimeAssViewModel(ViewModelServices services)
    : TwoStepViewModelBase<TimeAssService, TimeAssConfig>(services)
{
    public IEnumerable<string> SampleTimeFormats { get; } =
    [
        "HH:mm:ss",
        "HH:mm",
        "HH:mm:ss.ff",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.ff",
        "yyyy-MM-dd HH:mm"
    ];

    public IEnumerable<string> FontFamilyNames { get; } = FontManager.Current.SystemFonts.Select(p=>p.Name);


    protected override Task OnInitializedAsync()
    {
        return base.OnInitializedAsync();
    }
}