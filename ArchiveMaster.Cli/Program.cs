using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ArchiveMaster.Cli.Commands;

namespace ArchiveMaster.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        // 与 GUI 保持一致：EncodingConverter、批量命令行、异地同步都依赖 GBK 等代码页
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp();
                return 0;
            }

            CliArguments arguments = CliArguments.Parse(args);
            if (arguments.Has("help"))
            {
                PrintHelp();
                return 0;
            }

            switch (arguments.Command.ToLowerInvariant())
            {
                case "list":
                    return ListCommand.Execute(arguments);
                case "describe":
                    return DescribeCommand.Execute(arguments);
                case "plan":
                    return await ToolCommand.ExecuteAsync(arguments, false);
                case "run":
                    return await ToolCommand.ExecuteAsync(arguments, true);
                case "docs":
                    return DocsCommand.Execute(arguments);
                case "version":
                case "--version":
                    Console.Out.WriteLine(GetVersion());
                    return 0;
                default:
                    throw new CliException(2,
                        $"未知命令：{arguments.Command}。运行 am --help 查看用法。");
            }
        }
        catch (CliException ex)
        {
            Console.Error.WriteLine($"错误：{ex.Message}");
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"未处理的错误：{ex.Message}");
            return 1;
        }
    }

    private static bool IsHelp(string value)
    {
        return value is "help" or "--help" or "-h";
    }

    private static string GetVersion()
    {
        Version version = Assembly.GetExecutingAssembly().GetName().Version;
        return version == null ? "unknown" : version.ToString(3);
    }

    private static void PrintHelp()
    {
        Console.Out.WriteLine("""
            am - ArchiveMaster 命令行

            用法：
              am list [--json]
              am describe <工具ID> [--json]
              am plan <工具ID> (--config <文件|-> | --preset <名称> [--profile <目录>])
                     [--max-items <N>] [--allow-root <目录>]... [--report <文件>] [--json] [--quiet]
              am run  <工具ID> (--config <文件|-> | --preset <名称> [--profile <目录>]) --yes
                     [--max-items <N>] [--allow-root <目录>]... [--report <文件>] [--json] [--quiet]
              am docs [--out <目录>] [--install] [--source <目录>]
              am --version

            说明：
              plan 执行工具的准备阶段（可能写入目标目录）；run 在同一进程内继续执行第二阶段。
              配置来源二选一：--config 指定单工具配置，或 --preset 复用 profile 里已保存的预设。
              默认 profile 为用户数据目录下的 ArchiveMaster\cli；需要复用 GUI 配置时显式传 --profile。

            退出码：
              0 成功；1 有警告/错误条目或工具异常；2 参数/配置/路径/锁错误；3 已取消；4 缺少 --yes。
            """);
    }
}
