using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem
{
    public partial class WriteOnceFile : SimpleFileInfo
    {
        [ObservableProperty]
        private string hash;

        [ObservableProperty]
        private bool hasPhysicalFile;

        [ObservableProperty]
        private string physicalFile;

        [ObservableProperty]
        private bool isEncrypted;

        public WriteOnceFile()
        {
        }

        public WriteOnceFile(FileInfo file, string topDir) : base(file, topDir)
        {
        }
    }
}