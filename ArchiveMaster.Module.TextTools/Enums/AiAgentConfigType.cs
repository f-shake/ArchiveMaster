using System.ComponentModel;

namespace ArchiveMaster.Enums;

public enum AiAgentConfigType
{
    [Description("纯文本")]
    Text,
    
    [Description("文本源")]
    TextSource
}