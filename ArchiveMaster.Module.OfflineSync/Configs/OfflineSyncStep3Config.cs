using System.Diagnostics;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs
{
    public partial class OfflineSyncStep3Config : ConfigBase
    {
        [ObservableProperty]
        private string patchDir;

        [ObservableProperty]
        private SecurePassword password = new();

        public override void Check()
        {
            CheckDir(PatchDir, "补丁目录");
        }
    }
}