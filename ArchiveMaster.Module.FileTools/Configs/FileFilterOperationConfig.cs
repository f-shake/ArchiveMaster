using System;
using ArchiveMaster.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs
{
    public partial class FileFilterOperationConfig : ConfigBase
    {
        [ObservableProperty]
        private string sourceDirs;

        [ObservableProperty]
        private string targetDir;

        [ObservableProperty]
        private FileFilterOperationType type = FileFilterOperationType.Copy;

        [ObservableProperty]
        private FileFilterRule filter = new FileFilterRule();

        [ObservableProperty]
        private FileFilterOperationTargetFileNameMode targetFileNameMode = FileFilterOperationTargetFileNameMode.PreserveDirectoryStructure;

        [ObservableProperty]
        private HashAlgorithmType hashType=HashAlgorithmType.SHA256;

        public override void Check()
        {
            CheckEmpty(SourceDirs, "源程序");
            CheckEmpty(Filter, "筛选器");
            if (Type is FileFilterOperationType.Copy or FileFilterOperationType.Move)
            {
                CheckEmpty(TargetDir, "目标目录");
            }
        }
    }
}