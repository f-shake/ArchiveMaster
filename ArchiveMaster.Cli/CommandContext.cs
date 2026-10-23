using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArchiveMaster.Configs;

namespace ArchiveMaster.Cli;

/// <summary>
/// 命令层共用：模块目录、profile 定位、AppConfig 装配。
/// </summary>
public static class CommandContext
{
    private static bool moduleConvertersRegistered;

    public static IReadOnlyList<ToolDescriptor> CreateCatalog()
    {
        return ToolCatalog.Build(BuiltInModules.All,
            new HashSet<Type>() { typeof(FileBackupperModuleInfo) });
    }

    public static ToolDescriptor RequireTool(IReadOnlyList<ToolDescriptor> tools, string id,
        string usage)
    {
        ToolDescriptor tool = tools.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        return tool ?? throw new CliException(2, $"未知工具：{id}。运行 am list 查看可用工具。{usage}");
    }

    public static string ResolveProfile(CliArguments args)
    {
        string profile = args.Get("profile");
        if (profile != null)
        {
            return Path.GetFullPath(profile);
        }

        // 默认放到可写的用户数据目录：程序可能安装在只读目录，且不应在 exe 旁留锁文件。
        string baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.GetTempPath();
        }

        return Path.Combine(baseDirectory, "ArchiveMaster", "cli");
    }

    public static AppConfig LoadAppConfig(string profile)
    {
        Directory.CreateDirectory(profile);
        var appConfig = new AppConfig(Path.Combine(profile, "configs.json"));

        foreach (IModuleInfo module in BuiltInModules.All)
        {
            foreach (ConfigMetadata config in module.Configs ?? [])
            {
                appConfig.RegisterConfig(config);
            }
        }

        EnsureModuleConverters();
        appConfig.Initialize(applyPendingLoad: false);
        if (appConfig.LoadError != null)
        {
            Console.Error.WriteLine($"警告：{appConfig.LoadError.Message}");
        }

        return appConfig;
    }

    /// <summary>
    /// 注册模块自带的 JSON 转换器；describe / docs 不加载配置，也必须调用，否则配置模板的序列化行为与运行期不一致。
    /// </summary>
    public static void EnsureModuleConverters()
    {
        if (moduleConvertersRegistered)
        {
            return;
        }

        foreach (IModuleInfo module in BuiltInModules.All)
        {
            foreach (var converter in module.JsonConverters ?? [])
            {
                AppConfig.AddJsonConverter(converter);
            }
        }

        moduleConvertersRegistered = true;
    }

    public static object CreateDefaultConfig(ToolDescriptor tool)
    {
        return Activator.CreateInstance(tool.ConfigType)
               ?? throw new CliException(2, $"无法创建 {tool.Id} 的配置实例");
    }
}
