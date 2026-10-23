using System.Collections.Concurrent;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using ArchiveMaster.Services;
using ArchiveMaster.Tests.Infrastructure;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Tests.Core;

public class ServiceBaseTests : IDisposable
{
    private readonly GlobalStateScope globalState = new();
    private readonly TempWorkspace workspace = new();
    private readonly TestService service;

    public ServiceBaseTests()
    {
        service = new TestService(TestAppConfig.Create(workspace));
    }

    public void Dispose()
    {
        service.Dispose();
        workspace.Dispose();
        globalState.Dispose();
    }

    [Fact]
    public void TryForFiles_NormalPath_AppliesStatusProgressAndFinallyAction()
    {
        var files = CreateFiles(2);
        int finallyCount = 0;

        var states = service.Run(files, (file, _) => file.ProcessedByTest = true, CancellationToken.None,
            FilesLoopOptions.Builder()
                .AutoApplyStatus()
                .AutoApplyFileNumberProgress()
                .Finally(_ => finallyCount++)
                .Build());

        Assert.All(files, file => Assert.Equal(ProcessStatus.Success, file.Status));
        Assert.All(files, file => Assert.True(file.ProcessedByTest));
        Assert.Equal(2, finallyCount);
        Assert.Equal(2, states.FileIndex);
        Assert.Equal(2, states.FileCount);
    }

    [Fact]
    public void TryForFiles_Exception_WritesErrorAndInvokesCatchAction()
    {
        var file = CreateFiles(1)[0];
        Exception caught = null;

        service.Run([file], (_, _) => throw new InvalidOperationException("测试异常"), CancellationToken.None,
            FilesLoopOptions.Builder()
                .AutoApplyStatus()
                .Catch((_, ex) => caught = ex)
                .Build());

        Assert.Equal(ProcessStatus.Error, file.Status);
        Assert.IsType<InvalidOperationException>(caught);
        Assert.Contains("测试异常", file.Message);
    }

    [Fact]
    public void TryForFiles_ThrowExceptions_RethrowsOriginalException()
    {
        var file = CreateFiles(1)[0];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.Run([file], (_, _) => throw new InvalidOperationException("需要抛出"), CancellationToken.None,
                FilesLoopOptions.Builder().ThrowExceptions().Build()));

        Assert.Equal("需要抛出", exception.Message);
    }

    [Fact]
    public void TryForFiles_CancelInsideBody_MarksFileCancelledAndRethrows()
    {
        var file = CreateFiles(1)[0];
        using var cts = new CancellationTokenSource();

        Assert.Throws<OperationCanceledException>(() =>
            service.Run([file], (_, _) =>
            {
                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();
            }, cts.Token, FilesLoopOptions.Builder().AutoApplyStatus().Build()));

        Assert.Equal(ProcessStatus.Cancel, file.Status);
    }

    [Fact]
    public void TryForFiles_Break_StopsAfterCurrentFile()
    {
        var files = CreateFiles(3);
        int processed = 0;

        service.Run(files, (_, states) =>
        {
            processed++;
            states.Break();
        }, CancellationToken.None, FilesLoopOptions.Builder().AutoApplyStatus().Build());

        Assert.Equal(1, processed);
        Assert.Equal(ProcessStatus.Success, files[0].Status);
        Assert.Equal(ProcessStatus.Ready, files[1].Status);
    }

    [Fact]
    public void TryForFiles_MultipleThreads_ProcessesEveryFile()
    {
        var files = CreateFiles(8);
        var processed = new ConcurrentBag<string>();

        service.Run(files, (file, _) => processed.Add(file.Name), CancellationToken.None,
            FilesLoopOptions.Builder().AutoApplyStatus().WithMultiThreads(2).Build());

        Assert.Equal(8, processed.Count);
        Assert.All(files, file => Assert.Equal(ProcessStatus.Success, file.Status));
    }

    [Fact]
    public void TryForFiles_NegativeThreads_Throws()
    {
        var files = CreateFiles(1);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.Run(files, (_, _) => { }, CancellationToken.None,
                FilesLoopOptions.Builder().WithMultiThreads(-1).Build()));
    }

    [Fact]
    public void TryForFiles_FileLengthProgress_CalculatesTotalAndReportsCompletion()
    {
        var files = CreateFiles(2);
        files[0].Length = 10;
        files[1].Length = 30;
        var percents = new List<double>();
        service.ProgressUpdate += (_, e) => percents.Add(e.Progress);

        var states = service.Run(files, (_, _) => { }, CancellationToken.None,
            FilesLoopOptions.Builder().AutoApplyFileLengthProgress().Build());

        Assert.Equal(40, states.TotalLength);
        Assert.Equal(40, states.AccumulatedLength);
        Assert.Contains(1.0, percents);
    }

    [Fact]
    public void FilesLoopStates_UninitializedFileCount_Throws()
    {
        var states = new FilesLoopStates(service, FilesLoopOptions.DoNothing());

        Assert.Throws<ArgumentException>(() => _ = states.FileCount);
        Assert.Throws<ArgumentException>(() => _ = states.TotalLength);
    }

    [Fact]
    public void FilesLoopOptionsBuilder_DefaultsAreStable()
    {
        var options = FilesLoopOptions.Builder().Build();

        Assert.Equal(0, options.TotalCount);
        Assert.Equal(0, options.TotalLength);
        Assert.Equal(1, options.Threads);
        Assert.False(options.AutoApplyStatus);
        Assert.Equal(AutoApplyProgressMode.None, options.AutoApplyProgress);
    }

    private List<TestFile> CreateFiles(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new TestFile
            {
                Name = $"file{i}.txt",
                Path = workspace.PathOf($"file{i}.txt"),
                TopDirectory = workspace.Root,
            })
            .ToList();
    }

    private sealed class TestService(AppConfig appConfig) : ServiceBase(appConfig)
    {
        public FilesLoopStates Run<T>(IEnumerable<T> files, Action<T, FilesLoopStates> body,
            CancellationToken cancellationToken, FilesLoopOptions options)
            where T : SimpleFileInfo
        {
            return TryForFiles(files, body, cancellationToken, options);
        }
    }

    private sealed class TestFile : SimpleFileInfo
    {
        public bool ProcessedByTest { get; set; }
    }
}
