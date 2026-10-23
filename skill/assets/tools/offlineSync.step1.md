# 制作异地快照

- 工具 ID：`offlineSync.step1`
- 模块：异地备份（`offlineSync`）
- 准备阶段：无
- 准备阶段会写文件：否
- 提供条目级结果：否（结果见 notes）

在异地计算机创建所需要的目录快照

## 配置模板

```json
{
  "OutputFile": null,
  "SyncDirs": null
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `OutputFile` | String | `null` |
| `SyncDirs` | ObservableCollection<String> | `null` |
