using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ArchiveMaster.Configs;

namespace ArchiveMaster.Cli;

public static class HumanOutput
{
    public static void WriteToolList(IReadOnlyList<ToolDescriptor> tools)
    {
        Console.Out.WriteLine($"共 {tools.Count} 个可用工具：");
        foreach (ToolDescriptor tool in tools)
        {
            Console.Out.WriteLine($"  {tool.Id,-38} {tool.Title}");
        }
    }

    public static void WriteDescribe(ToolDescriptor tool, object config)
    {
        Console.Out.WriteLine($"{tool.Title}（{tool.Id}）");
        Console.Out.WriteLine(tool.Description);
        Console.Out.WriteLine($"模块：{tool.ModuleName}    准备阶段：{(tool.HasPrepareStage ? "有" : "无")}");
        Console.Out.WriteLine();
        Console.Out.WriteLine("配置模板：");
        Console.Out.WriteLine(JsonSerializer.Serialize(config, tool.ConfigType, AppConfig.CreateJsonOptions()));
    }

    public static void WriteResult(ToolRunResult result)
    {
        string stage = result.Stage == "prepare" ? "准备" : "执行";
        Console.Out.WriteLine($"{result.Tool} · {stage}{(result.Canceled ? "（已取消）" : string.Empty)}");

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            Console.Out.WriteLine($"错误：{result.Error}");
        }

        if (!result.ItemsAvailable)
        {
            Console.Out.WriteLine("条目级结果：无（工具只返回聚合信息）");
            foreach (string note in result.Notes)
            {
                Console.Out.WriteLine($"  {note}");
            }
        }
        else
        {
            Console.Out.WriteLine(
                $"共 {result.Summary.Total} 项，{FormatBytes(result.Summary.Bytes)}；" +
                $"成功 {result.Summary.Success}，警告 {result.Summary.Warn}，错误 {result.Summary.Error}，" +
                $"跳过 {result.Summary.Skip}，取消 {result.Summary.Cancel}");
        }

        foreach (ToolItemResult item in result.Items)
        {
            string message = string.IsNullOrWhiteSpace(item.Message) ? string.Empty : $"（{item.Message}）";
            string path = !string.IsNullOrWhiteSpace(item.RelativePath)
                ? item.RelativePath
                : !string.IsNullOrWhiteSpace(item.Path)
                    ? item.Path
                    : item.Name;
            Console.Out.WriteLine($"  [{StatusText(item.Status)}] {path}{message}");
            if (item.Extra is { Count: > 0 })
            {
                Console.Out.WriteLine($"      {string.Join("；", item.Extra.Select(p => $"{p.Key}={p.Value}"))}");
            }
        }

        if (result.ItemsTruncated)
        {
            Console.Out.WriteLine($"（仅显示前 {result.Items.Count} 项，完整结果见报告）");
        }

        if (!string.IsNullOrWhiteSpace(result.Report))
        {
            Console.Out.WriteLine($"报告：{result.Report}");
        }
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
    }

    private static string StatusText(string status)
    {
        return status switch
        {
            "Ready" => "就绪",
            "Processing" => "处理中",
            "Success" => "完成",
            "Warn" => "警告",
            "Error" => "错误",
            "Skip" => "跳过",
            "Cancel" => "取消",
            _ => status,
        };
    }
}

public static class ReportWriter
{
    public static string CreateDefaultPath(string toolId)
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "archivemaster");
        string name = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{toolId}.jsonl";
        return System.IO.Path.Combine(directory, name);
    }

    public static string Write(ToolRunResult result, string path)
    {
        string fullPath = System.IO.Path.GetFullPath(path);
        string directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        using var writer = new System.IO.StreamWriter(fullPath, false, new System.Text.UTF8Encoding(false));
        // 首行是运行摘要（含 error/notes），其余行才是逐条结果：
        // 无条目工具或异常时报告也不会是空文件。
        writer.WriteLine(JsonSerializer.Serialize(new
        {
            Type = "summary",
            result.Tool,
            result.Stage,
            result.Error,
            result.Canceled,
            result.ItemsAvailable,
            result.Summary,
            result.Notes,
        }, JsonOutput.Result));

        foreach (ToolItemResult item in result.Items)
        {
            writer.WriteLine(JsonSerializer.Serialize(item, JsonOutput.Result));
        }

        return fullPath;
    }
}
