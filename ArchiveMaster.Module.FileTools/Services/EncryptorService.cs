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

        public override async Task ExecuteAsync(CancellationToken ct)
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

                    // 先写临时文件再原子替换：取消/异常不会在目标位置留下半截密文，
                    // 也就不会被下一次的“跳过已存在”当成已完成
                    string targetPath = file.TargetPath;
                    string tempPath = targetPath + ".tmp";
                    string metadataPath = targetPath + EncryptedFileMetadataExtension;
                    string tempMetadataPath = null;
                    try
                    {
                        // 元数据也先写临时文件，并且放在替换目标之前：
                        // 这样元数据写入失败时不会先落一个“缺元数据的加密文件”
                        if (isEncrypting && Config.EncryptDirectoryStructure)
                        {
                            var bytes = aes.Encrypt(Encoding.UTF8.GetBytes(file.RelativePath));
                            tempMetadataPath = metadataPath + ".tmp";
                            await File.WriteAllBytesAsync(tempMetadataPath, bytes, ct);
                        }

                        if (isEncrypting)
                        {
                            await aes.EncryptFileAsync(file.Path, tempPath, BufferSize,
                                s.CreateFileProgressReporter("正在加密"),
                                cancellationToken: ct);
                        }
                        else
                        {
                            await aes.DecryptFileAsync(file.Path, tempPath, BufferSize,
                                s.CreateFileProgressReporter("正在解密"),
                                cancellationToken: ct);
                        }

                        File.Move(tempPath, targetPath, true);

                        // 元数据只在加密时写：解密时写会把明文路径的密文留在未加密目录里
                        if (tempMetadataPath != null)
                        {
                            File.Move(tempMetadataPath, metadataPath, true);
                            tempMetadataPath = null;
                        }

                        File.SetLastWriteTime(targetPath, File.GetLastWriteTime(file.Path));
                    }
                    catch
                    {
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }

                        if (tempMetadataPath != null && File.Exists(tempMetadataPath))
                        {
                            File.Delete(tempMetadataPath);
                        }

                        throw;
                    }

                    if (Config.DeleteSourceFiles)
                    {
                        if (File.GetAttributes(file.Path).HasFlag(FileAttributes.ReadOnly))
                        {
                            File.SetAttributes(file.Path, FileAttributes.Normal);
                        }

                        FileHelper.DeleteByConfig(file.Path, "文件加密解密_源目录");
                    }
                }, ct, FilesLoopOptions.Builder().AutoApplyStatus().AutoApplyFileLengthProgress().Build());
            }, ct);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return ProcessingFiles.Cast<SimpleFileInfo>();
        }

        public override async Task InitializeAsync(CancellationToken ct)
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
            await Task.Run(() =>
            {
                TryForFiles(new DirectoryInfo(sourceDir)
                    .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                    .Where(p => p.Extension != EncryptedFileMetadataExtension)
                    .ApplyFilter(ct)
                    .Select(p => new EncryptorFileInfo(p, sourceDir)), (file, s) =>
                {
                    ProcessFileNames(file);
                    NotifyMessage($"正在加入{s.GetFileNumberMessage()}：{file.Name}");
                    files.Add(file);
                }, ct, FilesLoopOptions.DoNothing());
            }, ct);
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

                        FileHelper.DeleteByConfig(path, "文件加密解密_被替换的目标文件");
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