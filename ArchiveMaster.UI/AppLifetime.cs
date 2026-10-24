using System;
using System.Threading;
using System.Threading.Tasks;
using ArchiveMaster.Configs;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ArchiveMaster;

public class AppLifetime(AppConfig config) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            config.Save();
        }
        catch (Exception ex)
        {
            // 退出路径不能因为保存失败而崩溃，但必须留下日志
            Log.Error(ex, "退出时保存配置失败");
        }

        return Task.CompletedTask;
    }
}
