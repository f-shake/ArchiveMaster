using System.Collections.ObjectModel;
using System.ComponentModel;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class TimeAssViewModel : TwoStepViewModelBase<TimeAssService, TimeAssConfig>
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

    [ObservableProperty]
    private string previewContent;
    
    private readonly DispatcherTimer previewTimer;

    [ObservableProperty]
    private ObservableCollection<TimeAssVideoFileInfo> files;

    public TimeAssViewModel(ViewModelServices services) : base(services)
    {
        previewTimer = new DispatcherTimer()
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        previewTimer.Tick += (s, e) =>
        {
            PreviewContent = DateTime.Now.ToString(Config.Format.TimeFormat);
        };
    }

    public override void OnEnter()
    {
        base.OnEnter();
        previewTimer.Start();
    }

    public override Task OnExitAsync(CancelEventArgs args)
    {
        previewTimer.Stop();
        return base.OnExitAsync(args);
    }

    protected override Task OnInitializedAsync()
    {
        Files = new ObservableCollection<TimeAssVideoFileInfo>(Service.Files);
        return base.OnInitializedAsync();
    }

    protected override void OnReset()
    {
        base.OnReset();
        Files = null;
    }
}