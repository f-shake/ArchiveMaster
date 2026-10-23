# 编码识别与转换

- 工具 ID：`textTools.encodingConverter`
- 模块：文本工具（`textTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

识别文本文件的编码，并可以转换为指定编码

## 配置模板

```json
{
  "Dir": null,
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
  "TargetEncoding": "UTF-8",
  "WithBom": null,
  "SaveToAnotherDir": false,
  "AnotherDir": null
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `Dir` | String | `null` |
| `Filter` | FileFilterRule | `{    "IsEnabled": true,    "ExcludeFiles": "Thumbs.db\nThumb.db\ndesktop.ini",    "ExcludeFolders": "$*",    "ExcludePaths": "",    "IncludeFiles": "*",    "IncludeFolders": "*",    "IncludePaths": "*",    "UseRegex": false  }` |
| `TargetEncoding` | String | `"UTF-8"` |
| `WithBom` | Boolean | `null` |
| `SaveToAnotherDir` | Boolean | `false` |
| `AnotherDir` | String | `null` |
