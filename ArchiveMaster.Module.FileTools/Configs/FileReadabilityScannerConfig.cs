using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;
using FzLib.Text;

namespace ArchiveMaster.Configs
{
    public partial class FileReadabilityScannerConfig : ConfigBase
    {
        [ObservableProperty]
        private string dir;

        [ObservableProperty]
        private FileFilterRule filter = new FileFilterRule();

        public override void Check()
        {
            CheckDir(Dir,"目录");
        }
    }
}
