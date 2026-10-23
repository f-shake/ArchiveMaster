# ArchiveMaster CLI + Agent Skill 开发计划

> 状态：阶段一定稿（两轮澄清完成，待人工确认）
> 日期：2026-10-23
> 范围：新增无界面 CLI 与配套 Agent Skill；不改动任何工具的业务逻辑与 GUI 行为。

## 0. 背景与目标

ArchiveMaster 目前是纯 GUI 桌面应用，能力只能由人点界面触发，AI 无法发现和调用。目标是提供一条 **命令行入口 + 一份 Agent Skill**，让 AI（以及脚本）能够：

1. 发现有哪些工具、每个工具需要什么配置；
2. 按工具的“准备 → 执行”两阶段模型运行，拿到结构化结果；
3. 在不打开 GUI 的前提下完成批量文件操作。

### 已定决策

| 项 | 结论 |
|---|---|
| 计划文件位置 | `docs/plans/cli-and-skill.md` |
| “备份的”范围 | 只排除 `FileBackupper`；保留 OfflineSync 与 WriteOnceArchive（29 个工具） |
| CLI 可执行名 | `am.exe`；GUI 仍为 `ArchiveMaster.exe` / `ArchiveMaster.UI.Desktop.exe`，两者不同名 |
| `run` 门槛 | 所有 `run` 必须带 `--yes` |
| `plan` 门槛 | 不需要 `--yes`（第一阶段重复执行理论无影响） |
| `plan` ↔ `run` | 跨命令不复用结果；`run` 在一条命令内连续执行两个阶段 |
| `items` 截断 | 默认 50，可用 `--max-items` 覆盖；全局默认值存于 `configs.json` 的 `Globals.CliMaxItems` |
| 不可预演工具调 `plan` | 拒绝并提示改用 `run --yes` |
| Skill 安装 | 仓库内一份 + `docs --install` 复制到 `~/.agents/skills/archive-master` |
| 单元测试 | 本次不建测试工程；验证用**临时** PowerShell 脚本，不进仓库 |
| `build.ps1` | v1 不改；CLI 用 `dotnet publish` 手动发布 |

### 明确不做

- 不做 MCP / HTTP / 命名管道等对外接口；
- 不暴露 AI 对话类工具（`textTools.textRewriter`、`textTools.smartDocSearch`）与常驻备份服务（`FileBackupper` 模块）；
- 不新增/修改任何工具的业务算法，只做“调用层”；
- 不改变 GUI 的启动方式与现有配置格式；
- 本次不引入单元测试工程。

## 1. 功能需求

每条需求都对应可执行的验收方式。

| # | 需求 | 验收方式 |
|---|---|---|
| R1 | `am list --json` 输出全部可运行工具（预期 29 个），字段含 id/module/title/description/previewable；不出现被排除工具 | 临时脚本断言数量、唯一性、排除集合 |
| R2 | `am describe <id> --json` 输出工具元数据 + 可直接编辑的默认配置 JSON | 临时脚本：模板能反序列化回 `ConfigType` |
| R3 | `am plan <id> --config <file\|->` 执行工具的**第一阶段（准备/预处理）**，输出待处理条目与汇总。该阶段可能产生副作用（如创建目录），不是纯只读预览 | 临时目录：TimeClassify plan 后输出条目；不把“文件未动”当作契约 |
| R4 | `am run <id> --config <file\|-> --yes` 在**同一进程、同一 Service 实例**上先执行第一阶段、再执行第二阶段，输出逐条结果与汇总 | 临时目录：TimeClassify run 后文件被移动、汇总正确 |
| R5 | 缺少 `--yes` 时拒绝执行（退出码 4），且不产生任何文件改动 | 临时脚本 |
| R6 | `--profile <dir>` 指定 `configs.json`；`--preset <name>` 运行已保存预设；与 `--config` 互斥 | 手工 + 临时脚本 |
| R7 | `--report <path>` 写 JSONL；未指定时写临时目录，结果 JSON 里回传路径 | 临时脚本 |
| R8 | `--json` 时 stdout 只有单个 JSON 对象，进度与提示全部走 stderr | 临时脚本解析 stdout |
| R9 | `--allow-root <dir>`（可重复）限制所有输入/输出路径；越界拒绝（退出码 2）并指出越界路径 | 临时脚本 |
| R10 | 同一 profile 并发运行 plan/run 会互斥，第二个进程退出码 2 并提示 | 临时脚本（两个进程） |
| R11 | Ctrl+C 取消：保留已完成结果、写报告、退出码 3 | 手工 |
| R12 | `am docs --out <dir>` 生成 `tool-catalog.json` 与 `tools/<id>.md`，仅依赖代码，可重复生成 | 生成两次 diff 一致 |
| R13 | `am docs --install` 把技能（含 assets）复制到 `~/.agents/skills/archive-master` | 临时脚本 + 手工 |
| R14 | 仓库内提供 `skill/SKILL.md`，AI 仅凭该技能即可完成「发现问题→准备→执行」 | 人工评审 + 一次真实调用演练 |
| R15 | `am --version` 输出版本，与 `AppVersion` 一致 | 临时脚本对比 csproj 属性 |
| R16 | 未知工具/缺参/坏 JSON/校验失败均有明确中文提示与固定退出码 | 临时脚本参数化 |

### 退出码约定（固定契约）

| 码 | 含义 |
|---|---|
| 0 | 成功（plan 完成；run 无 Error/Warn） |
| 1 | 执行完成，但存在 Warn 或 Error 条目 |
| 2 | 用法错误、配置错误、校验失败、路径越界、锁冲突、未知工具 |
| 3 | 被取消（部分完成） |
| 4 | 拒绝执行（缺少 `--yes`） |

## 2. 技术方案

### 选型

| 决策 | 选择 | 理由 | 否决的备选 |
|---|---|---|---|
| 入口形态 | 独立控制台工程 `ArchiveMaster.Cli`，产物名 `am` | 与 GUI 进程隔离，可无头运行；复用 Service 层 | 在 GUI 加 `--run` 参数（受单实例/托盘干扰） |
| 复用方式 | 新增 `ArchiveMaster.Core/Cli` 内核（工具目录 + 两阶段执行器 + 结果 DTO），CLI 只做参数与输出 | 业务逻辑不重复；后续 GUI 也可改用同一执行器 | 直接在 CLI 里复制 ViewModel 编排 |
| 模块清单 | 新增 `ArchiveMaster.Modules` 工程，UI 与 CLI 共用 `BuiltInModules.All` | 避免两份清单漂移；`Initializer` 已有静态列表可平移 | CLI 引用 `ArchiveMaster.UI`（拖入 GUI 程序集，语义混乱） |
| 工具发现 | 反射 ViewModel 的泛型基类 `TwoStepViewModelBase<TService,TConfig>` 取服务/配置类型 | 现有一切两步式工具都满足；零侵入 | 给 29 个工具逐个加元数据（改动面大） |
| 配置输入 | `--config`（文件/stdin）+ `--profile/--preset`（复用 GUI 预设） | 既支持 AI 生成的一次性配置，也支持人已调好的预设 | 仅 `--config`（AI 难以复用现有配置） |
| 两阶段 | `plan` 暴露第一阶段；`run` 内部连续执行两个阶段 | 与 GUI 的 `InitializeAsync` → `ExecuteAsync` 一一对应；第二阶段永远看到第一阶段的真实内存结果 | 把 plan 实现成“只读预览”（与工具语义不符） |
| JSON | `System.Text.Json` + 现有 `AppConfig` 转换器 | 与 GUI 配置格式完全一致（`SecurePassword` 等） | Newtonsoft（引入第二套序列化） |
| 参数解析 | 手写解析（约 200 行） | 零新依赖；参数面很小 | `System.CommandLine`（长期 beta、版本漂移） |
| Skill | 静态 `SKILL.md` + `docs` 生成的 `assets/` | 细节可机读、与代码同步生成 | 手写 29 份说明（必然漂移） |
| 测试 | 临时 PowerShell 冒烟脚本（不提交） | 本次不建测试工程；先保证端到端可运行 | xUnit 工程（留待后续） |

### 关键取舍

- **plan 不是 dry-run**：它是执行流程的第一阶段，负责扫描、计算、建立目标结构、筛出待处理项；可能创建目录或写入中间产物。第二阶段依赖第一阶段算出的列表与状态，因此 `run` 必须在同一进程、同一 Service 实例上连续执行两个阶段。
- **跨进程不复用状态**：`plan` 与 `run` 是两次进程；`run` 会重新执行第一阶段。文件系统在两次调用之间发生变化时，以 `run` 时重新计算的结果为准。
- **确认门槛分离**：`plan` 免 `--yes`（便于先看准备结果，且第一阶段可重复执行）；`run` 一律要求 `--yes`。
- **`EnableInitialize=false` 的工具**（`step1`、`photoTagManager`、`photoTagSearcher`、`typoChecker`）没有第一阶段，`plan` 返回退出码 2 并提示直接 `run --yes`。
- **不做能力矩阵**：v1 不维护“哪个工具会删文件”的静态表，靠 `run --yes` 统一兜底。

## 3. 系统结构

```
┌──────────────────────────┐        ┌───────────────────────────┐
│ ArchiveMaster.UI (GUI)   │        │ ArchiveMaster.Cli (新)    │
│  Initializer / MainVM    │        │  参数解析 / 输出 / 退出码 │
└────────────┬─────────────┘        └─────────────┬─────────────┘
             │ 引用                                │ 引用
             └──────────────┬─────────────────────┘
                            ▼
              ┌──────────────────────────────┐
              │ ArchiveMaster.Core           │
              │  Cli/ToolCatalog             │
              │  Cli/HeadlessToolRunner      │   ← 新
              │  Cli/ToolRunResult           │
              │  Configs/AppConfig           │   ← 小改
              │  Configs/GlobalConfigs       │   ← 加 CliMaxItems
              └──────────────┬───────────────┘
                             │ 引用
              ┌──────────────▼───────────────┐
              │ ArchiveMaster.Modules (新)   │
              │  BuiltInModules.All          │
              └──────────────┬───────────────┘
                             │ 引用 8 个模块
        ┌────────────────────┴─────────────────────┐
        ▼            ▼            ▼                  ▼
   Module.FileTools ... Module.TextTools ... Module.WriteOnceArchive
```

调用关系：

1. CLI 启动 → 构造 `AppConfig(<profile>/configs.json)` → 注册全部模块的 `Configs` 与 `JsonConverters` → `Initialize()`。
2. `ToolCatalog.Build(BuiltInModules.All)` → 反射得到 `ToolDescriptor[]`（过滤排除项）。
3. 读取配置（文件或预设）→ `HeadlessToolRunner.PrepareAsync`（第一阶段）。
4. `run` 时在同一 runner 实例上继续 `ExecuteAsync`（第二阶段），把 `SimpleFileInfo` 映射为 DTO。
5. CLI 输出人读文本或 JSON，按汇总映射退出码。

## 4. 数据结构

### 4.1 `ToolDescriptor`（Core，运行时对象，不序列化）

| 字段 | 类型 | 说明 |
|---|---|---|
| `Id` | `string` | 稳定 ID，`<module>.<tool>`，如 `fileTools.timeClassify` |
| `ModuleId` | `string` | 模块短名，如 `fileTools` |
| `ModuleName` | `string` | 中文模块名，如 `文件目录工具` |
| `Title` / `Description` | `string` | 来自 `ToolPanelInfo` |
| `ConfigType` / `ServiceType` | `Type` | 反射所得 |
| `HasPrepareStage` | `bool` | `EnableInitialize` 是否未被覆写为 false；决定 `plan` 是否可用 |
| `PrepareUnsafe` | `bool` | 预留：第一阶段副作用较强时为 true（v1 全 false，审计后修正） |

### 4.2 `ToolRunResult`（JSON 输出契约，`schemaVersion = 1`）

```json
{
  "schemaVersion": 1,
  "tool": "fileTools.timeClassify",
  "stage": "prepare",
  "canceled": false,
  "startedAt": "2026-10-23T05:00:00.0000000Z",
  "durationMs": 123,
  "summary": { "total": 2, "success": 0, "warn": 0, "error": 0, "skip": 0, "cancel": 0, "bytes": 1024 },
  "items": [
    { "path": "D:\\photos\\a.jpg", "relativePath": "a.jpg", "length": 1024, "isDir": false, "status": "Ready", "message": null }
  ],
  "itemsTruncated": false,
  "report": "C:\\Users\\...\\Temp\\archivemaster\\20261023-130000-fileTools.timeClassify.jsonl"
}
```

- `stage`：`prepare`（plan）或 `execute`（run）。
- `status` 取值：`Ready`/`Warn`/`Error`/`Processing`/`Success`/`Skip`/`Cancel`（沿用 `ProcessStatus`）。
- `items` 默认最多 50 条（`--max-items`），完整结果始终写入 JSONL 报告。
- 空列表合法：`total=0`，退出码 0，并在 stderr 提示“没有需要处理的条目”。

### 4.3 配置文件

- 单工具配置：`describe` 输出的 `config` 子树，字段与 `ConfigType` 一一对应（PascalCase，与 GUI 一致）。
- `--profile` 读取的 `configs.json` 结构不变（`Modules` / `Groups` / `Globals`）。
- 新增全局字段 `Globals.CliMaxItems`（`int`，默认 `50`）：CLI 的 `items` 截断默认值；命令行 `--max-items` 优先。
- 不写回配置：CLI 只读。

## 5. 模块划分

| 模块 | 负责 | **不负责** |
|---|---|---|
| `ArchiveMaster.Core/Cli/ToolCatalog` | 从 `IModuleInfo` 建立工具目录、ID 推导、排除规则、第一阶段可用性判定 | 参数解析、中文输出、文件 IO |
| `ArchiveMaster.Core/Cli/HeadlessToolRunner` | 构造 Service、执行 prepare/execute、进度与取消、结果映射、释放资源 | 退出码、JSON 格式化、锁、报告文件 |
| `ArchiveMaster.Core/Configs/AppConfig`（小改） | 公开序列化选项；提供按 `Type` 取配置的非泛型入口 | 任何 CLI 语义 |
| `ArchiveMaster.Core/Configs/GlobalConfigs`（小改） | 新增 `CliMaxItems` 默认值 | CLI 参数解析 |
| `ArchiveMaster.Cli` | 参数解析、校验、`--allow-root`、锁、报告写入、stdout/stderr 分工、退出码、`docs --install` | 工具业务逻辑、配置结构定义 |
| `ArchiveMaster.Modules` | 唯一模块清单 | 任何实例化与 UI 逻辑 |
| Skill（`skill`） | 告诉 AI 何时用、怎么用、如何读结果 | 不复制代码；细节指向生成的 assets |
| `ArchiveMaster.UI` | 仅把模块清单来源改为 `BuiltInModules` | 其余行为不变 |

## 6. 交互逻辑

### AI 侧（Skill 规定的标准流程）

1. `am list --json` → 找到目标工具 ID；
2. `am describe <id> --json` → 得到默认配置模板；
3. 按用户意图改写 `config`，写临时文件（或管道 stdin）；
4. `am plan <id> --config -` → 执行第一阶段，拿到待处理清单与汇总；
5. 把汇总（条目数、总大小、抽样条目）给用户看；必要时让用户调整配置；
6. 用户认可后 `am run <id> --config - --yes --json`（内部重做第一阶段再执行）；
7. 读 `summary` 与 `items`；有 Error/Warn 时逐条说明，并引用 `report` 路径。

### 人侧

同一条命令，去掉 `--json` 得到人读文本；`--quiet` 关闭进度。

### 两阶段语义

```
plan  = 校验配置 → InitializeAsync → 输出第一阶段结果
run   = 校验配置 → InitializeAsync → ExecuteAsync → 输出两阶段结果
```

- 第一阶段可能创建目录、生成中间文件、筛出可处理项；第二阶段消费同一内存中的列表与状态。
- `run` 不会复用上一次 `plan` 的结果；两次之间文件系统变了，以 `run` 重新计算为准。

取消：SIGINT → 取消令牌 → 工具内 `ThrowIfCancellationRequested` → 收集已完成条目 → 退出 3。

## 7. 异常情况（含提示文案）

| 场景 | 表现 | 文案（stderr） | 退出码 |
|---|---|---|---|
| 未知工具 | 立即失败 | `未知工具：{id}。运行 am list 查看可用工具。` | 2 |
| 缺必填参数 | 打印用法摘要 | `缺少参数：{name}。用法：am {cmd} [选项]` | 2 |
| 配置文件不存在 | 立即失败 | `配置文件不存在：{path}` | 2 |
| JSON 解析失败 | 指出工具类型 | `配置 JSON 解析失败：{message}（工具 {id}，类型 {type}）` | 2 |
| 配置校验失败 | 输出 Check 的中文消息 | `配置校验失败：{message}` | 2 |
| 断网/网络类（AI 工具） | 工具异常 | `工具执行失败：{message}`；报告保留已完成条目 | 1 |
| 缺 `--yes` | 不执行任何操作 | `拒绝执行：{id} 可能修改文件。先用 plan 查看准备结果，确认后加 --yes。` | 4 |
| 路径越界 | 不执行 | `路径不在允许范围内：{path}（允许：{roots}）` | 2 |
| 并发锁冲突 | 不执行 | `另一个 am 进程正在使用 {profile}，请稍后再试。` | 2 |
| 用户取消 | 停止并保留结果 | `已取消，已完成 {done}/{total}。部分结果见报告中。` | 3 |
| 无第一阶段工具调 plan | 不执行 | `工具 {id} 没有准备阶段，请直接 run --yes。` | 2 |
| 初始化无结果 | 正常结束 | `没有需要处理的条目。` | 0 |
| 缺 AI 提供商 | 工具抛错 | `工具 {id} 需要 AI 提供商，请先在 profile 的 configs.json 中配置。` | 2 |
| 第一阶段产生副作用 | 正常结束 | stderr 提示 `工具 {id} 的准备阶段可能已写入目标目录。` | 0 |

## 8. 边界条件

| 条件 | 处理 |
|---|---|
| 全新 profile（无 configs.json） | 使用默认配置，不报错；`--preset` 回落“默认” |
| 空目录 / 空结果 | 正常结束，提示“没有需要处理的条目” |
| 路径含空格、中文、超长路径 | 全程 `string`/API 传递，不拼接 shell；Windows 长路径由 .NET 处理 |
| 巨大结果集（10 万+） | stdout 截断到 `--max-items`（默认取 `CliMaxItems`，初始 50），全量写 JSONL |
| 配置文件含未知字段 | `System.Text.Json` 默认忽略 |
| 配置中相对路径 | 原样交给工具（沿用 GUI 语义），`--allow-root` 只检查根路径 |
| 同名工具跨模块 | ID 含模块前缀，天然唯一（`packing` 只出现在 WriteOnceArchive） |
| 非 Windows 调用 Windows-only 工具 | 工具自身抛 `PlatformNotSupportedException` → 退出码 1 |
| HEIC 原生库缺失 | 工具报错；CLI 输出目录需随包复制 `native/<rid>` |
| 两次 plan 同时跑 | 第一阶段可能写文件，因此与 run 一样受 profile 锁保护 |

## 9. 修改范围（文件清单基线）

### 新增

| 文件 | 作用 |
|---|---|
| `ArchiveMaster.Modules/ArchiveMaster.Modules.csproj` | 模块清单工程 |
| `ArchiveMaster.Modules/BuiltInModules.cs` | `All` 列表（8 个模块） |
| `ArchiveMaster.Core/Cli/ToolDescriptor.cs` | 描述符 |
| `ArchiveMaster.Core/Cli/ToolCatalog.cs` | 反射建目录、ID、排除、阶段能力 |
| `ArchiveMaster.Core/Cli/HeadlessToolRunner.cs` | prepare/execute 执行器 |
| `ArchiveMaster.Core/Cli/ToolRunResult.cs` | 结果 DTO（含 `ToolItemResult`、`ToolSummary`） |
| `ArchiveMaster.Core/Cli/ToolProgress.cs` | 进度/消息回调模型 |
| `ArchiveMaster.Cli/ArchiveMaster.Cli.csproj` | 控制台工程（`AssemblyName=am`，含原生库复制条件） |
| `ArchiveMaster.Cli/Program.cs` | 入口、命令分发 |
| `ArchiveMaster.Cli/CliArguments.cs` | 手写参数解析 |
| `ArchiveMaster.Cli/Commands/ListCommand.cs` | list |
| `ArchiveMaster.Cli/Commands/DescribeCommand.cs` | describe |
| `ArchiveMaster.Cli/Commands/PlanCommand.cs` | plan |
| `ArchiveMaster.Cli/Commands/RunCommand.cs` | run |
| `ArchiveMaster.Cli/Commands/DocsCommand.cs` | docs（生成 assets、`--install`） |
| `ArchiveMaster.Cli/Output/JsonOutput.cs` | 统一序列化（camelCase、缩进/紧凑） |
| `ArchiveMaster.Cli/Output/HumanOutput.cs` | 表格/摘要文本 |
| `ArchiveMaster.Cli/Infrastructure/ProfileLock.cs` | 文件锁 |
| `ArchiveMaster.Cli/Infrastructure/PathGuard.cs` | `--allow-root` 校验 |
| `skill/SKILL.md` | 技能正文 |
| `skill/assets/tool-catalog.json` | 生成物 |
| `skill/assets/tools/*.md` | 生成物（29 份） |

> 临时验证脚本写在系统临时目录，不进入仓库。

### 修改

| 文件 | 改动 |
|---|---|
| `ArchiveMaster.UI/Initializer.cs` | 模块清单改为 `BuiltInModules.All`；`DYNAMIC_DLL` 分支同步 |
| `ArchiveMaster.UI/ArchiveMaster.UI.csproj` | 8 个模块引用换成 `ArchiveMaster.Modules` |
| `ArchiveMaster.Core/Configs/AppConfig.cs` | 公开 `CreateJsonOptions()`；新增 `GetOrCreateConfig(Type, preset)` |
| `ArchiveMaster.Core/Configs/GlobalConfigs.cs` | 新增 `CliMaxItems = 50` |
| `ArchiveMaster.sln` | 加入两个新工程（CLI、Modules） |
| `AGENTS.md` | 增加 CLI/Skill 的使用与维护约定 |
| `README.md` | 增加“命令行”一节与示例 |

## 10. 实现步骤

1. **抽模块清单**：新建 `ArchiveMaster.Modules`，UI 改为引用；`dotnet build ArchiveMaster.UI.Desktop` 必须与改动前等价。
2. **Core 暴露点**：`AppConfig.CreateJsonOptions()`、非泛型配置读取、`GlobalConfigs.CliMaxItems`。
3. **目录**：实现 `ToolCatalog`；临时脚本核对 29 个 ID、排除集合、4 个无准备阶段的工具。
4. **两阶段执行器**：实现 `PrepareAsync`/`ExecuteAsync`；CLI 打通 `list/describe/plan`；用 `fileTools.timeClassify` 在临时目录跑通第一阶段。
5. **执行与安全**：`--yes`、进度、取消、汇总、报告、锁、`--allow-root`、退出码；冒烟覆盖成功/缺 `--yes`/取消。
6. **横向接入**：逐个模块跑第一阶段，人工审计是否写文件；有强副作用的工具在 `describe` 暴露 `prepareUnsafe`。
7. **文档生成**：`docs` 命令 + `SKILL.md` + `--install`；生成 assets 并人工评审。
8. **收尾**：README/AGENTS；用临时脚本做全量烟雾（29 个工具的 `describe` + 代表性 `plan`）。

每步都以“构建通过 + 对应临时脚本/人工验证通过”为完成判据。

## 11. 测试方案

> 本轮决定：**不引入单元测试工程**，验证脚本作为临时文件放在系统临时目录，不提交。

| 手段 | 覆盖 |
|---|---|
| 临时 PowerShell 冒烟 | 29 个工具 `describe` 全部可反序列化；`list --json` 数量/唯一性；TimeClassify 的 `plan`→`run`；缺 `--yes`；`--allow-root` 越界；并发锁；取消 |
| 人工评审 | `SKILL.md` 能否让 AI 独立完成一次“选工具→填配置→plan→run”；`docs` 生成物是否可读 |
| 构建 | `dotnet build ArchiveMaster.UI.Desktop` 与 `dotnet build ArchiveMaster.Cli` 均 0 警告 0 错误 |

后续如引入 xUnit，再补目录/参数/退出码的契约测试（不在本次范围）。

## 附录 A：v1 工具清单（29）

| ID | 中文名 | 有准备阶段 |
|---|---|---|
| `fileTools.encryptor` | 文件加密解密 | 是 |
| `fileTools.dirStructureSync` | 目录结构同步 | 是 |
| `fileTools.dirStructureClone` | 目录结构克隆 | 是 |
| `fileTools.rename` | 批量重命名 | 是 |
| `fileTools.duplicateFileCleanup` | 重复文件清理 | 是 |
| `fileTools.timeClassify` | 按时间段分类 | 是 |
| `fileTools.twinFileCleaner` | 附属文件清理 | 是 |
| `fileTools.batchCommandLine` | 批量命令行执行 | 是 |
| `fileTools.linkDeduplication` | 硬链接去重 | 是 |
| `fileTools.fileFilterOperation` | 文件筛选操作 | 是 |
| `fileTools.fileReadabilityScanner` | 文件可读性检测 | 是 |
| `photoTools.repairModifiedTime` | 修复照片修改时间 | 是 |
| `photoTools.photoSlimming` | 照片瘦身 | 是 |
| `photoTools.photoGeoTagging` | 照片地理信息写入 | 是 |
| `photoTools.photoGeoSorter` | 照片位置分类 | 是 |
| `photoTagging.photoTagGenerator` | 照片标签生成（AI 辅助） | 是 |
| `photoTagging.photoTagManager` | 图像标签管理 | 否 |
| `photoTagging.photoTagSearcher` | 照片标签搜索 | 否 |
| `videoTools.timeAss` | 视频时间戳字幕 | 是 |
| `videoTools.videoInfo` | 视频信息提取 | 是 |
| `textTools.encodingConverter` | 编码识别与转换 | 是 |
| `textTools.typoChecker` | 错别字检查（AI 辅助） | 否 |
| `textTools.lineByLineProcessor` | 逐行智能转换（AI 辅助） | 是 |
| `offlineSync.step1` | 制作异地快照 | 否 |
| `offlineSync.step2` | 本地生成补丁 | 是 |
| `offlineSync.step3` | 异地同步 | 是 |
| `writeOnceArchive.packing` | 打包和备份 | 是 |
| `writeOnceArchive.rebuild` | 重建和恢复 | 是 |
| `writeOnceArchive.verify` | 包信息和验证 | 是 |

### 排除

| 工具 | 原因 |
|---|---|
| `textTools.textRewriter` | AI 对话类 |
| `textTools.smartDocSearch` | AI 对话类 |
| `textTools.textEncryption` | 非两步式（直连 ViewModel），且属文本小工具 |
| `fileBackupper.backupTasks` / `fileBackupper.backupManageCenter` | 常驻备份服务，不是一次性工具 |
