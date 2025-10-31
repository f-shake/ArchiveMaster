using ArchiveMaster.Configs;

namespace ArchiveMaster.Services;

public abstract class AiTwoStepServiceBase<TConfig>(AppConfig appConfig)
    : TwoStepServiceBase<TConfig>(appConfig)
    where TConfig : ConfigBase
{
    protected AppConfig AppConfig { get; } = appConfig;
    public AiProviderConfig AI => AppConfig.GetOrCreateConfigWithDefaultKey<AiProvidersConfig>().CurrentProvider;
}