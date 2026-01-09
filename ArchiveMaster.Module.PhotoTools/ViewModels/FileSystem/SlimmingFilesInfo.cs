using ArchiveMaster.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem
{
    public partial class SlimmingFilesInfo : SimpleFileInfo
    {
        [ObservableProperty]
        private SlimmingTaskType slimmingTaskType;

        [ObservableProperty]
        private SimpleFileInfo distFile;


        public SlimmingFilesInfo(FileInfo file, string rootDir, SlimmingTaskType slimmingTaskType, SimpleFileInfo distFile)
            : base(file, rootDir)
        {
            SlimmingTaskType = slimmingTaskType;
            DistFile = distFile;
            if (slimmingTaskType == SlimmingTaskType.Skip)
            {
                IsChecked = false;
            }
        }

        public SlimmingFilesInfo(SimpleFileInfo file, SlimmingTaskType slimmingTaskType, SimpleFileInfo distFile) :
            base(file)
        {
            SlimmingTaskType = slimmingTaskType;
            DistFile = distFile;
            if (slimmingTaskType == SlimmingTaskType.Skip)
            {
                IsChecked = false;
            }
        }
    }
}