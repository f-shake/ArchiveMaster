using System.ComponentModel;

namespace ArchiveMaster.Enums
{
    public enum DeleteMode
    {
        [Description("直接删除")]
        DeleteDirectly,
        [Description("移动到指定文件夹")]
        MoveToSpecialFolder,
        [Description("优先删除到回收站（较慢）")]
        RecycleBinPrefer
    }
}
