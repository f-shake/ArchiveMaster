# 错别字检查

- 工具 ID：`textTools.typoChecker`
- 模块：文本工具（`textTools`）
- 准备阶段：无
- 准备阶段会写文件：否
- 提供条目级结果：否（结果见 notes）

使用AI检查文本是否存在错别字

## 配置模板

```json
{
  "Source": {
    "FromFile": true,
    "Text": null,
    "Files": [],
    "IgnoreLineBreak": false
  },
  "MinSegmentLength": 200,
  "ExtraAiPrompt": null
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `Source` | TextSource | `{    "FromFile": true,    "Text": null,    "Files": [],    "IgnoreLineBreak": false  }` |
| `MinSegmentLength` | Int32 | `200` |
| `ExtraAiPrompt` | String | `null` |
