using FzLib.IO;

namespace ArchiveMaster.Services;

public class FilesLoopStates
{
    public FilesLoopStates(FilesLoopOptions options)
    {
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

    public FilesLoopOptions Options { get; }
    private long totalLength = 0;
    private int fileCount = 0;
    private int fileIndex = 0;
    private long fileLength = 0;

    internal bool NeedBroken { get; private set; }

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

    public long TotalLength
    {
        get => CanAccessTotalLength ? totalLength : throw new ArgumentException("未初始化总大小，不可调用TotalLength");
        internal set
        {
            totalLength = value;
            CanAccessTotalLength = true;
        }
    }

    internal bool CanAccessTotalLength { get; set; }

    internal bool CanAccessFileCount { get; set; }

    public long AccumulatedLength => fileLength;

    public static string ProgressMessageFormat { get; set; } = "（{0}/{1}）";

    public static string ProgressMessageIndexOnlyFormat { get; set; } = "（{0}个）";

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

    public void Break()
    {
        NeedBroken = true;
    }

    public Progress<FileProcessProgress> GetFileProcessProgress(string message, string file,
        Action<double> NotifyProgress, Action<string> NotifyMessage)
    {
        string numMsg = GetFileNumberMessage("{0}/{1}");
        return new Progress<FileProcessProgress>(p =>
        {
            NotifyProgress(1.0 * (AccumulatedLength + p.ProcessedBytes) / TotalLength);
            NotifyMessage(
                $"{message}（{numMsg}，本文件{1.0 * p.ProcessedBytes / 1024 / 1024:0}MB/{1.0 * p.TotalBytes / 1024 / 1024:0}MB）：{file}");
        });
    }
}