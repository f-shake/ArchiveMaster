# 异地同步

- 工具 ID：`offlineSync.step3`
- 模块：异地备份（`offlineSync`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

在异地应用补丁包，实现数据同步

## 配置模板

```json
{
  "PatchDir": null,
  "Password": ""
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `PatchDir` | String | `null` |
| `Password` | SecurePassword | `""` |
