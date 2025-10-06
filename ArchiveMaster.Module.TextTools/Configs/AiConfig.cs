using System.Collections.ObjectModel;
using ArchiveMaster.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using FzLib.IO;

namespace ArchiveMaster.Configs;

public partial class AiConfig : ConfigBase
{
    public List<AiProviderConfig> Providers { get; set; } = new List<AiProviderConfig>();

    public override void Check()
    {
    }
}