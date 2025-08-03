using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ArchiveMaster.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using FzLib.Application;
using FzLib.Programming;
using Serilog;

namespace ArchiveMaster.UI.Desktop;

class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        GC.KeepAlive(typeof(SvgImageExtension).Assembly);
        GC.KeepAlive(typeof(Avalonia.Svg.Skia.Svg).Assembly);
        return AppBuilder.Configure<App>()
            .With(new X11PlatformOptions()
            {
                UseDBusFilePicker = false,
            })
            .UsePlatformDetect()
            .LogToTrace();
    }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static async Task Main(string[] args)
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("ProcessId", App.ProcessId)
            .WriteTo.File("logs/logs.txt",
                outputTemplate: "{Timestamp:yyyy-MM-ddTHH:mm:ss.fffZ} [{Level:u3}] [PID:{ProcessId}] {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day)
            .CreateLogger();
        Log.Information("程序启动");

        UnhandledExceptionCatcher.WithCatcher(() =>
            {
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }).Catch((ex, s) =>
            {
                Log.Fatal(ex, "未捕获的异常，来源：{ExceptionSource}", s);
                Log.CloseAndFlush();
            })
            .Finally(() =>
            {
                Log.Information("程序结束");
                Log.CloseAndFlush();
            })
            .Run();
    }
}