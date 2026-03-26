using System.Drawing;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs
{
    public partial class TimeAssConfig : ConfigBase
    {
        [ObservableProperty]
        private TimeAssFormat format = new TimeAssFormat();

        [ObservableProperty]
        private string files;
        
        [ObservableProperty]
        private string exportFile;
        
        [ObservableProperty]
        private bool combineIntoSingleFile = true;

        public override void Check()
        {
            CheckEmpty(Files, "文件列表");
            CheckEmpty(ExportFile, "导出文件");
        }
    }
}