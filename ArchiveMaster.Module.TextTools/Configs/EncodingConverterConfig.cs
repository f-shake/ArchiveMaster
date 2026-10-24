using System;
using System.Collections.ObjectModel;
using System.Text;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;
using FzLib.Text;

namespace ArchiveMaster.Configs
{
    public partial class EncodingConverterConfig : ConfigBase
    {
        [ObservableProperty]
        private string dir;

        [ObservableProperty]
        private FileFilterRule filter = new FileFilterRule();

        [ObservableProperty]
        private string targetEncoding = "UTF-8";

        [ObservableProperty]
        private bool? withBom;
        
        [ObservableProperty]
        private bool saveToAnotherDir;
        
        [ObservableProperty]
        private string anotherDir;

        public override void Check()
        {
            CheckDir(Dir, "目录");
            if (SaveToAnotherDir)
            {
                CheckDir(AnotherDir, "另存目录");
                EnsureTargetNotOverlappingSource();
            }

            CheckEmpty(TargetEncoding, "目标编码");
            try
            {
                Encoding.GetEncoding(TargetEncoding);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException($"目标编码{TargetEncoding}无效");
            }
        }

        /// <summary>
        /// 另存目录与源目录相同或互相包含时，目标路径可能正好等于源文件：
        /// 转换是“先建目标再读源”，会先把源文件截断成 0 字节。
        /// </summary>
        private void EnsureTargetNotOverlappingSource()
        {
            StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string separator = Path.DirectorySeparatorChar.ToString();

            string[] sources = FileNameHelper.GetDirNames(Dir, false);
            string[] targets = FileNameHelper.GetDirNames(AnotherDir, false);
            foreach (string source in sources)
            {
                string sourceFull = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
                foreach (string target in targets)
                {
                    string targetFull = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
                    if (string.Equals(sourceFull, targetFull, comparison)
                        || targetFull.StartsWith(sourceFull + separator, comparison)
                        || sourceFull.StartsWith(targetFull + separator, comparison))
                    {
                        throw new Exception("另存目录不能与源目录相同，也不能与源目录互相包含");
                    }
                }
            }
        }
    }
}