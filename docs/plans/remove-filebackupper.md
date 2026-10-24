# 删除 FileBackupper 模块（文件备份服务）

> 阶段一计划初稿。目标：把「文件备份服务」从 ArchiveMaster 中整体移除——功能已由
> <https://github.com/f-shake/FileBackupTool> 独立提供。
> 本文件只描述计划，不代表已开始修改代码。

## 0. 已定决策（来自本轮追问）

1. **托盘、后台服务、开机自启全部删除**：不再保留后台服务契约/注册/查询、托盘图标与托盘菜单、
   「开机自启」设置项及 `"s"` 静默启动参数。
2. **README**：保留开头历史项目列表，在原备份章节位置加一行迁移说明指向
   `https://github.com/f-shake/FileBackupTool`；删除其他备份相关文字（截图、帮助链接、工具表、
   “下一步计划”等）。
3. **历史文档保持原样**：`docs/plans`、`docs/code-review` 不改写。
4. **验证范围**：Desktop + CLI 构建、全量单测；不要求 Android/Release。
5. **旧数据**：不迁移、不删除；旧 `configs.json` 中的备份配置忽略并随下次保存消失；
   备份目录中的 `db.sqlite`/快照原样保留。

## 1. 功能需求

| 编号 | 需求 | 验收方式 |
| --- | --- | --- |
| R1 | GUI 不再显示「实时备份服务」分组及两个备份工具 | 启动 GUI，模块列表中无该分组 |
| R2 | 解决方案、模块清单、CLI 目录不再引用 `ArchiveMaster.Module.FileBackupper` 的任何类型 | `dotnet build` 通过；全仓 grep 无残留引用 |
| R3 | 不再引入 `Microsoft.EntityFrameworkCore.Sqlite`（仅备份模块使用），依赖树/包体不再包含 EF Core | `dotnet list package`/产物无 EF Core；`THIRD-PARTY-NOTICES.txt` 同步 |
| R4 | 当前文档（README、AGENTS、skill）不再宣传备份模块；README 只保留历史列表 + 新项目地址 | 文档 grep 无模块宣传文字（历史文档除外） |
| R5 | 用户已有备份数据不被删除或改写；旧 `configs.json` 中的 `FileBackupperConfig` 不再加载，保存时自然消失 | 用带旧配置的临时 `configs.json` 启动验证；备份目录内容不变 |
| R6 | CLI 工具数与行为不变（29 个工具；备份模块原本就未暴露给 CLI） | `am list --json` 仍为 29 项，无 `fileBackUpper.*` |
| R7 | 删除后台服务、托盘、开机自启后：关闭主窗口即退出；设置页无「开机自启」；程序启动不再有 `"s"` 静默模式 | GUI 手测 + 代码 grep |
| R8 | 保留单实例激活、`AppLifetime` 退出保存、通用模块/DI 机制 | 第二实例能激活第一实例；退出时保存配置 |

## 2. 技术方案（含选型理由与否决方案）

**采用：删除模块工程 + 清理所有引用 + 移除仅它使用的 EF Core 依赖，同时删除后台服务/托盘/开机自启三套外围机制。**

理由：

- 功能已迁到独立仓库，保留空壳工程只会留下 EF/DB 代码与死 UI；
- 模块原本未暴露给 CLI，删除不影响 29 个工具的目录契约；
- EF Core Sqlite 仅被该模块引用，删除后减少桌面/Android 依赖；
- 后台服务只有 `BackupBackgroundService` 一个实现，托盘与开机自启都是为它服务；
  三者一起删除后，应用回到「普通桌面程序」模型：关闭窗口即退出。

具体做法：

- **模块**：删除整个工程目录；从 `.sln`、`ArchiveMaster.Modules.csproj`、
  `BuiltInModules.All` 移除；CLI 不再需要显式排除类型。
- **后台服务**：删除 `IBackgroundService` 接口、`IModuleInfo.BackgroundServices` 契约及其
  所有实现（均为空）、`HostServices.GetBackgroundServices`、`Initializer` 的后台服务注册与
  `GetBackgroundServices`；保留 `AppLifetime`（`IHostedService`，负责退出保存）。
- **托盘**：删除 `App.axaml` 的 `TrayIcon` 定义、`App.axaml.cs` 的托盘点击/退出菜单/
  `TrayIcon.GetIcons` 释放；关闭主窗口回到默认 `OnLastWindowClose` 行为。
- **开机自启**：删除 `SettingViewModel` 的 `IStartupManager`/`IsAutoStart`/`SetAutoStart`、
  `SettingDialog.axaml` 的「开机自启」复选框、`Initializer.TryAddStartupManager`；
  删除 `App.axaml.cs` 中 `Args[0] == "s"` 的静默启动分支（启动总是显示主窗口/启动画面）。
- **配置兼容**：不写迁移。`AppConfig.ParseModules` 只加载已注册类型，旧
  `FileBackupperConfig` 被忽略，下次保存消失；备份数据库/快照不做任何处理。
- **文档**：只改当前文档；`docs/plans`、`docs/code-review` 作为历史记录保持原样。

否决方案：

- **保留工程但不注册**：仍编译、仍带 EF 依赖、仍有死代码。
- **只删模块、保留后台/托盘/自启**：没有任何后台服务实现，托盘与自启成为死功能。
- **迁移备份数据库**：新工具在独立仓库，ArchiveMaster 不再读写该库，无需迁移。

## 3. 系统结构

```
ArchiveMaster.UI
  ├─ 单实例 TcpSingleInstanceHelper（保留）
  ├─ AppLifetime : IHostedService（保留，退出保存）
  ├─ 设置页（删除开机自启项）
  └─ ArchiveMaster.Modules
       ├─ FileTools / PhotoTools / PhotoTagging / VideoTools
       ├─ TextTools / OfflineSync / WriteOnceArchive
       └─ FileBackupper（删除）
ArchiveMaster.Cli
  └─ ToolCatalog.Build(BuiltInModules.All)（删除排除参数）
ArchiveMaster.Core
  ├─ IModuleInfo（删除 BackgroundServices 契约）
  ├─ HostServices（删除 GetBackgroundServices）
  └─ IBackgroundService（删除）
ArchiveMaster.Module.FileBackupper
  └─ Microsoft.EntityFrameworkCore.Sqlite（一并移除）
```

- 删除后 `BuiltInModules.All` 为 7 个模块；`ToolCatalog.Build` 只需模块列表。
- UI 通过反射加载模块；除设置页/托盘/静默启动外无需改 UI 逻辑。
- Android/Desktop 启动器只引用 `ArchiveMaster.UI`，无需改动。

## 4. 数据结构

无新增数据结构。删除：

- `FileBackupperConfig`、`BackupTask`、`BackupTaskStatus`、`FileRecordType`、`SnapshotType`；
- EF 实体：`BackupFileEntity`、`BackupLogEntity`、`BackupSnapshotEntity`、`EntityBase`、
  `BackupperDbContext` 及 `DbService.*`；
- `IBackgroundService` 接口与 `IModuleInfo.BackgroundServices` 契约（代码层结构）；
- `SettingViewModel.IsAutoStart`、`StartupManager` 属性与命令；
- 备份目录中的 `db.sqlite` **不删除**（用户数据）。

保留：`configs.json` 的 `Modules`/`Groups`/`Globals` 结构不变；旧备份配置键成为未知键，
加载时忽略。

## 5. 模块划分（职责 / 不负责什么）

- 删除 `ArchiveMaster.Module.FileBackupper` 的职责：备份任务配置、后台备份、快照/文件历史、
  备份数据库、托盘常驻、开机自启。
- 删除通用后台服务/托盘/开机自启机制（已无使用方）。
- 本计划**不负责**：删除用户备份目录或 `db.sqlite`；把旧数据导入 FileBackupTool；
  修改其他 7 个模块；修改单实例 TCP 机制；修改 `AppLifetime` 退出保存；版本号升级。

## 6. 交互逻辑

1. 启动 GUI：主页分组不再出现「实时备份服务」；启动总是显示主窗口（Windows 先显示启动画面）。
2. 关闭主窗口：默认 `OnLastWindowClose` → 应用退出；不再进入托盘。
3. 设置页：无「开机自启」复选框；其余设置不变。
4. 单实例：第二个进程通过 `TcpSingleInstanceHelper` 激活已有窗口后退出。
5. 打开旧 `configs.json`：备份配置被忽略；其他预设正常加载；保存后备份键消失。
6. `am list`：仍 29 个工具；`fileBackUpper.*` 本就不在目录中。

## 7. 异常情况（含提示文案）

| 场景 | 表现 |
| --- | --- |
| 删除后仍有旧项目引用/`BackgroundServices` 实现 | 编译报错（CS0246/CS0006），由构建阶段发现并清理 |
| 旧 `configs.json` 含 `FileBackupperConfig` | 不报错、不加载；下次保存丢弃。无额外提示 |
| 备份目录中的 `db.sqlite`/快照 | 不读取、不修改；用户可继续用旧数据或迁移到新工具 |
| 关闭主窗口 | 进程正常退出；不残留托盘图标 |
| 用户从旧版本升级后找不到备份工具 | README 提供新项目地址；应用内不弹提示 |
| 第二实例 | 激活第一实例窗口，自身退出 |

## 8. 边界条件

- 仅安装过备份模块、`configs.json` 里只有备份配置：加载后 `configs` 为空；其他工具按默认值
  创建配置，保存后不残留备份键。
- `currentPresets` 中残留 `FileBackupperConfig` 组名：不参与其他组查询；`Save` 时按已注册的
  组过滤 `currentPresets`，旧组名随保存一起消失；无异常。
- 备份目录不存在/无权限：应用不再访问，无需处理。
- 无后台服务时关闭窗口：默认生命周期退出；`Exit`/`Desktop_Exit` 仍执行单实例释放与
  `Initializer.StopAsync`。
- Android：删除 EF Sqlite 后包体减小；`ArchiveMaster.UI.Android` 无需改代码。
- 历史文档（`docs/plans`、`docs/code-review`）保留原样。

## 9. 修改范围（文件清单）

删除：

- `ArchiveMaster.Module.FileBackupper/`（52 个受版本控制文件，含工程、源码、axaml、3 个 svg）
- `ArchiveMaster.Core/IBackgroundService.cs`
- `helps/backup.md`
- `imgs/实时备份服务/`（2 个 README 截图）

修改：

- `ArchiveMaster.sln`：删除项目块、UI 项目依赖行、12 行配置、`NestedProjects` 一行
- `ArchiveMaster.Modules/ArchiveMaster.Modules.csproj`：删除工程引用
- `ArchiveMaster.Modules/BuiltInModules.cs`：删除 `new FileBackupperModuleInfo()`
- `ArchiveMaster.Cli/CommandContext.cs`：`ToolCatalog.Build(BuiltInModules.All)`，删除排除类型
- `ArchiveMaster.Core/IModuleInfo.cs`：删除 `BackgroundServices` 契约
- 各 `*ModuleInfo.cs`（FileTools/PhotoTools/PhotoTagging/VideoTools/TextTools/OfflineSync/
  WriteOnceArchive/Module.Test/Module.DiscArchive）：删除 `BackgroundServices` 属性
- `ArchiveMaster.Core/HostServices.cs`：删除 `GetBackgroundServices`
- `ArchiveMaster.UI/Initializer.cs`：删除后台服务注册循环、`GetBackgroundServices`、
  `TryAddStartupManager` 及 `FzLib.Application.Startup` using
- `ArchiveMaster.UI/App.axaml.cs`：删除托盘事件/释放、`ShutdownMode.OnExplicitShutdown`、
  关闭窗口的后台服务判断、`Args[0]=="s"` 静默启动分支
- `ArchiveMaster.UI/App.axaml`：删除 `TrayIcon` 块
- `ArchiveMaster.UI/ViewModels/SettingViewModel.cs`：删除 `IStartupManager`/`IsAutoStart`/
  `SetAutoStart` 与 `FzLib.Application.Startup` using
- `ArchiveMaster.UI/Views/SettingDialog.axaml`：删除「开机自启」复选框
- `Directory.Packages.props`：删除 `Microsoft.EntityFrameworkCore.Sqlite` 版本
- `THIRD-PARTY-NOTICES.txt`：删除 EF Core Sqlite 条目
- `README.md`：删除截图/帮助/模块三节；更新测试边界与模块数；增加新项目地址一行
- `AGENTS.md`：模块清单、托盘/单实例描述、后台服务契约、备份数据库、CLI 排除、测试边界
- `skill/SKILL.md`：删除「常驻备份服务（FileBackupper）」说明
- `ArchiveMaster.Core/Configs/AppConfig.cs`：`Save` 按已注册组过滤 `currentPresets`
- `ArchiveMaster.Tests/Core/AppConfigTests.cs`：新增旧配置兼容回归测试
- 本计划文件 `docs/plans/remove-filebackupper.md`

不改：`docs/plans/*`、`docs/code-review/*`（历史记录）；其他模块业务；`AppLifetime`；
单实例机制。

## 10. 实现步骤

1. 关闭正在运行的 ArchiveMaster 实例；确认工作区基线。
2. 删除模块工程目录、`helps/backup.md`、`imgs/实时备份服务/`。
3. 清理解决方案与工程引用（sln、Modules.csproj、BuiltInModules、CLI 排除）。
4. 删除后台服务契约/注册/查询与各模块 `BackgroundServices` 属性。
5. 删除托盘与开机自启代码（App.axaml/App.axaml.cs/SettingViewModel/SettingDialog/
   Initializer）。
6. 移除 EF Core Sqlite 包与第三方声明。
7. 更新 README/AGENTS/skill。
8. `dotnet build ArchiveMaster.UI.Desktop`；`dotnet build ArchiveMaster.Cli`；
   `dotnet test ArchiveMaster.Tests`。
9. 全仓 grep `FileBackupper|FileBackupTool|实时备份|backup.md|BackgroundServices|开机自启|TrayIcon`，
   确认只余历史文档与新地址。
10. 用临时 `configs.json`（含备份预设）启动一次，确认不崩溃、其他预设可用；确认备份目录
    `db.sqlite` 未被改动；关闭主窗口确认进程退出、无托盘图标。
11. 汇报结果，停在闸门2。

## 11. 测试方案

- 构建：Desktop、CLI 均 0 警告 0 错误；Git 差异不含模块/后台服务/托盘/自启残留。
- 单测：既有 216 条 + 新增 1 条旧配置兼容回归 = 217 条；`ToolCatalogTests` 仍断言 29 个工具、
  7 个模块。
- CLI：`am list --json` 29 项且无 `fileBackUpper.*`；`am describe` 不受影响。
- GUI 手测：主页无备份分组；设置页无「开机自启」；关闭主窗口进程退出；任务栏无托盘图标；
  第二实例能激活第一实例；启动无 `"s"` 静默模式。
- 配置兼容：临时复制旧 `configs.json`（含 `FileBackupperConfig`）启动，确认其他预设可用、
  备份配置被忽略；保存后备份键消失。
- 数据安全：备份目录 `db.sqlite`/快照哈希不变。
- 不要求：Android 构建、Release 发布（按已定决策）。
