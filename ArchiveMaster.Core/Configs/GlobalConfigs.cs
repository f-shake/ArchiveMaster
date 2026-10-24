using ArchiveMaster.Enums;
using ArchiveMaster.Services;

namespace ArchiveMaster.Configs;

public class GlobalConfigs
{
    public static readonly string DefaultPassword = nameof(ArchiveMaster);

    private static GlobalConfigs instance = GetEmptyInstance();

    /// <summary>
    /// 全局配置实例被替换时触发（例如 AppConfig.Initialize 重新反序列化 Globals）。
    /// 测试基建用它保证替换后的实例仍然禁止删除到真实回收站，生产代码不订阅。
    /// </summary>
    public static event Action<GlobalConfigs> InstanceChanged;

    public static GlobalConfigs Instance
    {
        get => instance;
        internal set
        {
            instance = value;
            InstanceChanged?.Invoke(value);
        }
    }

    public FilenameCasePolicy FileNameCase { get; set; } = FilenameCasePolicy.Auto;

    public bool DebugMode { get; set; }

    public int DebugModeLoopDelay { get; set; } = 30;

    public DeleteMode DeleteMode { get; set; } = DeleteMode.RecycleBinPrefer;

    public string SpecialDeleteFolderName { get; set; }

    public char FlattenPathSeparatorReplacement { get; set; } = '-';

    public bool IsMainViewPaneOpen { get; set; } = true;

    public bool IsSmoothScrollingEnabled { get; set; } = true;

    /// <summary>
    /// CLI 输出中最多显示的条目数，完整结果始终写入 JSONL 报告。
    /// </summary>
    public int CliMaxItems { get; set; } = 50;

    public string MasterPassword { get; set; } = SecurePasswordStoreService.EncryptMasterPassword(DefaultPassword);

    public string FFmpegDir { get; set; } = "";

    public AiProvidersConfig AiProviders { get; set; } = new AiProvidersConfig();

    public static GlobalConfigs GetEmptyInstance()
    {
        string name = $"{nameof(ArchiveMaster)}回收站";
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