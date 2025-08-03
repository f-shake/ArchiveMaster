using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels;
using ArchiveMaster.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using FzLib;
using FzLib.Avalonia.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using FzLib.Programming;
using Serilog;

namespace ArchiveMaster;

public class App : Application
{
    private bool doNotOpen = false;

    private bool isMainWindowOpened = false;

    public static readonly int ProcessId = Process.GetCurrentProcess().Id;
    public event EventHandler<ControlledApplicationLifetimeExitEventArgs> Exit;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        if (RuntimeFeature.IsDynamicCodeSupported) //非AOT，启动速度慢
        {
            ShowSplashScreenIfNeeded();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (!TcpSingleInstanceHelper.EnsureSingleInstance(OnActivatedAsync))
            {
                Log.Information("检测到已有实例在运行，程序将退出");
                SplashWindow.CloseCurrent();
                doNotOpen = true;
                desktop.Shutdown();
                Environment.Exit(0);
                return;
            }
        }

        Initializer.Initialize();
        if (OperatingSystem.IsWindows())
        {
            Resources.Add("ContentControlThemeFontFamily", new FontFamily("Microsoft YaHei UI"));
        }
    }

    static Task OnActivatedAsync()
    {
        Dispatcher.UIThread.Invoke(() => { (App.Current as App).ActivateMainWindow(); });
        return Task.CompletedTask;
    }

    private void ShowSplashScreenIfNeeded()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (!(desktop.Args is { Length: > 0 } && desktop.Args[0] == "s"))
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime && OperatingSystem.IsWindows())
                {
                    SplashWindow.CreateAndShow();
                }
            }
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (doNotOpen)
        {
            return;
        }

        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += Desktop_Exit;

            if (!(desktop.Args is { Length: > 0 } && desktop.Args[0] == "s"))
            {
                SetNewMainWindow(desktop);
            }
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = HostServices.GetRequiredService<MainView>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void Desktop_Exit(object sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        TrayIcon.GetIcons(this)?[0]?.Dispose();
        Exit?.Invoke(sender, e);
        TcpSingleInstanceHelper.Dispose();
        await Initializer.StopAsync();
    }

    private async void ExitMenuItem_Click(object sender, EventArgs e)
    {
        await Initializer.StopAsync();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private MainWindow SetNewMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        //由于MainWindow的new需要一定时间，有时候连续调用就会导致重复创建，因此单独建立一个字段来记录
        if (isMainWindowOpened)
        {
            throw new InvalidOperationException("MainWindow已创建");
        }

        isMainWindowOpened = true;
        desktop.MainWindow = HostServices.GetRequiredService<MainWindow>();
        desktop.MainWindow.Closed += (s, e) =>
        {
            desktop.MainWindow = null;
            isMainWindowOpened = false;
            Initializer.ClearViewsInstance();

            var backgroundServices = Initializer.GetBackgroundServices();
            if (backgroundServices.Count == 0 || backgroundServices.All(p => !p.IsEnabled))
            {
                desktop.Shutdown();
            }
        };
        desktop.MainWindow.Opened += (s, e) => { SplashWindow.CloseCurrent(); };
        return desktop.MainWindow as MainWindow;
    }


    private void ActivateMainWindow()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is MainWindow m)
            {
                m.BringToFront();
            }
            else //关了窗口，重新开一个新的
            {
                if (!isMainWindowOpened)
                {
                    SetNewMainWindow(desktop).Show();
                }
            }
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
    }

    private void TrayIcon_Clicked(object sender, EventArgs e)
    {
        ActivateMainWindow();
    }
}