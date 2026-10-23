# 视频信息提取

- 工具 ID：`videoTools.videoInfo`
- 模块：视频工具（`videoTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

为一系列视频批量提取结构化的信息

## 配置模板

```json
{
  "Dir": null,
  "Filter": {
    "IsEnabled": true,
    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",
    "ExcludeFolders": "$*",
    "ExcludePaths": "",
    "IncludeFiles": "*.avi\n*.mp4\n*.webp\n*.3gp\n*.mkv\n*.mov",
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
| `Filter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*.avi\n*.mp4\n*.webp\n*.3gp\n*.mkv\n*.mov",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
