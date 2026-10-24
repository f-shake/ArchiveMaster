# CLI 配置字段覆盖（`--set`）开发计划

> 状态：阶段三复核修复完成，待再复核
> 日期：2026-10-24
> 范围：只扩展 `am` 的配置输入方式，不改动任何工具的业务逻辑、配置类与 GUI 行为。

## 0. 背景

当前 `am plan` / `am run` 的配置来源只有两种：

- `--config <文件|->`：整份 JSON；
- `--preset <名称> --profile <目录>`：已保存的预设。

想临时改一个输入目录或输出目录时，必须复制整份配置、改 JSON、再写临时文件，命令行体验很差。目标是提供一种类似 ffmpeg `-vf` 命名参数的轻量覆盖方式，但只借用 `key=value` 的思路，不引入 filtergraph 语法。

## 1. 功能需求

每条需求都要能被一条测试用例或一条明确的端到端断言验证。

| # | 需求 | 验收方式 |
|---|---|---|
| R1 | `plan` 与 `run` 接受可重复的 `--set <字段>=<值>` | 解析两条 `--set`，最终配置同时包含两个值 |
| R2 | `--set` 覆盖 `--config`、`--preset` 或默认配置中的同名字段 | 三种来源各一条用例，覆盖后的值生效 |
| R3 | 嵌套字段用点号路径，如 `PathFilter.IncludeFiles=*.jpg` | 修改 `FileFilterOperationConfig.PathFilter.IncludeFiles` 后断言生效 |
| R4 | 支持字符串、布尔、整数/浮点、枚举、`TimeSpan`、`DateTime`、`DateTimeOffset`、`Guid` 及其可空类型 | 每种类型至少一条解析用例 |
| R5 | 同名字段重复出现时，后出现的覆盖先出现的 | 两条 `--set Dir=A`、`Dir=B`，结果为 `B` |
| R6 | 字段名大小写不敏感，但优先精确匹配 | `dir` 与 `Dir` 都指向同一属性 |
| R7 | `--set` 在路径白名单校验和准备阶段之前应用 | 覆盖后的路径被 `--allow-root` 校验；`offlineSync.step2` 的 `LocalDir` 覆盖后参与自动匹配 |
| R8 | 无法解析的赋值、未知字段、只读字段、JSON 解析失败都返回退出码 2，且不启动工具 | 参数化用例断言异常消息与退出码 |
| R9 | `--set` 不写回 `configs.json`，不影响后续命令 | 运行后重新加载 profile，配置未变化 |
| R10 | 不传 `--set` 时，现有行为完全不变 | 原有 CLI e2e 与单元测试全部通过 |
| R11 | `--help`、README、AGENTS、Skill 文档都能看到 `--set` 的用法与边界 | 文档检查 |
| R12 | `--set` 不是安全沙箱；`--allow-root` 仍然是路径限制手段 | 越界路径用例返回 2 |
| R13 | 集合、数组、嵌套对象等复杂类型按目标类型自动反序列化 JSON，不需要额外的 `--set-json` | `PathFilter={"IncludeFiles":"*.jpg"}` 生效 |
| R14 | `SecurePassword` 也走自动转换，支持直接传入明文 | `EncryptorConfig.Password` 被设置，且文档提示密码会进入命令历史 |
| R15 | `--set` 只用于 `plan` / `run`；其他命令遇到 `--set` 返回 2 | `am list --set Dir=x` 退出码 2 |
| R16 | 不允许用 JSON `null` 清空字段；字符串字段的 `null` 按字面量处理 | `--set PathFilter=null` 返回 2，`--set Dir=null` 得到字符串 `null` |

## 2. 技术方案

### 2.1 选型

新增 `ArchiveMaster.Core/Cli/ConfigOverrides.cs`，由 CLI 调用。它负责：

1. 解析 `字段=值` 赋值；
2. 按点号路径找到配置属性；
3. 按目标属性类型转换值；
4. 写回配置对象。

CLI 只负责收集 `--set` 参数并在正确时机调用它。

### 2.2 为什么放在 Core

- 纯逻辑，不依赖控制台、文件或服务层，便于单元测试；
- CLI 不复制反射和类型转换代码；
- 未来若有其他无界面入口，可以复用。

### 2.3 为什么不用 ffmpeg filtergraph 语法

| 方案 | 结论 | 理由 |
|---|---|---|
| `--set key=value` 可重复 | 采用 | 解析简单，不需要为字段值定义分隔符和转义规则；Windows 路径中的 `:`、`\` 不会成为语法字符 |
| 单字符串 `--filter "a=1:b=2"` | 否决 | `:` 与盘符冲突，路径、正则、文件名都可能需要转义；错误定位困难 |
| 每个工具加 `--input` / `--output` | 否决 | 29 个工具字段异构，多个工具存在两个以上输入目录，维护映射容易与配置类漂移 |
| JSON Merge Patch 文件 | 暂不做 | 适合批量覆盖，但解决不了“命令行直接改一个目录”的主要痛点；可作为后续扩展 |
| 修改 GUI 或配置类 | 否决 | 本需求只影响 CLI 输入层 |

### 2.4 值转换策略

按目标属性的实际类型分两层处理。

**简单类型：标量解析**

- `string`：原样使用；
- `bool`：`true` / `false`，兼容 `1` / `0`；
- 整数、浮点、`decimal`、`char`：`InvariantCulture`；
- 枚举：名称（大小写不敏感）或数字；
- `TimeSpan`：`TimeSpan.Parse`，如 `01:00:00`；
- `DateTime` / `DateTimeOffset`：`InvariantCulture` 解析；
- `Guid`：`Guid.Parse`；
- `Nullable<T>`：先按 `T` 解析；
- 不支持用 `null` 清空字段：字符串字段的 `null` 是字面量；非字符串、复杂类型和 `SecurePassword` 的 `null` 直接报错（R16）。

**复杂类型：JSON 反序列化**

- 集合、数组、嵌套对象、带自定义 JSON 转换器的类型，直接把值当作 JSON；
- 使用 `JsonSerializer.Deserialize(value, type, AppConfig.CreateJsonOptions())`，与 GUI / `--config` 保持同一套转换器；
- 示例：
  - `--set PathFilter={"IncludeFiles":"*.jpg","UseRegex":false}`
  - `--set MatchingDirs=[...]`
- JSON 解析失败时不回退到字符串，也不尝试猜测，直接报错。

**`SecurePassword` 的特殊处理**

`SecurePassword` 的 JSON 转换器会把 64 位以上十六进制串当作密文。为了避免明文口令被误判：

1. 只有显式 JSON 字符串（以双引号开头并结尾）才走 `SecurePassword` 转换器，`--set Password="secret"` 走这条路径；
2. 其他值直接按明文构造 `SecurePassword`，`--set Password=secret`、`--set Password=<64位十六进制>` 都按明文保存；
3. JSON 对象/数组不接受，避免把 `{"Password":"x"}` 静默当成字面量密码。

这样既复用了现有的 JSON 转换器，也不会让 64 位十六进制明文静默变成空密码。代价是明文密码会出现在 shell 历史和进程列表，文档必须明确提示优先使用 `--config` / `--preset`。

### 2.5 应用顺序

```text
解析参数
  -> LoadConfig（--config / --preset / 默认“默认”预设）
  -> ConfigOverrides.Apply
  -> PathGuard.ValidateConfig
  -> PrepareConfigAsync（仅 offlineSync.step2 自动匹配）
  -> PathGuard.ValidateConfig
  -> Config.Check
  -> ProfileLock
  -> HeadlessToolRunner
```

覆盖必须在第一次 `PathGuard` 之前，否则白名单校验看不到用户传入的目录；也必须在 `PrepareConfigAsync` 之前，否则 `LocalDir` 覆盖不会参与自动匹配。

## 3. 系统结构

```text
ArchiveMaster.Cli
  Program
    -> CliArguments（新增 set 值选项）
    -> CliCommandPolicy.EnsureSetAllowed
    -> ToolCommand
         -> LoadConfig
         -> ConfigOverrides.Apply   ← 新增
         -> PathGuard
         -> HeadlessToolRunner
    -> List / Describe / Docs
         -> CommandContext.EnsureNoOverrides
              -> CliCommandPolicy

ArchiveMaster.Core
  Cli/ConfigOverrides.cs            ← 新增
  Cli/CliCommandPolicy.cs           ← 新增
  Cli/CliException.cs               ← 复用
  Configs/ConfigBase.cs             ← 不改
```

调用关系：

1. `CliArguments` 把每个 `--set` 的原始字符串按顺序保存；
2. `ToolCommand` 读取 `args.GetAll("set")`；
3. `ConfigOverrides.Apply(config, tool.Id, assignments)` 修改配置对象；
4. 后续流程不变。

不新增项目、不新增 NuGet 依赖、不修改 `ToolDescriptor` 与 `ToolRunResult`。

## 4. 数据结构

### 4.1 赋值解析

不新增公开 DTO：`ConfigOverrides.Apply` 在循环中把每个 `字段=值` 就地拆成属性路径和原始值，路径用 `string[]` 表示，错误消息保留原始赋值。这样避免为一次解析引入一个只在内部使用的类型。

### 4.2 `ConfigOverrides`（静态类）

```csharp
public static class ConfigOverrides
{
    public static void Apply(object config, string toolId, IEnumerable<string> assignments);
}
```

行为：

- `assignments` 为空时直接返回；
- 逐个解析并应用，任一失败立即抛出 `CliException(2, ...)`；
- 不捕获目标属性 setter 抛出的异常，统一包装为配置覆盖失败；
- 不负责加载配置、不负责路径校验、不负责执行工具。

### 4.3 配置对象

不新增配置类、不修改现有配置字段。`--set` 只操作已经存在的公开可写属性。

## 5. 模块划分

| 模块 | 负责 | 不负责 |
|---|---|---|
| `ArchiveMaster.Core/Cli/ConfigOverrides` | 赋值解析、属性路径解析、类型转换、JSON 反序列化、写回配置 | 参数收集、配置加载、路径白名单、工具执行、输出格式 |
| `ArchiveMaster.Core/Cli/CliCommandPolicy` | 判断 `--set` 是否只用于 `plan` / `run` | 参数解析、配置加载 |
| `ArchiveMaster.Cli/CliArguments` | 把 `--set` 识别为可重复值选项，保留顺序 | 解析 `key=value`、类型转换 |
| `ArchiveMaster.Cli/Commands/ToolCommand` | 在加载配置后调用覆盖器，包装工具 ID 上下文 | 反射细节、类型解析 |
| `ArchiveMaster.Cli/CommandContext` | 为 `list` / `describe` / `docs` 统一拒绝 `--set` | 工具执行 |
| `ArchiveMaster.Cli/Program` | 帮助文本；在 help/version 前调用命令策略 | 业务逻辑 |
| `ArchiveMaster.Cli/Output/HumanOutput` | `describe` 人类可读输出增加 `--set` 提示 | JSON 契约 |
| `ArchiveMaster.Tests/Core/ConfigOverridesTests` | 纯逻辑测试 | 进程级 e2e |
| `ArchiveMaster.Tests/Core/CliCommandPolicyTests` | 命令策略测试 | 进程级 e2e |
| `README.md` / `AGENTS.md` / `skill/SKILL.md` | 使用说明与边界 | 代码实现 |

## 6. 交互逻辑

### 6.1 基本用法

```powershell
am run fileTools.dirStructureSync `
  --set SourceDir=D:\input `
  --set TemplateDir=D:\input `
  --set TargetDir=D:\output `
  --set CompareName=true `
  --yes
```

### 6.2 覆盖预设

```powershell
am run fileTools.fileFilterOperation `
  --preset "我的预设" --profile "D:\gui-profile" `
  --set TargetDir=D:\output `
  --set PathFilter.IncludeFiles=*.jpg `
  --yes
```

### 6.3 覆盖 JSON 配置

```powershell
am plan fileTools.fileFilterOperation `
  --config cfg.json `
  --set SourceDirs=D:\input `
  --set TargetDir=D:\output `
  --json
```

### 6.4 不传配置来源

```powershell
am plan fileTools.timeClassify --set Dir=D:\input --json
```

此时使用默认 profile 中的“默认”预设；若不存在，则使用配置类默认值，再应用 `--set`。

### 6.5 复杂字段

```powershell
am run fileTools.fileFilterOperation `
  --set SourceDirs=D:\input `
  --set TargetDir=D:\output `
  --set 'PathFilter={"IncludeFiles":"*.jpg","UseRegex":false}' `
  --yes
```

JSON 值用单引号包住，避免 PowerShell 把花括号或双引号解释成自己的语法。

### 6.6 敏感字段

```powershell
am run fileTools.encryptor `
  --set RawDir=D:\input `
  --set EncryptedDir=D:\output `
  --set Password=secret `
  --yes
```

密码会进入命令历史，文档提示优先使用 `--config` / `--preset`。

### 6.7 状态流转

1. 参数解析阶段只收集字符串，不校验字段；
2. 配置加载完成后，按 `--set` 出现顺序依次应用；
3. 应用成功后继续原有的白名单、准备、校验、加锁、执行流程；
4. 任一 `--set` 失败则输出中文错误并返回 2，工具不会启动。

## 7. 异常情况

所有错误退出码均为 `2`，提示写 stderr。

| 场景 | 提示文案 |
|---|---|
| 缺少等号 | `--set 参数格式错误：{raw}，应为 字段=值` |
| 字段名为空 | `--set 的字段名不能为空：{raw}` |
| 未知字段 | `工具 {toolId} 没有配置字段：{path}` |
| 只读字段 | `配置字段 {path} 是只读的，不能通过 --set 修改` |
| 中间对象为空且无法创建 | `配置字段 {path} 的上级对象为空，无法设置` |
| 中间值不是对象 | `配置字段 {path} 的上级字段不是对象` |
| 简单值无法解析 | `配置字段 {path} 需要 {type} 类型的值，无法解析：{value}` |
| JSON 值无法解析 | `配置字段 {path} 的 JSON 值无法解析为 {type}：{message}` |
| `SecurePassword` 对象/数组 | `配置字段 {path} 的 SecurePassword 只接受明文或 JSON 字符串，不接受对象或数组` |
| JSON `null` | `配置字段 {path} 不支持 null，请改用 --config`（字符串字段除外，`Dir=null` 是字面量） |
| 大小写匹配到多个属性 | `配置字段 {path} 存在多个大小写匹配，请使用完整名称` |
| 非 `plan` / `run` 使用 | `--set 只用于 plan 和 run，{command} 不支持` |

补充：

- 未知字段错误只指出字段路径，不自动猜测“最接近的字段”，避免误导。
- JSON 解析失败不会回退到“当作字符串”；`SecurePassword` 只有显式 JSON 字符串才走转换器，其他值按明文处理。
- 错误消息不包含配置值以外的敏感信息；但 `SecurePassword` 的明文会出现在命令行，这是已接受的取舍。

## 8. 边界条件

| 条件 | 处理 |
|---|---|
| 不传 `--set` | 行为与当前完全一致 |
| `--set` 重复同一字段 | 后者覆盖前者 |
| 值为空字符串 | `string` 设为空串；其他简单类型报错；复杂类型按 JSON 解析 |
| 值包含 `=` | 只按第一个 `=` 拆分，其余原样作为值 |
| 值包含空格、中文、`\` | 由 shell 引号处理，解析器不拆分 |
| 字段名大小写不同 | 精确匹配优先，否则大小写不敏感匹配 |
| 可空类型 | 支持 `Nullable<T>`；不允许 JSON `null`；字符串字段的 `null` 按字面量处理 |
| 枚举 | 支持名称和数字；非法名称报错 |
| 集合、数组、复杂对象整体赋值 | 按 JSON 反序列化；JSON 不合法则报错 |
| `SecurePassword` | 显式 JSON 字符串走转换器，其他值按明文保存；64 位以上十六进制明文不会被误判为密文；明文会进入命令历史 |
| `EditableString`、颜色等带转换器的类型 | 按 JSON 反序列化；失败则报错 |
| 嵌套对象为 `null` | 若类型有无参构造函数则创建；否则报错 |
| 嵌套路径穿过字符串/数字等标量 | 报“上级字段不是对象” |
| `--config` 是 `describe` 完整输出 | 仍先取内层 `config`，再应用 `--set` |
| `--config -` | 先从 stdin 读完 JSON，再应用 `--set` |
| `--preset` 名称不存在 | 保持现有行为，直接报错，不进入 `--set` |
| `--set` 与 `--allow-root` 同时使用 | 覆盖后的配置先经过白名单校验，越界返回 2 |
| `--set` 修改 `offlineSync.step2.LocalDir` | 在自动匹配前生效 |
| `--set` 修改 `MatchingDirs` | 按 JSON 数组反序列化；格式错误则报错 |
| `list` / `describe` / `docs` / `version` / `help` 使用 `--set` | 直接报错，不执行命令 |
| 未安装/无权限的 profile | 保持现有错误处理 |

## 9. 修改范围

### 新增

| 文件 | 作用 |
|---|---|
| `ArchiveMaster.Core/Cli/ConfigOverrides.cs` | 赋值解析、属性路径、类型转换、JSON 反序列化、写回 |
| `ArchiveMaster.Core/Cli/CliCommandPolicy.cs` | 判断 `--set` 是否只用于 `plan` / `run`，供 CLI 与单测共用 |
| `ArchiveMaster.Tests/Core/ConfigOverridesTests.cs` | 纯逻辑单元测试 |
| `ArchiveMaster.Tests/Core/CliCommandPolicyTests.cs` | 命令策略单元测试 |
| `docs/plans/cli-set-overrides.md` | 本计划 |

### 修改

| 文件 | 改动 |
|---|---|
| `ArchiveMaster.Cli/CliArguments.cs` | `ValueOptions` 增加 `set` |
| `ArchiveMaster.Cli/Commands/ToolCommand.cs` | 加载配置后调用 `ConfigOverrides.Apply`，错误带工具 ID |
| `ArchiveMaster.Cli/CommandContext.cs` | 新增 `EnsureNoOverrides`，委托给 `CliCommandPolicy` |
| `ArchiveMaster.Cli/Commands/ListCommand.cs` | 拒绝 `--set` |
| `ArchiveMaster.Cli/Commands/DescribeCommand.cs` | 拒绝 `--set` |
| `ArchiveMaster.Cli/Commands/DocsCommand.cs` | 拒绝 `--set` |
| `ArchiveMaster.Cli/Output/HumanOutput.cs` | `describe` 人类可读输出增加 `--set` 提示 |
| `ArchiveMaster.Cli/Program.cs` | 帮助文本增加 `--set`；在 help/version 分支前调用 `CliCommandPolicy` 统一拒绝 |
| `README.md` | 命令行示例增加 `--set` |
| `AGENTS.md` | CLI 约定增加 `--set` 的语义与边界 |
| `skill/SKILL.md` | 标准工作流与配置来源说明增加 `--set` |

### 明确不改

- 任何 `Config` 类、`Service`、`ViewModel`、View；
- `ToolCatalog`、`ToolDescriptor`、`ToolRunResult`、退出码；
- `configs.json` 结构；
- GUI 行为；
- `build.ps1`、发布配置、依赖版本。

## 10. 实现步骤

1. **实现覆盖器**
   - 新增 `ConfigOverrides`；
   - 完成赋值解析、属性查找、标量转换、JSON 反序列化、`SecurePassword` 明文回退、错误消息；
   - 不接入 CLI，先保证可独立调用。

2. **补单元测试**
   - 新增 `ConfigOverridesTests`；
   - 覆盖第 11 节列出的维度；
   - 运行针对性测试，确认全绿。

3. **接入 CLI**
   - `CliArguments` 增加 `set`；
   - `ToolCommand` 在 `LoadConfig` 后、第一次 `PathGuard` 前调用；
   - 在 `ListCommand`、`DescribeCommand`、`DocsCommand` 调用 `CommandContext.EnsureNoOverrides`；
   - 确保 `--set` 错误在工具启动前返回 2。

4. **更新帮助与文档**
   - `Program.PrintHelp` 增加用法与示例；
   - `HumanOutput.WriteDescribe` 增加提示；
   - README、AGENTS、SKILL 同步；
   - 不手改 `skill/assets` 生成物。

5. **构建与手工验证**
   - `dotnet build ArchiveMaster.Cli`；
   - 临时目录中用 `plan` / `run` 验证字符串、嵌套字段、重复覆盖、JSON 复杂值、`SecurePassword`、错误路径；
   - 用 `--allow-root` 验证越界；
   - 确认 `configs.json` 未被写回。

6. **回归**
   - 运行相关单元测试；
   - 阶段四再按用户选择做全量单测 / e2e / 发布测试。

## 11. 测试方案

### 11.1 单元测试

新增 `ArchiveMaster.Tests/Core/ConfigOverridesTests.cs`，使用 `TimeClassifyConfig`、`DirStructureSyncConfig`、`FileFilterOperationConfig`、`EncryptorConfig` 等真实配置类型。

覆盖维度：

1. 顶层字符串赋值；
2. 嵌套 `PathFilter.IncludeFiles` 赋值；
3. `bool` 的 `true` / `false` / `1` / `0`；
4. 整数、浮点、`decimal`、`char`、`TimeSpan`、`DateTime`、`DateTimeOffset`、`Guid` 与可空类型；
5. 枚举名称与数字；
6. 重复字段后者覆盖前者；
7. 大小写不敏感匹配；
8. 值中包含 `=`；
9. 缺少 `=`；
10. 空字段名；
11. 未知字段；
12. 只读字段；
13. 复杂对象 JSON 赋值（`PathFilter`）；
14. 集合 JSON 赋值；
15. 非法 JSON；
16. JSON `null` 拒绝；
17. 空字符串对 `string` 与对非 `string` 的差异；
18. 嵌套对象为 `null` 时的创建或报错；
19. 嵌套路径穿过标量时的报错；
20. `SecurePassword` 明文、JSON 字符串、64 位十六进制明文与 JSON 对象的处理。

### 11.2 CLI 命令级验证

`CliCommandPolicyTests` 覆盖 `list` / `describe` / `docs` / `version` / `--version` / `help` / `--help` 带 `--set` 时返回 2，以及 `plan` / `run` 放行。

阶段四再用临时目录做 `plan` / `run`、`--config` + `--set`、`--preset` + `--set`、`--allow-root` 越界与“不写回 `configs.json`”的端到端验证。

### 11.3 回归

- `dotnet test ArchiveMaster.Tests`（阶段四由用户选择是否跑全量）；
- `dotnet build ArchiveMaster.Cli`；
- 原有 CLI e2e 脚本不回归。

## 附：未决问题

无。两轮追问的决策已全部并入本计划。
