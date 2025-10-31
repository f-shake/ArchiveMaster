using FzLib;
using ArchiveMaster.Configs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Events;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using Markdig;
using Serilog;
using UtfUnknown;

namespace ArchiveMaster.Services
{
    public class EncodingConverterService(AppConfig appConfig)
        : TwoStepServiceBase<EncodingConverterConfig>(appConfig)
    {
        private bool targetBom;
        private Encoding targetEncoding;
        public List<EncodingFileInfo> Files { get; private set; }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            await Task.Run(() =>
            {
                return TryForFilesAsync(Files.CheckedOnly().ToList(), async (file, s) =>
                {
                    NotifyMessage($"正在转换文件编码{s.GetFileNumberMessage()}：{file.Name}");
                    var tempFile = Path.Combine(Path.GetDirectoryName(file.Path), Guid.NewGuid().ToString());
                    var backupFile = $"{file.Path}.{DateTime.Now:yyyyMMddHHmmss}.bak";
                    int bufferLength = 1000;
                    char[] buffer = new char[bufferLength];
                    try
                    {
                        await using (var fsWrite = new StreamWriter(tempFile, false, targetEncoding))
                        {
                            using (var fsRead = new StreamReader(file.Path, file.Encoding.Encoding))
                            {
                                int readLength;
                                while ((readLength = await fsRead.ReadAsync(buffer, 0, bufferLength)) > 0)
                                {
                                    ct.ThrowIfCancellationRequested();
                                    await fsWrite.WriteAsync(buffer, 0, readLength);
                                }
                            }
                        }

                        File.Replace(tempFile, file.Path, backupFile);
                    }
                    catch (OperationCanceledException)
                    {
                        if (File.Exists(tempFile))
                        {
                            try
                            {
                                File.Delete(tempFile);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "删除文件失败");
                            }
                        }
                    }
                }, ct, FilesLoopOptions.Builder().AutoApplyFileLengthProgress().Build());
            }, ct);
        }

        public override IEnumerable<SimpleFileInfo> GetInitializedFiles()
        {
            return Files;
        }

        public override async Task InitializeAsync(CancellationToken ct)
        {
            try
            {
                PrepareTargetEncoding();
            }
            catch (Exception ex)
            {
                throw new Exception($"无法识别目标编码：{Config.TargetEncoding}");
            }

            var files = new List<EncodingFileInfo>();
            NotifyMessage("正在查找文件");
            await Task.Run(() =>
            {
                files = new DirectoryInfo(Config.Dir)
                    .EnumerateFiles("*", FileEnumerateExtension.GetEnumerationOptions())
                    .ApplyFilter(ct, Config.Filter)
                    .Select(p => new EncodingFileInfo(p, Config.Dir))
                    .ToList();
            }, ct);
            await TryForFilesAsync(files, async (file, s) =>
                {
                    ct.ThrowIfCancellationRequested();
                    NotifyMessage($"正在识别文件编码{s.GetFileNumberMessage()}：{file.Name}");

                    var result = await CharsetDetector.DetectFromFileAsync(file.Path, ct);
                    if (result.Detected != null)
                    {
                        file.Encoding = result.Detected;
                        file.Details = result.Details;
                        if (result.Detected.EncodingName == "ascii")
                        {
                            //大部分编码向后兼容ASCII
                            file.IsChecked = false;
                        }
                        else
                        {
                            if (targetEncoding.WebName == result.Detected.Encoding.WebName) //编码相同
                            {
                                //编码名相同，看是指定BOM，期望的BOM和实际的BOM是否一致
                                file.IsChecked = Config.WithBom.HasValue && targetBom != result.Detected.HasBOM;
                            }
                            else
                            {
                                //编码名不同，肯定要转换
                                file.IsChecked = true;
                            }
                        }
                    }
                    else
                    {
                        //没有识别出来
                        file.IsChecked = false;
                    }
                }, ct,
                FilesLoopOptions.Builder().AutoApplyFileNumberProgress().Build());
            Files = files;
        }

        private void PrepareTargetEncoding()
        {
            var encoding = Encoding.GetEncoding(Config.TargetEncoding);
            if (Config.WithBom.HasValue)
            {
                var withBom = Config.WithBom.Value;
                if (encoding.WebName == Encoding.UTF8.WebName)
                {
                    encoding = new UTF8Encoding(withBom);
                    targetBom = withBom;
                }
                else if (encoding.WebName == Encoding.UTF32.WebName)
                {
                    encoding = new UTF32Encoding(false, withBom);
                    targetBom = withBom;
                }
            }

            targetEncoding = encoding;
        }
    }
}