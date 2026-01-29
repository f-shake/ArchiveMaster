using System.ComponentModel;

namespace ArchiveMaster.Enums;

public enum AiChatMessageSender
{
    [Description("用户")]
    User,

    [Description("系统")]
    System,

    [Description("模型")]
    Assistant
}