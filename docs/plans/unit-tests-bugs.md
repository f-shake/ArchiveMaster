# 单元测试 2A/2B · BUG 清单（含手工复现）

这是阶段二 2A 的产物，2B 已修复全部 8 个缺陷。下列“实际”保留修复前的复现记录，便于对照。

- 测试总数：159
- 2A：150 通过、9 失败（对应 8 个缺陷）
- 2B：159 通过、0 失败
- 运行命令：`dotnet test ArchiveMaster.Tests`
- CLI 复现数据：`%TEMP%\am-bug-cli-*`（本次为 `am-bug-cli-248367bb1d854c29a7ba762a51cbed3b`）
- CLI 复现脚本：[unit-tests-cli-repro.ps1](unit-tests-cli-repro.ps1)（可用 `-AmPath` 指向其他构建）

## 0. 通用准备

CLI 使用构建产物：

```powershell
$am = 'C:\CodeTemp\bin\ArchiveMaster.Cli\net10.0\am.exe'
$root = Join-Path $env:TEMP ("am-bug-cli-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
```

先确认 CLI 是当前构建：

```powershell
dotnet build ArchiveMaster.Cli\ArchiveMaster.Cli.csproj
& $am --version
```

`am` 的退出码：`0` 正常；`1` 有警告/错误条目或工具异常；`2` 参数/配置/路径错误；`3` 取消；`4` 缺少 `--yes`。

---

## BUG-01 离线同步在时间容差为 0 时把相同文件判为修改

### 结论

本地与异地两个文件路径、长度、修改时间完全一致，`MaxTimeToleranceSecond=0` 时，`Step2Service` 仍生成 `Modify` 条目。用 CLI 实际跑 `step1 → step2 plan` 可稳定复现。

### 测试数据

- 异地目录：`$root\a\remote`
- 本地目录：`$root\b`（其下有同名子目录 `remote`，CLI 自动匹配要求顶级目录同名）
- `same.txt`：两边内容 `same`，长度相同，修改时间都是 `2020-01-01 10:00:00`
- `changed.txt`：异地 `old`，本地 `changed`，本地时间晚 1 分钟

### 复现步骤

```powershell
$remote = Join-Path $root 'a\remote'
$localRoot = Join-Path $root 'b'
$local = Join-Path $localRoot 'remote'
New-Item -ItemType Directory -Path $remote -Force | Out-Null
New-Item -ItemType Directory -Path $local -Force | Out-Null
$t0 = [DateTime]'2020-01-01 10:00:00'
$sameRemote = Join-Path $remote 'same.txt'
$sameLocal = Join-Path $local 'same.txt'
$changedRemote = Join-Path $remote 'changed.txt'
$changedLocal = Join-Path $local 'changed.txt'
[IO.File]::WriteAllText($sameRemote, 'same')
[IO.File]::WriteAllText($sameLocal, 'same')
[IO.File]::WriteAllText($changedRemote, 'old')
[IO.File]::WriteAllText($changedLocal, 'changed')
[IO.File]::SetLastWriteTime($sameRemote, $t0)
[IO.File]::SetLastWriteTime($sameLocal, $t0)
[IO.File]::SetLastWriteTime($changedRemote, $t0)
[IO.File]::SetLastWriteTime($changedLocal, $t0.AddMinutes(1))

$snapshot = Join-Path $root 'snapshot.os1'
@{ SyncDirs = @($remote); OutputFile = $snapshot } |
    ConvertTo-Json -Depth 10 | Set-Content (Join-Path $root 'step1.json') -Encoding utf8
& $am run offlineSync.step1 --config (Join-Path $root 'step1.json') --yes --json --quiet

$patch = Join-Path $root 'patch'
New-Item -ItemType Directory -Path $patch -Force | Out-Null
$filter = @{ IsEnabled = $true; ExcludeFiles = 'Thumbs.db'; ExcludeFolders = '$*';
    ExcludePaths = ''; IncludeFiles = '*'; IncludeFolders = '*'; IncludePaths = '*'; UseRegex = $false }
@{ CheckMoveIgnoreFileName = $true; EnableEncryption = $false; EncryptionPassword = '';
   ExportMode = 0; Filter = $filter; LocalDir = $localRoot; MaxTimeToleranceSecond = 0;
   OffsiteSnapshot = $snapshot; PatchDir = $patch } |
    ConvertTo-Json -Depth 10 | Set-Content (Join-Path $root 'step2.json') -Encoding utf8
& $am plan offlineSync.step2 --config (Join-Path $root 'step2.json') --json --quiet
```

### 期望

`same.txt` 不出现；待处理项只有 `changed.txt`（`Modify`）。

### 实际

输出中 `same.txt` 的 `Extra.UpdateType` 为 `Modify`，待处理项共 2 个。

### 证据

本次 CLI 运行：

```text
BUG-01 step1 exit=0 snapshot=True
BUG-01 step2 exit=0 same.txt UpdateType=Modify
BUG-01 items=2
```

### 定位

`ArchiveMaster.Module.OfflineSync/Services/Step2Service.cs:343-347`

```csharp
if ((offsiteFile.Time - file.LastWriteTime).Duration().TotalSeconds <
    Config.MaxTimeToleranceSecond && offsiteFile.Length == file.Length)
{
    continue;
}
```

`MaxTimeToleranceSecond=0` 时，`0 < 0` 为 false，完全相同的文件也会落到 `Modify`。

### 验收

- 时间与长度都相同时，不再产生 `UpdateType=Modify`。
- `MaxTimeToleranceSecond>0` 的容差行为保持原样。
- 保留 `Step2_Initialize_ComputesAddModifyDelete` 回归用例。

### 单测入口

```powershell
dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~Step2_Initialize_ComputesAddModifyDelete
```

---

## BUG-02 未标记的明文会被当作密文解密

### 结论

`TextEncryptionService.DecryptAsync` 对“没有任何前后缀标记”的文本返回乱码，而不是原样返回。GUI 的“文本简单加密”可以直接看到。

### 测试数据

- 密码：任意非空，例如 `pass`
- 文本：`plain text`
- 不包含前后缀 `##`

### 复现步骤（GUI）

1. 启动 `ArchiveMaster.UI.Desktop`。
2. 打开“文本工具 → 文本简单加密”。
3. 在密码框输入 `pass`。
4. 如果“自动转换”开启，先关闭，避免边输入边转换。
5. 把 `plain text` 粘贴到“密文”框。
6. 执行“解密”或打开自动转换，观察“明文”框。

### 期望

“明文”框仍为 `plain text`。没有标记的内容应按明文处理，而不是尝试解密。

### 实际

“明文”框出现乱码（本次单元测试使用的密码为 `password` 时结果为 `iyabq hmgh`；换密码后内容不同，但仍然是乱码）。

### 定位

`ArchiveMaster.Module.TextTools/Services/TextEncryptionService.cs:25`（`isEncrypted == false` 分支）与 `66-127`（拆分逻辑）。

`SplitIntoEncryptedAndUnencryptedText` 在找不到标记时返回 `(text, null)`；`DecryptAsync` 用：

```csharp
str.Append(isEncrypted == false ? part : encryptor.Decrypt(part));
```

`null == false` 为 false，于是进入了解密分支。

### 验收

- 无标记文本原样返回。
- 前后缀不成对时也按明文处理。
- 正确加密的 `##...##` 仍能正常解密。
- 保留 `TextEncryptionService_PlainTextDecryptsAsIs` 回归用例。

### 单测入口

```powershell
dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~TextEncryptionService_PlainTextDecryptsAsIs
```

---

## BUG-03 CSV 字段含双引号时没有整体加引号，且行尾多一个逗号

### 结论

`TableTextConverter` 导出的 CSV 不满足 RFC 4180：含双引号的字段没有被外层引号包住，行尾还残留逗号。

### 测试数据

```csharp
new Row { Name = "a", Age = 3, Note = "x\"y" }
```

期望整行：

```csv
a,3,"x""y"
```

### 复现步骤（代码/单测）

```powershell
dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~TableTextConverter_Csv
```

如果手工验证，可以在任意使用 `ConvertTableToTextButton` 的表格上导出 CSV。目前该按钮只出现在“逐行智能转换”面板，属于 AI 工具；不想配置 AI 时直接用上面的单测。

### 期望

- 字段内出现 `"` 时，整个字段用双引号包裹，内部 `"` 写成 `""`。
- 每个数据行末尾都没有多余的 `,` 或 `\r`。

### 实际

本次输出：

```text
Name,Age,Note\r\n
a,3,x""y,\r\n
b,4,m,\r
```

即 `x"y` 没有被包成 `"x""y"`，且**每个数据行**末尾都多一个逗号。

### 定位

- `ArchiveMaster.Core/Helpers/TableTextConverter.cs:108-125`：先替换 `"`，再判断是否包含逗号/空格；替换后的字符串不含这些字符，所以没有加外层引号。
- `ArchiveMaster.Core/Helpers/TableTextConverter.cs:175-186`：每个字段 `Append(',')` 后再 `AppendLine()`，每行都留下逗号；末尾 `sb.Length--` 只删掉 `\n`，还留下 `\r`。

### 验收

- `x"y` 输出为 `"x""y"`。
- 行尾不出现多余逗号。
- 保留两条 CSV 回归用例。

### 单测入口

```powershell
dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~TableTextConverter_CsvQuotesFieldContainingQuote
dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~TableTextConverter_CsvDoesNotEndWithComma
```

---

## BUG-04 共享预设重命名后旧预设仍在，并新建一份重复的新预设

### 结论

`AppConfig.RenamePreset` 按 `ConfigItem.Group` 过滤，但 `GetOrCreateConfig` 创建 `ConfigItem` 时没有写入 `Group`。结果是重命名只影响当前 ViewModel 的 `PresetNames`，配置项本身没改名。随后 UI 把 `PresetName` 设为新名称，`GetOrCreateConfig` 找不到新预设，便新建了一份默认配置；旧配置仍保留，最终同一共享组里 A、B 并存。

### 测试数据

- 两个共享同一组的配置：OfflineSync 的 step1、step2、step3（组名 `OfflineSync`）
- 组内已有预设 `A`，并在 step1、step2 都使用过，确保两边都有 `A` 的配置项
- 在 step1 的配置里填入可识别的数据，例如把某个路径改成 `A的数据`

### 复现步骤（GUI）

1. 启动桌面版，打开“异地备份 → 制作异地快照”。
2. 在顶部预设栏确认当前为 `A`，并确认 step2“本地生成补丁”也能看到 `A`。
3. 回到 step1，点击“修改配置名称”，输入 `B`，确认。
4. 切换到同组的 step2，查看预设列表。
5. 再切回 step1，查看预设列表。
6. 重启程序，再次查看两边的预设列表。

### 期望

- 两边都只显示 `B`，`A` 不再出现。
- `B` 就是原来的 `A`，step1 中填过的数据仍在。
- 重启后仍然只有 `B`。

### 实际

- step2 的预设列表变成 `A`、`B`；切回 step1 后也变成 `A`、`B`。
- `A` 保留了原数据，`B` 是新建的默认配置，数据没有跟着改名走。
- 重启后 `A`、`B` 仍然并存，不是临时显示问题。

### 定位

- `ArchiveMaster.Core/Configs/AppConfig.cs:107-131`：`new ConfigItem(key, new T(), preset)` 没有带 `ConfigMetadata.Group`。
- `ArchiveMaster.Core/Configs/AppConfig.cs:360-369`：`configs.Where(p => p.Group == group ...)` 因 `Group` 为空而匹配不到。

### 修复

`ConfigItem` 构造函数增加组名参数，`GetOrCreateConfig` 两个重载和 `ParseModules` 加载时写入 `configMetadata[key].Group`；`RenamePreset` 与 `RemovePreset` 继续按 `ConfigItem.Group` 查询。同一回归用例同时覆盖改名和共享组删除。

### 验收

- 共享组内所有配置项都改名，且保留原配置对象和数据。
- 改名后 `GetPresets(group)` 只返回新名称，不能多出旧名称或重复的新名称。
- 重启后仍然只有新名称。
- 保留 `RenameAndRemovePreset_WorkOnAllConfigsInGroup` 回归用例；该用例已补充“改名后取回原对象、数据仍在、组内只剩新名称”的断言。

### 单测入口

```powershell
dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~RenameAndRemovePreset_WorkOnAllConfigsInGroup
```

---

## BUG-05 `DirStructureSyncConfig.Check` 没检查模板目录，错误延后且是英文

### 结论

配置校验只重复检查 `SourceDir`，完全没有检查 `TemplateDir`。模板目录缺失时 CLI 不在配置阶段报“模板目录不存在”，而是在 `InitializeAsync` 深处抛英文路径异常。

### 测试数据

- `SourceDir`：存在
- `TemplateDir`：不存在
- `CompareName=true`
- `TargetDir`：任意

### 复现步骤（CLI）

```powershell
$source = Join-Path $root 'sync-source'
New-Item -ItemType Directory -Path (Join-Path $source 'sub') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $source 'sub\a.txt'), 'x')
$config = @{
    TemplateDir = (Join-Path $root 'missing-template')
    SourceDir = $source
    TargetDir = (Join-Path $root 'sync-target')
    CompareName = $true
    CompareTime = $false
    CompareLength = $false
    Copy = $true
    MaxTimeToleranceSecond = 0
}
$config | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $root 'dirsync.json') -Encoding utf8
& $am plan fileTools.dirStructureSync --config (Join-Path $root 'dirsync.json') --json --quiet
```

### 期望

配置校验阶段失败，错误为“模板目录不存在”，退出码 2。

### 实际

配置校验通过；准备阶段抛：

```text
Could not find a part of the path '...\missing-template'.
```

退出码 1。

### 定位

`ArchiveMaster.Module.FileTools/Configs/DirStructureSyncConfig.cs:35-44`：`CheckDir(SourceDir, "源目录")` 被重复用于“模板目录”，`CheckEmpty(SourceDir, "目标目录")` 也没检查 `TargetDir`；三处都应改为对应字段。

### 验收

- 模板目录缺失时在 `Check()` 阶段抛中文“模板目录不存在”。
- 保留 `Check_MissingTemplateDirectory_ShouldThrow` 回归用例。

### 单测入口

```powershell
dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~Check_MissingTemplateDirectory_ShouldThrow
```

---

## BUG-06 `AllowDifferentTime=false` 时仍把时间不同的同内容文件做成硬链接

### 结论

`LinkDeduplicationService` 已经对“哈希相同但修改时间不同”调用 `Warn`，但随后 `AddSubFile` 按子项默认勾选状态把父分组重新置为勾选；`AllowDifferentTime=false` 没有真正阻止执行。

### 测试数据

- 目录：`$root\link`
- `a.txt` 与 `b.txt`：内容都是 `same content`
- `a.txt` 修改时间：`2020-01-01 10:00:00`
- `b.txt` 修改时间：`2020-01-01 11:00:00`
- `AllowDifferentTime=false`

### 复现步骤（CLI）

```powershell
$linkDir = Join-Path $root 'link'
New-Item -ItemType Directory -Path $linkDir -Force | Out-Null
$a = Join-Path $linkDir 'a.txt'
$b = Join-Path $linkDir 'b.txt'
[IO.File]::WriteAllText($a, 'same content')
[IO.File]::WriteAllText($b, 'same content')
[IO.File]::SetLastWriteTime($a, [DateTime]'2020-01-01 10:00:00')
[IO.File]::SetLastWriteTime($b, [DateTime]'2020-01-01 11:00:00')
@{ Dir = $linkDir; AllowDifferentTime = $false; HashType = 2 } |
    ConvertTo-Json -Depth 10 | Set-Content (Join-Path $root 'link.json') -Encoding utf8
& $am run fileTools.linkDeduplication --config (Join-Path $root 'link.json') --yes --json --quiet
fsutil hardlink list $a
```

### 期望

时间不同且未允许时，分组不勾选，`run` 不创建硬链接；`fsutil hardlink list` 只有文件自身一条。

### 实际

`run` 退出码 0，两个文件被做成硬链接，`fsutil hardlink list a.txt` 返回两条：

```text
...\link\a.txt
...\link\b.txt
```

### 定位

- `ArchiveMaster.Module.FileTools/Services/LinkDeduplicationService.cs:76-91`：`AllowDifferentTime=false` 时只 `Warn`，没有把 `group.IsChecked` 显式置 false。
- `ArchiveMaster.Core/ViewModels/FileSystem/TreeDirInfo.cs:146-167`：`AddSubFile` 根据子项默认勾选把父分组变为 true。

### 验收

- 时间不同且不允许时，分组不勾选，`ExecuteAsync` 不处理。
- 允许时保持现有行为。
- 保留 `Initialize_AllowsDifferentTimesOnlyWhenConfigured` 回归用例。

### 单测入口

```powershell
dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~Initialize_AllowsDifferentTimesOnlyWhenConfigured
```

---

## BUG-07 `SimpleFileInfo(string relativePath, string topDir)` 不保存 topDir

### 结论

公开构造函数接收 `relativePath` 与 `topDir`，但没有设置 `TopDirectory` 或 `SetRelativePath`，导致 `RelativePath` 返回绝对路径。当前产品代码很少使用该重载，但 API 语义错误。

### 测试数据

- `relativePath = dir\a.txt`
- `topDir = %TEMP%\任意目录`

### 复现步骤（单测）

没有 CLI/GUI 入口，直接用单测：

```powershell
dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~SimpleFileInfo_RelativeConstructor_ShouldKeepRelativePath
```

如果手工写 C#，等价于：

```csharp
var file = new SimpleFileInfo(Path.Combine("dir", "a.txt"), tempRoot);
Console.WriteLine(file.RelativePath);
```

### 期望

输出 `dir\a.txt`（相对路径）。

### 实际

输出 `C:\Users\...\Temp\...\dir\a.txt`（绝对路径）。

### 定位

`ArchiveMaster.Core/ViewModels/FileSystem/SimpleFileInfo.cs:87-93`：

```csharp
public SimpleFileInfo(string relativePath, string topDir)
{
    Name = Path.GetFileName(relativePath);
    Path = Path.Combine(topDir, relativePath);
}
```

没有赋值 `TopDirectory`，也没有 `SetRelativePath`。

### 验收

- `RelativePath` 返回传入的相对路径。
- 不改变其他构造重载行为。
- 保留 `SimpleFileInfo_RelativeConstructor_ShouldKeepRelativePath` 回归用例。

### 单测入口

```powershell
dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~SimpleFileInfo_RelativeConstructor_ShouldKeepRelativePath
```

---

## BUG-08 空文件的“文件为空”警告被比特比例警告覆盖

### 结论

空文件先被标记“文件为空”，但随后仍进入比特统计；由于 0 字节文件的 `bitZeroCount=0`，又触发“文件中比特位为1的比例小于1%”，把更准确的消息覆盖。

### 测试数据

- 目录中只有一个 0 字节文件 `empty.bin`

### 复现步骤（CLI）

```powershell
$readDir = Join-Path $root 'read'
New-Item -ItemType Directory -Path $readDir -Force | Out-Null
[IO.File]::WriteAllBytes((Join-Path $readDir 'empty.bin'), [byte[]]::new(0))
@{ Dir = $readDir } | ConvertTo-Json -Depth 10 |
    Set-Content (Join-Path $root 'read.json') -Encoding utf8
& $am run fileTools.fileReadabilityScanner --config (Join-Path $root 'read.json') --yes --json --quiet
```

### 期望

条目消息为“文件为空”，状态 `Warn`。

### 实际

条目消息为“文件中比特位为1的比例小于1%”，状态 `Warn`，退出码 1。

### 定位

`ArchiveMaster.Module.FileTools/Services/FileReadabilityScannerService.cs:33-36` 与 `68-71`：先 `Warn("文件为空")`，没有直接返回，后续又 `Warn(...)`。

### 验收

- 0 字节文件只显示“文件为空”。
- 非空但比特比例异常的文件仍显示比例警告。
- 保留 `Execute_EmptyFile_IsWarned` 回归用例。

### 单测入口

```powershell
dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~Execute_EmptyFile_IsWarned
```

---

## CLI 覆盖范围

本次用真实 `am.exe` 复现了 4 条：

- BUG-01：`offlineSync.step1` + `offlineSync.step2 plan`
- BUG-05：`fileTools.dirStructureSync plan`
- BUG-06：`fileTools.linkDeduplication run`
- BUG-08：`fileTools.fileReadabilityScanner run`

其余 4 条没有对应的 `am` 工具入口，只能通过 GUI 或单测复现：

- BUG-02：文本简单加密不是 `TwoStep` 工具，未进入 `ToolCatalog`。
- BUG-03：`TableTextConverter` 是 Core 内部 helper，只有 AI 面板的导出按钮间接使用。
- BUG-04：`AppConfig.RenamePreset` 没有 CLI 命令；`am` 只按 `--preset` 读取。
- BUG-07：`SimpleFileInfo` 是内部模型，CLI 不直接暴露该构造重载。

以上 CLI 数据在 `%TEMP%`；复现脚本 [unit-tests-cli-repro.ps1](unit-tests-cli-repro.ps1) 已加入仓库。
