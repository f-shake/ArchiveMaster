using System;

namespace ArchiveMaster.Cli;

/// <summary>
/// 命令与选项的通用策略。放在 Core 里是为了让 CLI 和单元测试共用同一套判定。
/// </summary>
public static class CliCommandPolicy
{
    public static bool IsPlanOrRun(string command)
    {
        return string.Equals(command, "plan", StringComparison.OrdinalIgnoreCase)
               || string.Equals(command, "run", StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureSetAllowed(bool hasSet, string command)
    {
        if (hasSet && !IsPlanOrRun(command))
        {
            throw new CliException(2, $"--set 只用于 plan 和 run，{command} 不支持");
        }
    }
}
