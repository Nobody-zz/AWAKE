# AWAKE / Persona Platform 新对话交接

> 生成日期：2026-08-21
> 目的：新对话只需读取本文件、`AGENTS.md`、`docs/AWAKE-CURRENT.md` 和当前 checkpoint，即可继续工作；不要从历史聊天或完整任务队列恢复状态。

## 1. 新对话启动顺序

1. 读取工作区与 AWAKE 嵌套 `AGENTS.md`。
2. 使用 `awake-task-continuity` 与 `bannerlord-mod-development-orchestrator`。
3. 读取：
   - `docs/AWAKE-CURRENT.md`
   - `docs/checkpoints/PERSONA-PLATFORM-20260821-checkpoint.md`
   - 本文件
4. 当前实现任务使用：
   - `docs/PLAN-PersonaWorkbench-TokenEfficiency-20260821.md`
   - `docs/PLAN-PersonaWorkbench-TokenEfficiency-REVIEW-LOG-20260821.md`
5. 不要完整读取 `docs/AWAKE-Task-Queue-20260816.md`，除非需要追溯历史证据。

## 2. 当前主线

- `task_id`: `PERSONA-PLATFORM-20260821`
- 角色卡已重新定位为 Persona Platform，不再只是“描述转 DSL”。
- 平台职责分层：
  1. Persona Workbench：作者编辑、AI 扩写、审核与独立工具体验。
  2. Persona authoring document：来源、事实、观察、规则、审核和迁移数据。
  3. Persona compiler：把已审核内容确定性编译为 `[PERSONA_LOAD]`。
  4. AWAKE runtime：合并当前游戏硬事实、关系、记忆、场景和时间线后注入 Prompt。
- DSL 是可重建运行时投影，不是唯一真相来源。
- 角色身份变化不得抹掉人格核心与历史经历；当前家族、王国、婚姻和职业以游戏硬事实为准。

## 3. 当前 Workbench 基线

- 最新本地已验证包：`tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-r46-20260821`
- r46 的 release whitelist、自包含发布、launcher、Windows PowerShell 编码和 SHA-256 manifest 已通过；Web executable SHA-256 为 `09FF78DC48978DF3A9BDDAA342677ECBAAAFF247853F964ACCDBBB72720D8947`。
- 当前 canonical preview：
  - `[PERSONA_LOAD]`
  - `[PERSONA_CONSTRAINTS]`
  - `[PERSONA_IDENTITY]`
  - `[PERSONA_APPEARANCE]`
  - `[PERSONALITY_CORE]`
  - `[PERSONALITY_PUBLIC]`
  - `[PERSONALITY_PRIVATE]`
  - `[PERSONALITY_CONTRADICTION]`
- 使用英文稳定 token；中文只放在显式 `DATA_CN` 数据行。
- 七条固定约束 token 保留。
- 旧 `[persona.*]` renderer 仅作兼容回退。
- 已验证过的基线包括 Core/Web、Release build、launcher、cold start、stop、diagnostics、Windows PowerShell、package manifest 和 loopback Provider 端到端；直播 Provider 结果仍需用户环境证据。

## 4. 当前实施批次：Token Efficiency

计划已经过五轮只读对抗审查并得到：`VERDICT: APPROVED`。

权威文件：

- `docs/PLAN-PersonaWorkbench-TokenEfficiency-20260821.md`
- `docs/PLAN-PersonaWorkbench-TokenEfficiency-REVIEW-LOG-20260821.md`

当前 checkpoint 表明实现已经开始，而不是单纯等待批准。已实现内容：

- 压缩 DSL conversion system prompt，同时保留 25 个 axis、2 个 flag、稀疏证据和既有 response schema。
- 严格解析和传播可选 `ProviderUsage`。
- sparse parser 增加 `documentSourceText` / `evidenceSourceText` 双来源职责。
- 浏览器只做临时 usage 展示，不持久化遥测。
- Usage 边界测试、Core/Web/Browser smoke 和 Release build 已通过。

本批本地收尾状态：

1. 浏览器 usage 生命周期断言已实现并通过。
2. 原始 24 KiB 输入失败路径回归已覆盖既有 Provider status、错误码、quarantine 和 HTTP action response。
3. CRLF/LF、首尾 Unicode 空白和双来源语义回归已通过。
4. display name、自动 ID、`SourceDescription` 保持不变的回归已通过。
5. r46 package manifest、launcher、自包含发布和 Windows PowerShell 编码门禁已通过。
6. 剩余仅为可选的用户环境真实 Provider `usage` 样本；没有该样本时不得声称真实模型 token 数，但不阻塞本地批次完成。

## 5. K1 与后续平台计划状态

### K1 Contract Lock

- `docs/PLAN-PersonaWorkbench-K1-ContractLock-20260821.md`
- 审查结果：`VERDICT: REVISE`
- 不允许按原 K1 计划直接实现。
- 主要问题：真实 v1 registry 映射不闭合、`sourcePackId` 去向不清、ID grammar 与现有连字符冲突、迁移状态会产生虚假 approved、现有 UI 缺逐 observation 审核、trigger/boundary 无完整编译语义、直接替换现有 generator 会破坏 r45 契约。
- 推荐拆分：
  - K1A：v2 DTO、真实 v1 migration、registry closure、rule-free token compiler。
  - K1B：基于真实 trigger/response 示例的规则编译。
  - K2：审核 UI。
  - K3：Provider observation/expansion contract。
  - K4：AWAKE runtime integration。
  - K5：save/timeline integration。

### Expansion Guidance

- `docs/PLAN-PersonaWorkbench-ExpansionGuidance-20260821.md`
- 审查结果：`VERDICT: APPROVED`
- 仅批准 Workbench AI prose expansion guidance；不批准 K1、AWAKE runtime 或游戏同步。
- 核心原则：人物描述是唯一声明事实源；direction/focus/keyword/avoid 只是写作控制；Provider 输出始终是未审核可编辑草稿。

## 6. 冻结的 AWAKE 运行时候选

- 版本：`v0.2.0`
- BuildId：`awake-20260820-syncpack-001`
- source/dist/game DLL SHA-256：`F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7`
- Worldbook manifest SHA-256：`2115664E1BB6BB26E63D8CB2097B891B51C03A00504D76DF6F8520FD9448468A`
- E3：完成；release status 为 `CANDIDATE_SYNC_OK`。
- E4/E5：用户暂不想测试游戏，因此保持 `pending_game`，不视为失败。
- 禁止修改该冻结候选 DLL；任何 AWAKE runtime 改动必须使用新 BuildId 和新候选。
- 不启动 Bannerlord，不要求用户现在进行游戏测试。

## 7. 工作边界

- Token Efficiency 本地实现与 r46 封包已经完成；下一批仍不得触碰 AWAKE runtime、dist 或游戏目录，除非另立计划并获批。
- Persona Platform 新功能仍须遵守 grill/review 流程。
- local_worker 只用于明确传入文本/JSON的机械初筛，不扫描目录、不修改文件、不做最终裁决。
- 遇到 429、取消或外部不确定结果立即 checkpoint 并停止，不自动重试。
- 不把编译、smoke 或 loopback Provider 测试冒充真实 Provider 或游戏内证据。

## 8. 新对话唯一推荐下一步

先确认 r46 与 checkpoint 状态；如用户愿意，可采集一次真实 Provider `usage` 样本。随后从 Persona Platform 待办中选择下一批，优先修订 K1A 计划或实施已批准的 Expansion Guidance，但必须保持与冻结 AWAKE 候选隔离。

不要直接开始 K1A、AWAKE runtime integration、游戏测试或新版本提版。