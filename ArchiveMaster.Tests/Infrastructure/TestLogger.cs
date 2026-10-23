using Serilog;

namespace ArchiveMaster.Tests.Infrastructure;

/// <summary>
/// 把 Serilog 静音，避免测试失败路径依赖未配置的全局日志器输出。
/// </summary>
public sealed class TestLogger : IDisposable
{
    private readonly ILogger previous;

    public TestLogger()
    {
        previous = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();
    }

    public void Dispose()
    {
        Log.CloseAndFlush();
        Log.Logger = previous;
    }
}
