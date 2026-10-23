# 图像标签管理

- 工具 ID：`photoTagging.photoTagManager`
- 模块：照片标签工具（`photoTagging`）
- 准备阶段：无
- 准备阶段会写文件：否
- 提供条目级结果：否（结果见 notes）

管理各图像的标签，进行新增、修改或和删除

## 配置模板

```json
{
  "TagFile": null,
  "RootDir": null,
  "Filter": {
    "IsEnabled": true,
    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",
    "ExcludeFolders": "$*",
    "ExcludePaths": "",
    "IncludeFiles": "*.heic\n*.heif\n*.jpg\n*.jpeg\n*.png\n*.tif",
    "IncludeFolders": "*",
    "IncludePaths": "*",
    "UseRegex": false
  }
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `TagFile` | String | `null` |
| `RootDir` | String | `null` |
| `Filter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*.heic\n*.heif\n*.jpg\n*.jpeg\n*.png\n*.tif",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
