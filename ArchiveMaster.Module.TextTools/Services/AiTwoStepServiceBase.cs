using ArchiveMaster.Configs;

namespace ArchiveMaster.Services;

public abstract class AiTwoStepServiceBase<TConfig>(AppConfig appConfig)
    : TwoStepServiceBase<TConfig>(appConfig) 
    where TConfig : ConfigBase
{
    public AiProviderConfig AI => appConfig.GetOrCreateConfigWithDefaultKey<AiProvidersConfig>().CurrentProvider;
}