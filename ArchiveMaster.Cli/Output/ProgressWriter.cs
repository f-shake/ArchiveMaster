using System;

namespace ArchiveMaster.Cli;

/// <summary>
/// 进度只写 stderr，保证 --json 时 stdout 是干净的单对象。
/// </summary>
public sealed class ProgressWriter(bool quiet) : IProgress<ToolProgress>
{
    private readonly object sync = new();
    private string lastMessage;
    private int lastPercent = -1;
    private DateTime lastPercentAt = DateTime.MinValue;

    public void Report(ToolProgress value)
    {
        if (quiet || value == null)
        {
            return;
        }

        lock (sync)
        {
            if (!string.IsNullOrWhiteSpace(value.Message) && value.Message != lastMessage)
            {
                lastMessage = value.Message;
                Console.Error.WriteLine(value.Message);
                return;
            }

            if (value.Percent is double percentValue)
            {
                int percent = (int)Math.Round(percentValue * 100);
                if (percent != lastPercent && (DateTime.UtcNow - lastPercentAt).TotalMilliseconds >= 500)
                {
                    lastPercent = percent;
                    lastPercentAt = DateTime.UtcNow;
                    Console.Error.WriteLine($"进度：{percent}%");
                }
            }
        }
    }

    public void WriteCancel()
    {
        Console.Error.WriteLine("正在取消…");
    }
}
