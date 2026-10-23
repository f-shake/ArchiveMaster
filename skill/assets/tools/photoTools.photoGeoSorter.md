# 照片位置分类

- 工具 ID：`photoTools.photoGeoSorter`
- 模块：照片工具（`photoTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

根据照片的GPS位置信息，将照片分为不同地理区域的多个目录

## 配置模板

```json
{
  "Dir": null,
  "VectorFile": null,
  "Filter": {
    "IsEnabled": true,
    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",
    "ExcludeFolders": "$*",
    "ExcludePaths": "",
    "IncludeFiles": "*.heic\n*.heif\n*.jpg\n*.jpeg\n*.png\n*.tif\n*.dng\n*.arw",
    "IncludeFolders": "*",
    "IncludePaths": "*",
    "UseRegex": false
  },
  "FieldName": null
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `Dir` | String | `null` |
| `VectorFile` | String | `null` |
| `Filter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*.heic\n*.heif\n*.jpg\n*.jpeg\n*.png\n*.tif\n*.dng\n*.arw",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
| `FieldName` | String | `null` |
