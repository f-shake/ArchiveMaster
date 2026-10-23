# 逐行智能转换

- 工具 ID：`textTools.lineByLineProcessor`
- 模块：文本工具（`textTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：否（结果见 notes）

针对每行文本调用AI进行独立转换，可用于分类、判断或文本改写等任务

## 配置模板

```json
{
  "Examples": [],
  "MaxLineEachCall": 200,
  "Prompt": null,
  "SkipEmptyLine": true,
  "Source": {
    "FromFile": true,
    "Text": null,
    "Files": [],
    "IgnoreLineBreak": false
  },
  "EnableRetry": true,
  "MaxRetryCount": 3,
  "EnableMajorityVote": false,
  "VoteCount": 3
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `Examples` | ObservableCollection<LineByLineItem> | `[]` |
| `MaxLineEachCall` | Int32 | `200` |
| `Prompt` | String | `null` |
| `SkipEmptyLine` | Boolean | `true` |
| `Source` | TextSource | `{    "FromFile": true,    "Text": null,    "Files": [],    "IgnoreLineBreak": false  }` |
| `EnableRetry` | Boolean | `true` |
| `MaxRetryCount` | Int32 | `3` |
| `EnableMajorityVote` | Boolean | `false` |
| `VoteCount` | Int32 | `3` |
