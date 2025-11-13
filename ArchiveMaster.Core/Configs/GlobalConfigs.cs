using ArchiveMaster.Enums;
using ArchiveMaster.Services;

namespace ArchiveMaster.Configs;

public class GlobalConfigs
{
    public static readonly string DefaultPassword = nameof(ArchiveMaster);

    public static GlobalConfigs Instance { get; internal set; } = GetEmptyInstance();

    public FilenameCasePolicy FileNameCase { get; set; } = FilenameCasePolicy.Auto;

    public bool DebugMode { get; set; }

    public int DebugModeLoopDelay { get; set; } = 30;

    public DeleteMode DeleteMode { get; set; } = DeleteMode.RecycleBinPrefer;

    public string SpecialDeleteFolderName { get; set; }

    public char FlattenPathSeparatorReplacement { get; set; } = '-';

    public bool IsMainViewPaneOpen { get; set; } = true;

    public bool IsSmoothScrollingEnabled { get; set; } = true;

    public string MasterPassword { get; set; } = SecurePasswordStoreService.EncryptMasterPassword(DefaultPassword);

    public static GlobalConfigs GetEmptyInstance()
    {
        string name = "AM回收站";
        var instance = new GlobalConfigs();
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            var home = Path.GetRelativePath("/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            instance.SpecialDeleteFolderName = Path.Combine(home, $".{name}");
        }
        else
        {
            instance.SpecialDeleteFolderName = name;
        }

        return instance;
    }
}