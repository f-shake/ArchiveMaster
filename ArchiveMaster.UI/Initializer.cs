//#define DYNAMIC_DLL

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using ArchiveMaster.Platforms;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels;
using ArchiveMaster.Views;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using FzLib.Application.Startup;
using FzLib.Avalonia.DependencyInjection;
using FzLib.Avalonia.Dialogs;
using FzLib.Avalonia.Services;
using FzLib.Programming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ArchiveMaster;

public static class Initializer
{
    public class ServiceInitializingEventArgs(IServiceCollection services) : EventArgs
    {
        public IServiceCollection Services { get; } = services;
    }

    public static event EventHandler<ServiceInitializingEventArgs> ServiceInitializing;

    private static bool stopped = false;
    private static List<ToolPanelGroupInfo> views = new List<ToolPanelGroupInfo>();
    public static IHost AppHost { get; private set; }

#if !DYNAMIC_DLL
    public static IModuleInfo[] ModuleInitializers { get; } =
    [
#if DEBUG

#else
#endif
        new FileToolsModuleInfo(),
        new PhotoToolsModuleInfo(),
        new TextToolsModuleInfo(),
        new OfflineSyncModuleInfo(),
        // new DiscArchiveModuleInfo(),
        new WriteOnceArchiveModuleInfo(),
        new FileBackupperModuleInfo(),
    ];
#endif

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
        config.Initialize();
        builder.Services.AddSingleton(config);
        builder.Services.AddTransient<ViewModelServices>();
        builder.Services.AddTransient<MainWindow>();
        builder.Services.AddTransient<MainView>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddSingleton<GlobalCommands>();
        builder.Services.AddViewAndViewModel<SettingDialog, SettingViewModel>();
        builder.Services.AddViewAndViewModel<MasterPasswordDialog, MasterPasswordViewModel>();
        builder.Services.AddHostedService<AppLifetime>();
        builder.Services.TryAddStartupManager();
        builder.Services.AddDialogService();
        builder.Services.AddClipboardService();
        builder.Services.AddStorageProviderService();
        builder.Services.AddProgressOverlayService();
        ServiceInitializing?.Invoke(null, new ServiceInitializingEventArgs(builder.Services));
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

#if DYNAMIC_DLL
        List<IModuleInfo> ModuleInitializers = new List<IModuleInfo>();
        string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string[] dllFiles = Directory.GetFiles(currentDirectory, "ArchiveMaster.Module.*.dll");


        foreach (string dllFile in dllFiles)
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(dllFile);
                var moduleInfoType = assembly.GetTypes()
                                         .FirstOrDefault(t => t.GetInterfaces().Contains(typeof(IModuleInfo)))
                                     ?? throw new Exception($"模块程序集{Path.GetFileName(dllFile)}中不包含分组信息");
                var groupInstance = (IModuleInfo)Activator.CreateInstance(moduleInfoType);
                ModuleInitializers.Add((IModuleInfo)Activator.CreateInstance(moduleInfoType));
            }
            catch (Exception ex)
            {
                throw new Exception($"加载模块{Path.GetFileName(dllFile)}失败：{ex.Message}", ex);
            }
        }
#endif

        foreach (var moduleInitializer in ModuleInitializers)
        {
            try
            {
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
                        throw new Exception($"后台服务{type?.Name}没有实现{nameof(IBackgroundService)}接口");
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

                var views = moduleInitializer.Views;
                if (views == null)
                {
                    Log.Warning("模块{ModuleInitializerModuleName}没有定义视图", moduleInitializer.ModuleName);
                    throw new Exception($"模块{moduleInitializer.ModuleName}没有定义视图");
                }
                //注册视图和视图模型
                foreach (var panel in views.Panels ?? [])
                {
                    services.AddTransient(panel.ViewType, s =>
                    {
                        var obj = (StyledElement)ActivatorUtilities.CreateInstance(s, panel.ViewType);
                        obj.DataContext = s.GetRequiredService(panel.ViewModelType);
                        return obj;
                    });
                    services.AddTransient(panel.ViewModelType);
                }

                if (moduleInitializer.HelpFileName != null)
                {
                    var command = new RelayCommand(() =>
                    {
                        try
                        {
                            string url =
                                $"https://github.com/f-shake/ArchiveMaster/blob/master/helps/{moduleInitializer.HelpFileName}";
                            Process.Start(new ProcessStartInfo(url)
                            {
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "打开帮助URL失败");
                        }
                    });                    

                    views.MenuItems.Add(new ModuleMenuItemInfo("在线帮助", command));
                }

                viewsWithOrder.Add((moduleInitializer.Order, views));
            }
            catch (Exception ex)
            {
                throw new Exception($"加载模块{moduleInitializer.ModuleName}时出错: {ex.Message}");
            }
        }

        views = viewsWithOrder.OrderBy(p => p.Order).Select(p => p.Group).ToList();
    }
}