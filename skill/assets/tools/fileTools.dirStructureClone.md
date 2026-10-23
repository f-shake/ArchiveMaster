# 目录结构克隆

- 工具 ID：`fileTools.dirStructureClone`
- 模块：文件目录工具（`fileTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

以一个目录为模板，导出一个新的稀疏文件目录，或包含文件结构的JSON文件

## 配置模板

```json
{
  "SourceDirOrFile": null,
  "InputStructureFile": false,
  "Filter": {
    "IsEnabled": true,
    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",
    "ExcludeFolders": "$*",
    "ExcludePaths": "",
    "IncludeFiles": "*",
    "IncludeFolders": "*",
    "IncludePaths": "*",
    "UseRegex": false
  },
  "TargetDirOrFile": null,
  "ExportStructureFile": false
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `SourceDirOrFile` | String | `null` |
| `InputStructureFile` | Boolean | `false` |
| `Filter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
| `TargetDirOrFile` | String | `null` |
| `ExportStructureFile` | Boolean | `false` |
