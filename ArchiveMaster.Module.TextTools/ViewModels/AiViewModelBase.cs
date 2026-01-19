using ArchiveMaster.Configs;
using ArchiveMaster.Services;

namespace ArchiveMaster.ViewModels;

public abstract class AiViewModelBase<TConfig> : MultiPresetViewModelBase<TConfig>
    where TConfig : ConfigBase, new()
{
    protected AiViewModelBase(ViewModelServices services) : base(services)
    {
        Services.AppConfig.GetOrCreateConfigWithDefaultKey<AiProvidersConfig>().PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AiProvidersConfig.CurrentProvider))
            {
                OnPropertyChanged(nameof(AI));
            }
        };
    }

    public AiProviderConfig AI =>
        Services.AppConfig.GetOrCreateConfigWithDefaultKey<AiProvidersConfig>().CurrentProvider;
}