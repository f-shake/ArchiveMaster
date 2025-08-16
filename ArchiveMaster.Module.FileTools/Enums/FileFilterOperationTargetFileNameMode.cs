using System.ComponentModel;

namespace ArchiveMaster.Enums;

public enum FileFilterOperationTargetFileNameMode
{
    [Description("保持原有目录结构，使用原文件名")]
    PreserveDirectoryStructure,

    [Description("平铺到目标目录，使用原文件名")]
    FlattenWithOriginalNames,

    [Description("平铺到目标目录，使用相对路径作为新文件名")]
    FlattenWithRelativePathNames
}
