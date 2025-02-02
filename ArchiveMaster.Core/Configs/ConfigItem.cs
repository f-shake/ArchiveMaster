namespace ArchiveMaster.Configs;

public class ConfigItem : ConfigMetadata
{
    public ConfigItem()
    {
    }

    public ConfigItem(string key, object config, string version)
    {
        Key = key;
        Config = config;
        Preset = version;
    }

    /// <summary>
    /// 配置对象
    /// </summary>
    public object Config { get; set; }

    /// <summary>
    /// 启用多版本配置时的版本号
    /// </summary>
    public string Preset { get; set; }
}