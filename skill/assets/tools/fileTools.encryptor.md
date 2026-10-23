# 文件加密解密

- 工具 ID：`fileTools.encryptor`
- 模块：文件目录工具（`fileTools`）
- 准备阶段：有
- 准备阶段会写文件：否
- 提供条目级结果：是

使用AES加密方法，对文件进行加密或解密

## 配置模板

```json
{
  "CipherMode": 1,
  "DeleteSourceFiles": false,
  "EncryptDirectoryStructure": false,
  "EncryptedDir": null,
  "FilenameDuplicationPolicy": 0,
  "KeySize": 256,
  "PaddingMode": 2,
  "Password": "",
  "RawDir": null,
  "Type": 0
}
```

## 配置字段

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `CipherMode` | CipherMode | `1` |
| `DeleteSourceFiles` | Boolean | `false` |
| `EncryptDirectoryStructure` | Boolean | `false` |
| `EncryptedDir` | String | `null` |
| `FilenameDuplicationPolicy` | FilenameDuplicationPolicy | `0` |
| `KeySize` | Int32 | `256` |
| `PaddingMode` | PaddingMode | `2` |
| `Password` | SecurePassword | `""` |
| `RawDir` | String | `null` |
| `Type` | EncryptorTaskType | `0` |
