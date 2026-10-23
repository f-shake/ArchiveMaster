using System;
using System.Collections.Generic;

namespace ArchiveMaster.Cli;

/// <summary>
/// CLI 输出契约。schemaVersion 变更时需同步技能文档。
/// </summary>
public sealed class ToolRunResult
{
    public int SchemaVersion { get; init; } = 1;

    public string Tool { get; set; }

    /// <summary>
    /// prepare（plan）或 execute（run）。
    /// </summary>
    public string Stage { get; set; }

    public bool Canceled { get; set; }

    /// <summary>
    /// 工具抛出的异常消息。非空时 CLI 仍会输出 JSON 与报告，并以退出码 1 结束。
    /// </summary>
    public string Error { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public long DurationMs { get; set; }

    public ToolSummary Summary { get; set; } = new();

    /// <summary>
    /// 工具是否提供条目级结果。部分工具（如快照、打包、AI 文本处理）只返回聚合信息，
    /// 此时 Items 为空而 Notes 会给出可读的摘要。
    /// </summary>
    public bool ItemsAvailable { get; set; } = true;

    public List<ToolItemResult> Items { get; set; } = [];

    /// <summary>
    /// 无条目级结果时，从服务公开属性提取的摘要（如 Packages.Count=3）。
    /// </summary>
    public List<string> Notes { get; set; } = [];

    public bool ItemsTruncated { get; set; }

    /// <summary>
    /// 完整 JSONL 报告路径。
    /// </summary>
    public string Report { get; set; }
}

public sealed class ToolSummary
{
    public int Total { get; set; }

    public int Success { get; set; }

    public int Warn { get; set; }

    public int Error { get; set; }

    public int Skip { get; set; }

    public int Cancel { get; set; }

    public long Bytes { get; set; }
}

public sealed class ToolItemResult
{
    public string Name { get; set; }

    public string Path { get; set; }

    public string RelativePath { get; set; }

    public long Length { get; set; }

    public bool IsDir { get; set; }

    /// <summary>
    /// 分组型条目（如按时间分类的目标目录）的子项数量；普通文件为 null。
    /// </summary>
    public int? ChildCount { get; set; }

    /// <summary>
    /// ProcessStatus 的枚举名。
    /// </summary>
    public string Status { get; set; }

    public string Message { get; set; }

    /// <summary>
    /// 工具专有字段（如检测出的编码、视频信息、新路径）。只包含简单类型，避免 JSON 爆炸。
    /// </summary>
    public Dictionary<string, string> Extra { get; set; }
}
