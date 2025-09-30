using System;
using System.Security.Cryptography;
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs
{
    public partial class EncryptorConfig : ConfigBase
    {
        [ObservableProperty]
        private CipherMode cipherMode = CipherMode.CBC;

        [ObservableProperty]
        private bool deleteSourceFiles;

        [ObservableProperty]
        private bool encryptDirectoryStructure;

        [ObservableProperty]
        private string encryptedDir;

        [ObservableProperty]
        private FilenameDuplicationPolicy filenameDuplicationPolicy;

        [ObservableProperty]
        private int keySize = 256;

        [ObservableProperty]
        private PaddingMode paddingMode = PaddingMode.PKCS7;

        [ObservableProperty]
        private SecurePassword password = new();

        [ObservableProperty]
        private string rawDir;

        [ObservableProperty]
        private EncryptorTaskType type = EncryptorTaskType.Encrypt;

        public enum EncryptorTaskType
        {
            Encrypt,
            Decrypt,
        }

        public override void Check()
        {
            switch (Type)
            {
                case EncryptorTaskType.Encrypt:
                    CheckDir(RawDir, "未加密目录");
                    break;
                case EncryptorTaskType.Decrypt:
                    CheckDir(EncryptedDir, "加密后目录");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            CheckEmpty(Password, "密码");
            if (KeySize is not (128 or 192 or 256))
            {
                throw new Exception("密钥长度应当为128、192或256");
            }
        }
    }
}