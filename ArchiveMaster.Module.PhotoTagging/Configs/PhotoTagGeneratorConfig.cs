using System;
using ArchiveMaster.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs
{
    public partial class PhotoTagGeneratorConfig : ConfigBase
    {
        [ObservableProperty]
        private int descriptionLength = 30;

        [ObservableProperty]
        private string dir;

        [ObservableProperty]
        private FileFilterRule filter = FileHelper.NoRawImageFileFilterRule;

        [ObservableProperty]
        private int resizingTargetResolutionIn10k = 800;

        [ObservableProperty]
        private string tagFile;

        [ObservableProperty]
        private int targetTagCount = 5;
        
        public override void Check()
        {
            CheckDir(Dir, "目录");
            CheckEmpty(TagFile, "标签文件");
        }
    }
}