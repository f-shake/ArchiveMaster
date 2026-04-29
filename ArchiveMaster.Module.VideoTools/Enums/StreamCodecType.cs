using System.ComponentModel;

namespace ArchiveMaster.Enums;

public enum StreamCodecType
{
    [Description("视频")]
    Video,

    [Description("音频")]
    Audio,

    [Description("字幕")]
    Subtitle,

    [Description("数据")]
    Data,

    [Description("附件")]
    Attachment,

    [Description("未知")]
    Unknown,
}