using System;
using System.IO;

namespace ArchiveMaster.Cli;

/// <summary>
/// 同一 profile 上只允许一个 CLI 进程操作文件，避免 plan/run 互相踩踏。
/// </summary>
public sealed class ProfileLock : IDisposable
{
    private readonly FileStream stream;

    private ProfileLock(FileStream stream)
    {
        this.stream = stream;
    }

    public static ProfileLock Acquire(string profileDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileDirectory);
        string path = Path.Combine(profileDirectory, ".am.lock");
        try
        {
            Directory.CreateDirectory(profileDirectory);
            var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                FileShare.None);
            return new ProfileLock(stream);
        }
        catch (IOException)
        {
            throw new CliException(2, $"另一个 am 进程正在使用 {profileDirectory}，请稍后再试。");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new CliException(2, $"无法在 {profileDirectory} 创建锁文件：{ex.Message}");
        }
    }

    public void Dispose()
    {
        stream.Dispose();
        // 故意保留锁文件：Unix 上“关闭后 unlink”存在竞态（另一个进程可能刚打开旧 inode，
        // 删除后第三方又能创建同名文件并加锁）。留一个空的 .am.lock 是安全的。
    }
}
