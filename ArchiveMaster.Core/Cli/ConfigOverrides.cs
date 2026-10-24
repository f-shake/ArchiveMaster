using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ArchiveMaster.Configs;

namespace ArchiveMaster.Cli;

/// <summary>
/// 把 --set 字段=值 覆盖到工具配置对象上。
/// 只负责配置层覆盖，不加载配置、不校验路径、不执行工具。
/// </summary>
public static class ConfigOverrides
{
    public static void Apply(object config, string toolId, IEnumerable<string> assignments)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (assignments == null)
        {
            return;
        }

        foreach (string assignment in assignments)
        {
            if (string.IsNullOrWhiteSpace(assignment))
            {
                throw new CliException(2, "--set 参数不能为空");
            }

            int equalsIndex = assignment.IndexOf('=');
            if (equalsIndex < 0)
            {
                throw new CliException(2, $"--set 参数格式错误：{assignment}，应为 字段=值");
            }

            string path = assignment[..equalsIndex].Trim();
            string value = assignment[(equalsIndex + 1)..];
            if (path.Length == 0)
            {
                throw new CliException(2, $"--set 的字段名不能为空：{assignment}");
            }

            string[] segments = path.Split('.');
            if (segments.Any(string.IsNullOrWhiteSpace))
            {
                throw new CliException(2, $"--set 的字段路径不合法：{path}");
            }

            ApplyOne(config, toolId, path, segments, value);
        }
    }

    private static void ApplyOne(object config, string toolId, string path, string[] segments, string value)
    {
        object current = config;
        for (int i = 0; i < segments.Length; i++)
        {
            PropertyInfo property = FindProperty(current.GetType(), segments[i], toolId, path);
            if (i == segments.Length - 1)
            {
                SetValue(current, property, path, value);
                return;
            }

            object next;
            try
            {
                next = property.GetValue(current);
            }
            catch (Exception ex)
            {
                throw new CliException(2, $"配置字段 {path} 的上级字段读取失败：{ex.Message}");
            }

            if (next == null)
            {
                next = CreateNestedObject(property, path);
                if (!property.CanWrite)
                {
                    throw new CliException(2, $"配置字段 {path} 的上级对象为空，无法设置");
                }

                try
                {
                    property.SetValue(current, next);
                }
                catch (Exception ex)
                {
                    throw new CliException(2, $"配置字段 {path} 的上级对象设置失败：{ex.Message}");
                }
            }
            else if (IsScalarValue(next))
            {
                throw new CliException(2, $"配置字段 {path} 的上级字段不是对象");
            }

            current = next;
        }
    }

    private static object CreateNestedObject(PropertyInfo property, string path)
    {
        Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (type.IsValueType || type.IsAbstract || type.IsInterface
            || type.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new CliException(2, $"配置字段 {path} 的上级对象为空，无法设置");
        }

        try
        {
            return Activator.CreateInstance(type)
                   ?? throw new CliException(2, $"配置字段 {path} 的上级对象为空，无法设置");
        }
        catch (CliException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CliException(2, $"配置字段 {path} 的上级对象创建失败：{ex.Message}");
        }
    }

    private static void SetValue(object target, PropertyInfo property, string path, string rawValue)
    {
        if (!property.CanWrite)
        {
            throw new CliException(2, $"配置字段 {path} 是只读的，不能通过 --set 修改");
        }

        object value = ConvertValue(rawValue, property.PropertyType, path);
        try
        {
            property.SetValue(target, value);
        }
        catch (Exception ex)
        {
            throw new CliException(2, $"配置字段 {path} 设置失败：{ex.Message}");
        }
    }

    private static PropertyInfo FindProperty(Type type, string name, string toolId, string fullPath)
    {
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        PropertyInfo exact = properties.FirstOrDefault(p => p.Name == name);
        if (exact != null)
        {
            return exact;
        }

        PropertyInfo[] matches = properties
            .Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length == 0)
        {
            throw new CliException(2, $"工具 {toolId} 没有配置字段：{fullPath}");
        }

        if (matches.Length > 1)
        {
            throw new CliException(2, $"配置字段 {fullPath} 存在多个大小写匹配，请使用完整名称");
        }

        return matches[0];
    }

    private static bool IsScalarValue(object value)
    {
        Type type = value.GetType();
        return type == typeof(string) || type.IsPrimitive || type.IsEnum
               || type == typeof(decimal) || type == typeof(DateTime)
               || type == typeof(DateTimeOffset) || type == typeof(TimeSpan)
               || type == typeof(Guid);
    }

    private static object ConvertValue(string rawValue, Type targetType, string path)
    {
        Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (type == typeof(string))
        {
            return rawValue;
        }

        // 字符串字段的 null 是字面量；其他字段一律拒绝，避免把“清空”误当成值。
        if (string.Equals(rawValue.Trim(), "null", StringComparison.OrdinalIgnoreCase))
        {
            throw new CliException(2, $"配置字段 {path} 不支持 null，请改用 --config");
        }

        if (type == typeof(SecurePassword))
        {
            return ConvertSecurePassword(rawValue, path);
        }

        if (type == typeof(bool))
        {
            string text = rawValue.Trim();
            if (bool.TryParse(text, out bool boolValue))
            {
                return boolValue;
            }

            if (text == "1")
            {
                return true;
            }

            if (text == "0")
            {
                return false;
            }

            throw CannotParse(path, targetType, rawValue);
        }

        if (type.IsEnum)
        {
            try
            {
                return Enum.Parse(type, rawValue.Trim(), true);
            }
            catch (Exception)
            {
                throw CannotParse(path, targetType, rawValue);
            }
        }

        if (type == typeof(TimeSpan))
        {
            if (TimeSpan.TryParse(rawValue.Trim(), CultureInfo.InvariantCulture, out TimeSpan timeSpan))
            {
                return timeSpan;
            }

            throw CannotParse(path, targetType, rawValue);
        }

        if (type == typeof(DateTime))
        {
            if (DateTime.TryParse(rawValue.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out DateTime dateTime))
            {
                return dateTime;
            }

            throw CannotParse(path, targetType, rawValue);
        }

        if (type == typeof(DateTimeOffset))
        {
            if (DateTimeOffset.TryParse(rawValue.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out DateTimeOffset dateTimeOffset))
            {
                return dateTimeOffset;
            }

            throw CannotParse(path, targetType, rawValue);
        }

        if (type == typeof(Guid))
        {
            if (Guid.TryParse(rawValue.Trim(), out Guid guid))
            {
                return guid;
            }

            throw CannotParse(path, targetType, rawValue);
        }

        if (type.IsPrimitive || type == typeof(decimal))
        {
            try
            {
                return Convert.ChangeType(rawValue.Trim(), type, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                throw CannotParse(path, targetType, rawValue);
            }
        }

        return ConvertJson(rawValue, targetType, path);
    }

    private static SecurePassword ConvertSecurePassword(string rawValue, string path)
    {
        string trimmed = rawValue.Trim();
        bool startsWithQuote = trimmed.StartsWith("\"", StringComparison.Ordinal);
        bool endsWithQuote = trimmed.EndsWith("\"", StringComparison.Ordinal);

        // 只有显式 JSON 字符串才走 SecurePassword 转换器；普通明文直接构造，
        // 避免 64 位以上十六进制明文被误判成密文而静默变成空密码。
        if (startsWithQuote || endsWithQuote)
        {
            if (!startsWithQuote || !endsWithQuote || trimmed.Length < 2)
            {
                throw new CliException(2, $"配置字段 {path} 的 SecurePassword 字符串格式不完整");
            }

            try
            {
                SecurePassword value = JsonSerializer.Deserialize<SecurePassword>(trimmed,
                    AppConfig.CreateJsonOptions());
                if (value == null)
                {
                    throw new CliException(2, $"配置字段 {path} 不支持 null，请改用 --config");
                }

                return value;
            }
            catch (JsonException ex)
            {
                throw new CliException(2,
                    $"配置字段 {path} 的 JSON 值无法解析为 SecurePassword：{ex.Message}");
            }
        }

        if (trimmed.StartsWith("{", StringComparison.Ordinal)
            || trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            throw new CliException(2,
                $"配置字段 {path} 的 SecurePassword 只接受明文或 JSON 字符串，不接受对象或数组");
        }

        return new SecurePassword(rawValue);
    }

    private static object ConvertJson(string rawValue, Type targetType, string path)
    {
        try
        {
            object value = JsonSerializer.Deserialize(rawValue, targetType, AppConfig.CreateJsonOptions());
            if (value == null)
            {
                throw new CliException(2, $"配置字段 {path} 不支持 null，请改用 --config");
            }

            return value;
        }
        catch (CliException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            throw new CliException(2,
                $"配置字段 {path} 的 JSON 值无法解析为 {FriendlyType(targetType)}：{ex.Message}");
        }
    }

    private static CliException CannotParse(string path, Type targetType, string value)
    {
        return new CliException(2,
            $"配置字段 {path} 需要 {FriendlyType(targetType)} 类型的值，无法解析：{value}");
    }

    private static string FriendlyType(Type type)
    {
        return (Nullable.GetUnderlyingType(type) ?? type).Name;
    }
}
