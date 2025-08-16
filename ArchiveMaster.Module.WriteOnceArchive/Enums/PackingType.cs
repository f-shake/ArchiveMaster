using System.ComponentModel;

namespace ArchiveMaster.Enums
{
    public enum PackingType
    {
        [Description("复制")]
        Copy,

        [Description("创建ISO")]
        ISO,

        [Description("创建硬链接")]
        HardLink
    }
}