using System;
using ArchiveMaster.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs
{
    public partial class PhotoTextTaggingConfig : ConfigBase
    {
        [ObservableProperty]
        private string dir;

        [ObservableProperty]
        private int resizingTargetResolutionIn10k = 800;

        public override void Check()
        {
            CheckDir(Dir, "目录");
        }
    }
}