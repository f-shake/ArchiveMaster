using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using EncryptorFileInfo = ArchiveMaster.ViewModels.FileSystem.EncryptorFileInfo;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Cryptography;
using FzLib.IO;

namespace ArchiveMaster.Services
{
    public class EncryptorService(AppConfig appConfig) : TwoStepServiceBase<EncryptorConfig>(appConfig)
    {
        public const string EncryptedFileExtension = ".$ept$";

        public const string EncryptedFileMetadataExtension = ".$eptm$";

        private Aes aes;

        public int BufferSize { get; set; } = 1024 * 1024;

        public List<EncryptorFileInfo> ProcessingFiles { get; set; }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(ProcessingFiles, nameof(ProcessingFiles));

            await Task.Run(async () =>
            {
                bool isEncrypting = IsEncrypting();
                //初始化进度通知
                var files = ProcessingFiles.CheckedOnly().ToList();

                await TryForFilesAsync(files, async (file, s) =>
                {
                    if (!CheckFileAndDirectoryExists(file))
                    {
                        return;
                    }

                    if (isEncrypting)
                    {
                        await aes.EncryptFileAsync(file.Path, file.TargetPath, BufferSize,
                            s.CreateFileProgressReporter("正在加密"),
                            cancellationToken: token);
                        if (Config.EncryptDirectoryStructure)
                        {
                            var bytes = aes.Encrypt(Encoding.UTF8.GetBytes(file.RelativePath));
                            await File.WriteAllBytesAsync(file.TargetPath + EncryptedFileMetadataExtension, bytes,
                                token);
                        }
                    }
                    else
                    {
                        await aes.DecryptFileAsync(file.Path, file.TargetPath, BufferSize,
                            s.CreateFileProgressReporter("正在解密"),
                            cancellationToken: token);
                    }

                    File.SetLastWriteTime(file.TargetPath, File.GetLastWriteTime(file.Path));

                    if (Config.DeleteSourceFiles)
                    {
                        if (File.GetAttributes(file.Path).HasFlag(FileAttributes.ReadOnly))
                        {
                            File.SetAttributes(file.Path, FileAttributes.Normal);
                        }

                        FileHelper.DeleteByConfig(file.Path);
                    }
                }, token, FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileLengthProgress().Build());
            }, token);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return ProcessingFiles.Cast<SimpleFileInfo>();
        }
        public override async Task InitializeAsync(CancellationToken token)
        {
            InitializeAes();
            List<EncryptorFileInfo> files = new List<EncryptorFileInfo>();

            var sourceDir = GetSourceDir();
            if (!Directory.Exists(sourceDir))
            {
                throw new Exception("源目录不存在");
            }

            NotifyProgressIndeterminate();
            NotifyMessage("正在枚举文件");

            await TryForFilesAsync(new DirectoryInfo(sourceDir)
                .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                .Where(p => p.Extension != EncryptedFileMetadataExtension)
                .ApplyFilter(token)
                .Select(p => new EncryptorFileInfo(p, sourceDir)), (file, s) =>
            {
                ProcessFileNames(file);

                NotifyMessage($"正在加入{s.GetFileNumberMessage()}：{file.Name}");
                files.Add(file);
            }, token, FilesLoopOptions.DoNothing());

            ProcessingFiles = files;
        }

        private static string Base64ToFileNameSafeString(string base64)
        {
            if (string.IsNullOrEmpty(base64))
            {
                throw new ArgumentException("Base64 string cannot be null or empty");
            }

            string safeString = base64.Replace('+', '-')
                .Replace('/', '_')
                .Replace('=', '~');

            return safeString;
        }

        private static string FileNameSafeStringToBase64(string safeString)
        {
            if (string.IsNullOrEmpty(safeString))
            {
                throw new ArgumentException("Safe string cannot be null or empty");
            }

            string base64 = safeString.Replace('-', '+')
                .Replace('_', '/')
                .Replace('~', '=');

            return base64;
        }

        private static string Hash(string input)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
        }

        private static bool IsEncryptedFile(string fileName)
        {
            if (fileName.EndsWith(EncryptedFileExtension, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查文件是否存在，并根据策略作出操作
        /// </summary>
        /// <param name="file"></param>
        /// <returns>是否继续加密或解密</returns>
        private bool CheckFileAndDirectoryExists(EncryptorFileInfo file)
        {
            string path = file.TargetPath;
            if (File.Exists(path))
            {
                switch (Config.FilenameDuplicationPolicy)
                {
                    case FilenameDuplicationPolicy.Overwrite:
                        if (File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly))
                        {
                            File.SetAttributes(path, FileAttributes.Normal);
                        }

                        FileHelper.DeleteByConfig(path);
                        break;

                    case FilenameDuplicationPolicy.Skip:
                        file.Skip();
                        return false;

                    case FilenameDuplicationPolicy.Throw:
                        file.Warn("目标文件已存在");
                        return false;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return true;
        }

        private string GetDistDir()
        {
            if (IsEncrypting())
            {
                return Config.EncryptedDir;
            }

            return Config.RawDir;
        }

        private string GetSourceDir()
        {
            if (IsEncrypting())
            {
                return Config.RawDir;
            }

            return Config.EncryptedDir;
        }

        private void InitializeAes()
        {
            aes = Aes.Create();
            aes.Mode = Config.CipherMode;
            aes.Padding = Config.PaddingMode;
            aes.KeySize = Config.KeySize;
            aes.SetStringKey(Config.Password);
        }

        private bool IsEncrypting()
        {
            ArgumentNullException.ThrowIfNull(Config);
            return Config.Type == EncryptorConfig.EncryptorTaskType.Encrypt;
        }

        private void ProcessFileNames(EncryptorFileInfo file)
        {
            var isEncrypting = IsEncrypting();
            ArgumentNullException.ThrowIfNull(file);
            if (Config.EncryptDirectoryStructure)
            {
                if (isEncrypting)
                {
                    EncryptDirStructure();
                }
                else
                {
                    DecryptDirStructure();
                }
            }
            else
            {
                string targetName = isEncrypting ? $"{file.Name}{EncryptedFileExtension}" : DecryptFileName(file.Name);
                string relativeDir = Path.GetDirectoryName(Path.GetRelativePath(GetSourceDir(), file.Path));
                file.TargetPath = Path.Combine(GetDistDir(), relativeDir, targetName);
                file.TargetName = targetName;
            }

            file.TargetRelativePath = Path.GetRelativePath(GetDistDir(), file.TargetPath);

            void EncryptDirStructure()
            {
                string relativePath = Path.GetRelativePath(GetSourceDir(), file.Path);
                string hash = Hash(relativePath);
                file.TargetName = hash;
                file.TargetPath = Path.Combine(GetDistDir(), hash);
            }

            void DecryptDirStructure()
            {
                var eptMetadataFile = file.Path + EncryptedFileMetadataExtension;
                if (!File.Exists(eptMetadataFile))
                {
                    throw new Exception($"找不到已加密文件的附带元数据与文件（{eptMetadataFile}）");
                }

                var data = File.ReadAllBytes(eptMetadataFile);

                string rawRelativePath = Encoding.UTF8.GetString(aes.Decrypt(data));
                file.TargetName = Path.GetFileName(rawRelativePath);
                file.TargetPath = Path.Combine(GetDistDir(), rawRelativePath);
            }

            string DecryptFileName(string fileName)
            {
                ArgumentException.ThrowIfNullOrEmpty(fileName);
                if (fileName.EndsWith(EncryptedFileExtension, StringComparison.InvariantCultureIgnoreCase))
                {
                    fileName = Path.GetFileNameWithoutExtension(fileName);
                }

                return fileName;
            }
        }
    }
}