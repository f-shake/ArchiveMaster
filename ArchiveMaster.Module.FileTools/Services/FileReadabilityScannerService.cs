using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Helpers;
using ArchiveMaster.ViewModels.FileSystem;
using FzLib.IO;

namespace ArchiveMaster.Services
{
    public class FileReadabilityScannerService(AppConfig appConfig)
        : TwoStepServiceBase<FileReadabilityScannerConfig>(appConfig)
    {
        public List<FileReadabilityInfo> Files { get; set; }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            var files = Files.CheckedOnly().ToList();
            return TryForFilesAsync(files, async (file, s) =>
            {
                ct.ThrowIfCancellationRequested();


                NotifyMessage($"正在读取{s.GetFileNumberMessage()}：{file.RelativePath}");

                if (file.Length == 0)
                {
                    file.Warn("文件为空");
                }

                int bufferSize = FileHelper.GetOptimalBufferSize(file.Length);
                byte[] buffer = new byte[bufferSize];

                long bitOneCount = 0;
                long bitZeroCount = 0;

                try
                {
                    await using FileStream fs = new FileStream(
                        file.Path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize,
                        FileOptions.SequentialScan);

                    int read;
                    while ((read = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                    {
                        for (int i = 0; i < read; i++)
                        {
                            byte b = buffer[i];
                            bitOneCount += BitOperations.PopCount(b);
                            bitZeroCount += 8 - BitOperations.PopCount(b);
                        }
                    }

                    file.IsReadable = true;
                    file.BitOneCount = bitOneCount;
                    file.BitZeroCount = bitZeroCount;
                    if (bitZeroCount == 0 || 1.0 * bitOneCount / (bitOneCount + bitZeroCount) < 0.01)
                    {
                        file.Warn("文件中比特位为1的比例小于1%");
                    }
                    else
                    {
                        file.Success();
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // 任何 IO / 逻辑错误都视为不可完整读取
                    file.IsReadable = false;
                    file.BitOneCount = null;
                    file.BitZeroCount = null;
                    file.Error(ex);
                }
            }, ct, FilesLoopOptions.Builder().AutoApplyFileLengthProgress().Build());
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return Files;
        }

        public override async Task InitializeAsync(CancellationToken ct)
        {
            Files = new List<FileReadabilityInfo>();
            await Task.Run(() =>
            {
                int index = 0;
                foreach (var file in new DirectoryInfo(Config.Dir)
                             .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                             .ApplyFilter(ct, Config.Filter)
                             .Select(p => new FileReadabilityInfo(p, Config.Dir)))
                {
                    index++;
                    NotifyMessage($"正在查找文件（{index}个）");
                    Files.Add(file);
                }
            }, ct);
        }
    }
}