using System.Drawing;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

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

        partial void OnFilesChanged(string value)
        {
            try
            {
                var fileList = FileNameHelper.GetFileNames(value, false);
                if (fileList.Length == 0)
                {
                    return;
                }
                var firstFile = fileList[0];
                ExportFile = Path.Combine(Path.GetDirectoryName(firstFile),
                    Path.GetFileNameWithoutExtension(firstFile) + ".ass");
            }
            catch (Exception ex)
            {
                
            }
        }

        public override void Check()
        {
            CheckEmpty(Files, "文件列表");
            CheckEmpty(ExportFile, "导出文件");
        }
    }
}