using System.ComponentModel;

namespace ArchiveMaster.Enums;

[Flags]
public enum TagType
{
    [Description("无")]
    None = 0x01,

    [Description("对象")]
    Object = 0x02,

    [Description("场景")]
    Scene = 0x04,

    [Description("情绪")]
    Mood = 0x08,

    [Description("颜色")]
    Color = 0x10,

    [Description("拍摄")]
    Technique = 0x20,

    [Description("文本")]
    Text = 0x40,

    [Description("描述")]
    Description = 0x80,
}