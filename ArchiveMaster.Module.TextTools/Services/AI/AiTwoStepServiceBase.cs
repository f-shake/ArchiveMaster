using ArchiveMaster.Configs;
using ArchiveMaster.ViewModels;

namespace ArchiveMaster.Services;

public abstract class AiTwoStepServiceBase<TConfig>(AppConfig appConfig)
    : TwoStepServiceBase<TConfig>(appConfig)
    where TConfig : ConfigBase
{
    public const int MaxLength = 300_000;

    public AiProviderConfig AI => AppConfig.GetOrCreateConfigWithDefaultKey<AiProvidersConfig>().CurrentProvider;
   
    protected AppConfig AppConfig { get; } = appConfig;

    protected void CheckTextSource(string text, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"{name}为空");
        }

        if (text.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(TextSource), $"{name}长度超过限制（{maxLength}），请缩减文本源长度。");
        }
    }
}