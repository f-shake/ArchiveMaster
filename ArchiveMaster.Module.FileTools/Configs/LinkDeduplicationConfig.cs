using ArchiveMaster.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs;

public partial class LinkDeduplicationConfig : ConfigBase
{
    [ObservableProperty]
    private string dir;

    [ObservableProperty]
    private FileFilterRule filter = new FileFilterRule();

    [ObservableProperty]
    private bool allowDifferentTime = true;

    [ObservableProperty]
    private HashAlgorithmType hashType = HashAlgorithmType.SHA256;

    public override void Check()
    {
        CheckDir(Dir, "目录");
    }
}