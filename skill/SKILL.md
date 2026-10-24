---
name: archive-master
description: 通过 am 命令行发现并运行 ArchiveMaster 的批量文件工具：按时间分类、重命名、去重、加密、照片瘦身、地理信息写入、编码转换、校验等。需要在不打开 GUI 的情况下批量整理或归档本机文件时使用；AI 对话类工具不在其中。
whenToUse: 用户要求批量整理、分类、重命名、去重、加密、压缩照片、写入照片地理信息、转换编码、校验文件完整性，且本机装有 ArchiveMaster（am 命令）时。
---

# ArchiveMaster CLI（am）

`am` 是 ArchiveMaster 的命令行入口。它把 GUI 里“选择工具 → 配置 → 初始化 → 执行”的流程拆成可脚本化的命令，输出机器可读的 JSON，供 AI 判断与汇报。

本技能只覆盖 29 个一次性批处理工具；AI 对话类（文本智能生成、文档智能搜索）不在范围内。

## 前置检查

1. 确认命令可用：`am --version`。
2. 确认目标工具存在：`am list --json`，按 `id` 选择。
3. 需要复用用户已保存的配置时，确认 `--profile` 指向含 `configs.json` 的目录；否则用 `--config` 传一次性配置。

## 核心规则

- **先 `plan`，后 `run`**。`plan` 执行工具的准备阶段并列出待处理项；把条目数、总大小与抽样给用户看，得到明确同意后才执行 `run --yes`。
- **临时改单个字段用 `--set`**。可重复，覆盖 `--config` / `--preset` 中的同名字段；嵌套字段用点号（`PathFilter.IncludeFiles=*.jpg`），集合/对象用 JSON。只有 `plan` / `run` 支持 `--set`。
- **不要替用户猜路径**。配置里的目录、文件必须来自用户输入或已确认的上下文；`--allow-root` 用于把操作限制在用户指定的根目录内。
- **`plan` 不是纯只读**。准备阶段可能创建目录或中间产物；不要向用户承诺“什么都没改”。
- **`run` 会重做准备阶段**。`plan` 与 `run` 是两次进程，`run` 不会复用上一次 `plan` 的结果；两次之间文件系统变了，以 `run` 时为准。
- **失败要如实说**。有 Error/Warn 条目或 `error` 字段时逐条汇报，并给出报告路径；不要用“已完成”掩盖部分失败。
- **不执行未经用户同意的删除/覆盖**。`--yes` 只在用户确认后才加。
- CLI 不写 `configs.json`（也不会处理其中的 `.load` 待加载文件）；但会在 profile 目录创建（并保留）`.am.lock`，并可能创建 profile 目录本身。

## 标准工作流

```bash
# 1. 找工具
am list --json

# 2. 拿配置模板（describe 的整个输出可以直接作为 --config 输入，
#    CLI 会自动取其中的 config 子树）
am describe fileTools.timeClassify --json > cfg.json
#    按用户意图编辑 cfg.json

# 3. 准备（第一阶段）
am plan fileTools.timeClassify --config cfg.json --json

# 4. 用户确认后执行（内部重做第一阶段 + 第二阶段）
am run fileTools.timeClassify --config cfg.json --yes --json

# 也可以只覆盖少量字段，不必复制整份配置
am run fileTools.timeClassify --config cfg.json --set Dir=D:\input --yes --json
```

也可以把配置从标准输入传入：`--config -`。

## 配置来源

| 来源 | 用法 | 说明 |
| --- | --- | --- |
| 一次性配置 | `--config <文件\|->` | `describe` 的完整输出，或只取 `config` 子树；PascalCase，与 GUI 一致 |
| 已保存预设 | `--profile <目录> --preset <名称>` | 读取 `<目录>/configs.json` 中该工具的预设；省略 `--preset` 时用“默认” |
| 字段覆盖 | `--set <字段>=<值>` | 可重复，覆盖前两种来源；嵌套点号、复杂 JSON；仅 `plan` / `run` |

`--config` 与 `--preset` 互斥；`--preset` 指向不存在的名字会直接报错，不会静默使用默认配置。`--set` 可与任一种来源组合，覆盖同名字段。

默认 profile 位于用户数据目录下的 `ArchiveMaster/cli`（Windows 为 `%LOCALAPPDATA%\ArchiveMaster\cli`）。要复用 GUI 的配置，显式传 `--profile <GUI 配置目录>`；GUI 的 `configs.json` 在 GUI 可执行文件旁。

## 输出约定

`--json` 时 stdout 只有一个对象，进度与提示在 stderr：

```json
{
  "schemaVersion": 1,
  "tool": "fileTools.timeClassify",
  "stage": "prepare",
  "canceled": false,
  "startedAt": "2026-10-23T05:00:00+08:00",
  "durationMs": 123,
  "summary": { "total": 2, "success": 0, "warn": 0, "error": 0, "skip": 0, "cancel": 0, "bytes": 1024 },
  "itemsAvailable": true,
  "items": [
    { "name": "2024-01-01 10:00:00 ~ 10:20:00", "length": 1024, "isDir": true, "childCount": 3, "status": "Ready" }
  ],
  "notes": [],
  "itemsTruncated": false,
  "report": "C:\\Users\\...\\Temp\\archivemaster\\20261023-130000-fileTools.timeClassify.jsonl"
}
```

- `items` 默认最多 50 条；完整结果始终写入 `report`。report 是 JSONL：首行是运行摘要（含 `error`、`notes`、`summary`），其后每行一个条目。需要更多条目用 `--max-items <N>`（默认值也可由 `configs.json` 的 `Globals.CliMaxItems` 覆盖）。
- 分组型工具（如按时间分类）的顶层条目是目录/分组，`name` 有意义、`path` 可能为空；`childCount` 是组内文件数，`summary.bytes` 已包含组内文件大小。
- 条目上的 `extra` 会带出工具自有字段中的简单值（检测编码、新路径、时间统计等）；复杂对象（如完整 `VideoInfo`）不会展开，必要时看 `notes` 或工具自身输出。
- 部分工具不提供条目级结果：`itemsAvailable` 为 false，`notes` 给出可读摘要（如 `Packages.Count=3`）。**这并不代表没有执行**，不要据此判断任务为空。
- 工具抛出异常时仍会输出 JSON/报告，`error` 字段非空、退出码 1。

## 退出码

| 码 | 含义 | 你该做什么 |
| --- | --- | --- |
| 0 | 成功 | 汇报结果 |
| 1 | 有 Warn/Error 条目，或工具异常 | 逐条说明，引用报告 |
| 2 | 参数、配置、路径、锁错误 | 按 stderr 修正后重试 |
| 3 | 用户取消 | 说明已完成部分 |
| 4 | 缺少 `--yes` | 先 plan 并征得同意 |

## 安全边界

- 配置中的绝对路径、含分隔符的相对路径、`..` 路径、多值路径与准备结果里的条目可被 `--allow-root` 限制（可重复），越界会直接失败并指出路径。这是尽力而为的护栏，**不是安全沙箱**：简单的相对目录名（如 `out`）、`fileTools.batchCommandLine` 启动的任意程序、工具执行时新算出的输出路径都可能超出白名单，仍需用户确认。
- `--set` 传入的密码等敏感值会留在 shell 历史和进程列表；优先用 `--config` / `--preset`。
- `tool-catalog.json` 中 `prepareUnsafe: true` 表示该工具的准备阶段已知会写文件（当前只有 `writeOnceArchive.packing`）。
- 同一 profile 同时只允许一个 `am` 进程；锁冲突退出码 2。
- 报告文件含本机路径，分享前提醒用户检查。
- HEIC 输出依赖随程序分发的原生库；缺失时对应工具会报错，不是 CLI 的问题。

## 工具细节

- 逐个工具的配置模板、字段表与能力标记：见 `assets/tool-catalog.json` 与 `assets/tools/<id>.md`。
- 没有准备阶段的工具（`offlineSync.step1`、`photoTagging.photoTagManager`、`photoTagging.photoTagSearcher`、`textTools.typoChecker`）不能 `plan`，只能直接 `run --yes`；执行前务必让用户确认配置。
- `offlineSync.step2` 在 `MatchingDirs` 为空时会按 `LocalDir` 与快照自动匹配目录，`LocalDir` 应填包含快照顶级目录的父目录。
- AI 辅助类工具（`photoTagging.photoTagGenerator`、`textTools.typoChecker`、`textTools.lineByLineProcessor`）需要 `configs.json` 的 `Globals.AiProviders` 中已配置可用的 AI 提供商。
- 发布用 `dotnet publish ArchiveMaster.Cli\ArchiveMaster.Cli.csproj -c Release`，并把 `native/<rid>` 一起带上。**实际使用请用 Release**：Debug 构建下 `fileTools.batchCommandLine` 只处理前 10 个文件（工具自身的调试限制）。

## 维护

工具目录随代码生成，不要手改：

```bash
am docs --out skill        # 重建 assets
am docs --install --source skill   # 安装到 ~/.agents/skills/archive-master
```
