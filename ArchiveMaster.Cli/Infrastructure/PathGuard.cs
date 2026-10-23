using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Cli;

/// <summary>
/// 把配置与准备结果限制在允许的根目录内，避免被诱导去操作白名单之外的路径。
/// 这是尽力而为的护栏，不是安全沙箱：批量命令行等工具可以自行启动进程访问任意路径。
/// </summary>
public sealed class PathGuard
{
    private const int MaxDepth = 8;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly List<(string NoSeparator, string WithSeparator)> roots = [];

    private PathGuard()
    {
    }

    public static PathGuard Create(IEnumerable<string> allowRoots)
    {
        var guard = new PathGuard();
        foreach (string root in allowRoots ?? [])
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            // 根路径（C:\、/）本身以分隔符结尾，不能再去补一个，否则会变成 C:\\
            string withSeparator = full.EndsWith(Path.DirectorySeparatorChar)
                ? full
                : full + Path.DirectorySeparatorChar;
            guard.roots.Add((full, withSeparator));
        }

        return guard;
    }

    public bool IsEmpty => roots.Count == 0;

    public void ValidateConfig(ConfigBase config)
    {
        if (IsEmpty || config == null)
        {
            return;
        }

        // 配置里可能嵌套列表/子对象（如 OfflineSync 的目录匹配），必须递归检查，
        // 不能只看顶层字符串属性，否则白名单可以被绕过去。
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        ValidateValue(config, 0, visited);
    }

    public void ValidateItems(IEnumerable<SimpleFileInfo> files)
    {
        if (IsEmpty || files == null)
        {
            return;
        }

        foreach (SimpleFileInfo file in files)
        {
            if (string.IsNullOrWhiteSpace(file?.Path))
            {
                continue;
            }

            if (Path.IsPathRooted(file.Path))
            {
                EnsureAllowed(file.Path);
            }
            else
            {
                EnsureAllowed(Path.GetFullPath(file.Path));
            }
        }
    }

    public void EnsureAllowed(string path)
    {
        if (IsEmpty)
        {
            return;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            throw new CliException(2, $"路径无法解析：{path}（{ex.Message}）");
        }

        string normalized = Path.TrimEndingDirectorySeparator(full);
        if (roots.Any(p => string.Equals(normalized, p.NoSeparator, PathComparison)
                           || normalized.StartsWith(p.WithSeparator, PathComparison)))
        {
            return;
        }

        throw new CliException(2,
            $"路径不在允许范围内：{path}（允许：{string.Join("；", roots.Select(p => p.NoSeparator))}）");
    }

    private void ValidateValue(object value, int depth, HashSet<object> visited)
    {
        if (value == null || depth > MaxDepth)
        {
            return;
        }

        if (value is string text)
        {
            ValidateString(text);
            return;
        }

        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(DateTime)
            || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid)
            || value is Type || value is Delegate)
        {
            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (object item in enumerable)
            {
                ValidateValue(item, depth + 1, visited);
            }

            return;
        }

        if (!type.IsClass || !visited.Add(value))
        {
            return;
        }

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            // 只检查配置自己的对象图；框架对象（如 ObservableObject 的事件订阅者）不碰
            if (property.DeclaringType == typeof(object))
            {
                continue;
            }

            ValidateValue(property.GetValue(value), depth + 1, visited);
        }
    }

    /// <summary>
    /// 配置里常见的多值路径用换行、竖线或分号分隔；整串当成一个路径会误判。
    /// </summary>
    private void ValidateString(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (string part in text.Split(['\r', '\n', '|', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            string path = part.Trim().Trim('"');
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (Path.IsPathRooted(path))
            {
                EnsureAllowed(path);
            }
            else if (IsRelativePathLike(path)
                     || path.Contains(Path.DirectorySeparatorChar)
                     || path.Contains(Path.AltDirectorySeparatorChar)
                     || RelativePathExists(path))
            {
                // 相对路径会以 CLI 的工作目录为基准被工具使用，必须按实际位置校验
                EnsureAllowed(Path.GetFullPath(path));
            }
        }
    }

    private static bool RelativePathExists(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            return Directory.Exists(full) || File.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRelativePathLike(string path)
    {
        string parent = ".." + Path.DirectorySeparatorChar;
        string current = "." + Path.DirectorySeparatorChar;
        return path == ".." || path.StartsWith(parent, StringComparison.Ordinal)
                             || path.StartsWith(current, StringComparison.Ordinal)
                             || path.StartsWith("../", StringComparison.Ordinal)
                             || path.StartsWith(".\\", StringComparison.Ordinal);
    }
}
