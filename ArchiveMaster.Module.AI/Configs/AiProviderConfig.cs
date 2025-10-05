using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs
{
    public partial class AiProviderConfig :ObservableObject
    {
        [ObservableProperty]
        private ProviderType type = ProviderType.OpenAI;

        [ObservableProperty]
        private string model = "";

        [ObservableProperty]
        private string url = "";

        [ObservableProperty]
        private string key = "";

        public enum ProviderType
        {
            OpenAI,
            Ollama
        }
    }
}