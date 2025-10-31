using System.Text;
using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;

namespace ArchiveMaster.Services;

public class TextEncryptionService(TextEncryptionConfig config, AppConfig appConfig)
    : ServiceBase(appConfig)
{
    public const int MAX_LENGTH = 300_000;
    
    public TextEncryptionConfig Config { get; } = config;

    public string ProcessedText { get; private set; }

    private void CheckStringLength(StringBuilder str)
    {
        if (str.Length > MAX_LENGTH)
        {
            throw new Exception($"文本长度超过限制（{MAX_LENGTH}）");
        }
    }

    public async Task DecryptAsync(TextSource source, CancellationToken ct = default)
    {
        StringBuilder str = new StringBuilder();

        TextEncryptor encryptor = new TextEncryptor(Config.Password.Password);

        await Task.Run(async () =>
        {
            await foreach (var line in source.GetPlainTextAsync().WithCancellation(ct))
            {
                foreach (var (part, isEncrypted) in SplitIntoEncryptedAndUnencryptedText(line.Text))
                {
                    str.Append(isEncrypted == false ? part : encryptor.Decrypt(part));
                    CheckStringLength(str);
                }
            }
        }, ct);

        ProcessedText = str.ToString();
    }

    public async Task EncryptAsync(TextSource source, CancellationToken ct = default)
    {
        StringBuilder str = new StringBuilder();
        str.Append(Config.Prefix);

        TextEncryptor encryptor = new TextEncryptor(Config.Password.Password);

        await Task.Run(async () =>
        {
            await foreach (var line in source.GetPlainTextAsync().WithCancellation(ct))
            {
                foreach (var (part, isEncrypted) in SplitIntoEncryptedAndUnencryptedText(line.Text))
                {
                    str.Append(isEncrypted == true ? part : encryptor.Encrypt(part));
                    CheckStringLength(str);
                }
            }
        }, ct);

        str.Append(Config.Suffix);
        if (str.Length == Config.Prefix.Length + Config.Suffix.Length)
        {
            str.Clear();
        }

        ProcessedText = str.ToString();
    }

    private IEnumerable<(string part, bool? isEncrypted)> SplitIntoEncryptedAndUnencryptedText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        string prefix = Config.Prefix;
        string suffix = Config.Suffix;

        // 若前后缀为空，无法判断
        if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(suffix))
        {
            yield return (text, null);
            yield break;
        }

        var result = new List<(string, bool?)>();
        int pos = 0;
        bool inEncrypted = false;
        int lastPos = 0;
        bool foundAnyMarker = false;

        while (pos < text.Length)
        {
            string marker = inEncrypted ? suffix : prefix;
            int next = text.IndexOf(marker, pos, StringComparison.Ordinal);
            if (next < 0)
            {
                break;
            }

            foundAnyMarker = true;

            // 提取 marker 前的部分
            if (next > pos)
            {
                string part = text[pos..next];
                result.Add((part, inEncrypted));
            }

            // 跳过 marker 本身
            pos = next + marker.Length;
            inEncrypted = !inEncrypted;
            lastPos = pos;
        }

        // 处理最后一段
        if (pos < text.Length)
        {
            result.Add((text[pos..], inEncrypted));
        }

        // 若全程没找到标记，或标记不成对
        if (!foundAnyMarker || inEncrypted)
        {
            yield return (text, null);
            yield break;
        }

        foreach (var r in result)
            yield return r;
    }
}