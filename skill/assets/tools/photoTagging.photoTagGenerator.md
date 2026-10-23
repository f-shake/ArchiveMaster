# 照片标签生成

- 工具 ID：`photoTagging.photoTagGenerator`
- 模块：照片标签工具（`photoTagging`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

通过AI，自动生成照片的关键词标签

## 配置模板

```json
{
  "DescriptionLength": 30,
  "Dir": null,
  "Filter": {
    "IsEnabled": true,
    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",
    "ExcludeFolders": "$*",
    "ExcludePaths": "",
    "IncludeFiles": "*.heic\n*.heif\n*.jpg\n*.jpeg\n*.png\n*.tif",
    "IncludeFolders": "*",
    "IncludePaths": "*",
    "UseRegex": false
  },
  "ResizingTargetResolutionIn10k": 800,
  "TagFile": null,
  "TargetTagCount": 20,
  "MaxDegreeOfParallelism": 1,
  "AutoSaveInterval": 10,
  "RetryCount": 3
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `DescriptionLength` | Int32 | `30` |
| `Dir` | String | `null` |
| `Filter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*.heic\n*.heif\n*.jpg\n*.jpeg\n*.png\n*.tif",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
| `ResizingTargetResolutionIn10k` | Int32 | `800` |
| `TagFile` | String | `null` |
| `TargetTagCount` | Int32 | `20` |
| `MaxDegreeOfParallelism` | Int32 | `1` |
| `AutoSaveInterval` | Int32 | `10` |
| `RetryCount` | Int32 | `3` |
