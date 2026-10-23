# 视频时间戳字幕

- 工具 ID：`videoTools.timeAss`
- 模块：视频工具（`videoTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

为视频添加录制时间的字幕

## 配置模板

```json
{
  "Format": {
    "HorizontalAlignment": 2,
    "VerticalAlignment": 0,
    "Bold": false,
    "BorderColor": "#000000",
    "BorderWidth": 3,
    "Font": "Microsoft YaHei UI",
    "TextColor": "#FFFFFF",
    "TimeFormat": "HH:mm:ss",
    "Interval": 1000,
    "Italic": false,
    "VerticalMargin": 20,
    "HorizontalMargin": 20,
    "Size": 32,
    "Underline": false
  },
  "Files": null,
  "ExportFile": null,
  "CombineIntoSingleFile": true
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `Format` | TimeAssFormat | `{    "HorizontalAlignment": 2,    "VerticalAlignment": 0,    "Bold": false,    "BorderColor": "#000000",    "BorderWidth": 3,    "Font": "Microsoft YaHei UI",    "TextColor": "#FFFFFF",    "TimeFormat": "HH:mm:ss",    "Interval": 1000,    "Italic": false,    "VerticalMargin": 20,    "HorizontalMargin": 20,    "Size": 32,    "Underline": false  }` |
| `Files` | String | `null` |
| `ExportFile` | String | `null` |
| `CombineIntoSingleFile` | Boolean | `true` |
