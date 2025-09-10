using System.Diagnostics;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.Cryptography;
using FzLib.IO;
using System.Security.Cryptography;
using System.Text.Json;
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels;
using WriteOnceFile = ArchiveMaster.ViewModels.FileSystem.WriteOnceFile;

namespace ArchiveMaster.Services
{
    public class VerifyService(AppConfig appConfig) : TwoStepServiceBase<VerifyConfig>(appConfig)
    {
        public TreeDirInfo FileTree { get; private set; }
        public List<WriteOnceFile> Files { get; private set; }

        public RebuildInitializeReport Report { get; private set; }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                Aes aes = null;
                var files = Files.Where(p => p.Status == ProcessStatus.Ready).ToList();
                if (files.Any(p => p.IsEncrypted))
                {
                    if (string.IsNullOrWhiteSpace(Config.Password))
                    {
                        throw new Exception("部分文件被加密，但未提供密码");
                    }

                    aes = AesHelper.GetDefaultAes(Config.Password);
                }

                await TryForFilesAsync(files, async (file, s) =>
                {
                    try
                    {
                        byte[] hash;

                        Stopwatch sw = Stopwatch.StartNew();
                        if (file.IsEncrypted)
                        {
                            hash = await aes.GetDecryptedFileHashAsync(file.PhysicalFile,
                                progress: s.CreateFileProgressReporter("正在验证文件"),
                                hashAlgorithmType: WriteOnceArchiveParameters.HashType, cancellationToken: ct);
                        }
                        else
                        {
                            hash = await FileHashHelper.ComputeHashAsync(file.PhysicalFile,
                                WriteOnceArchiveParameters.HashType,
                                progress: s.CreateFileProgressReporter("正在验证文件"),
                                cancellationToken: ct);
                        }

                        sw.Stop();
                        file.ReadTimeCostSecond = sw.Elapsed.TotalSeconds;
                        Debug.WriteLine(sw.Elapsed);

                        Debug.Assert(hash != null);
                        if (hash != null)
                        {
                            var hashString = Convert.ToHexString(hash);
                            if (hashString != file.Hash)
                            {
                                file.ErrorHashNotMatched = true;
                                file.Error($"重建后的文件Hash{hashString}与源文件{file.Hash}不一致");
                            }

                            file.Success();
                        }
                        else
                        {
                            file.ErrorFileReadFailed = true;
                            file.Error($"文件Hash获取失败（未知原因）");
                        }
                    }
                    catch (Exception ex)
                    {
                        file.ErrorFileReadFailed = true;
                        file.Error($"文件验证失败：{ex.Message}");
                    }
                }, ct, FilesLoopOptions.Builder().AutoApplyFileLengthProgress().Build());

                Report.TotalReadTimeCostSecond = files.Sum(p => p.ReadTimeCostSecond);
            }, ct);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return Files;
        }


        public override async Task InitializeAsync(CancellationToken ct)
        {
            NotifyMessage("正在建立文件树");
            var packageInfo = await WriteOnceArchiveHelper.ReadPackageInfoAsync([Config.PackageDir], null);

            (FileTree, Files, Report) =
                await WriteOnceArchiveHelper.ReadPackageFilesAsync(packageInfo, [Config.PackageDir], true, ct);
        }
    }
}