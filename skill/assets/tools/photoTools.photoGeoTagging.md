# 照片地理信息写入

- 工具 ID：`photoTools.photoGeoTagging`
- 模块：照片工具（`photoTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

将GPX轨迹中的GPS位置信息，根据拍摄时间自动匹配并写入照片Exif

## 配置模板

```json
{
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
  "GpxFile": null,
  "MaxTolerance": "00:01:00",
  "TimeOffset": "00:00:00",
  "InverseTimeOffset": false
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `Dir` | String | `null` |
| `Filter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*.heic\n*.heif\n*.jpg\n*.jpeg\n*.png\n*.tif",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
| `GpxFile` | String | `null` |
| `MaxTolerance` | TimeSpan | `"00:01:00"` |
| `TimeOffset` | TimeSpan | `"00:00:00"` |
| `InverseTimeOffset` | Boolean | `false` |
