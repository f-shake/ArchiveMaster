using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs;

public partial class TextEncryptionConfig : ConfigBase
{
    [ObservableProperty]
    private bool? lastOperation = null;

    [ObservableProperty]
    private SecurePassword password = new SecurePassword();

    [ObservableProperty]
    private string prefix = "##";

    [ObservableProperty]
    private string suffix = "##";
    public override void Check()
    {
        CheckEmpty(Password, "密码");
        CheckEmpty(Password.Password, "密码");
    }
}