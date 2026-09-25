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
                SetSkipped();
            }
        }

        public SlimmingFilesInfo(SimpleFileInfo file, SlimmingTaskType slimmingTaskType, SimpleFileInfo distFile) :
            base(file)
        {
            SlimmingTaskType = slimmingTaskType;
            DistFile = distFile;
            if (slimmingTaskType == SlimmingTaskType.Skip)
            {
                SetSkipped();
            }
        }

        /// <summary>
        /// 跳过项：置为未勾选且不可勾选（「筛选」按钮例外，仍可能勾上，但不影响处理），状态置为 Skip（状态列显示颜色而非透明）。
        /// </summary>
        private void SetSkipped()
        {
            IsChecked = false;
            CanCheck = false;
            Skip();
        }
    }
}