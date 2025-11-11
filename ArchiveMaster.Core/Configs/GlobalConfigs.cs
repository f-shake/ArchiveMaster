using ArchiveMaster.Enums;
using ArchiveMaster.Services;

namespace ArchiveMaster.Configs;

public class GlobalConfigs
{
    public static readonly string DefaultPassword = nameof(ArchiveMaster);

    public static GlobalConfigs Instance { get; internal set; } = new GlobalConfigs();

    public FilenameCasePolicy FileNameCase { get; set; } = FilenameCasePolicy.Auto;

    public bool DebugMode { get; set; }

    public int DebugModeLoopDelay { get; set; } = 30;

    public DeleteMode DeleteMode { get; set; } = DeleteMode.RecycleBinPrefer;

    public string SpecialDeleteFolderName { get; set; } = "AM回收站";

    public char FlattenPathSeparatorReplacement { get; set; } = '-';

    public bool IsMainViewPaneOpen { get; set; } = true;

    public bool IsSmoothScrollingEnabled { get; set; } = true;

    public string MasterPassword { get; set; } = SecurePasswordStoreService.EncryptMasterPassword(DefaultPassword);
}