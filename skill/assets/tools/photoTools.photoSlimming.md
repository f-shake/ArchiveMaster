# 照片瘦身

- 工具 ID：`photoTools.photoSlimming`
- 模块：照片工具（`photoTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

复制或压缩照片，用于生成更小的照片集副本

## 配置模板

```json
{
  "ClearAllBeforeRunning": false,
  "CompressFilter": {
    "IsEnabled": true,
    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",
    "ExcludeFolders": "$*",
    "ExcludePaths": "",
    "IncludeFiles": "*.jpg\n*.jpeg\n*.heic\n*.heif\n*.bmp\n*.png\n*.gif\n*.tiff\n*.tif",
    "IncludeFolders": "*",
    "IncludePaths": "*",
    "UseRegex": false
  },
  "CompressImageFormat": 114,
  "CopyDirectlyFilter": {
    "IsEnabled": true,
    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",
    "ExcludeFolders": "$*",
    "ExcludePaths": "",
    "IncludeFiles": "*.gpx\n*.doc\n*.docx\n*.ppt\n*.pptx\n*.xls\n*.xlsx\n*.pdf",
    "IncludeFolders": "*",
    "IncludePaths": "*",
    "UseRegex": false
  },
  "DeepestLevel": 10000,
  "DistDir": "C:\\目标\\目录",
  "FileNameTemplate": "{FileName}",
  "FolderNameTemplate": "{FolderName}",
  "MaxDurationTolerance": 60,
  "MaxLongSize": 10000,
  "MaxShortSize": 5000,
  "Quality": 50,
  "HeifPreset": "fast",
  "HeifTileSize": 512,
  "SkipIfExist": true,
  "SourceDir": "C:\\源\\目录",
  "Thread": 4
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `ClearAllBeforeRunning` | Boolean | `false` |
| `CompressFilter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*.jpg\n*.jpeg\n*.heic\n*.heif\n*.bmp\n*.png\n*.gif\n*.tiff\n*.tif",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
| `CompressImageFormat` | MagickFormat | `114` |
| `CopyDirectlyFilter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*.gpx\n*.doc\n*.docx\n*.ppt\n*.pptx\n*.xls\n*.xlsx\n*.pdf",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
| `DeepestLevel` | Int32 | `10000` |
| `DistDir` | String | `"C:\\目标\\目录"` |
| `FileNameTemplate` | String | `"{FileName}"` |
| `FolderNameTemplate` | String | `"{FolderName}"` |
| `MaxDurationTolerance` | Double | `60` |
| `MaxLongSize` | Int32 | `10000` |
| `MaxShortSize` | Int32 | `5000` |
| `Quality` | Int32 | `50` |
| `HeifPreset` | String | `"fast"` |
| `HeifTileSize` | Int32 | `512` |
| `SkipIfExist` | Boolean | `true` |
| `SourceDir` | String | `"C:\\源\\目录"` |
| `Thread` | Int32 | `4` |
