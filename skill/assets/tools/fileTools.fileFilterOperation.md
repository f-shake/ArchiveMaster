# 文件筛选操作

- 工具 ID：`fileTools.fileFilterOperation`
- 模块：文件目录工具（`fileTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

对筛选后的文件进行复制、移动、删除操作

## 配置模板

```json
{
  "SourceDirs": null,
  "TargetDir": null,
  "Type": 0,
  "PathFilter": {
    "IsEnabled": true,
    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",
    "ExcludeFolders": "$*",
    "ExcludePaths": "",
    "IncludeFiles": "*",
    "IncludeFolders": "*",
    "IncludePaths": "*",
    "UseRegex": false
  },
  "EarliestTime": null,
  "LatestTime": null,
  "MinLength": null,
  "MaxLength": null,
  "TargetFileNameMode": 0,
  "HashType": 2
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `SourceDirs` | String | `null` |
| `TargetDir` | String | `null` |
| `Type` | FileFilterOperationType | `0` |
| `PathFilter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
| `EarliestTime` | DateTime | `null` |
| `LatestTime` | DateTime | `null` |
| `MinLength` | Int64 | `null` |
| `MaxLength` | Int64 | `null` |
| `TargetFileNameMode` | FileFilterOperationTargetFileNameMode | `0` |
| `HashType` | HashAlgorithmType | `2` |
