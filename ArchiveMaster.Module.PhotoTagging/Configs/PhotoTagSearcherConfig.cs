using System;
using ArchiveMaster.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs
{
    public partial class PhotoTagSearcherConfig : ConfigBase
    {
        [ObservableProperty]
        private string tagFile;

        [ObservableProperty]
        private string rootDir;

        public override void Check()
        {
            CheckFile(TagFile, "标签文件");
            CheckDir(RootDir, "根目录");
        }
    }
}