# 按时间段分类

- 工具 ID：`fileTools.timeClassify`
- 模块：文件目录工具（`fileTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

识别目录中相同时间段的文件，将它们移动到相同的新目录中

## 配置模板

```json
{
  "Dir": null,
  "MinTimeInterval": "01:00:00"
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `Dir` | String | `null` |
| `MinTimeInterval` | TimeSpan | `"01:00:00"` |
