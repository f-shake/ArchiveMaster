using System.Reflection;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;

namespace ArchiveMaster.Tests.Infrastructure;

/// <summary>
/// 快照并恢复进程级全局状态。测试程序集关闭并行后，仍然需要防止用例之间互相污染。
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
        snapshot.DeleteMode = DeleteMode.DeleteDirectly;
    }

    public void Dispose()
    {
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
