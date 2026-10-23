using System.Text;

namespace ArchiveMaster.Tests.Infrastructure;

/// <summary>
/// 每个用例独享的临时目录。测试只在这里读写文件，避免污染仓库与用户目录。
/// </summary>
public sealed class TempWorkspace : IDisposable
{
    private const string KeepTempEnvironmentVariable = "ARCHIVEMASTER_TESTS_KEEP_TEMP";

    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "ArchiveMaster.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string PathOf(params string[] parts)
    {
        return Path.Combine([Root, .. parts]);
    }

    public string Dir(params string[] parts)
    {
        string path = PathOf(parts);
        Directory.CreateDirectory(path);
        return path;
    }

    public string WriteText(string relativePath, string content, Encoding encoding = null)
    {
        string path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, encoding ?? new UTF8Encoding(false));
        return path;
    }

    public string WriteBytes(string relativePath, byte[] content)
    {
        string path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    public string WriteFile(string relativePath, int length, DateTime? lastWriteTime = null)
    {
        string path = WriteBytes(relativePath, new byte[length]);
        if (lastWriteTime.HasValue)
        {
            File.SetLastWriteTime(path, lastWriteTime.Value);
        }

        return path;
    }

    public void Dispose()
    {
        if (Environment.GetEnvironmentVariable(KeepTempEnvironmentVariable) == "1")
        {
            Console.WriteLine($"保留测试临时目录：{Root}");
            return;
        }

        TryDeleteDirectory(Root);
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(path, true);
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException) when (attempt < 2)
            {
                Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                // 清理失败不能把原本的测试结果改成失败；保留目录并打印路径供排查。
                Console.Error.WriteLine($"清理测试临时目录失败：{path}（{ex.Message}）");
                return;
            }
        }
    }
}
