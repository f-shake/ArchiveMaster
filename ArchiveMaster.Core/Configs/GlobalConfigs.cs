using ArchiveMaster.Enums;

namespace ArchiveMaster.Configs;

public class GlobalConfigs
{
    public static GlobalConfigs Instance { get; internal set; } = new GlobalConfigs();
    public FilenameCasePolicy FileNameCase { get; set; } = FilenameCasePolicy.Auto;

    public bool DebugMode { get; set; }

    public int DebugModeLoopDelay { get; set; } = 30;

    public bool PreferDeleteToRecycleBin { get; set; } = true;

    public char FlattenPathSeparatorReplacement { get; set; } = '-';

    public bool IsMainViewPlanOpen { get; set; } = true;

    public string MajorPassword { get; set; } = nameof(ArchiveMaster);
}