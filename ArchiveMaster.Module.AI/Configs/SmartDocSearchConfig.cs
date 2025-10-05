using System;
using ArchiveMaster.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs
{
    public partial class SmartDocSearchConfig : ConfigBase
    {
        [ObservableProperty]
        private string dir;

        [ObservableProperty]
        private int threadCount = 2;

        [ObservableProperty]
        private TimeSpan maxDurationTolerance = TimeSpan.FromSeconds(1);

        [ObservableProperty]
        private FileFilterRule filter = FileHelper.ImageFileFilterRule;

        public override void Check()
        {
            CheckDir(Dir, "目录");
        }
    }
}