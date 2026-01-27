using System.ComponentModel;
using System.Text.Json.Serialization;
using ArchiveMaster.AiAgents;
using ArchiveMaster.Attributes;
using ArchiveMaster.Enums;
using ArchiveMaster.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArchiveMaster.Configs;

public partial class TextRewriterConfig : ConfigBase
{
    [ObservableProperty]
    private TextSource source = new TextSource();

    [ObservableProperty]
    private List<AiAgentBase> aiAgents = new List<AiAgentBase>();

    [ObservableProperty]
    private string selectedAiAgentTypeName;


    public override void Check()
    {
        if (Source.IsEmpty())
        {
            throw new ArgumentException("文本源为空");
        }
    }
}