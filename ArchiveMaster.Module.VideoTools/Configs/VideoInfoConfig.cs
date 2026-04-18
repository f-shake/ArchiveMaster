using System.Drawing;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs
{
    public partial class VideoInfoConfig : ConfigBase
    {
        [ObservableProperty]
        private string dir;

        [ObservableProperty]
        private FileFilterRule filter = FileHelper.VideoFileFilterRule;

        public override void Check()
        {
            CheckDir(Dir, "目录");
        }
    }
}