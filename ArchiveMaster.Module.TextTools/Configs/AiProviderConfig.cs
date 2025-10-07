using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs
{
    public partial class AiProviderConfig :ObservableObject
    {
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
    }
}