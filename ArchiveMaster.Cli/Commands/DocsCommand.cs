using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using ArchiveMaster.Configs;

namespace ArchiveMaster.Cli.Commands;

/// <summary>
/// 生成 Agent 技能所需的资产，并可复制到用户级技能目录。
/// </summary>
public static class DocsCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public static int Execute(CliArguments args)
    {
        CommandContext.EnsureNoOverrides(args, "docs");
        if (args.Positionals.Count > 0)
        {
            throw new CliException(2, $"docs 不接受位置参数：{string.Join(" ", args.Positionals)}");
        }

        string outDir = args.Get("out");
        bool install = args.Has("install");

        if (outDir == null && !install)
        {
            outDir = DefaultSkillDirectory;
        }

        if (outDir != null)
        {
            Generate(outDir);
        }

        if (install)
        {
            // 安装源默认是仓库里的技能目录；--out 只控制生成位置，避免把临时目录里
            // 只有 assets、没有 SKILL.md 的半成品装进去。
            string source = Path.GetFullPath(args.Get("source") ?? DefaultSkillDirectory);
            if (!File.Exists(Path.Combine(source, "SKILL.md")))
            {
                throw new CliException(2,
                    $"未找到技能定义：{Path.Combine(source, "SKILL.md")}。请从仓库运行，或用 --source 指定含 SKILL.md 的目录。");
            }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string target = Path.Combine(home, ".agents", "skills", "archive-master");
            CopyDirectory(source, target);
            Console.Out.WriteLine($"已安装技能：{target}");
        }

        return 0;
    }

    private static string DefaultSkillDirectory => "skill";

    private static void Generate(string outDir)
    {
        string directory = Path.GetFullPath(outDir);
        string assetsDir = Path.Combine(directory, "assets");
        string toolsDir = Path.Combine(assetsDir, "tools");
        Directory.CreateDirectory(toolsDir);

        // 清掉旧生成物，工具改名/删除后不会残留指向不存在工具的 md
        foreach (string oldFile in Directory.GetFiles(toolsDir, "*.md"))
        {
            File.Delete(oldFile);
        }

        var tools = CommandContext.CreateCatalog();
        CommandContext.EnsureModuleConverters();
        var catalog = new JsonObject()
        {
            ["schemaVersion"] = 1,
            ["tools"] = new JsonArray(),
        };

        foreach (ToolDescriptor tool in tools)
        {
            object config = CommandContext.CreateDefaultConfig(tool);
            JsonNode configNode = JsonSerializer.SerializeToNode(config, tool.ConfigType,
                AppConfig.CreateJsonOptions());

            catalog["tools"].AsArray().Add(new JsonObject()
            {
                ["id"] = tool.Id,
                ["module"] = tool.ModuleId,
                ["moduleName"] = tool.ModuleName,
                ["title"] = tool.Title,
                ["description"] = tool.Description,
                ["hasPrepareStage"] = tool.HasPrepareStage,
                ["prepareUnsafe"] = tool.PrepareUnsafe,
                ["hasItemResults"] = tool.HasItemResults,
                ["config"] = configNode?.DeepClone(),
            });

            string markdown = BuildToolMarkdown(tool, configNode);
            File.WriteAllText(Path.Combine(toolsDir, $"{tool.Id}.md"), NormalizeNewLines(markdown),
                new UTF8Encoding(false));
        }

        File.WriteAllText(Path.Combine(assetsDir, "tool-catalog.json"),
            NormalizeNewLines(catalog.ToJsonString(JsonOptions)), new UTF8Encoding(false));

        Console.Out.WriteLine($"已生成 {tools.Count} 个工具说明：{assetsDir}");
    }

    private static string BuildToolMarkdown(ToolDescriptor tool, JsonNode configNode)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {tool.Title}");
        builder.AppendLine();
        builder.AppendLine($"- 工具 ID：`{tool.Id}`");
        builder.AppendLine($"- 模块：{tool.ModuleName}（`{tool.ModuleId}`）");
        builder.AppendLine($"- 准备阶段：{(tool.HasPrepareStage ? "有" : "无")}");
        builder.AppendLine($"- 准备阶段会写文件：{(tool.PrepareUnsafe ? "是" : "否")}");
        builder.AppendLine($"- 提供条目级结果：{(tool.HasItemResults ? "是" : "否（结果见 notes）")}");
        builder.AppendLine();
        builder.AppendLine(tool.Description);
        builder.AppendLine();
        builder.AppendLine("## 配置模板");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine(configNode?.ToJsonString(JsonOptions));
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## 配置字段");
        builder.AppendLine();
        builder.AppendLine("| 字段 | 类型 | 默认值 |");
        builder.AppendLine("| --- | --- | --- |");

        foreach (PropertyInfo property in tool.ConfigType
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                     .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                     .Where(p => p.DeclaringType != typeof(object)
                                 && p.DeclaringType != typeof(ConfigBase)
                                 && p.DeclaringType?.FullName !=
                                 "CommunityToolkit.Mvvm.ComponentModel.ObservableObject"))
        {
            string defaultValue = configNode?[property.Name]?.ToJsonString(JsonOptions) ?? "null";
            builder.AppendLine(
                $"| `{property.Name}` | {FriendlyType(property.PropertyType)} | `{Escape(defaultValue)}` |");
        }

        return builder.ToString();
    }

    private static string FriendlyType(Type type)
    {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (!underlying.IsGenericType)
        {
            return underlying.Name;
        }

        string name = underlying.Name;
        int tick = name.IndexOf('`');
        if (tick >= 0)
        {
            name = name[..tick];
        }

        string arguments = string.Join(", ", underlying.GetGenericArguments().Select(FriendlyType));
        return $"{name}<{arguments}>";
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }

    /// <summary>
    /// 生成物里的换行统一成 LF，避免 Windows/Linux 上默认配置中的 Environment.NewLine
    /// 让同一份代码生成不同 diff。
    /// </summary>
    private static string NormalizeNewLines(string value)
    {
        return value.Replace("\\r\\n", "\\n").Replace("\r\n", "\n");
    }

    private static void CopyDirectory(string source, string target)
    {
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string destination = Path.Combine(target, relative);
            string directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(file, destination, true);
        }

        Console.Out.WriteLine($"已复制 {source} -> {target}");
    }
}
