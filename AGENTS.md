# ArchiveMaster 开发说明

面向在本仓库工作的编码代理（以及人类开发者）。先读本文件，再读 [README.md](README.md) 的「开发」一节与 `helps/` 下对应工具的帮助。

## 项目是什么

跨平台的文件归档 / 管理 / 备份工具箱，用 Avalonia 承载 GUI。一个模块（`ArchiveMaster.Module.*`）在主页上是一个分组，组内每个工具是一个标签页；工具通常分为「初始化 → 执行」两步。

- 技术栈：.NET 10、Avalonia 12、CommunityToolkit.Mvvm、Microsoft.Extensions.Hosting（DI）、Serilog、FzLib（作者自研通用库）
- 界面、文档、提交信息、面向用户的异常消息一律使用**中文**
- 许可：**GPL-3.0-or-later**（因随包分发自建 libheif + x265）。新增第三方代码前先确认许可兼容，并更新 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)

## 环境与命令

需要 .NET 10 SDK（本机已验证 10.0.400）。以下命令在仓库根目录执行：

| 目的 | 命令 |
| --- | --- |
| 构建（含全部模块） | `dotnet build ArchiveMaster.UI.Desktop` |
| 运行桌面版 | `dotnet run --project ArchiveMaster.UI.Desktop` |
| 发布（单文件，Windows） | `pwsh ./build.ps1 -w`；`-s` 自包含、`-c` 压缩（仅自包含）、`-noSingleFile` 关闭单文件 |
| 发布其他平台 | `./build.ps1 -l` / `-m` / `-all`，产物在 `./Publish/<rid>[_sc]/` |
| 打包 Android APK | `pwsh ./build_android.ps1`（Release、android-arm64，签名配置在 `ArchiveMaster.UI.Android.csproj` 内） |

构建现状：`dotnet build ArchiveMaster.UI.Desktop` 通过，0 警告 0 错误。仓库没有 CI，也没有单元测试工程（见「验证」）。

### 构建 / 运行时的三个坑

1. **输出目录被重定向**：`Directory.Build.props` 里 `SpecialOutputDir=true`，产物落在 `C:\CodeTemp\bin\<项目>\net10.0\`。换机器时改 `OutputDir`，或把 `SpecialOutputDir` 设为 `false`。
2. **运行期文件跟着 exe 走**：`Program.Main` 会 `Directory.SetCurrentDirectory(AppContext.BaseDirectory)`，因此 `configs.json`、`logs/logs.txt` 都在**可执行文件所在目录**，不在仓库里。调试时以输出目录为工作目录。
3. **单实例 + 托盘**：程序用 `TcpSingleInstanceHelper` 保证单实例，已有实例时新进程直接退出；关闭主窗口后若存在启用的后台服务（如 `FileBackupper`），程序仍在托盘运行。测试新构建前先确认旧进程已退出。

`UseLocalFzLib=true` 仅在联调本地 FzLib 仓库时使用（需 `..\..\FzLib\Publish\libs`），默认走 NuGet。

## 解决方案结构

| 项目 | 角色 |
| --- | --- |
| `ArchiveMaster.Core` | 接口与基类（`IModuleInfo`、`ServiceBase`、`TwoStepServiceBase`、`ViewModelBase`、`PanelBase`）、配置系统、通用视图与帮助类 |
| `ArchiveMaster.Core.AI` | LLM 集成：`AiServiceBase`、OpenAI / Ollama 客户端、聊天面板与消息模型 |
| `ArchiveMaster.Module.*` | 功能模块，每个模块 = 一组工具。已注册：FileTools、PhotoTools、PhotoTagging、VideoTools、TextTools、OfflineSync、WriteOnceArchive、FileBackupper |
| `ArchiveMaster.UI` | 主界面、DI 装配、模块加载与初始化（`Initializer.cs`） |
| `ArchiveMaster.Modules` | 内置模块清单 `BuiltInModules.All`，UI 与 CLI 共用 |
| `ArchiveMaster.Cli` | 命令行入口 `am`，无界面运行工具（见「命令行（am）与 Agent Skill」） |
| `ArchiveMaster.UI.Desktop` | 桌面启动器；HEIC 原生库也放在这里（见「原生库」） |
| `ArchiveMaster.UI.Android` | Android 启动器；实现 `IPermissionService` 等平台接口 |

两个「不在运行路径」的工程，别被误导：

- `ArchiveMaster.Module.DiscArchive`：未加入 `.sln`、未被 `UI` 引用，已被 `WriteOnceArchive` 取代（`helps/disc.md` 是遗留文档）。
- `ArchiveMaster.Module.Test`：在 `.sln` 里但没有在 `Initializer.ModuleInitializers` 注册，是手写试验面板，**不是测试工程**。

GUI 与模块工程都设 `RootNamespace=ArchiveMaster`，所以命名空间与项目名无关，模块的类同样落在 `ArchiveMaster.Services` / `ArchiveMaster.ViewModels` 等命名空间里；`ArchiveMaster.Cli` 是例外，使用 `ArchiveMaster.Cli` 命名空间。新增类型注意避免与其他模块重名。

## 模块与工具的组成

一个模块（`XxxModuleInfo : IModuleInfo`）要提供：

- `Configs`：`ConfigMetadata` 列表，注册后才能被配置系统读写
- `TransientServices` / `SingletonServices` / `BackgroundServices`：DI 注册；后台服务须实现 `IBackgroundService`
- `Views`：`ToolPanelGroupInfo`，其中每个工具是一个 `ToolPanelInfo(ViewType, ViewModelType, 标题, 描述, 图标 avares://…)`
- `ModuleName`、`ModuleDescription`、`HelpFileName`（对应 `helps/*.md`，菜单里打开 GitHub master 上的文件）
- 可选 `JsonConverters`（模块私有 JSON 转换器）

新增模块的接入链（缺一步就不会生效）：

1. 新建 `net10.0` 工程，`RootNamespace=ArchiveMaster`，引用 `ArchiveMaster.Core`；
2. 加入 `ArchiveMaster.sln`，并让 `ArchiveMaster.Modules.csproj` 引用它；
3. 在 `ArchiveMaster.Modules/BuiltInModules.cs` 的 `All` 列表里加入 `new XxxModuleInfo()`（GUI 与 CLI 都从这里取模块）；
4. `ArchiveMaster.UI` 只引用 `ArchiveMaster.Modules`，不需要再为单个模块加 `ProjectReference`。

> `Initializer.cs` 顶部的 `DYNAMIC_DLL` 反射加载开关是**注释状态**：当前是静态注册，不要指望把 dll 丢进输出目录就会被发现。

### 一个工具的四件套

| 层 | 基类 | 职责 |
| --- | --- | --- |
| Config | `ConfigBase` | 配置项（`[ObservableProperty]`），实现 `Check()`；抛中文异常描述哪项不合法 |
| Service | `ServiceBase` / `TwoStepServiceBase<TConfig>` | 业务逻辑；两步式实现 `InitializeAsync` + `ExecuteAsync` + `GetInitializedFiles` / `GetExecutedFiles` |
| ViewModel | `TwoStepViewModelBase<TService, TConfig>` | 串联 Config / Service / View，管理按钮状态与进度 |
| View | `PanelBase` / `VerticalTwoStepPanelBase` | 纯 XAML 布局，代码后置只做控件初始化 |

约定与要点：

- **配置键 = 配置类名**（`ConfigMetadata.Key = type.Name`）。重命名 Config 类会让用户已有配置“凭空消失”，需同时做迁移或保留旧类名。
- `Config.Check()` 由 ViewModel 在初始化 / 执行前自动调用，不要在其他地方重复校验；用户可见的错误消息写成中文。
- 文件批处理统一走 `TryForFiles` / `TryForFilesAsync` + `FilesLoopOptions`：`AutoApplyStatus()` 自动维护文件状态，`AutoApplyFileNumberProgress()` / `AutoApplyFileLengthProgress()` 自动报进度，`WithMultiThreads()` 开并行（`Threads` 默认 1，0 = 自动并行，≥2 = 指定线程数）。
- 耗时的枚举 / IO 放进 `Task.Run`，不要阻塞 UI 线程；用 `NotifyMessage` / `NotifyProgress` 反馈，不要直接碰控件。
- 多步骤共享预设（如 OfflineSync）在 `ConfigMetadata(type, group)` 与 ViewModel 构造的 `configGroupName` 使用同一个组名。
- ViewModel 通过构造函数注入 `ViewModelServices`（对话框、存储、进度遮罩、剪贴板、`AppConfig`），Service 通过 DI 构造注入 `AppConfig`。

### 面板与 XAML

- `AvaloniaUseCompiledBindingsByDefault=false`：默认反射绑定；`x:DataType` 只作设计时提示，不会强制编译绑定。
- 主题为 Fluent + FzLib 样式（`App.axaml`），控件多用 FzLib 的 `FormItem`、`StackFormItemGroup`、`FilePickerTextBox` 等，先找现成控件再自己写。
- 视图里不放逻辑；交互放 ViewModel 的 `[RelayCommand]`。
- 新增工具后同步更新：模块 `Views.Panels`、`README.md` 的工具表格、`helps/<模块>.md`。

## 配置与运行期数据

`AppConfig` 把三部分序列化到 `configs.json`（缩进 JSON、中文不转义）：

```
{ "Modules": [...], "Groups": {...}, "Globals": {...} }
```

- `Modules`：各配置项的预设（`ConfigItem`），默认预设名 `"默认"`；`Groups`：每组当前选中的预设；`Globals`：`GlobalConfigs`（文件名大小写策略、删除方式、调试模式、AI 供应商、FFmpeg 路径、主密码等）。
- 启动时若存在 `configs.json.load`，会用 `File.Replace` 覆盖当前配置并留下 `.bak` 备份；正常 `Save()` 直接覆盖 `configs.json`，不额外备份。
- 新增配置属性要保证旧 `configs.json` 仍能反序列化（给合理默认值）；加载失败时程序回退默认配置并记录 `LoadError`，不会崩溃。
- 各工具的运行数据：备份数据库为备份目录下的 `db.sqlite`（EF Core + SQLite）；日志为 exe 目录下 `logs/logs.txt`（按天滚动）；敏感字符串用 `SecurePassword`（PBKDF2-SHA256 20 万次派生 + AES-GCM）加密，主密码本身用机器标识派生密钥加密后存在 `Globals`。
- AI 工具读取 `GlobalConfigs.AiProviders.CurrentProvider`（OpenAI / Ollama）；AI 服务继承 `AiServiceBase` 或 `AiTwoStepServiceBase`，提示词接口为 `GetSystemPromptAsync` / `GetFirstUserPromptAsync`。

## 原生库（HEIC）与打包

- `PhotoSlimming` 通过自建 libheif + x265 输出 HEIC。原生库只放在 `ArchiveMaster.UI.Desktop/native/<rid>/`，**不要放进 PhotoTools 模块**——模块会被 Android 工程引用，放那里会把 Windows DLL 卷进 APK。
- csproj 按 `$(RuntimeIdentifier)` 选择原生库，不要用 `IsOSPlatform`（那是构建主机，交叉发布时会选错）。
- 原生库由 `build_scripts/native/build-heif.ps1`（Windows）和 `build-heif.sh`（Linux）从上游源码构建；`tools/heic-verify/` 是输出验证脚本。

## 依赖管理

- 版本集中在 `Directory.Packages.props`（中央包管理），**不要在 csproj 里写 Version**；`AvaloniaVersion`、`FzLibVersion`、`AppVersion` 在 `Directory.Build.props`。
- Avalonia 相关包（Avalonia、DataGrid、ItemsRepeater、ColorPicker、Diagnostics）要一起升级，保持同一大版本。
- `Svg.Controls.Skia.Avalonia` 偶发与 SkiaSharp 版本冲突（见 `Directory.Packages.props` 注释），升级需实测。
- 全局 `Nullable=disable`；新代码与周边保持一致，不必强行引入可空注解。

## 命令行（am）与 Agent Skill

- `ArchiveMaster.Cli` 是独立控制台工程（产物名 `am`），复用 Core/Service 层；**不要在 CLI 里复制工具业务逻辑**。
- 工具目录由 `ToolCatalog` 从 `IModuleInfo.Views` 反射 ViewModel 的 `TwoStepViewModelBase<TService,TConfig>` 得到；聊天类与非两步式工具会被自动跳过，`FileBackupper` 由 CLI 显式排除。
- 工具 ID = `<模块短名>.<配置类名去掉 Config 与模块前缀>`（如 `offlineSync.step1`）；**不要重命名配置类**，否则 ID 与用户已有配置一起失效。
- `plan` 执行 `InitializeAsync`（准备阶段，可能写文件），`run` 在同一进程内继续 `ExecuteAsync`；两阶段语义见 `docs/plans/cli-and-skill.md`。新增工具若在 `InitializeAsync` 里写文件，必须登记到 `ToolCatalog.PrepareUnsafeToolIds`。
- 只返回聚合结果的工具要登记到 `ToolCatalog.NoItemResultToolIds`；CLI 会把 `itemsAvailable` 置为 false，并从服务公开属性生成 `notes`。
- CLI 默认 profile 在用户数据目录（`%LOCALAPPDATA%\ArchiveMaster\cli`），不写 `configs.json`，只在 profile 下创建 `.am.lock`（保留不删，避免 Unix unlink 竞态）。
- 技能位于 `skill`；`assets/` 由 `am docs --out skill` 生成，**不要手改生成物**。用户级安装用 `am docs --install`。
- CLI 的退出码与 JSON 字段是给 AI 的契约：改动 `ToolRunResult` / 退出码 / 工具能力标记时同步更新 `SKILL.md` 与生成物。

## 代码风格

- 4 空格缩进、Allman 大括号、`using` 按 System → 第三方 → 本项目排序。
- 新文件用文件级命名空间与主构造函数（现有新代码风格）；老文件里的块级命名空间不必特意改写。
- 私有字段用小驼峰**不加下划线**（如 `isDisposed`、`pendingMessage`）；MVVM 属性用 `[ObservableProperty]` 生成，命令用 `[RelayCommand]`。
- 注释与 XML 文档用中文，解释“为什么”而不是复述代码；复杂流程可加 `#region`（现有代码常用中文 region 名）。
- 编码：`.cs` / `.csproj` 多为 UTF-8 with BOM，`.axaml`、`README.md`、`*.ps1` 无 BOM；工作区行尾为 CRLF（`core.autocrlf=true`），不要整体转换行尾。
- 提交信息用中文、一句话说清改动，按功能前缀，例如 `照片瘦身：HEIC 编码解除并发闸门`、`修复HEIC网格因尺寸量化而大面积转不出来的问题`。仅在用户要求时提交 / 推送；主开发分支为 `dev`。

## 验证

仓库目前**没有自动化测试**，也没有 CI。`ArchiveMaster.Module.Test` 是试验模块，不能当作回归保障。改动后的最低验证：

1. `dotnet build ArchiveMaster.UI.Desktop` 通过且不新增警告；
2. 运行 `dotnet run --project ArchiveMaster.UI.Desktop`，实际操作受影响的工具（至少走一遍「初始化 → 执行」，包括取消与报错路径）；
3. 涉及配置结构时，用旧 `configs.json` 启动一次，确认能加载、预设不丢；
4. 涉及文件操作时，在临时目录验证，注意工具会真实移动 / 删除文件（默认删除到回收站，`GlobalConfigs.DeleteMode` 可改）。

## 文档与披露

- 用户文档：`helps/*.md`（README 中按模块链接），模块的 `HelpFileName` 指向对应文件。
- README 的「Vibe Coding」小节披露了 AI 参与编写的文件及程度；新增或大改 AI 生成的文件时应同步更新该清单。
- 版本号、许可表达式在 `Directory.Build.props`；发版时同步 `AppVersion`、Android `ApplicationVersion` 与安装脚本。
