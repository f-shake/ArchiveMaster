using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;
using LocalAndOffsiteDir = ArchiveMaster.ViewModels.FileSystem.LocalAndOffsiteDir;

namespace ArchiveMaster.Configs
{
    public partial class OfflineSyncStep2Config : ConfigBase
    {
        [ObservableProperty]
        private FileFilterRule filter = new FileFilterRule();

        [ObservableProperty]
        private ExportMode exportMode = ExportMode.Copy;

        [ObservableProperty]
        private bool checkMoveIgnoreFileName = true;

        [ObservableProperty]
        private string localDir;

        [ObservableProperty]
        private int maxTimeToleranceSecond = 3;

        [ObservableProperty]
        private string patchDir;

        [ObservableProperty]
        private string offsiteSnapshot;

        [ObservableProperty]
        private bool enableEncryption;

        [ObservableProperty]
        private SecurePassword encryptionPassword = new();

        [ObservableProperty]
        [property: JsonIgnore]
        private ObservableCollection<LocalAndOffsiteDir> matchingDirs;

        partial void OnOffsiteSnapshotChanged(string value)
        {
            MatchingDirs = null;
        }

        public override void Check()
        {
            CheckFile(OffsiteSnapshot, "异地快照文件");
            CheckEmpty(LocalDir, "本地搜索目录");
            if (EnableEncryption && string.IsNullOrWhiteSpace(EncryptionPassword))
            {
                throw new Exception("已启动备份文件加密，但密码为空");
            }

            if (ExportMode != ExportMode.Copy && EnableEncryption)
            {
                throw new Exception("只有导出模式设置为“复制”时，才支持备份文件加密");
            }
        }
    }
}