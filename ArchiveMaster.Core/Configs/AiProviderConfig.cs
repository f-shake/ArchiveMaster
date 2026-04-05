using System;
using ArchiveMaster.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs
{
    public interface IAiProvider
    {
        public string Name { get; set; }
        public string Model { get; set; }
        public string Url { get; set; }
        public double? Temperature { get; set; }
        public double? TopP { get; set; }
        public int? MaxTokens { get; set; }
        public string ExtraParamsJson { get; set; }
    }

    public interface IOpenAIAiProvider : IAiProvider
    {
        public SecurePassword Key { get; set; }
        public bool SupportJsonOutput { get; set; }
    }

    public interface IOllamaAiProvider : IAiProvider
    {
    }

    public partial class AiProviderConfig : ObservableObject, IOpenAIAiProvider, IOllamaAiProvider
    {
        [property: SecurePassword.SecurePasswordAlwaysRemember]
        [ObservableProperty]
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
        private double? temperature = null;
        
        [ObservableProperty]
        private double? topP = null;
        
        [ObservableProperty]
        private int? maxTokens = null;
        
        [ObservableProperty]
        private string extraParamsJson = "";
        
        [ObservableProperty]
        private bool supportJsonOutput = true;
    }
}