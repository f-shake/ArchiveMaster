using System.Numerics;
using FzLib.IO;

namespace ArchiveMaster.Services;

public class FilesLoopStates
{
    private int fileCount = 0;

    private int fileIndex = 0;

    private long fileLength = 0;

    private long totalLength = 0;

    public FilesLoopStates(ServiceBase service, FilesLoopOptions options)
    {
        Service = service;
        Options = options;
        totalLength = options.TotalLength;
        fileLength = options.InitialLength;
        fileCount = options.TotalCount;
        fileIndex = options.InitialCount;

        if (totalLength > 0)
        {
            CanAccessTotalLength = true;
        }

        if (fileCount > 0)
        {
            CanAccessFileCount = true;
        }
    }

    public static string ProgressMessageFormat { get; set; } = "（{0}/{1}）";

    public static string ProgressMessageIndexOnlyFormat { get; set; } = "（{0}个）";

    public long AccumulatedLength => fileLength;

    public int FileCount
    {
        get => CanAccessFileCount ? fileCount : throw new ArgumentException("未初始化文件总数，不可调用TotalLength");
        internal set
        {
            fileCount = value;
            CanAccessFileCount = true;
        }
    }

    public int FileIndex => fileIndex;

    public FilesLoopOptions Options { get; }

    public ServiceBase Service { get; }

    public long TotalLength
    {
        get => CanAccessTotalLength ? totalLength : throw new ArgumentException("未初始化总大小，不可调用TotalLength");
        internal set
        {
            totalLength = value;
            CanAccessTotalLength = true;
        }
    }

    internal bool CanAccessFileCount { get; set; }

    internal bool CanAccessTotalLength { get; set; }

    internal bool NeedBroken { get; private set; }

    public void Break()
    {
        NeedBroken = true;
    }

    public string CreateFileProgressMessage(string message, FileProcessProgress progress, string file)
    {
        return
            $"{message}（{FileIndex + 1}/{FileCount}，当前文件{1.0 * progress.ProcessedBytes / 1024 / 1024:0}MB/{1.0 * progress.TotalBytes / 1024 / 1024:0}MB）：{file}";
    }

    public Progress<FileProcessProgress> CreateFileProgressReporter(string message)
    {
        return CreateFileProgressReporter(message,
            p => AccumulatedLength + p.ProcessedBytes,
            () => TotalLength,
            p => Path.GetFileName(p.SourceFilePath));
    }

    public Progress<FileProcessProgress> CreateFileProgressReporter<T>(string message,
        Func<FileProcessProgress, T> getCurrent,
        Func<T> getTotal,
        Func<FileProcessProgress, string> getFile)
        where T : struct, INumber<T>
    {
        return new Progress<FileProcessProgress>(p =>
        {
            Service.NotifyProgress(getCurrent(p), getTotal());
            Service.NotifyMessage(CreateFileProgressMessage(message, p, getFile(p)));
        });
    }

    public string GetFileNumberMessage(string format = null)
    {
        format = format ?? (CanAccessFileCount ? ProgressMessageFormat : ProgressMessageIndexOnlyFormat);
        int naturalIndex = FileIndex + 1;
        if (CanAccessFileCount)
        {
            return string.Format(format, naturalIndex, FileCount);
        }

        return string.Format(format, naturalIndex);
    }

    public void IncreaseFileIndex()
    {
        if (Options.Threads != 1)
        {
            Interlocked.Increment(ref fileIndex);
        }
        else
        {
            fileIndex++;
        }
    }

    public void IncreaseFileLength(long increment)
    {
        if (Options.Threads != 1)
        {
            Interlocked.Add(ref fileLength, increment);
        }
        else
        {
            fileLength += increment;
        }
    }
}