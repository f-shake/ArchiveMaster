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

        public override void Check()
        {
            CheckDir(TagFile, "标签文件");
        }
    }
}