namespace ArchiveMaster.Cli;

/// <summary>
/// 执行进度。Percent 为 null 表示不确定进度。
/// </summary>
public sealed record ToolProgress(double? Percent, string Message);
