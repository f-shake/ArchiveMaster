using Avalonia.Data.Converters;
using FzLib.IO;
using System.Globalization;
using System.Text;

namespace ArchiveMaster.Converters
{
    public class FileFilterDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return "未设置筛选";
            }

            if (value is not FileFilterRule rule)
            {
                throw new ArgumentException($"值不是{nameof(FileFilterRule)}", nameof(value));
            }

            return GetDescription(rule);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public string GetDescription(FileFilterRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);
            StringBuilder sb = new StringBuilder();

            if (rule.UseRegex)
            {
                sb.Append("[正则] ");
            }

            var includeParts = new List<string>();
            var excludeParts = new List<string>();

            // 获取默认值
            var defaultRule = new FileFilterRule();
            defaultRule.UseRegex = rule.UseRegex;
            string defaultInclude = defaultRule.IncludeFolders;
            string defaultExcludeFiles = defaultRule.ExcludeFiles;
            string defaultExcludeFolders = defaultRule.ExcludeFolders;

            // 辅助函数：处理规则显示
            string FormatRule(string value, string defaultValue)
            {
                if (string.IsNullOrWhiteSpace(value) || value == defaultValue)
                {
                    return null;
                }

                // 处理多行内容（只显示第一行）
                string firstLine = value.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

                if (firstLine != value)
                {
                    return firstLine + " 等";
                }

                // 截断过长的内容
                const int maxLength = 20;
                return value.Length > maxLength ? value[..maxLength] + "..." : value;
            }

            // 包含规则（白名单）
            string includeFiles = FormatRule(rule.IncludeFiles, defaultInclude);
            if (includeFiles != null) includeParts.Add($"文件（{includeFiles}）");

            string includeFolders = FormatRule(rule.IncludeFolders, defaultInclude);
            if (includeFolders != null) includeParts.Add($"目录（{includeFolders}）");

            string includePaths = FormatRule(rule.IncludePaths, defaultInclude);
            if (includePaths != null) includeParts.Add($"路径（{includePaths}）");

            // 排除规则（黑名单）
            string excludeFiles = FormatRule(rule.ExcludeFiles, defaultExcludeFiles);
            if (excludeFiles != null) excludeParts.Add($"文件（{excludeFiles}）");

            string excludeFolders = FormatRule(rule.ExcludeFolders, defaultExcludeFolders);
            if (excludeFolders != null) excludeParts.Add($"目录（{excludeFolders}）");

            string excludePaths = FormatRule(rule.ExcludePaths, "");
            if (excludePaths != null) excludeParts.Add($"路径（{excludePaths}）");

            // 构建描述
            if (includeParts.Count > 0)
            {
                sb.Append("包含：").Append(string.Join("、", includeParts));
            }
            else
            {
                sb.Append("包含：所有文件");
            }

            if (excludeParts.Count > 0)
            {
                sb.Append("；排除：").Append(string.Join("、", excludeParts));
            }
            else
            {
                sb.Append("；排除：（默认值）");
            }

            return sb.ToString();
        }
    }
}