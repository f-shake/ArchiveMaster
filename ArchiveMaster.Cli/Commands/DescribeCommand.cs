using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArchiveMaster.Cli.Commands;

public static class DescribeCommand
{
    public static int Execute(CliArguments args)
    {
        CommandContext.EnsureNoOverrides(args, "describe");
        if (args.Positionals.Count != 1)
        {
            throw new CliException(2, args.Positionals.Count == 0
                ? "缺少工具 ID。用法：am describe <工具ID>"
                : $"describe 只接受一个工具 ID。用法：am describe <工具ID>");
        }

        string id = args.Positionals[0];

        var tools = CommandContext.CreateCatalog();
        ToolDescriptor tool = CommandContext.RequireTool(tools, id, string.Empty);
        CommandContext.EnsureModuleConverters();
        object config = CommandContext.CreateDefaultConfig(tool);

        if (args.Has("json"))
        {
            var root = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["id"] = tool.Id,
                ["module"] = tool.ModuleId,
                ["moduleName"] = tool.ModuleName,
                ["title"] = tool.Title,
                ["description"] = tool.Description,
                ["hasPrepareStage"] = tool.HasPrepareStage,
                ["prepareUnsafe"] = tool.PrepareUnsafe,
                ["hasItemResults"] = tool.HasItemResults,
                ["config"] = JsonSerializer.SerializeToNode(config, tool.ConfigType,
                    ArchiveMaster.Configs.AppConfig.CreateJsonOptions()),
            };
            Console.Out.WriteLine(root.ToJsonString(JsonOutput.Result));
        }
        else
        {
            HumanOutput.WriteDescribe(tool, config);
        }

        return 0;
    }
}
