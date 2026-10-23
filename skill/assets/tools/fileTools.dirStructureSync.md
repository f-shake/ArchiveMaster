# 目录结构同步

- 工具 ID：`fileTools.dirStructureSync`
- 模块：文件目录工具（`fileTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

以一个目录为模板，将另一个目录中的文件同步到与模板内相同文件一直的位置

## 配置模板

```json
{
  "TemplateDir": null,
  "SourceDir": null,
  "TargetDir": null,
  "CompareTime": false,
  "CompareLength": false,
  "CompareName": false,
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
  "MaxTimeToleranceSecond": 0,
  "Copy": false
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `TemplateDir` | String | `null` |
| `SourceDir` | String | `null` |
| `TargetDir` | String | `null` |
| `CompareTime` | Boolean | `false` |
| `CompareLength` | Boolean | `false` |
| `CompareName` | Boolean | `false` |
| `Filter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
| `MaxTimeToleranceSecond` | Int32 | `0` |
| `Copy` | Boolean | `false` |
