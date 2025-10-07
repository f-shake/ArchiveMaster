using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public abstract class AiTwoStepViewModelBase<TService, TConfig> : TwoStepViewModelBase<TService, TConfig>
    where TService : TwoStepServiceBase<TConfig>
    where TConfig : ConfigBase, new()
{
    protected AiTwoStepViewModelBase(AppConfig appConfig, IDialogService dialogService) : base(appConfig, dialogService)
    {
        AppConfig.GetOrCreateConfigWithDefaultKey<AiProvidersConfig>().PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AiProvidersConfig.CurrentProvider))
            {
                OnPropertyChanged(nameof(AI));
            }
        };
    }

    public AiProviderConfig AI => AppConfig.GetOrCreateConfigWithDefaultKey<AiProvidersConfig>().CurrentProvider;
}