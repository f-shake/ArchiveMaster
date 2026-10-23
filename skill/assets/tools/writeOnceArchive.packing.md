# 打包和备份

- 工具 ID：`writeOnceArchive.packing`
- 模块：动态固存备份（`writeOnceArchive`）
- 准备阶段：有
- 准备阶段会写文件：是
- 提供条目级结果：否（结果见 notes）

扫描文件特征，制作文件归档包

## 配置模板

```json
{
  "SourceDir": null,
  "TargetDir": null,
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
  "PackingType": 0,
  "PackageSizeMB": 23500,
  "PreviousPackageInfoFiles": null,
  "Password": "",
  "HashCacheFile": null
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `SourceDir` | String | `null` |
| `TargetDir` | String | `null` |
| `Filter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
| `PackingType` | PackingType | `0` |
| `PackageSizeMB` | Int64 | `23500` |
| `PreviousPackageInfoFiles` | String | `null` |
| `Password` | SecurePassword | `""` |
| `HashCacheFile` | String | `null` |
