using ArchiveMaster.Enums;

namespace ArchiveMaster.ViewModels;

public class AiSystemChatMessage : AiChatMessage
{
    public override AiChatMessageSender Sender => AiChatMessageSender.System;
}