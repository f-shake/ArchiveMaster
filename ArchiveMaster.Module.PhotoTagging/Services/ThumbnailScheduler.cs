using System.Collections.Concurrent;
using System.Diagnostics;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Services;

public static class ThumbnailScheduler
{
    private record LoadRequest(ImageFileInfo Item, Action Action);

    private static readonly ConcurrentStack<LoadRequest> taskStack = new();
    private static readonly SemaphoreSlim concurrencyLimiter = new(Math.Max(2, Environment.ProcessorCount / 2));

    // 状态标记：0 为静止，1 为正在处理
    private static int isProcessing = 0;

    public static void Enqueue(ImageFileInfo item, Action loadAction)
    {
        taskStack.Push(new LoadRequest(item, loadAction));

        int waitingCount = taskStack.Count;
        Debug.WriteLine($"[Scheduler] 任务入栈。当前排队中: {waitingCount}");

        if (Interlocked.CompareExchange(ref isProcessing, 1, 0) == 0)
        {
            Debug.WriteLine("[Scheduler] 启动新的调度循环...");
            _ = Task.Run(ProcessQueueAsync);
        }
    }

    private static async Task ProcessQueueAsync()
    {
        try
        {
            while (true)
            {
                // 1. 尝试从栈顶获取任务 (LIFO)
                if (!taskStack.TryPop(out var current))
                {
                    // 再次确认是否真的空了，准备退出
                    Interlocked.Exchange(ref isProcessing, 0);

                    // 双重检查防止退出瞬间又有新任务进入
                    if (taskStack.IsEmpty)
                    {
                        Debug.WriteLine("[Scheduler] 所有任务处理完毕，调度循环退出。");
                        break;
                    }
                    else
                    {
                        // 如果不为空，尝试重新夺回控制权
                        if (Interlocked.CompareExchange(ref isProcessing, 1, 0) == 0)
                        {
                            continue;
                        }

                        break;
                    }
                }

                // 2. 预检：如果图片已经加载（比如重复入栈的情况），直接跳过
                if (current.Item.ThumbnailImage != null)
                {
                    continue;
                }

                // 3. 等待并发槽位
                await concurrencyLimiter.WaitAsync();

                // 4. 执行任务
                _ = Task.Run(() =>
                {
                    try
                    {
                        Debug.WriteLine($"[Worker] 开始执行。剩余排队: {taskStack.Count}");
                        current.Action();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Worker] 任务执行出错: {ex.Message}");
                    }
                    finally
                    {
                        concurrencyLimiter.Release();
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Scheduler] 调度循环崩溃: {ex.Message}");
            Interlocked.Exchange(ref isProcessing, 0);
        }
    }
}