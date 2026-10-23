# 重复文件清理

- 工具 ID：`fileTools.duplicateFileCleanup`
- 模块：文件目录工具（`fileTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

清理一个目录内的重复文件，或已包含在另一个目录中的相同文件

## 配置模板

```json
{
  "CleaningDir": null,
  "CompareLength": true,
  "CompareName": true,
  "CompareTime": true,
  "ReferenceDir": null,
  "TimeToleranceSecond": 0
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `CleaningDir` | String | `null` |
| `CompareLength` | Boolean | `true` |
| `CompareName` | Boolean | `true` |
| `CompareTime` | Boolean | `true` |
| `ReferenceDir` | String | `null` |
| `TimeToleranceSecond` | Int32 | `0` |
