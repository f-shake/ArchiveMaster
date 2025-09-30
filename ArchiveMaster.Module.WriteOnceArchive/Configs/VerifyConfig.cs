using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs;

public partial class VerifyConfig : ConfigBase
{
    [ObservableProperty]
    private string packageDir;

    [ObservableProperty]
    private SecurePassword password = new();

    public override void Check()
    {
        CheckDir(PackageDir, "源目录");
    }
}