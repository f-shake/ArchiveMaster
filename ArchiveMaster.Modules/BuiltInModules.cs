namespace ArchiveMaster;

/// <summary>
/// 内置模块清单。GUI 与 CLI 都从这里取模块，避免两处各维护一份列表而漂移。
/// </summary>
/// <remarks>
/// 不包含 DiscArchive（已被 WriteOnceArchive 取代，未进解决方案）与 Test（手工试验模块）。
/// </remarks>
public static class BuiltInModules
{
    public static IReadOnlyList<IModuleInfo> All { get; } =
    [
        new FileToolsModuleInfo(),
        new PhotoToolsModuleInfo(),
        new PhotoTaggingModuleInfo(),
        new VideoToolsModuleInfo(),
        new TextToolsModuleInfo(),
        new OfflineSyncModuleInfo(),
        new WriteOnceArchiveModuleInfo(),
        new FileBackupperModuleInfo(),
    ];
}
