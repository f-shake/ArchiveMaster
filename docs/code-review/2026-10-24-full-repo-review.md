# ArchiveMaster 全仓代码复核报告（2026-10-24）

> 本文件是 2026-10-24 阶段三「独立复核」第 1 轮的落盘结果。
> 首轮只审不改；随后按用户逐条裁决进入修复，代码已改动（见「实施记录」）。

## 元信息

| 项 | 值 |
| --- | --- |
| 复核对象 | 整仓当前代码（非某次 diff） |
| 基线 | `dev` @ `b781a7f`（CLI：新增 --set 配置字段覆盖） |
| 复核时间 | 2026-10-24 |
| 方式 | 9 个只读子代理分域扫描 + 主代理逐条抽验 |
| 验证手段 | 静态阅读；修复后 `dotnet build ArchiveMaster.UI.Desktop` 通过（0 警告 0 错误），并跑了受影响测试类 |
| 结论 | 45 条问题（严重 10 / 中等 21 / 质量 5 / 文档发布测试 9）；已实施 25 条（A5、B6 部分实施），第 2 轮复核提出的 16 条中 14 条已修复 |
| 流水线状态 | 第 2 轮独立复核已确认无「仍存在」项；未做全量测试与 GUI/CLI 端到端 |

> 口径说明：对话中的概览曾按「合并同类项」记 41 条；本文件是拆分后的完整清单，共 45 条（按用户 2026-10-24 裁决，已移出 DiscArchive 与 FileBackupper 的 6 条，见第五节）。

## 判定口径

| 标记 | 含义 |
| --- | --- |
| 已核实 | 主代理亲自读到代码/配置/文档，所述机制成立 |
| 已核实（逻辑） | 代码路径成立，但依赖的竞态/时序未实测 |
| 子代理 | 仅子代理给出证据与行号，主代理未亲自复核 |
| 未复核 | 同上，且子代理自述未读完相关文件 |

「可能成立」的典型情形：需要特定运行时状态（权限、磁盘满、并发、外部进程）才触发，静态阅读无法排除。

## 结果概览

| 级别 | 条数 | 说明 |
| --- | --- | --- |
| 严重 | 10 | 数据丢失、静默未备份、安全预期落空 |
| 中等 | 21 | 特定条件下出错、可恢复但不提示、并发与资源问题 |
| 质量 | 5 | 死代码、潜伏错误、遗留工程 |
| 文档/发布/测试 | 9 | 文档与实现不符、发布配置漏项、测试鉴别力不足 |
| 合计 | 45 | —— |

优先级最高的三条：

1. **A1**——WriteOnce 增量打包会把修改过的文件当成已备份而静默跳过；
2. **A3**——SecurePassword 一次保存失败即永久丢掉全部密码与 AI Key；
3. **A2**——回收站删除失败时静默降级为永久删除。

## 处置决定（2026-10-24，用户裁决）

> 本节只记录处置口径，**不代表已经修改代码**；实现待后续开发轮次。

| 编号 | 处置 | 采纳的做法 | 状态 |
| --- | --- | --- | --- |
| A1 | 修复 | 缓存键加入长度/修改时间，包信息不复用旧 Hash | 已实施 |
| A2 | 标记为单文件错误 | 回收站删除失败不再降级为永久删除；只把该文件置为错误，其余文件继续 | 已实施 |
| A3 | 修复（按推荐，去掉确认） | 写失败抛出并阻断保存；读失败保留原密文；清空密码不做二次确认 | 已实施 |
| A4 | 修复（指定做法） | 先建临时硬链接，成功后删除原名 | 已实施 |
| A5 | 修复（按推荐） | 临时文件＋原子替换（已做）；Skip 前校验密文完整性未做 | 部分实施 |
| A7 | 修复（按推荐） | 原子保存、失败报错、导入前校验 `LoadError` | 已实施 |
| A8 | 修复（按推荐） | 校验 `RelativePath` 为相对路径，规范化后必须仍在 `TargetDir` 下 | 已实施 |
| A9 | 修复（按推荐） | Hash 不一致走 Error 分支，不再调用 `Success()` | 已实施 |
| A10 | 修复 | 写入前检查目标是否存在并按策略处理；0 长度文件跳过 `Seek` | 已实施 |
| A11 | 修复（指定做法） | 第二步失败先把 `TempPath` 移回原名；移回也失败则忽略该错误并抛出原始异常 | 已实施 |
| B5 | 修复 | 仅在 `Check()` 中拒绝目标目录等于/位于源目录内（不做额外确认） | 已实施 |
| B6 | 修复 | 写临时文件、成功后改名（已做）；HEIC 编码内部仍不能逐帧响应取消 | 部分实施 |
| B7 | 修复 | 写临时副本再替换（或失败时从备份恢复） | 已实施 |
| B8 | 修复 | 同路径直接跳过；目标存在时按重名策略处理 | 已实施 |
| B10 | 修复 | `CheckRange(Interval, 1, …)`，循环内加步进保护 | 已实施 |
| B11 | 修复 | 写入串行化（SemaphoreSlim）＋临时文件替换 | 已实施 |
| B12 | 修复 | 复用 `IHttpClientFactory` 或静态 HttpClient，ChatClient 按配置缓存 | 已实施 |
| B13 | 修复（指定做法） | 先判断两个命令是否在跑，再改状态与遮罩 | 已实施 |
| B14 | 修复（指定做法） | `try/finally` 取消并等待定时器结束 | 已实施 |
| B19 | 修复（指定做法） | 保留源相对路径层级，不再用 `file.Name` 扁平化 | 已实施 |
| B20 | 修复（指定做法） | 正则缓存键改为 `(pattern, options)` | 已实施 |
| B22 | 修复 | 读取时校验预设是否在列表内，否则回退默认并修正字典 | 已实施 |
| Q3 | 保留 | 当前未接线的消息防抖；用户说明有意保留，后续可能启用 | 保留 |
| D4 | 不处理 | versionCode 维持现状（用户裁决：不管了） | 不修复 |
| D5 | 修复 | 帮助文档改为「默认 CBC」 | 已实施 |
| D6 | 修复 | 删除帮助中不存在的「删除文件位置」，改述为按全局 `DeleteMode` 处理 | 已实施 |
| D7 | 修复 | 帮助文档改为准确描述：确定性 SHA256(相对路径)，非随机、可复算 | 已实施 |

> B 组按用户要求「只修提到的」：**B1、B2、B3、B4、B9、B18、B21、B23、B24 不修复**（用户裁决，记录保留、不删除）。
> FileBackupper 与 DiscArchive 的相关条目已按用户要求移出本报告，统一见「五、范围外（不处理）」。

---

## 一、严重问题

### A1 增量打包把修改过的文件当「已备份」静默跳过

- 位置：[PackingService.cs:113](../../ArchiveMaster.Module.WriteOnceArchive/Services/PackingService.cs#L113)、[:123](../../ArchiveMaster.Module.WriteOnceArchive/Services/PackingService.cs#L123)、[:367](../../ArchiveMaster.Module.WriteOnceArchive/Services/PackingService.cs#L367)；[SimpleFileInfo.cs:134](../../ArchiveMaster.Core/ViewModels/FileSystem/SimpleFileInfo.cs#L134)
- 判定：已核实
- 机制：哈希缓存的键是 `file.GetHashCode()`（对路径做 FNV-32），不校验长度与修改时间。同一路径的文件被修改后再次初始化，`hashCaches.TryGetValue` 直接返回旧 Hash；旧 Hash 已在 `package_info.wpk` 中，`packagedHashes.Add(hash)` 返回 false 后 `return`。
- 后果：新内容从未落盘，包信息却记着「新长度/新时间 + 旧 Hash」，重建时 Hash 不一致；界面显示完成。32 位路径哈希在 10 万文件量级还有约一次碰撞的期望值，碰撞到的文件同样被跳过。
- 修复方向（未实施）：缓存键改为 `(路径, 长度, 修改时间)`，或缓存值同时存长度/时间并比对；包信息写入前拒绝 Hash 复用。
- 处置（2026-10-24）：待修复。

### A2 回收站删除失败时静默降级为永久删除

- 位置：[FileHelper.cs:67](../../ArchiveMaster.Core/Helpers/FileHelper.cs#L67)
- 判定：已核实
- 机制：`TryDeleteToRecycleBin` 捕获异常后直接 `FileDeleteHelper.DirectlyDelete(path)`，仅写日志；`DeleteMode` 默认就是 `RecycleBinPrefer`（[GlobalConfigs.cs:18](../../ArchiveMaster.Core/Configs/GlobalConfigs.cs#L18)）。
- 后果：网络盘、回收站被禁用、配额不足时，用户按「可恢复」的预期清理文件，实际不可逆。
- 修复方向：回收站删除失败时不再降级为永久删除，只把该文件置为错误（单文件错误），其余文件继续。
- 处置（2026-10-24）：已裁决为「单文件错误」，不做永久删除降级。

### A3 SecurePassword 读/写失败都静默写空串，导致密码与 AI Key 永久丢失

- 位置：[SecurePassword.cs:121](../../ArchiveMaster.Core/Configs/SecurePassword.cs#L121)、[:86](../../ArchiveMaster.Core/Configs/SecurePassword.cs#L86)；[MasterPasswordViewModel.cs:38](../../ArchiveMaster.UI/ViewModels/MasterPasswordViewModel.cs#L38)、[:30](../../ArchiveMaster.UI/ViewModels/MasterPasswordViewModel.cs#L30)
- 判定：已核实
- 机制：写失败 `writer.WriteStringValue("")`；读失败只 `Log.Error`，对象仍是 `Remember=true`、`Password=null`。主密码依赖机器标识（`IdentityProvider.GetCombinedId`），换机器/重装后无法解密。
- 后果：此后任意一次保存（点初始化、改设置、退出）都会把全部密文改写成空串。主密码解析失败时还会先 `ClearAllPassword()`，没有二次确认；`ClearPasswords` 命令本身也没有确认。
- 修复方向：写入失败应抛出并阻断保存；读取失败应保留原密文（写回原字符串）而不是空串；清空密码需显式确认。
- 处置（2026-10-24）：待修复（写失败抛出、读失败保留原密文）；「清空密码需二次确认」不采纳，清空密码保持无需确认。

### A4 硬链接去重先删文件、再建链接，失败或取消即丢文件

- 位置：[LinkDeduplicationService.cs:26](../../ArchiveMaster.Module.FileTools/Services/LinkDeduplicationService.cs#L26)
- 判定：已核实
- 机制：`DeleteByConfig` 与 `HardLinkCreator.CreateHardLink` 之间无回滚。
- 后果：exFAT/FAT32/网络共享、无权限、文件被占用时删除已完成而建链抛异常；若 `DeleteMode` 为直接删除则不可恢复。
- 修复方向：先建临时硬链接再删原名，或失败时从回收站恢复。
- 处置（2026-10-24）：待修复，采用「先建临时硬链接、成功后再删原名」。

### A5 加密/解密直接写最终文件，半截密文会被下次「跳过」当成已完成

- 位置：[EncryptorService.cs:53](../../ArchiveMaster.Module.FileTools/Services/EncryptorService.cs#L53)、[:183](../../ArchiveMaster.Module.FileTools/Services/EncryptorService.cs#L183)
- 判定：已核实
- 机制：没有临时文件＋原子替换；取消、磁盘满、进程退出会留下不完整 `.$ept$`。下次执行 `FilenameDuplicationPolicy.Skip` 直接跳过。
- 后果：用户以为已加密，实际解密必然损坏；删除源文件选项开启时源文件已被删。
- 修复方向：写临时文件、成功后原子 `File.Move`；跳过前校验密文完整性（至少长度/尾部标记）。
- 处置（2026-10-24）：待修复（按上述方向）。

### A7 configs.json 保存非原子、异常被吞；导入校验形同虚设

- 位置：[AppConfig.cs:384](../../ArchiveMaster.Core/Configs/AppConfig.cs#L384)、[:402](../../ArchiveMaster.Core/Configs/AppConfig.cs#L402)、[:320](../../ArchiveMaster.Core/Configs/AppConfig.cs#L320)、[:244](../../ArchiveMaster.Core/Configs/AppConfig.cs#L244)
- 判定：已核实
- 机制：`File.WriteAllText` 直接覆盖，无临时文件、无锁；`SaveBackground` 用 `Task.Run` 包同步 Save 且不等待，初始化/执行前各触发一次。`LoadNextStart` 内 `Initialize()` 把所有异常吞成 `LoadError`，外层 try/catch 永不触发。`File.Replace` 在 `configs.json` 缺失时抛异常，导致整份配置都不加载。
- 后果：断电/磁盘满/被同步盘占用会留下半截 JSON，失败只进日志；坏配置照样被复制成 `.load` 并在下次启动顶替好配置；`.load` 存在而 `configs.json` 缺失时每次启动都失败。
- 修复方向：写临时文件＋`File.Replace`；保存失败向用户报错；导入前校验 `LoadError == null`。
- 处置（2026-10-24）：待修复（按上述方向）。

### A8 重建时把包信息里的相对路径直接拼进目标目录，可写出重建目录

- 位置：[RebuildService.cs:42](../../ArchiveMaster.Module.WriteOnceArchive/Services/RebuildService.cs#L42)
- 判定：已核实
- 机制：`RelativePath` 来自外部 `package_info.wpk`；`..\..\` 可穿越，绝对路径会让 `Path.Combine` 丢弃 `TargetDir`；`SkipIfExisted=false` 时先删同名既有文件。
- 后果：从他人介质或被篡改的包重建时，可写到重建目录之外并覆盖既有文件；CLI 的 PathGuard 只校验配置对象，不校验包内路径。
- 修复方向：校验 `RelativePath` 为相对路径且规范化后仍在 `TargetDir` 下。
- 处置（2026-10-24）：待修复（按上述方向）。本条是重建路径校验，与 DiscArchive 无关；DiscArchive 已整体移出（见第五节）。

### A9 校验发现 Hash 不一致后又调用 `Success()`，失败被覆盖成成功

- 位置：[VerifyService.cs:67](../../ArchiveMaster.Module.WriteOnceArchive/Services/VerifyService.cs#L67)
- 判定：已核实
- 机制：`file.Error(...)` 紧接 `file.Success()`。
- 后果：介质损坏时，「仅验证完整性」仍把状态置为成功，汇总数与 CLI 退出码按成功统计，只剩 `Message` 与 `ErrorHashNotMatched` 标志。
- 修复方向：`else` 分支调用 `Success()`。
- 处置（2026-10-24）：待修复（按上述方向）。

### A10 目录结构克隆：截断目标同名文件；0 字节源文件必然报错

- 位置：[DirStructureCloneService.cs:114](../../ArchiveMaster.Module.FileTools/Services/DirStructureCloneService.cs#L114)
- 判定：已核实
- 机制：`File.Create(newPath)` 直接清空目标已存在文件；源文件长度为 0 时 `SetLength(0)` 后 `Seek(-1, End)` 抛异常。
- 后果：目标非空时误清真实文件；0 字节文件在目标留下空壳并报错，形成半成品结构。
- 修复方向：写入前检查目标是否存在并按策略处理；对 0 长度文件跳过 `Seek`。
- 处置（2026-10-24）：待修复（按上述方向）。

### A11 批量重命名第二步失败后，文件永久停在 GUID 名

- 位置：[RenameService.cs:45](../../ArchiveMaster.Module.FileTools/Services/RenameService.cs#L45)
- 判定：已核实
- 机制：两阶段重命名无回滚；第二步逐文件执行，失败只把该文件标错。
- 后果：新名字含非法字符、路径超长、被占用、被取消时，文件仍叫 `3f2a…`，原名字只剩界面日志；再次初始化时被当作普通文件重新匹配。
- 修复方向：失败时把 `TempPath` 移回原名；或在第二步前统一校验目标名合法性。
- 处置（2026-10-24）：待修复，采用「第二步失败先把 `TempPath` 移回原名；移回也失败则忽略该错误并抛出原始异常」。

---

## 二、中等（功能与数据风险）

### B1 默认主密码是公开常量

- 位置：[GlobalConfigs.cs:8](../../ArchiveMaster.Core/Configs/GlobalConfigs.cs#L8)、[:33](../../ArchiveMaster.Core/Configs/GlobalConfigs.cs#L33)
- 判定：已核实
- 内容：`DefaultPassword = nameof(ArchiveMaster)`。用户不设主密码时，所有 SecurePassword 都用这枚公开常量加密，实质没有保护。
- 建议：首次启动强制设置主密码，或至少在界面标注「当前使用默认密码」。
- 处置（2026-10-24）：不修复（用户裁决，保留记录）。

### B2 密码只在 Copy 分支使用，切到 ISO/硬链接后产出明文且无提示

- 位置：[PackingService.cs:211](../../ArchiveMaster.Module.WriteOnceArchive/Services/PackingService.cs#L211)、[:264](../../ArchiveMaster.Module.WriteOnceArchive/Services/PackingService.cs#L264)、[:299](../../ArchiveMaster.Module.WriteOnceArchive/Services/PackingService.cs#L299)；[PackingConfig.cs:44](../../ArchiveMaster.Module.WriteOnceArchive/Configs/PackingConfig.cs#L44)
- 判定：已核实
- 内容：密码框仅在 `PackingType == Copy` 时显示，但切到 ISO/硬链接后 `Config.Password` 仍保留、`Check()` 不拦截，执行时不加密且无提示；CLI 可用 `--set` 组合出同样状态。
- 建议：切换类型时清空密码，或在 `Check()` 中拒绝「已设密码 + 非 Copy」。
- 处置（2026-10-24）：不修复（用户裁决，保留记录）。

### B3 ISO 临时文件泄漏；重跑只清理选中包，旧包残留

- 位置：[PackingService.cs:329](../../ArchiveMaster.Module.WriteOnceArchive/Services/PackingService.cs#L329)、[:169](../../ArchiveMaster.Module.WriteOnceArchive/Services/PackingService.cs#L169)
- 判定：已核实
- 内容：`Path.GetTempFileName()` 生成的包信息临时文件从不删除，内含完整文件清单；`ClearTargetPackageDir` 只遍历 `ExecutingPackages`，包数变少或只勾选部分包时旧目录/ISO 原样保留。
- 建议：`try/finally` 删除临时文件；清理按目录扫描而非按选中集合。
- 处置（2026-10-24）：不修复（用户裁决，保留记录）。

### B4 ffprobe 封装会死锁，且无超时、不看退出码

- 位置：[ExecutableDependencyHelper.cs:34](../../ArchiveMaster.Core/Helpers/ExecutableDependencyHelper.cs#L34)
- 判定：已核实
- 内容：先 `StandardOutput.ReadToEnd()` 再 `StandardError.ReadToEnd()`，stderr 写满管道即死锁；`WaitForExit()` 无超时；`ExitCode` 从未检查；调用方（TimeAss/VideoInfo）传不进 CancellationToken。
- 后果：损坏视频或网络盘上的文件会让工具永久卡在「正在处理」，取消按钮无效，ffprobe 子进程常驻。
- 建议：异步读取两个流并 `WaitForExitAsync(ct)`，超时杀进程，检查退出码。
- 处置（2026-10-24）：不修复（用户裁决，保留记录）。

### B5 照片瘦身不校验目标目录与源目录关系

- 位置：[PhotoSlimmingConfig.cs:160](../../ArchiveMaster.Module.PhotoTools/Configs/PhotoSlimmingConfig.cs#L160)；[PhotoSlimmingService.cs:45](../../ArchiveMaster.Module.PhotoTools/Services/PhotoSlimmingService.cs#L45)
- 判定：已核实
- 内容：`Check()` 只 `CheckEmpty(DistDir)`，不阻止 DistDir 等于或位于 SourceDir 内；勾选「处理前清空目标目录」且指向源目录时会删除源文件；目标在源内还会被下一轮递归处理，路径逐轮加一层。
- 建议：`Check()` 中拒绝「目标位于源内/等于源」，且 ClearAll 时二次确认。
- 处置（2026-10-24）：待修复。只在 `Check()` 中拒绝「目标等于/位于源目录内」，不做额外确认。

### B6 压缩直接写最终路径，取消/异常留残片；HEIC 编码期间取消不生效

- 位置：[PhotoSlimmingService.cs:162](../../ArchiveMaster.Module.PhotoTools/Services/PhotoSlimmingService.cs#L162)
- 判定：已核实
- 内容：`new FileStream(file.DistFile.Path, FileMode.Create)` 直接打开最终文件；HEIC 走 `heif_context_write_to_file`，ct 只在编码前检查一次。
- 后果：取消或编码失败留下 0 字节/半张图，且工具不清理；用户可能把残片当成品。
- 建议：写临时文件，成功后改名；编码循环内检查 ct。
- 处置（2026-10-24）：待修复（按上述方向）。

### B7 写 GPS 是原地重写原图，中途失败即毁图

- 位置：[ExifHelper.cs:132](../../ArchiveMaster.Module.PhotoTools/Helpers/ExifHelper.cs#L132)
- 判定：已核实
- 内容：`ImageFile.FromFile(filePath)` 后直接 `Save(filePath)`，无临时文件、无备份；`File.SetLastWriteTime` 在其后执行，失败即不一致。
- 建议：写临时副本再替换；或失败时从备份恢复。
- 处置（2026-10-24）：待修复（按上述方向）。

### B8 地理分类重跑时源=目标，`File.Move` 抛异常；同名照片冲突

- 位置：[PhotoGeoSorterService.cs:46](../../ArchiveMaster.Module.PhotoTools/Services/PhotoGeoSorterService.cs#L46)
- 判定：已核实
- 内容：递归枚举 `Config.Dir`，目标为 `Dir/Region/Name`；重跑时已归位文件的源路径与目标相同，`File.Move` 抛异常；不同子目录的同名照片归入同一区域也会冲突。
- 建议：同路径直接跳过；目标存在时按重名策略处理。
- 处置（2026-10-24）：待修复（按上述方向）。

### B9 GPX（UTC）与 EXIF（本地墙钟）未做时区归一

- 位置：[PhotoGeoTaggingService.cs:50](../../ArchiveMaster.Module.PhotoTools/Services/PhotoGeoTaggingService.cs#L50)、[:162](../../ArchiveMaster.Module.PhotoTools/Services/PhotoGeoTaggingService.cs#L162)
- 判定：已核实
- 内容：GPX 的 `Z` 时间解析为 `DateTimeKind.Utc`，与 EXIF 本地时间直接相减；默认偏移 0、容差 1 分钟。国内用户 8 小时差，结果全量「没有找到匹配的GPX位置」。
- 说明：界面提供手动时间偏移，属设计上的绕行手段，但默认值对非 UTC 用户不可用。
- 建议：按 GPX 时区或系统时区自动归一，偏移量作为额外修正。
- 处置（2026-10-24）：不修复（用户裁决，保留记录）。

### B10 TimeAss 帧间隔 ≤0 时死循环

- 位置：[TimeAssService.cs:41](../../ArchiveMaster.Module.VideoTools/Services/TimeAssService.cs#L41)；[TimeAssConfig.cs:41](../../ArchiveMaster.Module.VideoTools/Configs/TimeAssConfig.cs#L41)
- 判定：已核实
- 内容：`interval` 来自配置（默认 1000ms），`Check()` 不校验；为 0 或负数时 `nextTime` 不推进、循环不退出，`StringBuilder` 无限追加直到内存耗尽。
- 建议：`CheckRange(Interval, 1, …)`，循环内加步进保护。
- 处置（2026-10-24）：待修复（按上述方向）。

### B11 标签文件整文件覆盖写、无临时文件与互斥

- 位置：[TagFileHelper.cs:52](../../ArchiveMaster.Module.PhotoTagging/Helpers/TagFileHelper.cs#L52)
- 判定：写实现已核实；并发调用点来自子代理
- 内容：`File.WriteAllTextAsync` 直接覆盖；多线程 + 自动保存间隔时可能同时重写同一文件，轻则锁冲突，重则半截 JSON，下次初始化反序列化失败、历史标签一并不可用。
- 建议：写入串行化（SemaphoreSlim）＋临时文件替换。
- 处置（2026-10-24）：待修复（按上述方向）。

### B12 AI 每次调用新建 HttpClient

- 位置：[LlmCallerService.cs:112](../../ArchiveMaster.Core.AI/Services/LlmCallerService.cs#L112)；[BaseChatClient.cs:20](../../ArchiveMaster.Core.AI/Services/BaseChatClient.cs#L20)
- 判定：已核实
- 内容：`GetChatClient()` 每次 `new`，其构造函数每次 `new HttpClient`；`LlmCallerService` 未实现 IDisposable。逐图打标（并行最多 8）时句柄与 TIME_WAIT 持续累积。
- 建议：复用 `IHttpClientFactory` 或静态 HttpClient，ChatClient 按配置缓存。
- 处置（2026-10-24）：待修复（按上述方向）。

### B13 取消按钮存在竞态，可能永久卡在「正在取消」

- 位置：[TwoStepViewModelBase.cs:419](../../ArchiveMaster.Core/ViewModels/TwoStepViewModelBase.cs#L419)
- 判定：已核实（逻辑）
- 内容：`Status = Cancelling`、遮罩显示后，只有 `InitializeCommand.IsRunning`/`ExecuteCommand.IsRunning` 才真正取消；若点击瞬间两者都已结束，既不取消也没有 finally 收尾，状态与遮罩永久停留，四个按钮全禁用。
- 建议：先检查命令是否在跑，再改状态；或在 finally 统一复位。
- 处置（2026-10-24）：待修复，先判断两个命令是否在跑，再改状态与遮罩。

### B14 进度定时器 fire-and-forget，异常路径永不取消

- 位置：[TwoStepViewModelBase.cs:207](../../ArchiveMaster.Core/ViewModels/TwoStepViewModelBase.cs#L207)
- 判定：已核实
- 内容：`_ = StartMessageAndProgressTimer(cts.Token)` 之后才 `await func(...)`；`func` 抛非 OCE 异常时 `CancelAsync` 不执行，50ms 的 `PeriodicTimer` 永久运行并持有 VM。
- 建议：`try/finally` 取消并等待定时器；或改用可取消的循环。
- 处置（2026-10-24）：待修复，`try/finally` 取消并等待定时器结束。

### B18 冲突策略选「报错」实际只警告并跳过

- 位置：[EncryptorService.cs:187](../../ArchiveMaster.Module.FileTools/Services/EncryptorService.cs#L187)
- 判定：已核实
- 内容：`case FilenameDuplicationPolicy.Throw: file.Warn("目标文件已存在"); return false;` 不抛异常、不置错误状态。
- 建议：抛异常或至少置 Error，并在界面说明。
- 处置（2026-10-24）：不修复（用户裁决，保留记录）。

### B19 编码转换「另存目录」丢失子目录层级，同名互相覆盖

- 位置：[EncodingConverterService.cs:38](../../ArchiveMaster.Module.TextTools/Services/EncodingConverterService.cs#L38)
- 判定：已核实
- 内容：`Path.Combine(Config.AnotherDir, file.Name)`；`a\config.txt` 与 `b\config.txt` 都写到 `另存目录\config.txt`，后者静默覆盖，状态仍显示成功。
- 建议：保留相对路径，或目标存在时按重名策略处理。
- 处置（2026-10-24）：待修复，保留源相对路径层级。

### B20 正则缓存只按模式字符串缓存，忽略「忽略大小写」

- 位置：[RenameService.cs:23](../../ArchiveMaster.Module.FileTools/Services/RenameService.cs#L23)、[:125](../../ArchiveMaster.Module.FileTools/Services/RenameService.cs#L125)
- 判定：已核实
- 内容：静态 `Dictionary<string, Regex>` 以 pattern 为键，`regexOptions` 不参与；同进程内先忽略大小写、再取消勾选，仍用旧实例；字典非线程安全。
- 建议：键改为 `(pattern, options)`，或改 `ConcurrentDictionary`。
- 处置（2026-10-24）：待修复，缓存键改为 `(pattern, options)`。

### B21 配置键＝类名：类改名即静默丢预设，且不可恢复

- 位置：[AppConfig.cs:522](../../ArchiveMaster.Core/Configs/AppConfig.cs#L522)、[:388](../../ArchiveMaster.Core/Configs/AppConfig.cs#L388)；[AppLifetime.cs:17](../../ArchiveMaster.UI/AppLifetime.cs#L17)
- 判定：已核实
- 内容：加载只遍历已注册键，未匹配的 JSON 项既不保留也不报错（`LoadError` 仍为 null）；退出时 `AppLifetime.StopAsync` 调 `config.Save()` 全量重写，旧预设被彻底抹掉。仓库只有极旧格式的迁移逻辑。
- 建议：加载时检测未注册键→备份原文件并提示；关键类改名提供迁移表。
- 处置（2026-10-24）：不修复（用户裁决，保留记录）。

### B22 当前预设名从不校验存在性，删除/改名后留下「幽灵预设」

- 位置：[AppConfig.cs:101](../../ArchiveMaster.Core/Configs/AppConfig.cs#L101)；[MultiPresetViewModelBase.cs:152](../../ArchiveMaster.Core/ViewModels/MultiPresetViewModelBase.cs#L152)
- 判定：已核实（逻辑）
- 内容：`GetCurrentPreset` 直接返回字典里的值；`PresetNames` 不含它时下拉空白，随后 `OnPresetNameChanged` 用该名字 `GetOrCreateConfig` 新建默认配置并落盘。
- 建议：读取时校验是否在预设列表内，否则回退默认并修正字典。
- 处置（2026-10-24）：待修复（按上述方向）。

### B23 PathGuard 不解析符号链接/junction，白名单可被绕过

- 位置：[PathGuard.cs:97](../../ArchiveMaster.Cli/Infrastructure/PathGuard.cs#L97)
- 判定：已核实
- 内容：`Path.GetFullPath` 后直接做 `StartsWith` 判断，无 `ResolveLinkTarget`。允许根内存在指向根外的链接时，`…\link\..\secret` 字面化后通过校验，实际 IO 落到根外。
- 说明：类注释已声明这是「尽力而为的护栏，不是安全沙箱」，此处按边界问题记录，非谎报。
- 处置（2026-10-24）：不修复（用户裁决，保留记录）。

### B24 占位符替换结果不做文件名合法性/分隔符校验

- 位置：[FilePlaceholderReplacer.cs](../../ArchiveMaster.Core/Helpers/FilePlaceholderReplacer.cs)（子代理定位，未给行号）
- 判定：未复核
- 内容：`<Path>`/`<RelPath>` 可把 `\`、`..`、`:` 带进新文件名，`Path.Combine` 遇到绝对路径会丢弃基路径，文件被移到目录之外或重命名抛异常（进而落入 A11 的 GUID 残留）。
- 建议：替换后校验非法字符与分隔符，拒绝绝对路径。
- 处置（2026-10-24）：不修复（用户裁决，保留记录）。

---

## 三、代码质量与架构

### Q1 HostServices 两个按类型取服务的方法实现错误（潜伏）

- 位置：[HostServices.cs:21](../../ArchiveMaster.Core/HostServices.cs#L21)、[:11](../../ArchiveMaster.Core/HostServices.cs#L11)
- 判定：已核实
- 内容：`GetRequiredService(Type)` 忽略参数、永远返回 `IHostedService`；`GetBackgroundServices()` 查 `IBackgroundService`，而注册用的是 `AddSingleton(typeof(IHostedService), …)`。目前无调用点（`Initializer.GetBackgroundServices` 走的是 `GetServices<IHostedService>().OfType<…>`）。
- 建议：修正实现或删除这两个方法。

### Q2 ToolCatalog.HasPrepareStage 依赖一个会失效的假设

- 位置：[ToolCatalog.cs:114](../../ArchiveMaster.Core/Cli/ToolCatalog.cs#L114)
- 判定：已核实
- 内容：靠「getter 是否声明在 `TwoStepViewModelBase<,>` 上」推断有无准备阶段，只在所有覆写都返回 `false` 时成立；现有 4 处覆写确实都是 `=> false`，但将来有人改成 `=> true` 时 CLI 会静默当作无准备阶段。
- 建议：改为反射读取实例值，或显式声明常驻开关。

### Q3 ServiceBase 的防抖逻辑当前未接线

- 位置：[ServiceBase.cs:49](../../ArchiveMaster.Core/Services/ServiceBase.cs#L49)、[:61](../../ArchiveMaster.Core/Services/ServiceBase.cs#L61)、[:114](../../ArchiveMaster.Core/Services/ServiceBase.cs#L114)
- 判定：已核实
- 内容：`pendingMessage` 从未赋值、`debounceTimer` 建好后从未 `Change()` 启动，`SendPendingMessage` 不可达；真正的 50ms 合并在 `TwoStepViewModelBase` 里。
- 说明：这套防抖原本是为 `NotifyMessage` 的高频调用准备的（把连续消息合并成约 100ms 一次）。用户说明是有意保留、后续可能启用，不作为缺陷清理。
- 处置（2026-10-24）：保留（不删除）。

### Q6 日志无保留上限

- 位置：[Program.cs:44](../../ArchiveMaster.UI.Desktop/Program.cs#L44)
- 判定：已核实
- 内容：按天滚动，但没有 `retainedFileCountLimit`/`fileSizeLimitBytes`，`logs/` 长期无界增长。
- 建议：加保留天数与单文件上限。

### Q7 十六进制明文可能被误判为密文

- 位置：[SecurePassword.cs:133](../../ArchiveMaster.Core/Configs/SecurePassword.cs#L133)
- 判定：已核实
- 内容：`^[0-9a-fA-F]{64,}$` 即当密文解密；64 位以上十六进制明文会解密失败，触发 A3 的空串路径。
- 建议：增加长度/结构校验（现有格式自带长度字段），或对解密失败保留原文。

---

## 四、文档、发布与测试

### D1 README 与模块帮助漏掉已注册的「视频信息提取」

- 位置：[README.md:161](../../README.md#L161)；[helps/video.md](../../helps/video.md)；[VideoToolsModuleInfo.cs:57](../../ArchiveMaster.Module.VideoTools/VideoToolsModuleInfo.cs#L57)
- 判定：已核实
- 内容：模块注册了 `VideoInfoPanel`「视频信息提取」，README 视频表与 helps 全文都只有时间戳字幕。用户与 AI 都无从发现该工具，也违反 AGENTS「新增工具后同步更新 README 工具表格、helps/<模块>.md」。

### D2 只提交了 win-x64 原生库，Linux/macOS 发布静默丢 HEIC

- 位置：[ArchiveMaster.UI.Desktop.csproj:58](../../ArchiveMaster.UI.Desktop/ArchiveMaster.UI.Desktop.csproj#L58)；[build.ps1:41](../../build.ps1#L41)
- 判定：已核实
- 内容：csproj 为 linux-x64/arm64、osx-x64/arm64 声明了 Content 通配，仓库 `native/` 实际只有 win-x64 三个 dll；`build.ps1 -l/-m/-all` 会成功产出但完全不含 libheif/x265，HEIC 输出只能运行期报错。

### D3 安装器与 DEB 版本未随 AppVersion 更新

- 位置：[installer.nsi:6](../../build_scripts/win/installer.nsi#L6)、[:53](../../build_scripts/win/installer.nsi#L53)；[control:2](../../build_scripts/linux/control#L2)
- 判定：已核实
- 内容：`ClientVersion 1.0.0`、`ArchiveMasterSetup_1.0.0_*.exe`、卸载项 `DisplayVersion`、deb `Version: 1.0.0`；`Directory.Build.props` 的 `AppVersion` 已是 2.9.1。

### D4 Android versionCode 固定为 2

- 位置：[ArchiveMaster.UI.Android.csproj:7](../../ArchiveMaster.UI.Android/ArchiveMaster.UI.Android.csproj#L7)
- 判定：已核实
- 内容：`ApplicationVersion` 写死 2，显示版本走 `$(AppVersion)` 2.9.1；同机升级/侧载可能被判为同版本。
- 说明：versionCode 是整数且必须单调递增，不能直接放 2.9.1；可把 `AppVersion` 映射成 `major*10000 + minor*100 + patch`（2.9.1 → 20901），或单独维护一个构建号。
- 处置（2026-10-24）：不处理（用户裁决：不管了）。

### D5 帮助称 AES 默认 ECB，代码默认 CBC

- 位置：[helps/files.md:14](../../helps/files.md#L14)；[EncryptorConfig.cs:13](../../ArchiveMaster.Module.FileTools/Configs/EncryptorConfig.cs#L13)
- 判定：已核实
- 内容：文档「默认值位ECB」，字段是 `CipherMode.CBC`；文档里 ECB 安全性表格因此指错默认项。
- 处置（2026-10-24）：待修复（文档改为 CBC）。

### D6 重复文件清理帮助里的「删除文件位置」配置不存在

- 位置：[helps/files.md:223](../../helps/files.md#L223)、[:230](../../helps/files.md#L230)；[DuplicateFileCleanupConfig.cs:5](../../ArchiveMaster.Module.FileTools/Configs/DuplicateFileCleanupConfig.cs#L5)
- 判定：已核实
- 内容：文档列「删除文件位置：指定删除文件的存放位置」与「移动到指定回收站目录」，配置类没有该字段；实际删除走全局 `DeleteMode`（可被改成直接删除）。
- 处置（2026-10-24）：待修复（删除该项，改述为按全局删除方式处理）。

### D7 「加密目录结构」的安全承诺高于实现

- 位置：[helps/files.md:19](../../helps/files.md#L19)；[EncryptorService.cs:147](../../ArchiveMaster.Module.FileTools/Services/EncryptorService.cs#L147)
- 判定：已核实
- 内容：文档称文件名「通过 SHA256 哈希替换为随机字符串……即使文件被窃取也无法直接获取有效信息」；实现是确定性 `SHA256(RelativePath)`，无盐、无需密码即可复算，可用候选相对路径反查某文件是否存在。
- 处置（2026-10-24）：待修复（文档改为准确描述，去掉「随机/无法推断」的表述）。

### D8 帮助声明密码最长 32 位，校验只查空与密钥长度

- 位置：[helps/files.md:12](../../helps/files.md#L12)；[EncryptorConfig.cs:62](../../ArchiveMaster.Module.FileTools/Configs/EncryptorConfig.cs#L62)
- 判定：已核实
- 内容：`Check()` 无长度上限校验；实际约束取决于 FzLib 的派生实现，文档与实现脱节。

### D9 两个测试用例鉴别力不足

- 位置：[DuplicateFileCleanupServiceTests.cs:25](../../ArchiveMaster.Tests/FileTools/DuplicateFileCleanupServiceTests.cs#L25)；[DirStructureCloneServiceTests.cs:85](../../ArchiveMaster.Tests/FileTools/DirStructureCloneServiceTests.cs#L85)
- 判定：子代理（未复核）
- 内容：重复文件清理唯一用例先 `IsChecked = false`，只断言文件还在，从不测「勾选后删除」；flatten 用例只 `Assert.Single`，不断言路径与命名。两个最需要回归的破坏性路径都没有正向覆盖。

---

## 五、范围外（不处理）

按用户 2026-10-24 裁决，以下两个范围整体移出本报告，不作为本轮修复对象。原条目已删除，编号不回收、后续不复用。

| 范围 | 原条目 | 处置 | 依据 |
| --- | --- | --- | --- |
| `ArchiveMaster.Module.DiscArchive` | Q4 | 不处理 | 已废弃：不在 `ArchiveMaster.sln`，也不被 `ArchiveMaster.Modules.csproj` 引用；功能由 WriteOnceArchive 取代，仅剩 `helps/disc.md` 遗留文档 |
| `ArchiveMaster.Module.FileBackupper`（备份服务） | A6、B15、B16、B17、Q5 | 不处理 | 用户裁决本轮不动备份服务；备份失败被吞、跨线程日志、DbService 生命周期等风险不再列入本报告，需另起专项 |

> 删除的是条目，不是问题本身：这两个范围的实际风险仍然存在，只是不在本轮范围。

## 六、已被核销的疑点

- **206 条测试属实**：静态统计 157 个 `[Fact]` + 12 个 `[Theory]`（49 条 `InlineData`），合计 206 个用例，与 AGENTS.md 的「159 既有 + 47 覆盖」口径吻合。
- `SecurePasswordStoreService` 的 AES-GCM 参数（16 字节随机 salt、12 字节随机 nonce、16 字节 tag、PBKDF2-SHA256 20 万次）未发现问题；正常路径不落明文。
- `--set` 仅限 plan/run、先应用覆盖再校验白名单与准备阶段，顺序符合 AGENTS.md；`ConfigOverrides` 的类型转换与嵌套字段处理未发现缺陷。
- 模块注册链完整；`ConfigMetadata.Group` 与 ViewModel 组名对齐；DI 无单例持有 UI 对象。
- 除 D1 外，其余六个模块的 README 工具表与 `Views.Panels` 一一对应；8 个 `HelpFileName` 都指向存在的文件。
- `ToolCatalog.PrepareUnsafeToolIds` / `NoItemResultToolIds` 与当前工具行为一致（抽查范围内）。
- PathGuard 的定位本来就是「尽力而为的护栏」而非沙箱，B23 是边界说明而非谎报。

## 七、覆盖范围与未覆盖项

| 域 | 覆盖 | 备注 |
| --- | --- | --- |
| 安全与密钥、WriteOnceArchive | 深 | 子代理 + 主代理复核 |
| 文件工具、文本工具 | 深 | 子代理 + 主代理复核 |
| 照片、视频、标签 | 深 | 子代理 + 主代理复核 |
| 并发与生命周期 | 深 | 子代理 + 主代理复核（FileBackupper 部分已移出） |
| 配置与模块注册 | 深 | 子代理 + 主代理复核 |
| 文档、发布、测试 | 中 | 帮助文档只逐项比对了 files.md、video.md |
| CLI 契约与执行路径 | 中 | ToolCatalog、`--set`、PathGuard、Program、ToolCommand 已读；HeadlessToolRunner、JsonOutput、DocsCommand 未细读 |
| UI 外壳与绑定 | 浅 | 只读了 App、Initializer、MainWindow、Setting、MasterPassword；各 Panel.axaml 与模块 ViewModel 未逐行 |
| `ArchiveMaster.Module.FileBackupper` | 范围外 | 按用户裁决不处理，见第五节 |
| `ArchiveMaster.Module.DiscArchive` | 范围外 | 已废弃，按用户裁决不处理，见第五节 |

未覆盖：

- TextTools 的 TypoChecker / SmartDocSearch / TextEncryption / TextRewriter；
- FileTools 多数 ViewModel 与 Config；
- `tools/heic-verify/`；
- 其余 6 个 helps 的逐项选项比对；
- FzLib 外部库实现（`Aes.EncryptFileAsync`、`FileHashHelper` 等）；
- 未构建、未跑单测、未启 GUI/CLI，所有结论均为静态阅读。

## 八、本轮计划修复的顺序（尚未实施）

按用户裁决，本轮实际要动的条目如下；B 组未列出的、以及 Q/D 组未裁决的条目本轮不动。

1. **止血**：A3、A2、A5、A7、A1。
2. **会毁数据的失败路径**：A4、A9、A10、A11、B6、B7。
3. **后台/取消**：B13、B14。
4. **其余已裁决项**：B5、B8、B10、B11、B12、B19、B20、B22。
5. **文档**：D5、D6、D7（都在 `helps/files.md`）。
6. **待裁决**：D1–D3、D8、D9、Q1、Q2、Q6、Q7。
7. **不修/保留**：B1、B2、B3、B4、B9、B18、B21、B23、B24；D4（不处理）；Q3（有意保留）。

## 九、实施记录（2026-10-24）

已实施 25 条，涉及 24 个源文件与 1 个帮助文档。构建 `dotnet build ArchiveMaster.UI.Desktop` 通过（0 警告 0 错误）。

| 条目 | 落地位置 |
| --- | --- |
| A1 | `PackingService`：缓存改为「相对路径 + 长度 + 修改时间」四段格式，旧格式行直接忽略 |
| A2 | `FileHelper`：回收站失败不再降级永久删除，改为抛出、由文件循环记为该文件错误 |
| A3 | `SecurePassword`：写失败抛出阻断保存；读失败保留原密文并在保存时写回 |
| A4 | `LinkDeduplicationService`：先建临时硬链接→删原名→改名，失败清理临时链接 |
| A5（部分） | `EncryptorService`：临时文件 + `File.Move(..., true)`；未做 Skip 前的密文完整性校验 |
| A7 | `AppConfig`：临时文件保存、失败抛出、`.load` 用备份拷贝 + 改名；`AppLifetime`/`MasterPasswordViewModel`/`SettingViewModel` 处理保存异常 |
| A8 | `RebuildService`：拒绝绝对路径与规范化后越出 `TargetDir` 的 `RelativePath` |
| A9 | `VerifyService`：Hash 一致才 `Success()` |
| A10 | `DirStructureCloneService`：目标已存在直接报错；0 字节文件跳过 `Seek` |
| A11 | `RenameService`：第二步失败把 `TempPath` 移回原名，回滚失败则忽略并抛原始异常 |
| B5 | `PhotoSlimmingConfig.Check()`：拒绝目标等于/位于源目录内 |
| B6（部分） | `PhotoSlimmingService`：压缩写临时文件后改名；编码前检查 ct（编码内部仍不响应取消） |
| B7 | `ExifHelper`：写同扩展名临时文件再替换 |
| B8 | `PhotoGeoSorterService`：同路径跳过，目标重名自动加序号 |
| B10 | `TimeAssConfig.Check()` 校验间隔；`TimeAssService` 循环前兜底 |
| B11 | `TagFileHelper`：静态互斥 + 临时文件替换 |
| B12 | `LlmCallerService`：静态缓存 ChatClient（键含类型/Url/Model/Key 哈希） |
| B13/B14 | `TwoStepViewModelBase`：取消前先确认命令在跑；定时器 `try/finally` 取消并等待 |
| B19 | `EncodingConverterService`：按 `RelativePath` 保留层级并建目录 |
| B20 | `RenameService`：正则缓存键改为 `(pattern, options)` 并加锁 |
| B22 | `AppConfig.GetCurrentPreset`：失效预设回退首项并修正字典 |
| D5–D7 | `helps/files.md`：默认算法 CBC、删除方式按全局设置、加密目录结构改为准确描述 |

与报告建议的差异：

1. **A7 未用 `File.Replace`**：实测在测试环境（及部分同步盘）下会「拒绝访问」，改用「拷贝旧配置到 `.bak` → `File.Move(tmp, path, true)`」。备份语义保留，原子性略弱于 Replace。
2. **A10 没有重名策略可依**：`DirStructureCloneConfig` 无覆盖选项，目标已存在时直接报错，避免清空真实文件。
3. **A5 的完整性校验未做**：密文格式由 FzLib 生成，尾部没有可校验标记，强行加校验会误伤旧文件。
4. **B6 的 HEIC 取消**：`HeifEncoder` 内部是原生调用，未逐帧接入 ct，只在编码前检查。
5. **B12 未引入 `IHttpClientFactory`**：用静态字典缓存 ChatClient，改动面更小。

验证记录：

- `dotnet build ArchiveMaster.UI.Desktop`：通过，0 警告 0 错误。
- 受影响测试类（AppConfigTests、ServiceBaseTests、RenameServiceTests、TextToolsTests、PhotoGeoAndSlimmingTests、ExifAndMetadataTests、PhotoTaggingTests、VideoToolsTests、HelperTests、WriteOnceArchiveTests）：全部通过。
- `dotnet test` 在本机沙箱下无法启动 testhost（访问父进程被拒），改用 xUnit v3 in-process runner 直接运行测试 dll；这是环境限制，不是用例失败。
- **未跑全量测试**（需单独授权）、未做 GUI/CLI 端到端。

## 十、复核方式与后续更新

- 子代理分域：安全与密钥、并发与生命周期、文件/文本工具、照片/视频/标签、CLI 契约、配置与注册、UI 与 XAML、文档/测试/发布、AI 集成（最后一次因并行上限未派出）。
- 结论均附文件路径与行号，便于逐条复核；行号以 `b781a7f` 为准，后续改动可能漂移。
- 更新方式：新一轮复核请在同一目录下按日期新建文件，并在 `index.md` 增加条目；不要直接改写本文件的历史结论。

## 十一、复核与再确认（第 2 轮）

两个只读子代理分域审查本批 diff（其一：Core/FileTools/WriteOnce/UI；其二：PhotoTools/TextTools/VideoTools/PhotoTagging/helps）。共提出 16 条，**14 条已修复并由复核者逐条确认「已修复」，2 条判定不属实且复核者「接受」**，最终无「仍存在」项。

第 2 轮修复要点：

1. 密码清空与“解密失败保留密文”用独立标志区分，用户主动清空仍写空串；
2. `CancellationTokenRegistration` 显式释放，避免对已释放 CTS 调 `Cancel`；
3. 加密元数据先写临时文件再替换，catch 同时清理两个临时文件；
4. ChatClient 缓存键加入配置对象标识，配置替换后不再命中旧实例；
5. 初始化/执行前的保存改为 try 内同步 `Save()`，失败可见；随之删除已无调用方的 `SaveBackground`；
6. 导入配置无论成败都还原内存中的 `GlobalConfigs`，保持“下次启动生效”；
7. 重命名回滚也失败时，异常消息带临时路径与两个错误；
8. 编码转换拒绝“另存目录与源目录相等或互相包含”（覆盖截断源文件的路径）；
9. 照片瘦身拒绝源/目标互相包含（含 `ClearAllBeforeRunning` 删源场景）；
10. Windows/macOS 的路径比较统一按不区分大小写处理；
11. 地理分类“已在目标位置”分支显式 `Success()`，不再停在“处理中”；
12. Exif 临时文件在使用前清理同名残留；
13. 帮助文档改为“哈希作文件名、平铺存放”，与实现一致；
14. 全部改动的 `.cs` 恢复 UTF-8 BOM。

判为不属实并接受的两条：回收站失败不降级（A2 用户裁决的直接代价，Linux 无回收站时删除不可用）；旧两列哈希缓存不再兼容（缺少长度/时间，无法安全复用）。

留档边界：Exif 临时文件在进程被强杀后仍可能残留；符号链接/联接点可绕开目录重叠的字面比较；大小写敏感的 APFS 卷上会过度拒绝；ChatClient 旧实例保留到进程结束；加密元数据替换失败时密文完整但元数据缺失（记为错误态）。

## 十二、最终验证（2026-10-24）

用户选择 A + B + C 三种方式。

**A · 全量单元测试**：通过。用 xUnit v3 in-process runner 直接运行测试 dll（沙箱下 `dotnet test` 的 testhost 无法启动），结果 `total=206, failed=0, skipped=0`。

**B · 端到端 CLI 验证**：通过。构建 `am` 后在工作区临时目录真实调用：

| 场景 | 结果 |
| --- | --- |
| encryptor：2 个文件加密 → 删除源 → 解密 | 两次均 2/2 Success；解密后目录层级与内容逐字节一致；解密目录未残留 `.$eptm$` |
| encryptor：缺密码 | 计划阶段 exit 2，提示「密码为空」 |
| packing：打包 → 修改文件 → 重打包 | 两次均 exit 0；重打包产出新 Hash |
| verify：对重打包目录校验 | 2/2 Success（证明 A1 的缓存失效修复生效，旧行为会 Hash 不一致） |
| rebuild：从包信息重建 | 2/2 Success，内容与修改后的源一致 |
| rebuild：篡改 `RelativePath=..\..\evil.txt` | 该文件 Error「包内路径不合法（超出目标目录）」、exit 1；目标目录外未生成文件（A8 生效） |
| encodingConverter：GBK → UTF-8 另存 | Success，输出为 UTF-8(BOM)，内容一致 |
| encodingConverter：另存目录 = 源目录 | exit 2，提示「另存目录不能与源目录相同，也不能与源目录互相包含」（新增校验） |

**C · 发布测试**：构建与产物检查通过；运行调用按用户裁决不做（「只需要构建」）。
`dotnet publish -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true` 成功；单文件 `ArchiveMaster.UI.Desktop.exe` 约 70 MB，`FileVersion/ProductVersion=2.9.1`，`LICENSE.txt`、`THIRD-PARTY-NOTICES.txt`、libheif/libde265/libx265 等原生库齐备。运行阶段因本机已有用户正在运行的 ArchiveMaster 实例（单实例机制会直接退出），且不应干扰用户进程，用户确认只做构建验证，未执行发布版运行调用。

未覆盖：GUI 手动走查、HEIC 实机编码、真实 LLM、ffprobe 外部进程（均属项目测试边界）。验证用的临时目录已清理。

## 变更记录

| 日期 | 变更 |
| --- | --- |
| 2026-10-24 | 首轮全仓复核落盘；51 条，未修复 |
| 2026-10-24 | 记录 A1–A8 处置（A2 按单文件错误）；核对 DiscArchive 已废弃；未改代码 |
| 2026-10-24 | 按用户要求把 DiscArchive 与 FileBackupper 相关条目移出（Q4、A6、B15–B17、Q5），新增「范围外（不处理）」一节；总数 51→45 |
| 2026-10-24 | A8、A9 确认按推荐修复；未改代码 |
| 2026-10-24 | A10、A11 与 B5–B8、B10–B14、B19、B20、B22 确认修复；B 组其余不修复；未改代码 |
| 2026-10-24 | B5 限定为「仅 `Check()` 拒绝对源/目标关系」；D5–D7 确认修复、D4 待定；Q3 补充原意说明；未改代码 |
| 2026-10-24 | Q3 按用户说明保留（有意保留、后续可能启用）；D4 不处理；未改代码 |
| 2026-10-24 | 按裁决实施 25 条（A5、B6 部分实施），涉及 24 个源文件与 helps/files.md；构建通过，受影响测试类全绿；新增「实施记录」 |
| 2026-10-24 | 第 2 轮独立复核：16 条中 14 条修复并确认、2 条不属实被接受；恢复 BOM、删除 SaveBackground；构建与受影响测试通过 |
| 2026-10-24 | 最终验证：A 全量 206/206；B CLI 端到端全部通过；C 构建与产物通过（发布版运行按用户裁决只做构建） |
