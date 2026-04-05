using ArchiveMaster.Configs;
using ArchiveMaster.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.ViewModels;

public partial class AiProviderConfigViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isDefault = false;

    [property: SecurePassword.SecurePasswordAlwaysRemember] [ObservableProperty]
    private SecurePassword key = new SecurePassword();

    [ObservableProperty]
    private string model = "";

    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private AiProviderType type = AiProviderType.OpenAI;

    [ObservableProperty]
    private string url = "";

    [ObservableProperty]
    private bool specialTemperature = false;

    [ObservableProperty]
    private double temperature = 0.7;

    [ObservableProperty]
    private bool specialTopP = false;

    [ObservableProperty]
    private double topP = 0.9;

    [ObservableProperty]
    private bool specialMaxTokens = false;

    [ObservableProperty]
    private int maxTokens = 16384;

    [ObservableProperty]
    private string extraParamsJson = "";

    [ObservableProperty]
    private bool supportJsonOutput;

    public static AiProviderConfigViewModel FromConfig(AiProviderConfig config)
    {
        var viewModel = new AiProviderConfigViewModel
        {
            Name = config.Name,
            Model = config.Model,
            Url = config.Url,
            ExtraParamsJson = config.ExtraParamsJson,
            Type = config.Type,
            Key = config.Key,
            SupportJsonOutput =  config.SupportJsonOutput,
        };
        if (config.Temperature.HasValue)
        {
            viewModel.SpecialTemperature = true;
            viewModel.Temperature = config.Temperature.Value;
        }

        if (config.TopP.HasValue)
        {
            viewModel.SpecialTopP = true;
            viewModel.TopP = config.TopP.Value;
        }

        if (config.MaxTokens.HasValue)
        {
            viewModel.SpecialMaxTokens = true;
            viewModel.MaxTokens = config.MaxTokens.Value;
        }

        return viewModel;
    }

    public AiProviderConfig ToConfig()
    {
        var config = new AiProviderConfig
        {
            Name = Name,
            Model = Model,
            Url = Url,
            ExtraParamsJson = ExtraParamsJson,
            Type = Type,
            Key = Key,
            Temperature = SpecialTemperature ? Temperature : null,
            TopP = SpecialTopP ? TopP : null,
            MaxTokens = SpecialMaxTokens ? MaxTokens : null,
            SupportJsonOutput = SupportJsonOutput
        };
        return config;
    }
}