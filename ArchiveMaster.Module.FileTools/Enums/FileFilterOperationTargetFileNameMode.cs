using System.ComponentModel;

namespace ArchiveMaster.Enums;

public enum FileFilterOperationTargetFileNameMode
{
    [Description("保持原目录结构")]
    PreserveDirectoryStructure,

    [Description("平铺目录，使用原文件名")]
    FlattenWithOriginalNames,

    [Description("平铺目录，相对路径作为文件名")]
    FlattenWithRelativePathNames
}
