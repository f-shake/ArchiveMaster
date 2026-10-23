# 本地生成补丁

- 工具 ID：`offlineSync.step2`
- 模块：异地备份（`offlineSync`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

在本地计算机生成与异地的差异文件的补丁包

## 配置模板

```json
{
  "CheckMoveIgnoreFileName": true,
  "EnableEncryption": false,
  "EncryptionPassword": "",
  "ExportMode": 0,
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
  "LocalDir": null,
  "MaxTimeToleranceSecond": 3,
  "OffsiteSnapshot": null,
  "PatchDir": null
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `CheckMoveIgnoreFileName` | Boolean | `true` |
| `EnableEncryption` | Boolean | `false` |
| `EncryptionPassword` | SecurePassword | `""` |
| `ExportMode` | ExportMode | `0` |
| `Filter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
| `LocalDir` | String | `null` |
| `MaxTimeToleranceSecond` | Int32 | `3` |
| `OffsiteSnapshot` | String | `null` |
| `PatchDir` | String | `null` |
