using ArchiveMaster.Enums;
using ArchiveMaster.Models;
using FzLib.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using Microsoft.Extensions.DependencyInjection;
using LocalAndOffsiteDir = ArchiveMaster.ViewModels.FileSystem.LocalAndOffsiteDir;

namespace ArchiveMaster.Services
{
    public static class TestService
    {
        private const int CostTimeCount = 0;
        private const int Count = 2;
        private static Random random = new Random();

        public static Task CreateSyncTestFilesAsync(string dir)
        {
            return Task.Run(() =>
            {
                DateTime time = new DateTime(2000, 1, 1, 0, 0, 0);
                var root = new DirectoryInfo(dir);
                if (root.Exists)
                {
                    root.Delete(true);
                }

                root.Create();

                var local = root.CreateSubdirectory("local");
                var remote = root.CreateSubdirectory("remote");

                CreateTestFiles(local.CreateSubdirectory("syncDir1"), remote.CreateSubdirectory("syncDir1"));
                CreateTestFiles(local.CreateSubdirectory("folder").CreateSubdirectory("syncDir2"),
                    remote.CreateSubdirectory("folder").CreateSubdirectory("syncDir2"));
            });
        }

        public static async Task TestAllAsync()
        {
            var appConfig = HostServices.GetRequiredService<AppConfig>();
            string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string localDir = Path.Combine(dir, "local");
            string remoteDir = Path.Combine(dir, "remote");
            Debug.WriteLine(dir);
            await CreateSyncTestFilesAsync(dir);

            try
            {
                ObservableCollection<string> syncDirs = new ObservableCollection<string>
                {
                    Path.Combine(remoteDir, "syncDir1"),
                    Path.Combine(remoteDir, "folder", "syncDir2"),
                };
                OfflineSyncStep1Config c1 = new OfflineSyncStep1Config()
                {
                    SyncDirs = syncDirs,
                    OutputFile = Path.GetTempFileName()
                };
                Step1Service s1 = new Step1Service(appConfig) { Config = c1 };
                await s1.ExecuteAsync();

                string[] searchingDirs =
                [
                    localDir,
                    Path.Combine(localDir, "folder")
                ];
                OfflineSyncStep2Config c2 = new OfflineSyncStep2Config()
                {
                    OffsiteSnapshot = c1.OutputFile,
                    PatchDir = Path.Combine(dir, "patch"),
                    ExportMode = ExportMode.Copy,
                    Filter = new FileFilterRule()
                    {
                        ExcludeFiles = "黑名单文件.+",
                        ExcludeFolders = "黑名单目录",
                        ExcludePaths = "Thumb",
                        UseRegex = true
                    }
                };
                var match = await Step2Service.MatchLocalAndOffsiteDirsAsync(c2.OffsiteSnapshot, searchingDirs);
                c2.MatchingDirs = new ObservableCollection<LocalAndOffsiteDir>(match);
                Step2Service u2 = new Step2Service(appConfig) { Config = c2 };
                await u2.InitializeAsync();

                Check(u2.UpdateFiles != null);
                Check(u2.UpdateFiles.Count == Count * 10);
                Check(!u2.UpdateFiles.Any(p => p.Name.Contains('黑')));
                Check(u2.UpdateFiles.Where(p => p.Name.Contains("新建"))
                    .All(p => p.UpdateType == FileUpdateType.Add));
                Check(u2.UpdateFiles.Where(p => p.Name.Contains("新建")).Count() == Count * 2);
                Check(u2.UpdateFiles.Where(p => p.Name.Contains("删除"))
                    .All(p => p.UpdateType == FileUpdateType.Delete));
                Check(u2.UpdateFiles.Where(p => p.Name.Contains("删除")).Count() == Count * 2);
                Check(u2.UpdateFiles.Where(p => p.Name.Contains("移动"))
                    .All(p => p.UpdateType == FileUpdateType.Move));
                Check(u2.UpdateFiles.Where(p => p.Name.Contains("移动")).Count() == Count * 2);
                Check(u2.UpdateFiles.Where(p => p.Name.Contains("修改"))
                    .All(p => p.UpdateType == FileUpdateType.Modify));
                Check(u2.UpdateFiles.Where(p => p.Name.Contains("修改的文件")).Count() == Count * 2);

                await u2.ExecuteAsync();

                OfflineSyncStep3Config c3 = new OfflineSyncStep3Config()
                {
                    PatchDir = c2.PatchDir,
                };
                Step3Service u3 = new Step3Service(appConfig) { Config = c3 };
                await u3.InitializeAsync();
                await u3.ExecuteAsync();
                await u3.DeleteEmptyDirectoriesAsync();

                var localFiles = Directory.EnumerateFiles(localDir, "*", SearchOption.AllDirectories)
                    .Select(p => Path.GetRelativePath(localDir, p))
                    .Where(p => !p.Contains("黑"))
                    .OrderBy(p => p)
                    .ToList();
                var remoteFiles = Directory.EnumerateFiles(remoteDir, "*", SearchOption.AllDirectories)
                    .Select(p => Path.GetRelativePath(remoteDir, p))
                    .Where(p => !p.Contains("黑"))
                    .OrderBy(p => p)
                    .ToList();

                Check(localFiles.SequenceEqual(remoteFiles));

                localFiles = Directory.EnumerateFiles(localDir, "*", SearchOption.AllDirectories)
                    .Select(p => Path.GetRelativePath(localDir, p))
                    .Where(p => p.Contains("黑"))
                    .OrderBy(p => p)
                    .ToList();
                remoteFiles = Directory.EnumerateFiles(remoteDir, "*", SearchOption.AllDirectories)
                    .Select(p => Path.GetRelativePath(remoteDir, p))
                    .Where(p => p.Contains("黑"))
                    .OrderBy(p => p)
                    .ToList();

                Check(localFiles.Count == remoteFiles.Count);

                var localDirs = Directory.EnumerateDirectories(localDir, "*", SearchOption.AllDirectories)
                    .Select(p => Path.GetRelativePath(localDir, p))
                    .OrderBy(p => p)
                    .ToList();
                var remoteDirs = Directory.EnumerateDirectories(remoteDir, "*", SearchOption.AllDirectories)
                    .Select(p => Path.GetRelativePath(remoteDir, p))
                    .OrderBy(p => p)
                    .ToList();

                Check(localDirs.SequenceEqual(remoteDirs));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static void Check(bool b, [CallerLineNumber] int lineNumber = 0)
        {
            if (!b)
            {
                throw new Exception($"测试代码第{lineNumber}行发生错误");
            }
        }

        private static void CreateRandomFile(string path)
        {
            using (FileStream file = File.Create(path))
            {
                byte[] buffer = new byte[1024 * 1024 * 4 + random.Next(0, 31)];
                for (int i = 0; i < buffer.Length; i++)
                {
                    // 生成32-126范围内的随机ASCII可见字符
                    buffer[i] = (byte)random.Next(32, 127);
                }

                file.Write(buffer, 0, buffer.Length);
            }

            File.SetLastWriteTime(path, DateTime.Now.AddSeconds(-random.Next(0, 86400)));
        }

        private static void CreateTestFiles(DirectoryInfo local, DirectoryInfo remote)
        {
            string fileName, fileName2;
            var localDir = local.CreateSubdirectory("增加处理时间的目录");
            var remoteDir = remote.CreateSubdirectory("增加处理时间的目录");
            for (int i = 0; i < CostTimeCount; i++)
            {
                fileName = Path.Combine(localDir.FullName, i.ToString());
                CreateRandomFile(fileName);
                File.Copy(fileName, Path.Combine(remoteDir.FullName, i.ToString()));
            }


            localDir = local.CreateSubdirectory("测试目录");
            remoteDir = remote.CreateSubdirectory("测试目录");
            var localBlackDir = localDir.CreateSubdirectory("黑名单目录");
            var remoteBlackDir = remoteDir.CreateSubdirectory("黑名单目录");
            var localMovedDir = local.CreateSubdirectory("已移动文件的目录");

            for (int i = 1; i <= Count; i++)
            {
                DateTime now = DateTime.Now;

                fileName = Path.Combine(localDir.FullName, $"未修改的文件{i}");
                CreateRandomFile(fileName);
                fileName2 = Path.Combine(remoteDir.FullName, $"未修改的文件{i}");
                File.Copy(fileName, fileName2);

                fileName = Path.Combine(localDir.FullName, $"新建的文件{i}");
                CreateRandomFile(fileName);

                fileName = Path.Combine(remoteDir.FullName, $"删除的文件{i}");
                CreateRandomFile(fileName);

                fileName = Path.Combine(localDir.FullName, $"修改的文件{i}");
                CreateRandomFile(fileName);
                fileName = Path.Combine(remoteDir.FullName, $"修改的文件{i}");
                CreateRandomFile(fileName);
                File.SetLastWriteTime(fileName, now.AddDays(-1));

                fileName = Path.Combine(localDir.FullName, $"修改但时间错误的文件{i}");
                CreateRandomFile(fileName);
                File.SetLastWriteTime(fileName, now.AddDays(-1));
                fileName = Path.Combine(remoteDir.FullName, $"修改但时间错误的文件{i}");
                CreateRandomFile(fileName);

                fileName = Path.Combine(localMovedDir.FullName, $"移动的文件{i}");
                CreateRandomFile(fileName);

                fileName2 = Path.Combine(remoteDir.FullName, $"移动的文件{i}");
                File.Copy(fileName, fileName2);


                fileName = Path.Combine(localMovedDir.FullName, $"黑名单文件{i}");
                CreateRandomFile(fileName);
                fileName = Path.Combine(remoteDir.FullName, $"黑名单文件{i}");
                CreateRandomFile(fileName);
                fileName = Path.Combine(localBlackDir.FullName, $"黑名单目录中的文件{i}");
                CreateRandomFile(fileName);
                fileName = Path.Combine(remoteBlackDir.FullName, $"黑名单文件{i}");
                CreateRandomFile(fileName);
                File.SetLastWriteTime(fileName, now.AddDays(-1));
            }

            remoteDir.CreateSubdirectory("空目录1");
            localDir.CreateSubdirectory("空目录1");
            var emptyDir2 = remoteDir.CreateSubdirectory("空目录2");
            emptyDir2.CreateSubdirectory("子空目录3");
            var emptyDir4 = emptyDir2.CreateSubdirectory("子空目录4（带缩略图数据库）");
            File.WriteAllText(Path.Combine(emptyDir4.FullName, "Thumb.db"), "");
        }
    }
}