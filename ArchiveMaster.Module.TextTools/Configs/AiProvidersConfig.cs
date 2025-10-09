using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using ArchiveMaster.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs;

public partial class AiProvidersConfig : ConfigBase
{
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(CurrentProvider))]
    private int currentProviderIndex = 0;

    [JsonIgnore]
    public AiProviderConfig CurrentProvider
    {
        get
        {
            if (Providers.Count == 0)
            {
                Providers.Add(DefaultProvider);
            }

            if (CurrentProviderIndex < 0)
            {
                CurrentProviderIndex = 0;
            }
            else if (CurrentProviderIndex >= Providers.Count)
            {
                CurrentProviderIndex = Providers.Count - 1;
            }

            return Providers[CurrentProviderIndex];
        }
    }

    public ObservableCollection<AiProviderConfig> Providers { get; set; } = [DefaultProvider];

    private static AiProviderConfig DefaultProvider => new AiProviderConfig
    {
        Name = "请配置AI模型",
        Type = AiProviderType.Ollama,
        Model = "qwen3:4b",
        Url = "http://localhost:11434/api",
    };

    public override void Check()
    {
    }
}