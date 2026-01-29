using ArchiveMaster.Configs;
using ArchiveMaster.Services;

namespace ArchiveMaster.ViewModels;

public abstract class AiChatViewModelBase<TService,TConfig> : MultiPresetViewModelBase<TConfig>
    where TConfig : ConfigBase, new()
    where TService : AiServiceBase<TConfig>, IAiService
{
    protected AiChatViewModelBase(ViewModelServices services) : base(services)
    {
        AiConversation = HostServices.GetRequiredService<AiConversation>();
        Service = HostServices.GetRequiredService<TService>();
        AiConversation.BindService(Service);

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

    public AiConversation AiConversation { get; }
    protected TService Service { get; }

    protected override void OnConfigChanged()
    {
        base.OnConfigChanged();
        Service.Config = Config;
    }
}