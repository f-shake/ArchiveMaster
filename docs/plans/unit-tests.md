# ArchiveMaster 单元测试计划

状态：2A 已执行（测试 159 条，150 通过、9 条按预期因产品缺陷失败），已进入阶段3 独立复核；BUG 待后续 2B 修复
目标：为除备份（`FileBackupper`）与真实 AI 调用之外的模块补上一套可重复、可维护的单元测试，作为重构与回归的基线。

已确认的四个决策：

1. 覆盖深度取“纯逻辑 + 临时目录文件工作流”，跳过 HEIC/ffprobe/UI/网络等外部依赖。
2. “AI 聊天类”按“是否真实调用 LLM”划分：标签管理、标签搜索等外围逻辑照测，生成类工具跳过。
3. 断言按**正确行为**书写，不按当前实现迁就。
4. 阶段二拆成 2A/2B：2A 只写测试、不修产品代码，确认的 bug 保留为失败用例并附清单；2B 修复 bug，最终全绿。

## 1. 功能需求

- R1 新增一个单元测试工程 `ArchiveMaster.Tests`，可通过 `dotnet test ArchiveMaster.Tests` 运行。
- R2 覆盖以下项目的非 UI 逻辑：
  - `ArchiveMaster.Core`
  - `ArchiveMaster.Module.FileTools`
  - `ArchiveMaster.Module.PhotoTools`（不依赖 HEIC 原生编码的部分）
  - `ArchiveMaster.Module.PhotoTagging`
  - `ArchiveMaster.Module.VideoTools`
  - `ArchiveMaster.Module.TextTools`（不依赖 LLM 的部分）
  - `ArchiveMaster.Module.OfflineSync`
  - `ArchiveMaster.Module.WriteOnceArchive`
- R3 明确排除：
  - `ArchiveMaster.Module.FileBackupper`（备份模块，整体不测）；
  - `ArchiveMaster.Core.AI` 的聊天/LLM 调用链；
  - 依赖 LLM 的工具：文本智能生成、错别字检查、文档智能搜索、逐行智能转换、照片标签生成；
  - 所有 View、AXAML、依赖 `Dispatcher`/`Bitmap` 的 UI 逻辑；
  - 依赖外部程序的逻辑（`ffprobe`、批量命令行、任意用户命令）；
  - 依赖随包 libheif 原生库的 HEIC 真编码。
- R4 每个用例使用独立临时目录，不读写仓库内文件；测试结束清理，不向用户目录写入业务文件。
- R5 测试不依赖网络、不调用真实 LLM、不依赖用户 `configs.json`、不修改系统回收站。
- R6 同一套测试在 Windows 上连续运行两次结果一致；不因执行顺序不同失败。
- R7 测试工程加入解决方案，并在 `AGENTS.md`、`README.md` 写明运行方式与覆盖边界。
- R8 验收标准：
  - 2A 结束时测试工程构建 0 错误；允许因确认的 bug 出现失败用例，失败原因必须是产品缺陷而不是测试写错；
  - 2B 结束时 `dotnet test` 全部通过；
  - 全程不新增产品代码警告。

## 2. 技术方案

### 2.1 选型

- 测试框架：**xUnit v3**（`xunit.v3` 3.1.0）。
- 测试运行器：`Microsoft.NET.Test.Sdk` 17.14.1 + `xunit.runner.visualstudio` 3.1.4。
- 目标框架：`net10.0`，与业务工程一致。
- 断言：xUnit 内置 `Assert`，不引入 FluentAssertions。
- Mock：不引入 Mock 框架。需要隔离的只有文件系统与时钟，使用临时目录和固定时间值；LLM 相关工具不在范围内。
- 图片素材：用 `Magick.NET`（Core 已传递引用）在测试运行时生成 JPEG/PNG；用 `ExifLibNet` 写 GPS/时间。
- 压缩包素材：`System.IO.Compression` 直接生成。
- 文本素材：测试代码内联字符串，不提交二进制 fixture。

### 2.2 为什么不用其他方案

- **MSTest**：可用，但 `[Theory]`/数据驱动写法不如 xUnit 简洁，社区示例少。
- **NUnit**：能力足够，但仓库没有历史包袱，没必要引入第二套断言风格。
- **xUnit v2**：仍可用，但 v3 已在本机 NuGet 缓存中，且是当前 .NET 10 模板的默认方向；v3 的隔离与诊断更好。
- **每个模块一个测试工程**：隔离更彻底，但 8 个工程的构建与包管理成本明显高于收益；单工程按目录分组已足够。
- **Avalonia.Headless**：只有测 View/ViewModel 才需要；本计划明确不测 UI，因此不引入。

### 2.3 全局状态与并行

`GlobalConfigs.Instance`、`Log.Logger`、`AppConfig` 的静态 JSON 选项、`RenameService` 的正则/脚本缓存都是进程级状态。测试程序集统一**关闭并行**，并用 `GlobalStateScope` 恢复 `GlobalConfigs`/`Log.Logger`；正则与脚本缓存不做逐项清理，靠串行执行和每个用例使用的独立模式避免交叉。

```csharp
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

这样不需要为每个测试类写复杂的隔离集合；代价是总耗时增加，但本套用例以本地文件操作为主，预计仍在 1~2 分钟内。

## 3. 系统结构

```
ArchiveMaster.Tests
├─ Infrastructure/       临时目录、全局状态、素材构造、测试基类
├─ Core/                 Core 的纯逻辑与配置
├─ FileTools/            FileTools 服务与配置
├─ PhotoTools/           EXIF/GPX/照片瘦身分类等
├─ PhotoTagging/         标签模型、标签文件、搜索
├─ VideoTools/           字幕生成、ffprobe JSON 解析辅助
├─ TextTools/            文本加密、编码转换、文本源
├─ OfflineSync/          快照、差异、补丁
└─ WriteOnceArchive/     包信息、打包、校验、重建
```

测试工程直接引用 Core 与 7 个非备份模块工程；不引用 `ArchiveMaster.UI*`、`ArchiveMaster.Cli`、`ArchiveMaster.Modules`、`ArchiveMaster.Module.FileBackupper`。`ToolCatalog` 的测试自行构造 7 个模块的 `IModuleInfo` 列表，避免把备份模块带入。

加载测试程序集时会一并加载 Avalonia 程序集（模块工程通过 `Directory.Build.props` 引用），但不会创建 `Application`、窗口或控件；只使用纯数据类与服务类。

## 4. 数据结构

### 4.1 `TempWorkspace`

测试专用的一次性目录：

- `Root`：`%TEMP%\ArchiveMaster.Tests\<guid>`；
- `Dir(params string[] parts)`：创建并返回子目录；
- `File(string relativePath, string content)`：写 UTF-8 文本；
- `Bytes(string relativePath, byte[] content)`；
- `PathOf(string relativePath)`；
- `Dispose()`：递归删除，失败时重试并清理只读属性。

### 4.2 测试环境辅助

- `TestAppConfig.Create()`：返回 `new AppConfig(临时路径)`，不自动 `Initialize`，避免测试间通过配置文件互相影响。
- `GlobalStateScope`：进入时快照 `GlobalConfigs.Instance` 的可变属性（`DeleteMode`、`SpecialDeleteFolderName`、`FlattenPathSeparatorReplacement`、`DebugMode` 等），退出时恢复；测试内统一把 `DeleteMode` 设为 `DeleteDirectly`，避免回收站依赖。
- `TestLogger`：把 `Log.Logger` 设为静默 logger，避免 Serilog 未配置时输出噪声；测试结束恢复。
- `ImageFactory`：生成带指定尺寸/EXIF 时间/GPS 的 JPEG；`DataFactory`：生成带/不带命名空间的 GPX、标签 JSON、GeoJSON 与快照 zip。

### 4.3 测试组织

按 `Core/`、`FileTools/`、`PhotoTools/` 等目录与命名空间分组，用 `--filter FullyQualifiedName~<模块>` 筛选。纯逻辑、临时目录工作流和跨服务组合都放在对应模块目录下，默认全部执行。

## 5. 模块划分

### Core
负责：配置系统、文件循环框架、通用 helper、文件树、密码与加密、CLI 工具目录。
不负责：任何模块业务、Avalonia 控件、Host/DI 启动。

### FileTools
负责：重命名、按时间分类、目录结构克隆/同步、重复文件、附属文件、硬链接去重、文件筛选、加解密、可读性扫描、批量命令行的**参数/配置**部分。
不负责：批处理命令真实执行、Roslyn C# 脚本、稀疏文件 P/Invoke、回收站删除。

### PhotoTools
负责：EXIF 时间/GPS 读写、GPX 解析、照片瘦身分类与目标路径、按地理信息归类。
不负责：HEIC 真编码、照片瘦身实际压缩输出、依赖原生库的缩略图。

### PhotoTagging
负责：标签模型、标签文件读写、标签管理、标签搜索。
不负责：照片标签生成（LLM）、缩略图 UI。

### VideoTools
负责：ffprobe JSON 数值解析、视频流模型、ASS 字幕导出、CSV 导出。
不负责：调用 `ffprobe` 获取真实视频信息、视频属性面板。

### TextTools
负责：文本简单加密、编码识别与转换、文本源读取、逐行处理的纯辅助逻辑。
不负责：所有 LLM 工具与聊天代理。

### OfflineSync
负责：快照 zip、目录匹配、差异计算、补丁生成/应用、空目录清理。
不负责：脚本导出模式、加密文件在真实介质上的长期兼容性。

### WriteOnceArchive
负责：包信息读写、哈希文件映射、打包（Copy）、校验、重建。
不负责：ISO 实际刻录/挂载、加密包在真实光盘上的兼容性。

## 6. 交互逻辑

开发者流程：

1. `dotnet test ArchiveMaster.Tests` 构建测试工程及其引用工程并运行全部用例。
2. 只跑某一层：`dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~Core`。
3. 只跑某个模块：`dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~FileTools`。
4. 失败时保留临时目录？默认不保留；需要排查时用环境变量 `ARCHIVEMASTER_TESTS_KEEP_TEMP=1` 跳过清理，并打印目录路径。
5. 测试不修改 `configs.json`；如需配置，使用临时路径。

测试生命周期：

- 每个测试类创建新的 `TempWorkspace`；
- `GlobalStateScope` 在测试类/用例边界恢复全局状态；
- 服务对象实现 `IDisposable` 的，用 `using` 释放（`ServiceBase` 内部有 `Timer`）；
- 测试结束删除临时目录。

### 阶段二的 2A/2B 子阶段

**2A：只写测试，不修产品代码**

1. 按正确行为写完本计划内的测试。
2. 运行全量测试，记录失败用例。
3. 逐条判断失败原因：测试写错 → 立即修正测试；产品缺陷 → 保留失败用例，不修改产品代码。
4. 输出 BUG 清单（格式见 §7.1），停下来等用户确认，再进入 2B。

**2B：修复 bug，直到全绿**

1. 只修 2A 清单中确认的 bug，最小改动，不顺手重构。
2. 每个修复都保留原失败用例作为回归测试；必要时补一个更小的边界用例。
3. 若某个“bug”实际是需求/设计取舍，或修复会改变对外行为，停下来请用户裁决，不自行扩大范围。
4. 全量测试连续通过两次后，才进入闸门2。

## 7. 异常情况

| 场景 | 测试表现 | 断言 |
|---|---|---|
| 目录/文件不存在 | `Config.Check()` 抛中文异常 | 异常消息包含字段名与“不存在/为空” |
| 非法配置（枚举越界、范围越界、模板缺占位符） | `Check()` 抛异常 | 消息包含具体约束 |
| 重命名目标冲突 | `ExecuteAsync` 抛异常 | 消息包含“目标路径相同” |
| 空目录 | `TimeClassifyService.InitializeAsync` 抛“目录为空” | 精确消息 |
| 坏 JSON / 坏 zip / 缺包信息 | 对应读取方法抛异常 | 异常类型/消息 |
| 错误密码 | 解密抛 `CryptographicException` 或返回失败 | 不产生明文输出 |
| 目标文件已存在（Skip/Overwrite/Throw） | 按配置跳过/覆盖/告警 | 文件状态与目标内容 |
| 取消 | 抛 `OperationCanceledException`，已产生的临时文件被清理 | 目录无残留 |
| 外部依赖缺失 | 不纳入测试，文档说明 | 不出现“时好时坏”的用例 |
| 测试清理失败 | 不掩盖原测试失败；输出临时目录路径 | 失败信息可定位 |
| 测试发现产品缺陷（2A） | 保留失败用例，不修改产品代码 | 失败消息含期望/实际；进入 BUG 清单 |
| 测试发现产品缺陷（2B） | 修复后原用例转绿 | 同一用例保留为回归测试 |

所有断言消息使用中文；测试方法名使用英文，便于 `--filter`。

### 7.1 BUG 清单格式（2A 出口）

每条包含：

- 编号与标题；
- 失败用例的完全限定名；
- 期望行为与实际行为；
- 最小复现（测试内的输入/步骤）；
- 初步定位（文件:行，若已查明）；
- 严重程度（阻断 / 数据错误 / 体验 / 一致性）；
- 是否属于本次修复范围（2B 逐条确认）。

清单先给用户确认，不直接开修。

## 8. 边界条件

- 空目录、仅一个文件、零字节文件、全 0 字节文件、超大文件（仅用小文件模拟长度字段，不生成大文件）。
- 重复文件名、仅大小写不同、中文/空格/特殊字符文件名、超长相对路径（控制在 Windows 路径限制内）。
- 时间边界：`DateTime.MinValue/MaxValue`、毫秒截断、时间容差等于/略小于/略大于阈值。
- 数量边界：0 个/1 个/多个重复文件；包容量刚好等于、略小于、略大于文件总长。
- 文本边界：空串、仅空白、仅标点、前后缀在开头/结尾/不成对、达到 300000 字符上限。
- 配置边界：空字符串、`null` 集合、未注册配置键、重复注册、共享预设组。
- 并发边界：`Threads=0/1/2`、取消令牌已取消、取消发生在文件循环中。

## 9. 修改范围

新增：

- `ArchiveMaster.Tests/ArchiveMaster.Tests.csproj`
- `ArchiveMaster.Tests/AssemblyInfo.cs`
- `ArchiveMaster.Tests/GlobalUsings.cs`
- `ArchiveMaster.Tests/Infrastructure/TempWorkspace.cs`
- `ArchiveMaster.Tests/Infrastructure/GlobalStateScope.cs`
- `ArchiveMaster.Tests/Infrastructure/TestAppConfig.cs`
- `ArchiveMaster.Tests/Infrastructure/TestLogger.cs`
- `ArchiveMaster.Tests/Infrastructure/ImageFactory.cs`
- `ArchiveMaster.Tests/Infrastructure/DataFactory.cs`
- `ArchiveMaster.Tests/Core/*.cs`
- `ArchiveMaster.Tests/FileTools/*.cs`
- `ArchiveMaster.Tests/PhotoTools/*.cs`
- `ArchiveMaster.Tests/PhotoTagging/*.cs`
- `ArchiveMaster.Tests/VideoTools/*.cs`
- `ArchiveMaster.Tests/TextTools/*.cs`
- `ArchiveMaster.Tests/OfflineSync/*.cs`
- `ArchiveMaster.Tests/WriteOnceArchive/*.cs`

修改：

- `Directory.Packages.props`：增加 xUnit/Test.Sdk/runner 版本；不引入覆盖率工具。
- `ArchiveMaster.sln`：加入测试工程。
- `AGENTS.md`：把“没有单元测试工程”改为实际命令与边界。
- `README.md`：开发章节增加测试命令；Vibe Coding 清单加入测试工程与计划文件。
- `docs/plans/unit-tests.md`：本文件。
- `docs/plans/unit-tests-bugs.md`：2A 缺陷与手工复现。
- `docs/plans/unit-tests-cli-repro.ps1`：CLI 复现脚本。

不改（2A）：

- 任何 `ArchiveMaster.Module.*` / `ArchiveMaster.Core` 的业务源码；
- UI、CLI、Android、构建脚本、技能目录。

2B 允许修改：

- 2A BUG 清单中确认的、修复所必需的最小产品源码；
- 对应的回归用例；
- 若修复必须触及公共 API/配置结构，先停下来请用户裁决。

不为测试给产品代码加 `InternalsVisibleTo`，也不把 private 成员改成 public；只通过公开入口或真实工作流测试。

## 10. 实现步骤

1. 在 `Directory.Packages.props` 增加测试包版本；创建测试工程并加入解决方案。
2. 建立 `Infrastructure`：临时目录、全局状态、日志、素材工厂；写 1 个冒烟测试，确认 `dotnet test` 能跑。
3. Core：配置系统、helper、文件树、密码、`ToolCatalog`。
4. FileTools：先纯逻辑与配置，再逐个服务的临时目录工作流。
5. PhotoTools：EXIF/GPX/照片瘦身分类；确认 Magick.NET 在当前测试输出目录可用。
6. PhotoTagging：标签模型、标签文件、管理/搜索。
7. VideoTools：JSON 解析、模型、ASS/CSV。
8. TextTools：加密、编码转换、文本源。
9. OfflineSync：zip → step1 → step2 → step3 的组合用例。
10. WriteOnceArchive：包信息/哈希映射 → 打包 → 校验 → 重建。
11. 更新 `AGENTS.md`/`README.md`；全量运行两遍，清理临时目录与偶发失败。

每一步完成后单独运行该目录的测试，避免最后一次性排查。

阶段二按 2A/2B 执行：

- 步骤 1~11 全部属于 2A；2A 结束只交付测试与 BUG 清单，不修业务代码。
- 2A 的失败用例保留原样，不注释、不 `[Skip]`、不改断言迁就现状。
- 2B 从 BUG 清单逐条修复，每修一条重跑对应用例；全部修完后跑全量两次。
- 2B 若发现 2A 的断言本身写错，先改测试并在 BUG 清单中标注“测试问题，非产品缺陷”，不计入 bug 数。

## 11. 测试方案

### 11.1 运行命令

```powershell
dotnet test ArchiveMaster.Tests
dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~Core
dotnet test ArchiveMaster.Tests --filter FullyQualifiedName~FileTools
```

### 11.2 覆盖矩阵（实际 159 个用例）

| 模块 | 重点 | 约计 |
|---|---|---|
| Core | `AppConfig` 预设/保存/加载/坏 JSON；`ServiceBase.TryForFiles` 异常/取消/进度/线程；`FilePlaceholderReplacer`；`TableTextConverter`；`TreeDirInfo` JSON/勾选；`SecurePasswordStoreService`；`PagedList`；`FileHelper`；`ToolCatalog` | 53 |
| FileTools | Rename、Encryptor、ReadabilityScanner、TimeClassify、DirStructureSync、Duplicate、Twin、FileFilterOperation、Config.Check | 35 |
| PhotoTools | `ExifHelper.ConvertToDmsTuple`、EXIF 时间/GPS 往返、`ParseGpx`、照片瘦身分类、GeoJSON 归类、`HeifEncoder` 纯策略、修复修改时间 | 27 |
| PhotoTagging | `PhotoTags`、`ObservablePhotoTags`、`TagFileHelper`、Manager/Searcher | 8 |
| VideoTools | `ParseNumberFromJson`、`VideoStream`、`TimeAss` 导出、CSV 导出 | 12 |
| TextTools | `TextEncryptor`、`TextEncryptionService`、`EncodingConverter`、`TextSource` | 11 |
| OfflineSync | `ZipService`、Step1 快照、目录匹配、Step2 差异、Step3 应用/空目录 | 7 |
| WriteOnceArchive | `GetHashFileMap`、包信息读取、打包/校验/重建、异常包 | 5 |
| 冒烟 | 测试工程可被 `dotnet test` 发现并执行 | 1 |

### 11.3 关键用例清单

**Core**

- `AppConfig`：默认配置创建、同键不同预设、共享预设组、`Save`→`Initialize` 往返、非法 JSON 设 `LoadError`、`Initialize(false)` 不处理 `.load`、未注册键忽略、`RemovePreset`/`RenamePreset`。
- `ServiceBase.TryForFiles`：正常执行、异常写 `Error` 并调用 `CatchAction`、`ThrowExceptions` 重新抛出、取消写 `Cancel`、`FinallyAction`、`Threads=1/2/0`、`Break` 提前结束、`FileLength` 进度。
- `FilePlaceholderReplacer`：`<NameExt>/<Name>/<Ext>/<RelPath>/<DirName>`、`<Name-Left/Right>`、未知占位符原样、无占位符短路、`extraProcess` 只作用于占位符结果。
- `TableTextConverter`：CSV/TSV/空格分隔的引号转义、空集合、属性筛选、JSON/XML 输出。
- `TreeDirInfo`：`AddFile` 自动建目录、`Flatten` 顺序、`Reorder`、`Search`、`ToJson/FromJson` 往返与父节点修复、三态勾选传播。
- `SecurePasswordStoreService`：保存/加载往返、错误主密码抛异常、篡改密文抛异常、`VerifyMasterPassword`。
- `AesHelper`：相同密码派生相同密钥、不同密码不同密钥。
- `FileHelper`：`GetNoDuplicateFile/Directory`、`GetOptimalBufferSize` 边界、`GetStringComparer` 在大小写策略下的结果。
- `PagedList`：页数向上取整、空列表、`pageSize=0` 的边界行为。
- `ToolCatalog`：7 个非备份模块生成 29 个工具、ID/模块短名/`PrepareUnsafe`/`HasItemResults`、重复 ID 抛异常。

**FileTools**

- `RenameService`：Contain/Equal/Regex 搜索；ReplaceMatched/ReplaceExtension/ReplaceName/ReplaceAll/RetainMatched；手动映射；忽略大小写；子目录；目标冲突抛；实际重命名成功。
- `TimeClassifyService`：间隔等于阈值不分组、文件/目录混排、空目录抛；执行后目录名与文件位置正确。
- `DirStructureCloneService`：导出 JSON、导入 JSON 后树结构一致；目标文件已存在时按配置处理。
- `DirStructureSyncService`：按名称/时间/长度匹配、多个候选中位置正确者优先、Copy/Move。
- `DuplicateFileCleanupService`：同名同长同时间识别为重复、仅时间差在容差内、参考目录与清理目录重叠、全选删除保护。
- `TwinFileCleanerService`：主文件与附属文件模式匹配、仅删除勾选、不同后缀。
- `LinkDeduplicationService`：哈希相同/不同、大小相同但时间不同的警告、`AllowDifferentTime`。
- `FileFilterOperationService`：Copy/Move/Delete/Hash；时间与大小过滤；`PreserveDirectoryStructure`/`FlattenWithOriginalNames`/`FlattenWithRelativePathNames`；目标重名自动去重。
- `EncryptorService`：加密/解密往返、`.$ept$` 命名、目录结构加密元数据、`DeleteSourceFiles` 开/关、Overwrite/Skip/Throw。
- `FileReadabilityScannerService`：空文件、全 0 文件、正常文件、不可读文件。
- 各 Config.Check：必填、范围、枚举组合、模板占位符。

**PhotoTools**

- `ExifHelper.ConvertToDmsTuple`：0、负值、59.999 进位、整度。
- `ExifHelper`：写入 GPS 后读回、写入拍摄时间后 `FindExifTime` 读回、无 EXIF 返回 null。
- `ImageMetadataHelper.SyncXmpDimensions`：属性/元素两种写法、缺 GPano、`dc:format` 更新、非法 UTF-8 原样返回。
- `HeifEncoder`：`NormalizePreset` 别名、`IsHeifFormat`、`GetTargetSize` 的 1600 万像素阈值与 16 对齐（不调用原生编码）。
- `PhotoGeoTaggingService.ParseGpx`：带命名空间、不带命名空间、空轨迹、按时间排序。
- `PhotoGeoTaggingService`：GPX 匹配照片时间、容差内写入、容差外不勾选、已有 GPS 不勾选、时间偏移。
- `RepairModifiedTimeService`：EXIF 与修改时间差超容差才修复；执行后文件时间正确。
- `PhotoGeoSorterService`：GeoJSON 面包含点、点外、字段缺失抛、执行后移动到区域目录。
- `PhotoSlimmingService.InitializeAsync`：Compress/Copy/Skip/Delete 分类、`DeepestLevel` 展平、模板重命名、目标已存在跳过；不执行实际 HEIC 压缩。

**PhotoTagging**

- `PhotoTags`：`GetAllTags` 去重、`Count` 不去重、`ContainsTag` 大小写、`Matches` 忽略大小写、`TagType` 组合、null OCR/Description。
- `ObservablePhotoTags`：Trim/Distinct、`Count`、`ToPhotoTags`。
- `TagFileHelper`：中文/空格标签往返、无效 JSON 抛异常。
- `PhotoTagManagerService`：匹配/未匹配标签、`SaveTagsAsync` 忽略空标签、执行后树节点带标签。
- `PhotoTagSearcherService`：标签计数与排序、精确/包含搜索、多关键词 AND、空关键词返回空。

**VideoTools**

- `VideoInfoService.ParseNumberFromJson`：数字、字符串数字、非法值抛 `JsonException`。
- `VideoStream`：`CodecType` 映射、`Size` 边界、帧率分数。
- `TimeAssService`：单文件/合并导出、开始时间、倍率、间隔、文件长度非整秒、缺少开始时间/长度抛。
- `VideoInfoService.ExportCsvAsync`：单流/全流、表头与行数。

**TextTools**

- `TextEncryptor`：可逆、同密码确定性、不同密码不同结果、空白/标点/控制符不变、空密码、代理区字符。
- `MersenneTwister`：同种子复现、范围边界。
- `UnicodeRange`：区间有效且不重叠。
- `TextEncryptionService`：前后缀包裹、未包裹明文、标记不成对、明文含标记、空串、300000 上限。
- `EncodingConverterService`：ASCII 自动不勾选、BOM 期望与实际、UTF-8→GB18030、另存目录、取消清理临时文件。
- `TextSourceExtensions`：`FromFile=false` 的 PerFile/PerParagraph/Combined。
- `DocFileExtensions`：txt/md 读取（不做 PDF/Office 重测试）。

**OfflineSync**

- `ZipService`：对象→zip→对象、缺文件、坏 zip、空 zip。
- `Step1Service`：多个目录、重复顶级目录名抛、快照文件生成、空目录。
- `Step2Service.MatchLocalAndOffsiteDirsAsync`：按顶级目录名匹配、未匹配为 null、快照缺失。
- `Step2Service.InitializeAsync`：Add/Modify/Delete/Move、时间容差边界、过滤规则、重复匹配、本地目录名与异地不同时不误判为全部删除（按正确行为断言）。
- `Step2Service.ExecuteAsync`：Copy 模式生成补丁与 `file.os2`、已存在补丁跳过。
- `Step3Service.InitializeAsync`：补丁缺失、加密补丁无密码、Add/Modify/Delete/Move 状态校验。
- `Step3Service.ExecuteAsync`：应用补丁、删除/移动、时间恢复；`DeleteEmptyDirectoriesAsync` 删除空目录、保留有文件目录、忽略系统缩略图。

**WriteOnceArchive**

- `WriteOnceArchiveHelper.GetHashFileMap`：多文件、重复哈希共享、树结构与平铺。
- `ReadPackageInfoAsync`：自动选最新、强制文件、缺失、非法 JSON、无 `AllFiles`。
- `ReadPackageFilesAsync`：缺失物理文件、未引用文件、`_enc` 后缀、报告计数。
- `PackingService`：Copy 打包、哈希去重、超大文件、包拆分、`caches.whc`、排除已打包哈希。
- `VerifyService`：哈希一致成功、哈希不一致报错、加密包需要密码。
- `RebuildService`：重建成功、`SkipIfExisted`、`CheckOnly`、加密包解密重建、目标已存在覆盖。

### 11.4 2A/2B 验收

- 2A：测试工程可构建、全部用例可被发现；失败只来自确认的产品缺陷。交付测试代码 + BUG 清单，停在用户确认。
- 2B：BUG 清单中确认的项全部修复；全量测试连续两次通过；产品构建仍为 0 警告 0 错误。
- 2A 结束不进入阶段三；必须等用户确认 BUG 清单并同意开始 2B。

### 11.5 不纳入的用例

- 所有 LLM 调用与聊天消息；
- 所有 UI 控件、ViewModel、`Bitmap` 缩略图；
- `ffprobe`/`ffmpeg` 实际进程；
- HEIC 真编码与 libheif P/Invoke；
- 回收站删除、特殊删除目录；
- 硬链接、符号链接、稀疏文件在非 Windows 上的行为；
- 批处理命令行真实执行、Roslyn C# 脚本；
- Android 构建；
- 性能/压力/长时间运行。

## 附：已确认决策

| 问题 | 决定 |
|---|---|
| 覆盖深度 | B：纯逻辑 + 临时目录文件工作流；跳过 HEIC/ffprobe/UI/网络 |
| AI 边界 | A：只跳过真实 LLM 调用；标签管理/搜索等外围逻辑照测 |
| 断言形式 | 按正确行为写，不迁就当前实现 |
| Bug 处理 | B：2A 只写测试并保留失败用例；2B 修复后全绿 |
| 测试工程 | 单个 `ArchiveMaster.Tests`，xUnit v3 |
| 覆盖率 | 不引入 Coverlet，不做覆盖率阈值 |
| 文档 | 更新 `AGENTS.md`、`README.md`、本计划，并加入 `.sln` |

待闸门1：人工审查本计划，确认范围、排除项、2A/2B 流程与修改范围。

## 12. 2A 执行结果

- 测试工程：`ArchiveMaster.Tests`，xUnit v3，159 条用例。
- 结果：150 通过、9 失败。失败均为测试按正确行为断言后暴露的产品缺陷，不是测试代码错误。
- BUG 清单：[unit-tests-bugs.md](unit-tests-bugs.md)。
- 2A 未修改任何产品源码；下一步需用户确认 BUG 清单后进入 2B。
