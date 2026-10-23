# 修复照片修改时间

- 工具 ID：`photoTools.repairModifiedTime`
- 模块：照片工具（`photoTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

寻找EXIF信息中的拍摄时间与照片修改时间不同的文件，将修改时间更新闻EXIF时间

## 配置模板

```json
{
  "Dir": null,
  "ThreadCount": 2,
  "MaxDurationTolerance": "00:00:01",
  "Filter": {
    "IsEnabled": true,
    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",
    "ExcludeFolders": "$*",
    "ExcludePaths": "",
    "IncludeFiles": "*.heic\n*.heif\n*.jpg\n*.jpeg\n*.png\n*.tif\n*.dng\n*.arw",
    "IncludeFolders": "*",
    "IncludePaths": "*",
    "UseRegex": false
  }
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `Dir` | String | `null` |
| `ThreadCount` | Int32 | `2` |
| `MaxDurationTolerance` | TimeSpan | `"00:00:01"` |
| `Filter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*.heic\n*.heif\n*.jpg\n*.jpeg\n*.png\n*.tif\n*.dng\n*.arw",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
