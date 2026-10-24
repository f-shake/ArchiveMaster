using System;
using System.Collections.Generic;

namespace ArchiveMaster.Cli;

/// <summary>
/// 手写参数解析：参数面很小，引第三方解析库的维护成本高于收益。
/// </summary>
public sealed class CliArguments
{
    private static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase)
    {
        "json", "yes", "quiet", "install", "help",
    };

    private static readonly HashSet<string> ValueOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "profile", "config", "preset", "report", "max-items", "allow-root", "source", "out", "set",
    };

    private readonly Dictionary<string, List<string>> options = new(StringComparer.OrdinalIgnoreCase);

    private CliArguments()
    {
    }

    public string Command { get; private set; }

    public List<string> Positionals { get; } = [];

    public static CliArguments Parse(string[] args)
    {
        var result = new CliArguments();
        if (args.Length == 0)
        {
            return result;
        }

        result.Command = args[0];
        bool positionalOnly = false;

        for (int i = 1; i < args.Length; i++)
        {
            string token = args[i];
            if (!positionalOnly && token == "--")
            {
                positionalOnly = true;
                continue;
            }

            if (positionalOnly || !token.StartsWith("--", StringComparison.Ordinal))
            {
                result.Positionals.Add(token);
                continue;
            }

            string name = token[2..];
            string value = null;
            int equalsIndex = name.IndexOf('=');
            if (equalsIndex >= 0)
            {
                value = name[(equalsIndex + 1)..];
                name = name[..equalsIndex];
            }

            if (!Flags.Contains(name) && !ValueOptions.Contains(name))
            {
                throw new CliException(2, $"未知选项：--{name}");
            }

            if (Flags.Contains(name))
            {
                if (value != null)
                {
                    throw new CliException(2, $"选项 --{name} 不接受值");
                }

                result.AddOption(name, "true");
                continue;
            }

            if (value == null)
            {
                if (i + 1 >= args.Length)
                {
                    throw new CliException(2, $"选项 --{name} 缺少值");
                }

                value = args[++i];
            }

            result.AddOption(name, value);
        }

        return result;
    }

    public bool Has(string name)
    {
        return options.ContainsKey(name);
    }

    public string Get(string name, string defaultValue = null)
    {
        return options.TryGetValue(name, out List<string> values) ? values[^1] : defaultValue;
    }

    public IReadOnlyList<string> GetAll(string name)
    {
        return options.TryGetValue(name, out List<string> values) ? values : [];
    }

    public int GetInt(string name, int defaultValue)
    {
        string value = Get(name);
        if (value == null)
        {
            return defaultValue;
        }

        if (!int.TryParse(value, out int parsed))
        {
            throw new CliException(2, $"选项 --{name} 需要整数，实际为：{value}");
        }

        return parsed;
    }

    private void AddOption(string name, string value)
    {
        if (!options.TryGetValue(name, out List<string> values))
        {
            values = [];
            options.Add(name, values);
        }

        values.Add(value);
    }
}
