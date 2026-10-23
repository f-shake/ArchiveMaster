# 批量命令行执行

- 工具 ID：`fileTools.batchCommandLine`
- 模块：文件目录工具（`fileTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

以文件或目录为元素，批量执行命令行

## 配置模板

```json
{
  "Arguments": null,
  "AutoCreateDir": null,
  "Dir": null,
  "Level": 1,
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
  "Program": null,
  "Target": 0
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `Arguments` | String | `null` |
| `AutoCreateDir` | String | `null` |
| `Dir` | String | `null` |
| `Level` | Int32 | `1` |
| `Filter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
| `Program` | String | `null` |
| `Target` | BatchTarget | `0` |
