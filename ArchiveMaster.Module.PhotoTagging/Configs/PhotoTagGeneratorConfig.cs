using System;
using ArchiveMaster.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs
{
    public partial class PhotoTagGeneratorConfig : ConfigBase
    {
        [ObservableProperty]
        private string dir;

        [ObservableProperty]
        private FileFilterRule filter = FileHelper.NoRawImageFileFilterRule;

        [ObservableProperty]
        private int maxTagCount = 20;

        [ObservableProperty]
        private int minTagCount = 5;

        [ObservableProperty]
        private int resizingTargetResolutionIn10k = 800;

        [ObservableProperty]
        private string tagFile;
        public override void Check()
        {
            CheckDir(Dir, "目录");
        }
    }
}