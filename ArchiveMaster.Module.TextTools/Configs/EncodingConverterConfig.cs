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
    }
}