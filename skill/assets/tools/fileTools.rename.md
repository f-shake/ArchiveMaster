# 批量重命名

- 工具 ID：`fileTools.rename`
- 模块：文件目录工具（`fileTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

批量对一个目录中的文件或文件夹按规则进行重命名操作

## 配置模板

```json
{
  "Manual": false,
  "ManualMaps": "",
  "Dir": null,
  "SearchPattern": null,
  "ReplacePattern": null,
  "RenameTarget": 0,
  "SearchMode": 0,
  "RenameMode": 0,
  "SearchPath": false,
  "IgnoreCase": true,
  "IncludeSubDirs": true
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `Manual` | Boolean | `false` |
| `ManualMaps` | String | `""` |
| `Dir` | String | `null` |
| `SearchPattern` | String | `null` |
| `ReplacePattern` | String | `null` |
| `RenameTarget` | RenameTargetType | `0` |
| `SearchMode` | SearchMode | `0` |
| `RenameMode` | RenameMode | `0` |
| `SearchPath` | Boolean | `false` |
| `IgnoreCase` | Boolean | `true` |
| `IncludeSubDirs` | Boolean | `true` |
