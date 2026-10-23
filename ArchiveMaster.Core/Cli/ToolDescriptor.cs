using System;

namespace ArchiveMaster.Cli;

/// <summary>
/// 一个可被 CLI 调用的工具：由模块面板的 ViewModel 泛型基类反推得到。
/// </summary>
public sealed class ToolDescriptor
{
    public required string Id { get; init; }

    /// <summary>
    /// 模块短名，小驼峰形式（如 fileTools）。
    /// </summary>
    public required string ModuleId { get; init; }

    public required string ModuleName { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required Type ConfigType { get; init; }

    public required Type ServiceType { get; init; }

    /// <summary>
    /// 是否有“准备”阶段（对应 ViewModel 的 EnableInitialize）。
    /// </summary>
    public bool HasPrepareStage { get; init; }

    /// <summary>
    /// 准备阶段是否会写入文件系统。已审计出写操作的工具有：动态固存备份的“打包和备份”
    /// （创建目标目录并写哈希缓存）。未列出的工具按“准备阶段无写入”处理。
    /// </summary>
    public bool PrepareUnsafe { get; init; }

    /// <summary>
    /// 是否提供条目级结果。快照、打包、标签管理等工具只返回聚合信息，CLI 用 Notes 说明。
    /// </summary>
    public bool HasItemResults { get; init; } = true;
}
