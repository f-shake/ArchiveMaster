# 包信息和验证

- 工具 ID：`writeOnceArchive.verify`
- 模块：动态固存备份（`writeOnceArchive`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

查看备份介质中文件包的信息，验证文件完整性

## 配置模板

```json
{
  "PackageDir": null,
  "Password": ""
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `PackageDir` | String | `null` |
| `Password` | SecurePassword | `""` |
