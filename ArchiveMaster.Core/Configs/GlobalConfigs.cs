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

    public bool PreferDeleteToRecycleBin { get; set; } = true;

    public char FlattenPathSeparatorReplacement { get; set; } = '-';

    public bool IsMainViewPlanOpen { get; set; } = true;

    public string MasterPassword { get; set; } = SecurePasswordStoreService.EncryptMasterPassword(DefaultPassword);
}