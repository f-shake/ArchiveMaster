using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs;

public partial class TextEncryptionConfig : ConfigBase
{
    [ObservableProperty]
    private bool autoFlush = true;

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
        if (string.IsNullOrEmpty(Prefix) != string.IsNullOrEmpty(Suffix))
        {
            throw new Exception("密文前缀和后缀必须同时为空或同时非空");
        }
    }
}