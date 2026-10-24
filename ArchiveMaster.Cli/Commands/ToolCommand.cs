using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Cli.Commands;

/// <summary>
/// plan 与 run 共用的一条执行路径：两者只在“是否执行第二阶段”和确认门槛上不同。
/// </summary>
public static class ToolCommand
{
    public static async Task<int> ExecuteAsync(CliArguments args, bool execute)
    {
        string usage = execute
            ? "用法：am run <工具ID> --config <文件> --yes"
            : "用法：am plan <工具ID> --config <文件>";
        if (args.Positionals.Count == 0)
        {
            throw new CliException(2, $"缺少工具 ID。{usage}");
        }

        if (args.Positionals.Count > 1)
        {
            throw new CliException(2,
                $"多余的参数：{string.Join(" ", args.Positionals.Skip(1))}。{usage}（配置文件请用 --config）");
        }

        string id = args.Positionals[0];
        var tools = CommandContext.CreateCatalog();
        ToolDescriptor tool = CommandContext.RequireTool(tools, id, string.Empty);

        if (tool.PrepareUnsafe)
        {
            Console.Error.WriteLine($"警告：{tool.Id} 的准备阶段会写入目标目录（创建目录、写哈希缓存）。");
        }

        if (!execute && !tool.HasPrepareStage)
        {
            throw new CliException(2, $"工具 {tool.Id} 没有准备阶段，请直接 run --yes。");
        }

        if (execute && !args.Has("yes"))
        {
            throw new CliException(4,
                $"拒绝执行：{tool.Id} 可能修改文件。先用 plan 查看准备结果，确认后加 --yes。");
        }

        if (args.Get("config") != null && args.Get("preset") != null)
        {
            throw new CliException(2, "--config 与 --preset 不能同时使用");
        }

        string profile = CommandContext.ResolveProfile(args);
        AppConfig appConfig = CommandContext.LoadAppConfig(profile);
        var guard = PathGuard.Create(args.GetAll("allow-root"));

        // 先解析、校验会写文件的参数，避免工具已经执行后才因参数错误失败
        int? explicitMaxItems = args.Get("max-items") == null ? null : args.GetInt("max-items", 0);
        string reportArgument = args.Get("report");
        string reportPath = reportArgument == null ? null : Path.GetFullPath(reportArgument);
        if (reportPath != null)
        {
            if (Directory.Exists(reportPath))
            {
                throw new CliException(2, $"--report 指向的是目录，需要文件路径：{reportPath}");
            }

            guard.EnsureAllowed(reportPath);
            string reportDirectory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(reportDirectory))
            {
                try
                {
                    Directory.CreateDirectory(reportDirectory);
                }
                catch (Exception ex)
                {
                    throw new CliException(2, $"--report 目录不可写：{reportDirectory}（{ex.Message}）");
                }
            }
        }

        object config = LoadConfig(tool, appConfig, args);
        ConfigOverrides.Apply(config, tool.Id, args.GetAll("set"));
        // 准备阶段可能读取快照/目录；在它之前先做一次白名单校验
        guard.ValidateConfig((ConfigBase)config);
        try
        {
            await PrepareConfigAsync(tool, config);
        }
        catch (CliException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CliException(2, $"准备配置失败：{ex.Message}");
        }

        // 准备阶段可能补充运行时路径（如 step2 的 MatchingDirs），再校验一次
        guard.ValidateConfig((ConfigBase)config);

        try
        {
            ((ConfigBase)config).Check();
        }
        catch (Exception ex)
        {
            throw new CliException(2, $"配置校验失败：{ex.Message}");
        }

        using ProfileLock profileLock = ProfileLock.Acquire(profile);
        var progress = new ProgressWriter(args.Has("quiet"));
        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            progress.WriteCancel();
            cts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        ToolRunResult result;
        try
        {
            var runner = new HeadlessToolRunner(appConfig);
            result = await runner.RunAsync(tool, (ConfigBase)config, execute, progress, cts.Token,
                files => guard.ValidateItems(files));
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }

        try
        {
            result.Report = ReportWriter.Write(result, reportPath ?? ReportWriter.CreateDefaultPath(tool.Id));
        }
        catch (Exception ex)
        {
            throw new CliException(2, $"写入报告失败：{ex.Message}");
        }

        if (!result.ItemsAvailable)
        {
            Console.Error.WriteLine(
                $"提示：{tool.Id} 不提供条目级结果，摘要见 notes；这并不代表没有执行。");
        }
        else if (result.Error == null && result.Summary.Total == 0)
        {
            Console.Error.WriteLine("没有需要处理的条目。");
        }

        int maxItems = explicitMaxItems ?? GlobalConfigs.Instance.CliMaxItems;
        if (maxItems < 0)
        {
            maxItems = 0;
        }

        if (result.Items.Count > maxItems)
        {
            result.Items = result.Items.Take(maxItems).ToList();
            result.ItemsTruncated = true;
        }

        if (args.Has("json"))
        {
            Console.Out.WriteLine(JsonOutput.Serialize(result));
        }
        else
        {
            HumanOutput.WriteResult(result);
        }

        if (result.Canceled)
        {
            return 3;
        }

        if (result.Error != null)
        {
            return 1;
        }

        return result.Summary.Warn > 0 || result.Summary.Error > 0 ? 1 : 0;
    }

    /// <summary>
    /// 某些工具的准备依赖 GUI 在 OnInitializingAsync 里补的运行时状态，CLI 需要复刻这一步。
    /// 目前只有 offlineSync.step2 的目录自动匹配属于这一类。
    /// </summary>
    private static async Task PrepareConfigAsync(ToolDescriptor tool, object config)
    {
        if (config is not OfflineSyncStep2Config step2
            || step2.MatchingDirs is { Count: > 0 })
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(step2.OffsiteSnapshot))
        {
            return;
        }

        string[] localDirs = string.IsNullOrWhiteSpace(step2.LocalDir)
            ? []
            : step2.LocalDir.Split(['|', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var matched = await Step2Service.MatchLocalAndOffsiteDirsAsync(step2.OffsiteSnapshot, localDirs);
        step2.MatchingDirs = new ObservableCollection<LocalAndOffsiteDir>(matched);
    }

    private static object LoadConfig(ToolDescriptor tool, AppConfig appConfig, CliArguments args)
    {
        string configPath = args.Get("config");
        if (configPath != null)
        {
            try
            {
                string json = JsonOutput.ReadAllInput(configPath);
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement element = document.RootElement;
                // describe 输出的是 { ..., config: {...} }；直接回填时取内层 config，
                // 否则 System.Text.Json 会把整个对象当成“全是未知属性”而得到默认配置。
                if (element.ValueKind == JsonValueKind.Object
                    && element.TryGetProperty("config", out JsonElement inner)
                    && inner.ValueKind == JsonValueKind.Object)
                {
                    element = inner;
                }

                object config = element.Deserialize(tool.ConfigType, AppConfig.CreateJsonOptions());
                return config ?? throw new CliException(2, $"配置内容为空：{configPath}");
            }
            catch (CliException)
            {
                throw;
            }
            catch (JsonException ex)
            {
                throw new CliException(2,
                    $"配置 JSON 解析失败：{ex.Message}（工具 {tool.Id}，类型 {tool.ConfigType.Name}）");
            }
            catch (IOException ex)
            {
                throw new CliException(2, $"读取配置失败：{ex.Message}");
            }
        }

        string explicitPreset = args.Get("preset");
        string preset = explicitPreset ?? AppConfig.DefaultPresetName;
        if (explicitPreset != null && !appConfig.HasPreset(tool.ConfigType, preset))
        {
            throw new CliException(2,
                $"预设不存在：{preset}（工具 {tool.Id}）。先运行 am list 或在 GUI 中保存该预设。");
        }

        try
        {
            return appConfig.GetOrCreateConfig(tool.ConfigType, preset);
        }
        catch (Exception ex)
        {
            throw new CliException(2, $"读取预设失败：{ex.Message}");
        }
    }
}
