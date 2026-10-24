using System.Reflection;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;

namespace ArchiveMaster.Tests.Infrastructure;

/// <summary>
/// 快照并恢复进程级全局状态。测试程序集关闭并行后，仍然需要防止用例之间互相污染。
/// 注意：<see cref="GlobalConfigs.AiProviders"/> 是引用类型，本类没有深拷贝；涉及 AI 供应商配置的用例需自行恢复。
/// </summary>
public sealed class GlobalStateScope : IDisposable
{
    private readonly GlobalConfigs originalInstance;
    private readonly GlobalConfigs snapshot;
    private readonly FilenameCasePolicy fileNameCase;
    private readonly DeleteMode deleteMode;
    private readonly string specialDeleteFolderName;
    private readonly char flattenPathSeparatorReplacement;
    private readonly bool debugMode;
    private readonly int debugModeLoopDelay;
    private readonly bool isMainViewPaneOpen;
    private readonly bool isSmoothScrollingEnabled;
    private readonly int cliMaxItems;
    private readonly string masterPassword;
    private readonly string ffmpegDir;
    private readonly TestLogger testLogger;

    public GlobalStateScope()
    {
        testLogger = new TestLogger();
        originalInstance = GlobalConfigs.Instance;
        snapshot = originalInstance;
        fileNameCase = snapshot.FileNameCase;
        deleteMode = snapshot.DeleteMode;
        specialDeleteFolderName = snapshot.SpecialDeleteFolderName;
        flattenPathSeparatorReplacement = snapshot.FlattenPathSeparatorReplacement;
        debugMode = snapshot.DebugMode;
        debugModeLoopDelay = snapshot.DebugModeLoopDelay;
        isMainViewPaneOpen = snapshot.IsMainViewPaneOpen;
        isSmoothScrollingEnabled = snapshot.IsSmoothScrollingEnabled;
        cliMaxItems = snapshot.CliMaxItems;
        masterPassword = snapshot.MasterPassword;
        ffmpegDir = snapshot.FFmpegDir;

        // 单测绝不能把文件丢进系统回收站或磁盘根目录。
        // AppConfig.Initialize 会整体替换 GlobalConfigs.Instance，替换后的实例也要重新套上护栏。
        GlobalConfigs.InstanceChanged += ProtectInstance;
        snapshot.DeleteMode = DeleteMode.DeleteDirectly;
    }

    private static void ProtectInstance(GlobalConfigs instance)
    {
        if (instance != null)
        {
            instance.DeleteMode = DeleteMode.DeleteDirectly;
        }
    }

    public void Dispose()
    {
        GlobalConfigs.InstanceChanged -= ProtectInstance;

        if (!ReferenceEquals(GlobalConfigs.Instance, originalInstance))
        {
            PropertyInfo property = typeof(GlobalConfigs).GetProperty(nameof(GlobalConfigs.Instance),
                BindingFlags.Public | BindingFlags.Static)!;
            property.GetSetMethod(true)!.Invoke(null, [originalInstance]);
        }

        snapshot.FileNameCase = fileNameCase;
        snapshot.DeleteMode = deleteMode;
        snapshot.SpecialDeleteFolderName = specialDeleteFolderName;
        snapshot.FlattenPathSeparatorReplacement = flattenPathSeparatorReplacement;
        snapshot.DebugMode = debugMode;
        snapshot.DebugModeLoopDelay = debugModeLoopDelay;
        snapshot.IsMainViewPaneOpen = isMainViewPaneOpen;
        snapshot.IsSmoothScrollingEnabled = isSmoothScrollingEnabled;
        snapshot.CliMaxItems = cliMaxItems;
        snapshot.MasterPassword = masterPassword;
        snapshot.FFmpegDir = ffmpegDir;
        testLogger.Dispose();
    }
}
