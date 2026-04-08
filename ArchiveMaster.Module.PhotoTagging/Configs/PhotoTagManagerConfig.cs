using System;
using ArchiveMaster.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs
{
    public partial class PhotoTagManagerConfig : ConfigBase
    {
        [ObservableProperty]
        private string tagFile;

        [ObservableProperty]
        private string rootDir;
        
        [ObservableProperty]
        private FileFilterRule filter = FileHelper.NoRawImageFileFilterRule;

        public override void Check()
        {
            CheckFile(TagFile, "标签文件");
            CheckDir(RootDir, "根目录");
        }
    }
}