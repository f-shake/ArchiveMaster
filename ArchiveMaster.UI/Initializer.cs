using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using ArchiveMaster.Models;
using ArchiveMaster.Platforms;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels;
using ArchiveMaster.Views;
using Avalonia;
using Avalonia.Controls;
using FzLib.Avalonia.Dialogs;
using FzLib.Program.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ArchiveMaster;

public static class Initializer
{
    private static bool stopped = false;
    private static List<ToolPanelGroupInfo> views = new List<ToolPanelGroupInfo>();
    public static IHost AppHost { get; private set; }

    public static IModuleInfo[] ModuleInitializers { get; } =
    [
#if DEBUG
        // new TestModuleInfo(),
#endif
        new FileToolsModuleInfo(),
        // new PhotoArchiveModuleInfo(),
        // new OfflineSyncModuleInfo(),
        // new DiscArchiveModuleInfo(),
        // new FileBackupperModuleInfo(),
    ];

    public static IReadOnlyList<ToolPanelGroupInfo> Views => views.AsReadOnly();

    public static void ClearViewsInstance()
    {
        foreach (var group in views)
        {
            foreach (var panel in group.Panels)
            {
                panel.PanelInstance = null;
            }
        }
    }

    public static IReadOnlyList<IBackgroundService> GetBackgroundServices()
    {
        return HostServices.GetServices<IHostedService>()
            .OfType<IBackgroundService>()
            .ToList()
            .AsReadOnly();
    }

    public static void Initialize()
    {
        if (AppHost != null)
        {
            throw new InvalidOperationException("已经初始化");
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var builder = Host.CreateApplicationBuilder();
        var config = new AppConfig();
        InitializeModules(builder.Services, config);
        config.Load();
        builder.Services.AddSingleton(config);
        builder.Services.AddTransient<MainWindow>();
        builder.Services.AddTransient<MainView>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddHostedService<AppLifetime>();
        builder.Services.TryAddStartupManager();
        AppHost = builder.Build();
        HostServices.Initialize(AppHost.Services);
        AppHost.Start();
    }

    public static Task StopAsync()
    {
        if (!stopped)
        {
            stopped = true;
            return AppHost.StopAsync();
        }

        return Task.CompletedTask;
    }

    private static void InitializeModules(IServiceCollection services, AppConfig appConfig)
    {
        List<(int Order, ToolPanelGroupInfo Group)> viewsWithOrder = new List<(int, ToolPanelGroupInfo)>();

        foreach (var moduleInitializer in ModuleInitializers)
        {
#if !DEBUG
            try
            {
#endif
                //注册配置
                foreach (var config in moduleInitializer.Configs ?? [])
                {
                    appConfig.RegisterConfig(config);
                }

                //注册后台服务
                foreach (var type in moduleInitializer.BackgroundServices ?? [])
                {
                    if (!typeof(IBackgroundService).IsAssignableFrom(type))
                    {
                        throw new Exception($"后台服务{type.Name}没有实现{nameof(IBackgroundService)}接口");
                    }

                    services.AddSingleton(typeof(IHostedService), s => ActivatorUtilities.CreateInstance(s, type));
                    //无法使用 services.AddHostedService(type);
                }

                //注册单例服务
                foreach (var service in moduleInitializer.SingletonServices ?? [])
                {
                    services.AddSingleton(service);
                }

                //注册瞬时服务
                foreach (var service in moduleInitializer.TransientServices ?? [])
                {
                    services.AddTransient(service);
                }

                //注册视图和视图模型
                foreach (var panel in moduleInitializer.Views?.Panels ?? [])
                {
                    services.AddTransient(panel.ViewType, s =>
                    {
                        var obj = (StyledElement)ActivatorUtilities.CreateInstance(s, panel.ViewType);
                        obj.DataContext = s.GetRequiredService(panel.ViewModelType);
                        return obj;
                    });
                    services.AddTransient(panel.ViewModelType);
                }

                viewsWithOrder.Add((moduleInitializer.Order, moduleInitializer.Views));
#if !DEBUG
            }
            catch (Exception ex)
            {
                throw new Exception($"加载模块{moduleInitializer.ModuleName}时出错: {ex.Message}");
            }
#endif
            }

            views = viewsWithOrder.OrderBy(p => p.Order).Select(p => p.Group).ToList();
        }
    }