# 附属文件清理

- 工具 ID：`fileTools.twinFileCleaner`
- 模块：文件目录工具（`fileTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

当目录中存在某后缀文件（如.dng）时，自动删除同名不同后缀的关联文件（如.jpg）

## 配置模板

```json
{
  "DeletingPatterns": [
    "{Name}.JPG",
    "{Name}(*).JPG"
  ],
  "Dir": null,
  "MasterExtensions": [
    "DNG",
    "ARW",
    "RW2"
  ]
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `DeletingPatterns` | ObservableStringList | `[    "{Name}.JPG",    "{Name}(*).JPG"  ]` |
| `Dir` | String | `null` |
| `MasterExtensions` | ObservableStringList | `[    "DNG",    "ARW",    "RW2"  ]` |
