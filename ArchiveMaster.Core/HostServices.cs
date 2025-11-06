using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArchiveMaster;

public static class HostServices
{
    private static IServiceProvider Provider { get; set; }

    public static IEnumerable<IBackgroundService> GetBackgroundServices()
    {
        return Provider.GetServices<IBackgroundService>();
    }

    public static T GetRequiredService<T>()
    {
        return Provider.GetRequiredService<T>();
    }

    public static object GetRequiredService(Type type)
    {
        return Provider.GetRequiredService<IHostedService>();
    }

    public static T GetService<T>()
    {
        return Provider.GetService<T>();
    }

    public static object GetService(Type type)
    {
        return Provider.GetService(type);
    }

    public static IEnumerable<T> GetServices<T>()
    {
        return Provider.GetServices<T>();
    }

    public static IEnumerable<object> GetServices(Type type)
    {
        return Provider.GetServices(type);
    }

    public static void Initialize(IServiceProvider services)
    {
        if (Provider != null)
        {
            throw new InvalidOperationException("已经经过初始化");
        }

        Provider = services ?? throw new ArgumentNullException();
    }
}