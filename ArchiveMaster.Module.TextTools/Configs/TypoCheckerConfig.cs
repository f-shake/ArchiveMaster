using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs;

public partial class TypoCheckerConfig : ConfigBase
{
    [ObservableProperty]
    private TextSource source = new TextSource();

    [ObservableProperty]
    private int minSegmentLength = 200;
    
    public override void Check()
    {
    }
}