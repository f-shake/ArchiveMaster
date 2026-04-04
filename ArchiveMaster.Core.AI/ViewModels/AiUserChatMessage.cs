using ArchiveMaster.Enums;

namespace ArchiveMaster.ViewModels;

public class AiUserChatMessage : AiChatMessage
{
    public override AiChatMessageSender Sender => AiChatMessageSender.User;
}