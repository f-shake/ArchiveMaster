using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs;

public partial class VerifyConfig : ConfigBase
{
    [ObservableProperty]
    private string packageDir;

    [ObservableProperty]
    private string password;

    public override void Check()
    {
        CheckDir(PackageDir, "源目录");
    }
}