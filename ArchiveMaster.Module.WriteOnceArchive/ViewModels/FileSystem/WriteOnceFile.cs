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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ReadSpeedBytePerSecond))]
        private double? readTimeCostSecond;

        [ObservableProperty]
        private bool errorNoPhysicalFile;

        [ObservableProperty]
        private bool errorHashNotMatched;

        [ObservableProperty]
        private bool errorFileReadFailed;

        [ObservableProperty]
        private bool errorNotInFileList;

        public double? ReadSpeedBytePerSecond => ReadTimeCostSecond.HasValue ? Length / ReadTimeCostSecond : null;

        public WriteOnceFile()
        {
        }

        public WriteOnceFile(FileInfo file, string topDir) : base(file, topDir)
        {
        }
    }
}