# AWAKE 长效 Skill 架构建设计划

## 目标

建立一套不依赖版本号、候选 BuildId、红测阶段或当前功能线的 AWAKE skill 架构。Skill 只负责稳定的决策与操作边界；当前任务、候选、证据与阻断项只存在于项目状态和证据文件中。

本计划不修改 AWAKE Runtime 行为，也不授权游戏目录同步或游戏启动。

## 实施状态（2026-09-11）

- 已完成：`awake-task-continuity` 路径与恢复规则调整。
- 已完成：`CURRENT.schema.json` 控制面 schema。
- 已完成：`bannerlord-runtime-evidence` 及其正向 fixture 验证。
- 已完成：`bannerlord-module-sync-integrity`、`marcus-framework-contract-change`、`awake-content-runtime-boundary` 及三类正反 fixture 验证。
- 待完成：从现有 `AWAKE-CURRENT.md` 提取唯一活动事实并生成首个 `CURRENT.json`。当前摘要同时出现 009 交付、010 当前源码和 004 红测历史，不能无审查自动选择其中一个作为活动批次；迁移必须先建立明确的 control-plane migration checkpoint。
- 待完成：对新 skill 运行正式 `quick_validate.py`。当前机器未安装 Python 3，初始化器和该验证器无法执行；已用 frontmatter 检查、PowerShell 解析、正反 fixture 作为替代证据，不能将其包装为等价验证。

## 设计原则

1. **按边界路由，不按进度路由。** 不创建 `redtest-004`、`dialogue-010` 这类会自然过期的 skill。
2. **单一职责与单一权威。** 状态恢复、实机证据、模块同步、Framework 公共契约、内容运行时边界分别只有一个 owner。
3. **状态不进入 skill。** BuildId、哈希、计划路径、P0/P1、版本目标和用户待办由状态文件管理，skill 只定义读取和验证规则。
4. **机械判断脚本化。** 日志关联、哈希一致性、BuildId 归属、证据完整性必须由确定性脚本完成，模型只解释异常和决定下一步。
5. **渐进加载。** `SKILL.md` 只保留路由、不可违反的约束与最低流程；具体 schema、日志字段和脚本说明置于按需 references。
6. **现有通用能力复用。** 不复制 Bannerlord 调度、执行 lease、构建、测试、集成契约或设计审查能力。

## 长期职责图

```text
Bannerlord 请求
  -> bannerlord-mod-development-orchestrator     # 生命周期与候选边界
     -> awake-task-continuity                    # 恢复当前状态
     -> marcus-framework-contract-change         # 仅改嵌入式 Framework 公共面时
     -> awake-content-runtime-boundary           # 仅改内容包与 Runtime 边界时
     -> test-strategy-and-quality                 # 测试层选择
     -> integration-contract-testing              # IPC/文件/存储/Provider/序列化边界
     -> bannerlord-module-sync-integrity          # 打包、同步、回滚或哈希核验时
     -> bannerlord-runtime-evidence               # E4/E5 实机或存档证据时
```

## Skill 清单

### 保留并调整

| Skill | 长期职责 | 调整内容 |
| --- | --- | --- |
| `awake-task-continuity` | 从中断、切换和游戏验证暂停中恢复唯一当前任务 | 去除 `_houkai_merge` 硬编码路径；改为读取工作区内的 CURRENT 指针；禁止全文读取历史状态 |
| `bannerlord-mod-development-orchestrator` | Bannerlord 任务分类、候选冻结、授权和证据层级 | 保持为主路由；不加入同步、实机日志或 Framework API 的细节 |
| `long-horizon-short-task-execution` | 已批准批次内的短任务和 checkpoint | 保持；只读取 compact state 与当前 checkpoint |
| `grill-me-codex` | 高风险设计与独立只读挑战 | 仅用于新公共契约、存档、跨模块或不可逆决策；不作为红测执行门 |

### 新建

| Skill | 触发条件 | 唯一职责 | 明确排除 |
| --- | --- | --- | --- |
| `bannerlord-runtime-evidence` | 需要 E4/E5、用户实机、日志归属、存读档验证 | 生成/校验实机 checklist，绑定日志会话、BuildId、哈希、测试身份与证据 verdict | 不启动游戏，不同步模块，不修改 Runtime |
| `bannerlord-module-sync-integrity` | 打包、source/dist/game/test package 核验、同步、回滚 | 检查目标进程、受管文件、哈希、白名单、同步报告与回滚边界 | 不决定发布版本，不替代构建，不擅自覆盖游戏目录 |
| `marcus-framework-contract-change` | 改 embedded Marcus Framework 的 public API、DI/注入、IPC 能力、权限或协议 | 审计 API 面、默认路径兼容、调用方、跨版本/协议契约与最小验证 | 不用于普通 AWAKE 调用方改动；不与“子 Mod 使用 API”的 extension skill 重叠 |
| `awake-content-runtime-boundary` | 内容包、世界书、Persona、ContentPolicy、Runtime 加载/投放/降级发生变化 | 定义内容权威、包格式、加载失败降级、部署与 Runtime 的隔离证据 | 不创作内容正文，不代替 Studio/内容侧工具工作流 |

## 状态与证据契约

建立下列项目内控制面文件；它们是事实源，skill 不复制其内容：

```text
docs/control-plane/
  CURRENT.json                 # 唯一活动任务与候选摘要
  candidates/<build-id>.json   # 候选身份、产物、哈希与同步状态
  evidence/<build-id>.json     # E1-E5 证据索引和 verdict
  checkpoints/<task-id>.json   # lease 内短任务恢复点
```

`CURRENT.json` 必须最多只包含：`active_batch`、`candidate`、`task_status`、`evidence_level`、`blocker`、`checkpoint_path`、`next_action` 和更新时间。`AWAKE-CURRENT.md` 改为人类摘要和到该文件的链接，不再复制历史候选记录。

每一条 E4/E5 证据至少绑定：BuildId、模块版本、程序集 SHA-256、日志会话起止、测试场景/存档身份、用例 ID、可观察结果、verdict 和原始证据路径。缺任何归属字段的日志只能作为诊断材料，不得升级证据等级。

## 每个新 Skill 的最小结构

```text
<skill-name>/
  SKILL.md
  references/                 # 仅按模式读取
  scripts/                    # 只放重复且确定性的检查
```

建议脚本职责：

| Skill | 首个脚本 |
| --- | --- |
| `bannerlord-runtime-evidence` | `validate-runtime-evidence.ps1`：校验 BuildId、哈希、日志会话、必需事件和用例 verdict |
| `bannerlord-module-sync-integrity` | `verify-module-sync.ps1`：校验 source/dist/game/test package 受管文件及白名单 |
| `marcus-framework-contract-change` | `verify-framework-contract-change.ps1`：校验 public API 基线、默认装配、调用方和协议 fixture |
| `awake-content-runtime-boundary` | `verify-content-runtime-boundary.ps1`：校验包 schema、内容来源、加载降级与部署隔离 |

脚本只产生 JSON/文本证据；不得写入游戏目录、修改候选或访问真实 Provider。

## 迁移顺序

1. **状态连续性。** 调整 `awake-task-continuity`，定义 `CURRENT.json` schema，并从现有 `AWAKE-CURRENT.md` 提取唯一活动事实。验证：任意新会话可只读 CURRENT 与命名 checkpoint 恢复唯一下一动作。
2. **实机证据。** 新建 `bannerlord-runtime-evidence` 和日志/证据校验脚本。验证：故意使用错误 BuildId、错误哈希、缺日志字段的 fixture 必须拒绝；完整 fixture 必须定位到一个用例 verdict。
3. **模块同步。** 新建 `bannerlord-module-sync-integrity`。验证：受管文件不一致、运行中进程、未声明白名单与错误目标路径必须拒绝。
4. **Framework 契约。** 新建 `marcus-framework-contract-change`。验证：公共 API 改动、默认路径污染、调用方未接线、协议版本不符至少各有一个失败 fixture。
5. **内容运行时边界。** 新建 `awake-content-runtime-boundary`。验证：格式不匹配、内容缺失、未授权 Runtime 写入和错误部署目标均有明确 fail-closed verdict。
6. **文档收敛。** 将 `AWAKE/AGENTS.md` 缩减为路由规则和项目不变量，删除与上述 skill 重复的细节；更新 skill 路径引用并移除失效引用。

每一步均为独立、可审查批次；若涉及现有 Runtime 行为、公开契约或同步语义，先经过适用的设计/审查门并取得用户签收。

## 验收标准

架构建设完成的必要条件：

1. 当前环境能发现所有列出的 skill，且每个新 skill 通过 `quick_validate.py`。
2. 每个新 skill 的 description 能区分触发条件，且不吸引无关任务。
3. 所有 skill 都不包含 BuildId、版本号、历史计划文件名、固定游戏日志结论或当前 P0/P1。
4. `awake-task-continuity` 不再引用 `_houkai_merge`，且 CURRENT 恢复不需要读取历史队列。
5. E4/E5、同步、Framework 契约、内容边界各有一个确定性校验脚本和正反 fixture。
6. 现有 `bannerlord-mod-development-orchestrator`、`grill-me-codex`、构建与测试 skill 没有被重复实现。
7. 从空白新会话执行一次“当前状态恢复”、一次“无效实机证据”、一次“哈希不一致同步前检查”时，均能在不读取历史长文的情况下得到唯一且可解释的结果。

## 非目标

- 不将当前红测、对话链、Persona 或 Worldbook Studio 的功能计划包装成 skill。
- 不自动运行 Bannerlord、写入游戏目录、使用真实 Provider 或消耗 API Key。
- 不把项目规则、历史审查记录或内容正文复制到 skill 内。
- 不改变 AWAKE Runtime 的架构权威或内容包边界。
