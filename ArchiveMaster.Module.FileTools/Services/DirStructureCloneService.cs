using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Xml.Serialization;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Controls.Platform;
using FzLib.IO;
using Microsoft.Win32.SafeHandles;

namespace ArchiveMaster.Services
{
    public class DirStructureCloneService(AppConfig appConfig)
        : TwoStepServiceBase<DirStructureCloneConfig>(appConfig)
    {
        public TreeDirInfo RootDir { get; private set; }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            await Task.Run(() =>
            {
                if (Config.ExportStructureFile)
                {
                    NotifyMessage($"正在创建目录结构文件");
                    NotifyProgressIndeterminate();
                    var json = RootDir.ToJson();
                    File.WriteAllText(Config.TargetDirOrFile, json);
                }
                else
                {
                    var flatten = RootDir.Flatten().ToList();
                    TryForFiles(flatten, (file, s) =>
                    {
                        NotifyMessage($"正在创建{s.GetFileNumberMessage()}：{file.RelativePath}");
                        if (!OperatingSystem.IsWindows())
                        {
                            throw new PlatformNotSupportedException("仅支持Windows系统");
                        }

                        CreateSparseFile(file);
                    }, ct, FilesLoopOptions.Builder().AutoApplyFileNumberProgress().AutoApplyStatus().Build());
                }
            }, ct);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return RootDir.Flatten();
        }

        public override async Task InitializeAsync(CancellationToken ct)
        {
            List<SimpleFileInfo> files = new List<SimpleFileInfo>();

            NotifyMessage("正在枚举文件");
            NotifyProgressIndeterminate();
            if (Config.InputStructureFile)
            {
                var json = await File.ReadAllTextAsync(Config.SourceDirOrFile, ct);
                RootDir = TreeDirInfo.FromJson(json);
            }
            else
            {
                RootDir = await TreeDirInfo.BuildTreeAsync(Config.SourceDirOrFile, Config.Filter, ct);
            }
        }

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            int dwIoControlCode,
            IntPtr InBuffer,
            int nInBufferSize,
            IntPtr OutBuffer,
            int nOutBufferSize,
            ref int pBytesReturned,
            [In]
            ref NativeOverlapped lpOverlapped
        );

        [SupportedOSPlatform("windows")]
        private static void MarkAsSparseFile(SafeFileHandle fileHandle)
        {
            int bytesReturned = 0;
            NativeOverlapped lpOverlapped = new NativeOverlapped();
            bool result =
                DeviceIoControl(
                    fileHandle,
                    590020, //FSCTL_SET_SPARSE,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    0,
                    ref bytesReturned,
                    ref lpOverlapped);
            if (result == false)
                throw new Win32Exception();
        }

        [SupportedOSPlatform("windows")]
        private void CreateSparseFile(SimpleFileInfo file)
        {
            Debug.Assert(OperatingSystem.IsWindows() && !Config.ExportStructureFile);
            string newPath = Path.Combine(Config.TargetDirOrFile, file.RelativePath);
            FileInfo newFile = new FileInfo(newPath);
            newFile.Directory.Create();

            using (FileStream fs = File.Create(newPath))
            {
                MarkAsSparseFile(fs.SafeFileHandle);
                fs.SetLength(file.Length);
                fs.Seek(-1, SeekOrigin.End);
            }

            File.SetLastWriteTime(newPath, File.GetLastWriteTime(file.Path));
        }
    }
}