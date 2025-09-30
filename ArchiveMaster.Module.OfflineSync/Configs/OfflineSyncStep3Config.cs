using System.Diagnostics;
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs
{
    public partial class OfflineSyncStep3Config : ConfigBase
    {
        [ObservableProperty]
        private DeleteMode deleteMode = DeleteMode.MoveToDeletedFolder;

        [ObservableProperty]
        private string deleteDir = "异地备份离线同步-删除的文件";

        [ObservableProperty]
        private string patchDir;

        [ObservableProperty]
        private SecurePassword password = new();

        public override void Check()
        {
            CheckDir(PatchDir, "补丁目录");
            if (DeleteMode == DeleteMode.MoveToDeletedFolder)
            {
                CheckEmpty(DeleteDir, "回收站目录名");
            }
        }
    }
}