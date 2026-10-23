using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Cli;

/// <summary>
/// 无界面的两阶段执行器：与 GUI 的“初始化 → 执行”一一对应，
/// 只是把进度、取消和结果收敛为 CLI 可用的形式。
/// </summary>
public sealed class HeadlessToolRunner(AppConfig appConfig)
{
    private const int MaxNotes = 8;
    private const int MaxExtraFields = 20;

    public async Task<ToolRunResult> RunAsync(ToolDescriptor tool, ConfigBase config, bool execute,
        IProgress<ToolProgress> progress, CancellationToken ct,
        Action<IReadOnlyList<SimpleFileInfo>> onPrepared = null)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(config);

        DateTimeOffset startedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();
        ServiceBase service = null;
        ITwoStepService twoStep = null;
        List<SimpleFileInfo> files = null;
        var notes = new List<string>();
        bool canceled = false;
        string error = null;
        string lastMessage = null;

        try
        {
            service = Activator.CreateInstance(tool.ServiceType, appConfig) as ServiceBase
                      ?? throw new InvalidOperationException($"无法创建工具 {tool.Id} 的服务实例");
            PropertyInfo configProperty = tool.ServiceType.GetProperty("Config")
                                          ?? throw new InvalidOperationException(
                                              $"工具 {tool.Id} 的服务没有 Config 属性");
            configProperty.SetValue(service, config);

            twoStep = (ITwoStepService)service;
            service.MessageUpdate += (_, e) =>
            {
                lastMessage = e.Message;
                progress?.Report(new ToolProgress(null, e.Message));
            };
            service.ProgressUpdate += (_, e) =>
                progress?.Report(new ToolProgress(double.IsNaN(e.Progress) ? null : e.Progress, null));

            if (tool.HasPrepareStage)
            {
                await twoStep.InitializeAsync(ct);
                files = twoStep.GetInitializedFiles()?.ToList();
                onPrepared?.Invoke(files ?? []);
            }
            else if (!execute)
            {
                throw new InvalidOperationException($"工具 {tool.Id} 没有准备阶段，不能单独执行 prepare");
            }

            if (execute)
            {
                await twoStep.ExecuteAsync(ct);
                files = twoStep.GetExecutedFiles()?.ToList() ?? files;
            }
        }
        catch (OperationCanceledException)
        {
            canceled = true;
            TryReadFiles(twoStep, ref files);
        }
        catch (CliException)
        {
            // 路径白名单等 CLI 语义错误不能被包装成“工具异常”，否则退出码会从 2 变 1
            throw;
        }
        catch (Exception ex)
        {
            // 工具异常也要留下结果与报告，否则 AI 只能看到一行 stderr
            error = ex.Message;
            TryReadFiles(twoStep, ref files);
        }
        finally
        {
            if (service != null && (files == null || files.Count == 0))
            {
                notes.AddRange(CollectNotes(service));
                if (notes.Count == 0 && !string.IsNullOrWhiteSpace(lastMessage))
                {
                    notes.Add($"message={Truncate(lastMessage)}");
                }
            }

            service?.Dispose();
        }

        stopwatch.Stop();
        return BuildResult(tool, execute ? "execute" : "prepare", startedAt, stopwatch.ElapsedMilliseconds,
            canceled, error, files, notes);
    }

    private static void TryReadFiles(ITwoStepService twoStep, ref List<SimpleFileInfo> files)
    {
        try
        {
            files = twoStep?.GetExecutedFiles()?.ToList() ?? files;
        }
        catch
        {
            // 取消/异常时读取部分结果是尽力而为，读不到就保留已有列表
        }
    }

    private static ToolRunResult BuildResult(ToolDescriptor tool, string stage, DateTimeOffset startedAt,
        long durationMs, bool canceled, string error, List<SimpleFileInfo> files, List<string> notes)
    {
        List<ToolItemResult> items = files?.Select(MapItem).ToList() ?? [];

        var summary = new ToolSummary()
        {
            Total = items.Count,
            Bytes = items.Sum(p => p.Length),
        };

        foreach (ToolItemResult item in items)
        {
            switch (item.Status)
            {
                case nameof(ProcessStatus.Success):
                    summary.Success++;
                    break;
                case nameof(ProcessStatus.Warn):
                    summary.Warn++;
                    break;
                case nameof(ProcessStatus.Error):
                    summary.Error++;
                    break;
                case nameof(ProcessStatus.Skip):
                    summary.Skip++;
                    break;
                case nameof(ProcessStatus.Cancel):
                    summary.Cancel++;
                    break;
            }
        }

        return new ToolRunResult()
        {
            Tool = tool.Id,
            Stage = stage,
            Canceled = canceled,
            Error = error,
            StartedAt = startedAt,
            DurationMs = durationMs,
            Summary = summary,
            ItemsAvailable = tool.HasItemResults,
            Items = items,
            Notes = notes,
        };
    }

    private static ToolItemResult MapItem(SimpleFileInfo file)
    {
        var item = new ToolItemResult()
        {
            Name = file.Name,
            Path = file.Path,
            RelativePath = file.RelativePath,
            Length = file.Length,
            IsDir = file.IsDir,
            Status = file.Status.ToString(),
            Message = file.Message,
        };

        // 部分工具用分组对象（如按时间段分类的目标目录）作为顶层结果，
        // 它们本身没有路径和长度，需要把子项数量与大小汇总出来，否则 CLI 只能看到空条目。
        if (file.GetType().GetProperty("Subs")?.GetValue(file) is IEnumerable<SimpleFileInfo> subs)
        {
            List<SimpleFileInfo> children = subs.ToList();
            item.ChildCount = children.Count;
            if (item.Length == 0)
            {
                item.Length = children.Sum(p => p.Length);
            }
        }

        item.Extra = CollectItemExtra(file);
        return item;
    }

    /// <summary>
    /// 把工具自有的简单结果字段带出来（检测编码、新路径、视频信息等），
    /// 只限值类型/字符串，避免把整棵对象图塞进列表。
    /// </summary>
    private static Dictionary<string, string> CollectItemExtra(SimpleFileInfo file)
    {
        Dictionary<string, string> extra = null;
        foreach (PropertyInfo property in file.GetType()
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0
                || property.DeclaringType == typeof(SimpleFileInfo)
                || property.DeclaringType == typeof(object))
            {
                continue;
            }

            Type type = property.PropertyType;
            Type underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (!(underlying.IsPrimitive || underlying.IsEnum || underlying == typeof(string)
                  || underlying == typeof(DateTime) || underlying == typeof(decimal)
                  || underlying == typeof(Guid)))
            {
                continue;
            }

            try
            {
                object value = property.GetValue(file);
                if (value == null)
                {
                    continue;
                }

                extra ??= new Dictionary<string, string>(StringComparer.Ordinal);
                extra[property.Name] = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (extra.Count >= MaxExtraFields)
                {
                    break;
                }
            }
            catch
            {
                // 结果对象的属性 getter 不应影响整个运行
            }
        }

        return extra;
    }

    /// <summary>
    /// 没有条目级结果时，从服务公开属性提取少量可读摘要（数量、状态、路径等）。
    /// </summary>
    private static IEnumerable<string> CollectNotes(ServiceBase service)
    {
        var notes = new List<string>();
        foreach (PropertyInfo property in service.GetType()
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0
                || property.Name == "Config")
            {
                continue;
            }

            Type type = property.PropertyType;
            Type underlying = Nullable.GetUnderlyingType(type) ?? type;
            try
            {
                object value = property.GetValue(service);
                if (value == null)
                {
                    continue;
                }

                if (underlying.IsPrimitive || underlying.IsEnum || underlying == typeof(string)
                    || underlying == typeof(DateTime) || underlying == typeof(decimal)
                    || underlying == typeof(Guid))
                {
                    string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        notes.Add($"{property.Name}={Truncate(text)}");
                    }
                }
                else if (value is ICollection collection)
                {
                    notes.Add($"{property.Name}.Count={collection.Count}");
                }
            }
            catch
            {
                // 忽略无法读取的属性
            }

            if (notes.Count >= MaxNotes)
            {
                break;
            }
        }

        return notes;
    }

    private static string Truncate(string value)
    {
        const int max = 120;
        return value.Length <= max ? value : value[..max] + "…";
    }
}
