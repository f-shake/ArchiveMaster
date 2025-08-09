using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem
{
    public partial class WriteOnceFile : SimpleFileInfo
    {
        public WriteOnceFile()
        {
            
        }
        public WriteOnceFile(FileInfo file,string topDir) : base(file,topDir)
        {
        }

        [ObservableProperty]
        private string hash;
    }
}