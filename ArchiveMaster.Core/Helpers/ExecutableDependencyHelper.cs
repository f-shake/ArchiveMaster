using System.Diagnostics;
using System.Text;
using ArchiveMaster.Configs;

namespace ArchiveMaster.Helpers;

public static class ExecutableDependencyHelper
{
    public static string GetFFprobeExePath()
    {
        var ffprobeExe = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";

        if (!string.IsNullOrWhiteSpace(GlobalConfigs.Instance.FFmpegDir))
        {
            if (!Directory.Exists(GlobalConfigs.Instance.FFmpegDir))
            {
                throw new DirectoryNotFoundException($"FFmpeg 目录不存在: {GlobalConfigs.Instance.FFmpegDir}");
            }

            ffprobeExe = Path.Combine(GlobalConfigs.Instance.FFmpegDir, ffprobeExe);
            if (!File.Exists(ffprobeExe))
            {
                throw new FileNotFoundException($"FFmpeg 可执行文件不存在: {ffprobeExe}");
            }
        }
        else
        {
            EnsureExeAvailable(ffprobeExe, "-version");
        }

        return ffprobeExe;
    }

    public static string GetProgramOutput(string exePath, string args = "")
    {
        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            FileName = exePath,
            Arguments = args
        };

        using Process p = new Process();
        p.StartInfo = startInfo;
        p.Start();
        string output = p.StandardOutput.ReadToEnd();
        string error = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return output + error;
    }

    private static void EnsureExeAvailable(string exePath, string args)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = exePath;
            process.StartInfo.Arguments = args;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();

            // 关键：设置等待超时，防止程序挂起
            if (!process.WaitForExit(100))
            {
                process.Kill();
            }
        }
        catch (Exception ex)
        {
            throw new FileNotFoundException($"无法调用可执行文件: {exePath}。请检查环境变量或配置路径。", ex);
        }
    }
}