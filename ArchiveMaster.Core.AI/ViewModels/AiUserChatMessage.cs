using ArchiveMaster.Enums;

namespace ArchiveMaster.ViewModels;

public class AiUserChatMessage : AiChatMessage
{
    public override AiChatMessageSender Sender => AiChatMessageSender.User;

    public IReadOnlyList<byte[]> Images => images.AsReadOnly();

    private List<byte[]> images;

    public void AddImage(byte[] image)
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("当前消息已冻结，不可新增图像");
        }

        if (images == null)
        {
            images = new List<byte[]>();
        }

        images.Add(image);
    }
}