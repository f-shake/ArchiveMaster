using System;

namespace ArchiveMaster.Cli;

/// <summary>
/// 带退出码的 CLI 错误。业务层只负责描述问题，退出码由命令层决定。
/// </summary>
public sealed class CliException(int exitCode, string message) : Exception(message)
{
    public int ExitCode { get; } = exitCode;
}
