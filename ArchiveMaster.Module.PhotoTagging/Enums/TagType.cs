using System.ComponentModel;

namespace ArchiveMaster.Enums;

public enum TagType
{
    [Description("全部")]
    All,
    [Description("对象")]
    Object,
    [Description("场景")]
    Scene,
    [Description("情绪")]
    Mood,
    [Description("颜色")]
    Color,
    [Description("拍摄")]
    Technique,
    [Description("文本")]
    Text,
    [Description("描述")]
    Description
}