using ArchiveMaster.Attributes;

namespace ArchiveMaster.Enums;

public enum CustomType
{
    [AiAgent("自定义",
        "手动设置提示词",
        "{info}",
        NeedExtraInformation = true,
        ExtraInformationLabel = "自定义提示词")]
    Custom
}