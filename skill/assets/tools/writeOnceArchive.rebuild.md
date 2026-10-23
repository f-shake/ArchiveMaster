# 重建和恢复

- 工具 ID：`writeOnceArchive.rebuild`
- 模块：动态固存备份（`writeOnceArchive`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

从文件归档包重建目录结构，或验证文件完整性

## 配置模板

```json
{
  "SourceDirs": null,
  "TargetDir": null,
  "PackageInfoFile": null,
  "SkipIfExisted": true,
  "MaxTimeToleranceSecond": 2,
  "CheckOnly": false,
  "Password": ""
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `SourceDirs` | String | `null` |
| `TargetDir` | String | `null` |
| `PackageInfoFile` | String | `null` |
| `SkipIfExisted` | Boolean | `true` |
| `MaxTimeToleranceSecond` | Int32 | `2` |
| `CheckOnly` | Boolean | `false` |
| `Password` | SecurePassword | `""` |
