//#define WRITEMESSAGE

using System.Collections;
using System.Diagnostics;
using System.Numerics;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using SimpleFileInfo = ArchiveMaster.ViewModels.FileSystem.SimpleFileInfo;

namespace ArchiveMaster.Services
{
    public abstract class ServiceBase<TConfig>(AppConfig appConfig) : ServiceBase(appConfig) where TConfig : ConfigBase
    {
        public TConfig Config { get; set; }
    }

    public abstract class ServiceBase : IDisposable
    {
        /// <summary>
        /// 消息最短通知间隔（毫秒）
        /// </summary>
        private const int DebounceIntervalMs = 50;

        private readonly AppConfig appConfig;

        private readonly object syncLock = new object();

        private Timer debounceTimer;

        private bool isDisposed = false;
        private DateTime lastMessageTime = DateTime.MinValue;

        private string pendingMessage;

        protected ServiceBase(AppConfig appConfig)
        {
            this.appConfig = appConfig;
            InitializeDebounceTimer();
        }

        public event EventHandler<MessageUpdateEventArgs> MessageUpdate;

        public event EventHandler<ProgressUpdateEventArgs> ProgressUpdate;

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            debounceTimer?.Dispose();
            GC.SuppressFinalize(this);
        }

        protected internal void NotifyMessage(string message)
        {
#if DEBUG && WRITEMESSAGE
            Debug.WriteLine("{0:HH:m:s.fff}\t{1}", DateTime.Now, message);
#endif
            MessageUpdate?.Invoke(this, new MessageUpdateEventArgs(message));
            return;
            if (isDisposed)
            {
                return;
            }

            lock (syncLock)
            {
                pendingMessage = message;

                // 如果距离上次发送不足50ms，则重置计时器
                var elapsedMs = (DateTime.Now - lastMessageTime).TotalMilliseconds;
                if (elapsedMs < DebounceIntervalMs)
                {
                    debounceTimer?.Change(DebounceIntervalMs, Timeout.Infinite);
                    return;
                }

                // 立即发送消息并记录时间
                SendPendingMessage();
            }
        }

        protected internal void NotifyProgress<T>(T current,T total) where T : struct, INumber<T>
        {
            double percent = Convert.ToDouble(current) / Convert.ToDouble(total);
            NotifyProgress(percent);
        }
        
        protected internal void NotifyProgress(double percent)
        {
#if DEBUG && WRITEMESSAGE
            Debug.WriteLine("{0:HH:m:s.fff}\t更新进度：{1:P2}", DateTime.Now, percent);
#endif
            ProgressUpdate?.Invoke(this, new ProgressUpdateEventArgs(percent));
            //Debug.WriteLine($"{percent * 100:0.00}%");
        }

        protected void NotifyProgressIndeterminate()
        {
            NotifyProgress(double.NaN);
        }

        protected FilesLoopStates TryForFiles<T>(IEnumerable<T> files, Action<T, FilesLoopStates> body,
            CancellationToken cancellationToken,
            FilesLoopOptions options = null)
            where T : SimpleFileInfo
        {
            options ??= FilesLoopOptions.DoNothing();
            var states = new FilesLoopStates(this, options);

            PreProcessStatistic(files, options, states);

            switch (options.Threads)
            {
                case 1:
                    ForEachFileCore(files, body, cancellationToken, options, states);
                    break;
                case >= 2 or 0:
                    ParallelForEachFileCore(files, body, cancellationToken, options, states);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Threads应为大于等于0的整数，其中1表示单线程，0表示自动多线程，>=2表示指定线程数");
            }

            return states;
        }

        protected async Task<FilesLoopStates> TryForFilesAsync<T>(IEnumerable<T> files, Action<T, FilesLoopStates> body,
            CancellationToken cancellationToken, FilesLoopOptions options = null)
            where T : SimpleFileInfo
        {
            FilesLoopStates states = null;
            await Task.Run(() => { states = TryForFiles(files, body, cancellationToken, options); }, cancellationToken);
            return states;
        }

        protected async Task<FilesLoopStates> TryForFilesAsync<T>(IEnumerable<T> files,
            Func<T, FilesLoopStates, Task> asyncBody,
            CancellationToken cancellationToken, FilesLoopOptions options = null)
            where T : SimpleFileInfo
        {
            options ??= FilesLoopOptions.DoNothing();
            var states = new FilesLoopStates(this, options);

            PreProcessStatistic(files, options, states);

            switch (options.Threads)
            {
                case 1:
                    await ForEachFileCoreAsync(files, asyncBody, cancellationToken, options, states);
                    break;
                case >= 2 or 0:
                    await ParallelForEachFileCoreAsync(files, asyncBody, cancellationToken, options, states);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Threads应为大于等于0的整数，其中1表示单线程，0表示自动多线程，>=2表示指定线程数");
            }

            return states;
        }

        private static void PreProcessStatistic<T>(IEnumerable<T> files, FilesLoopOptions options,
            FilesLoopStates states)
            where T : SimpleFileInfo
        {
            if (options.AutoApplyProgress == AutoApplyProgressMode.FileLength && options.TotalLength == 0)
            {
                states.CanAccessTotalLength = true;

                foreach (var file in files)
                {
                    if (file is not SimpleFileInfo f)
                    {
                        states.CanAccessTotalLength = false;
                        throw new ArgumentException(
                            "集合内的元素必须都为SimpleFileInfo时，才可以使用AutoApplyProgressMode.FileLength",
                            nameof(files));
                    }

                    states.TotalLength += f.Length;
                }
            }

            if (options.TotalCount == 0)
            {
                if (files is ICollection collection)
                {
                    states.FileCount = collection.Count;
                }
                else
                {
                    if (options.AutoApplyProgress == AutoApplyProgressMode.FileNumber)
                    {
                        throw new ArgumentException(
                            "必须为集合，才可以使用AutoApplyProgressMode.FileNumber",
                            nameof(files));
                    }
                }
            }
        }

        private static void ProcessAutoApplyStatus<T>(FilesLoopOptions options, T file) where T : SimpleFileInfo
        {
            if (options.AutoApplyStatus)
            {
                if (file.Status == ProcessStatus.Ready)
                {
                    file.Success();
                }
            }
        }

        private static void ProcessException<T>(FilesLoopOptions options, T file, Exception ex) where T : SimpleFileInfo
        {
            file.Error(ex);
            options.CatchAction?.Invoke(file, ex);
            if (options.ThrowExceptions)
            {
                throw ex;
            }
        }

        private void ForEachFileCore<T>(IEnumerable<T> files, Action<T, FilesLoopStates> body,
            CancellationToken cancellationToken,
            FilesLoopOptions options, FilesLoopStates states) where T : SimpleFileInfo
        {
            foreach (var file in files)
            {
                TryForFilesSingle(body, cancellationToken, options, file, states);
                if (states.NeedBroken)
                {
                    break;
                }
            }
        }

        private async Task ForEachFileCoreAsync<T>(IEnumerable<T> files, Func<T, FilesLoopStates, Task> asyncBody,
            CancellationToken cancellationToken, FilesLoopOptions options, FilesLoopStates states)
            where T : SimpleFileInfo
        {
            foreach (var file in files)
            {
                await TryForFilesSingleAsync(asyncBody, cancellationToken, options, file, states);
                if (states.NeedBroken)
                {
                    break;
                }
            }
        }

        private void InitializeDebounceTimer()
        {
            debounceTimer = new Timer(_ =>
            {
                lock (syncLock)
                {
                    if (pendingMessage != null)
                    {
                        SendPendingMessage();
                    }
                }
            }, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void ParallelForEachFileCore<T>(IEnumerable<T> files, Action<T, FilesLoopStates> body,
            CancellationToken cancellationToken,
            FilesLoopOptions options, FilesLoopStates states) where T : SimpleFileInfo
        {
            Parallel.ForEach(files, new ParallelOptions()
            {
                MaxDegreeOfParallelism = options.Threads <= 0 ? -1 : options.Threads,
                CancellationToken = cancellationToken
            }, (file, s) =>
            {
                TryForFilesSingle(body, cancellationToken, options, file, states);
                if (states.NeedBroken)
                {
                    s.Break();
                }
            });
        }

        private Task ParallelForEachFileCoreAsync<T>(IEnumerable<T> files,
            Func<T, FilesLoopStates, Task> asyncBody,
            CancellationToken cancellationToken, FilesLoopOptions options, FilesLoopStates states)
            where T : SimpleFileInfo
        {
            return Parallel.ForEachAsync(files, new ParallelOptions()
            {
                MaxDegreeOfParallelism = options.Threads <= 0 ? -1 : options.Threads,
                CancellationToken = cancellationToken
            }, async (file, c) => { await TryForFilesSingleAsync(asyncBody, c, options, file, states); });
        }

        private void ProcessFinally<T>(FilesLoopOptions options, T file, FilesLoopStates states)
            where T : SimpleFileInfo
        {
            if (states.CanAccessTotalLength)
            {
                states.IncreaseFileLength(file.Length);
            }

            states.IncreaseFileIndex();

            switch (options.AutoApplyProgress)
            {
                case AutoApplyProgressMode.FileLength:
                    NotifyProgress(1.0 * states.AccumulatedLength / states.TotalLength);
                    break;
                case AutoApplyProgressMode.FileNumber:
                    NotifyProgress(1.0 * states.FileIndex / states.FileCount);
                    break;
            }

            options.FinallyAction?.Invoke(file);
        }

        private void SendPendingMessage()
        {
            if (isDisposed)
            {
                return;
            }

            if (pendingMessage == null)
            {
                return;
            }

            MessageUpdate?.Invoke(this, new MessageUpdateEventArgs(pendingMessage));
            lastMessageTime = DateTime.Now;
            pendingMessage = null;
        }

        private void TryForFilesSingle<T>(Action<T, FilesLoopStates> body, CancellationToken cancellationToken,
            FilesLoopOptions options, T file,
            FilesLoopStates states) where T : SimpleFileInfo
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                body(file, states);
                ProcessAutoApplyStatus(options, file);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ProcessException(options, file, ex);
            }
            finally
            {
                ProcessFinally(options, file, states);
            }


            if (GlobalConfigs.Instance.DebugMode && GlobalConfigs.Instance.DebugModeLoopDelay > 0)
            {
                Thread.Sleep(GlobalConfigs.Instance.DebugModeLoopDelay);
            }
        }

        private async Task TryForFilesSingleAsync<T>(Func<T, FilesLoopStates, Task> asyncBody,
            CancellationToken cancellationToken,
            FilesLoopOptions options, T file,
            FilesLoopStates states) where T : SimpleFileInfo
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await asyncBody(file, states);
                ProcessAutoApplyStatus(options, file);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                ProcessException(options, file, new Exception($"加解密操作失败，可能是密钥或配置不正确，也有可能是文件损坏（{ex.Message}）", ex));
            }
            catch (Exception ex)
            {
                ProcessException(options, file, ex);
            }
            finally
            {
                ProcessFinally(options, file, states);
            }


            if (GlobalConfigs.Instance.DebugMode && GlobalConfigs.Instance.DebugModeLoopDelay > 0)
            {
                Thread.Sleep(GlobalConfigs.Instance.DebugModeLoopDelay);
            }
        }
    }
}