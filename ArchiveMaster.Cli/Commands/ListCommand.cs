using System;
using System.Linq;

namespace ArchiveMaster.Cli.Commands;

public static class ListCommand
{
    public static int Execute(CliArguments args)
    {
        if (args.Positionals.Count > 0)
        {
            throw new CliException(2, $"list 不接受位置参数：{string.Join(" ", args.Positionals)}");
        }

        var tools = CommandContext.CreateCatalog();

        if (args.Has("json"))
        {
            var payload = new
            {
                SchemaVersion = 1,
                Tools = tools.Select(p => new
                {
                    p.Id,
                    p.ModuleId,
                    p.ModuleName,
                    p.Title,
                    p.Description,
                    p.HasPrepareStage,
                    p.PrepareUnsafe,
                    p.HasItemResults,
                }),
            };
            Console.Out.WriteLine(JsonOutput.Serialize(payload));
        }
        else
        {
            HumanOutput.WriteToolList(tools);
        }

        return 0;
    }
}
