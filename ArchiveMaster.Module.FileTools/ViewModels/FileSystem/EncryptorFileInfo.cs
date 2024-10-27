﻿using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels.FileSystem
{
    public partial class EncryptorFileInfo : SimpleFileInfo
    {
        [ObservableProperty]
        private bool isEncrypted;

        [ObservableProperty]
        private bool isFileNameEncrypted;

        [ObservableProperty]
        private string relativePath;

        [ObservableProperty]
        private string targetName;

        [ObservableProperty]
        private string targetPath;

        [ObservableProperty]
        private string targetRelativePath;


        public EncryptorFileInfo(FileInfo file, string topDir) : base(file, topDir)
        {
        }
    }
}