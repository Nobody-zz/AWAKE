# Plan Review Log: AWAKE Worldbook Studio 第一版

Act 1（需求拷问）完成：已与用户逐项锁定工具边界、资料分层、源文件形式、本地化显示、AI 可选性、导出流程和审阅方式。

MAX_ROUNDS=5

## Act 1 decisions

- 第一阶段先做独立开发工具和验证流程，不直接替换当前 AWAKE 读取器。
- 工具采用本地 Web，不启动游戏；编辑、编译、导出、同步分离。
- 游戏数据只负责当前运行状态；AWAKE 开发者正典负责历史、文化、动机和因果解释。
- 卡拉迪亚编年史和卡拉迪亚之王资料是参考输入，现有四版成人化世界书是扩展参考，所有内容必须经开发者裁定。
- 普通作者使用本地化界面，高级作者使用结构化源文件，程序开发者使用内部数据和 CLI。
- AI 是可插拔可关闭的辅助层，不能自动写入正式正典。
- 第一版采用本地版本快照和审阅状态，不做在线多人协作。

## Round 1 — Codex

### Verdict
`VERDICT: REVISE`

### Findings incorporated

- 第一版范围过宽，没有冻结最小字段、页面、CLI 和样本规模。
- Schema、迁移、错误级别、未知字段和运行时映射没有锁定。
- YAML/Markdown 双源的写入权不明确。
- 来源、时代、宇宙和 Quote 定位不够可追溯。
- AI 建议缺少完整 provenance 和不可编译隔离。
- NPC 权限组合、拒绝优先级和解释链不完整。
- 稳定 ID、alias/redirect 和文件名回退存在迁移风险。
- 多语言、关键词、Unicode 归一化和运行时目录 ownership 需要明确。
- Token 预算、性能、导入安全、原子写入、失败回退和 fixture 矩阵不足。
- Persona Workbench 复用边界、Studio 自身构建契约和游戏同步硬阻断需要补充。
- 成人内容的默认隐藏和导出门禁需要进入 Schema 与验收。

### Revision
将计划收窄为四条 MVP 垂直切片：来源登记、YAML 单一规范源、Schema/确定性编译、NPC 预览/权限解释；AI 延后为仅建议接口；补充来源不可变元数据、稳定 ID 迁移、权限优先级、运行时映射报告、目录 ownership、离线 fixture、导入安全、原子导出和成人层门禁。

## Act 2 status

Round 1 已完成；计划已修订，等待同一只读审查会话复审。

## Round 2 — Codex

### Verdict
`VERDICT: REVISE`

### Remaining findings incorporated

- 仍需把 Schema、运行时映射、目录所有权和 fixture 在用户签收前冻结，而不是留作实现阶段开放问题。
- Schema 需要真实机器可校验文件，且 assertion/expression 级别要有独立来源或原创声明。
- `author_created` 必须带作者、理由、审阅状态和审计事件，不能直接绕过正典门槛。
- 来源需要 SourceContentHash、规范化引用、稳定 locator、许可证/使用状态和失配硬失败。
- v2 候选必须有独立目录和 incompatibility marker，证明当前 v1 加载器不会误消费。
- authority、grant/deny、deny 优先、解释链 DTO 分离、实体生命周期和 redirect 规则需要变成确定性契约。
- 纯净导出必须做跨别名、redirect、缓存、索引和来源摘录的 content-tier 闭包检查。
- warning 确认、路径 realpath、原子替换、fixture 最小数量、无 AI E2E、工具链版本和复用边界需要可执行定义。

### Revision
新增 `docs/worldbook-studio-plan/` 前置契约包：机器可校验 Schema、v1/v2 运行时映射、目录所有权、fixture 验收矩阵和工具链契约；将五个文件的 SHA-256 写回计划，并补充 additional hard gates。仍待同一只读会话复审。

## Round 3 — Codex

### Verdict
`VERDICT: REVISE`

### Remaining findings incorporated

- `sources` 与 `author_created` 的 XOR、原创正典批准条件和 assertion 级来源需要由 Schema 强制。
- profile 必须有冻结注册表、继承规则和版本校验。
- grant/deny 的 OR/AND、空集合、scope、min_detail 和 deny 优先级需要写成正式契约。
- authority owner 与 conflict policy 的非法组合需要 Schema 拒绝。
- redirect 需要 typed from/to/reason/event_id；所有对象采用带类型命名空间的稳定 ID。
- 多语言字段必须是 locale map，关键词和正文翻译边界需要固定。
- 成人层必须覆盖所有 typed reference 的引用闭包。
- 编译输入必须显式白名单，suggestions 及间接引用不得进入编译。
- v2 候选必须放在旧加载器不会扫描的独立目录，并做旧加载器负向回归。
- 原子导出、CLI、退出码、报告路径、离线网络拦截和 .NET 10.0.301 依赖锁定需要可执行。

### Revision
更新机器可校验 Schema，新增 `PROFILE-REGISTRY.md`、`PERMISSION-CONTRACT.md`、`ID-MIGRATION-CONTRACT.md`，扩展 fixture/CLI 契约和工具链契约，更新附件哈希及计划硬门槛。仍待同一只读会话复审。

## Round 4 — Codex

### Verdict
`VERDICT: REVISE`

### Remaining finding incorporated

- 表达层 `expression` 尚未具有独立的 `sources XOR author_created` 契约，导致无来源原创表达无法被机器审计。

### Revision
为 `expression` 增加独立来源/原创声明 XOR；同步更新 Schema 哈希，并把 `status=canon` 的原创表达层批准状态和审计事件加入计划硬门槛。

## Round 5 — Codex

### Verdict
`VERDICT: REVISE`

### Remaining unresolved blocker

- `expression` 已有 `sources XOR author_created`，但 Schema 尚未根据父档案 `status=canon` 强制原创 expression 的 `review_status=approved`，且 `review_event_id` 仍允许为 null；需要编译器遍历所有 canon assertion/expression，要求原创项为 `approved`、`review_event_id` 非空且匹配 `event.*`，并新增失败 fixture。

### Resolution status

已达到 `MAX_ROUNDS=5`，未得到 `VERDICT: APPROVED`。不伪造收敛，不开始实现。该单一阻塞点交由用户决定是否按建议修正并重新启动一轮新的审查/签收流程。

## Resolution after Round 5

用户已确认按审查建议修正：

- `review_event_id` 改为非空、格式为 `event.*` 的稳定 ID。
- `status=canon` 的原创 assertion/expression 必须由编译器确认 `review_status=approved`，否则阻止编译。
- 新增 F17 失败 fixture：原创正典表达层未批准或缺少有效审计事件时，必须返回 `WB-CANON-001`。
- 已重新计算并更新 Schema 与 fixture 附件哈希。

计划仍等待用户对“开始实现 Worldbook Studio MVP”的最终签收；本次只完成计划契约修正，没有写入运行时代码或游戏目录。

## Round 6 — Independent re-review after Round 5 revision (2026-08-21)

### Verdict
`VERDICT: REVISE`

### Findings

- F17 的批准约束已补入 Schema，但有效 `event.*` 仍需要独立审计事件登记与对象/hash 闭合；已新增 `AUDIT-EVENT-CONTRACT.md` 并拆分 F17-A 至 F17-E。
- v1/v2 隔离不能只依赖 marker；已改为输出路径硬阻断、真实 v1 loader 负向 fixture 和显式适配拒绝。
- 统一 `universe`、`summary`、`content_tier` 字段，补入 `lifecycle`；新增来源登记和内容层闭包契约。
- 仍需重新计算全部附件哈希并进行下一轮只读复审，未获得 APPROVED 前不实现代码。
## Round 7 — Independent re-review after second revision

### Verdict
`VERDICT: REVISE`

### Findings incorporated

- F15 改为不自动扫描/输出路径硬阻断，不再声称旧 loader 对手工注入 v2 会拒绝消费。
- 审计事件增加 revision、canonical object hash、event hash 链和 append-only 路径。
- 内容层增加 content graph 与统一 `--content-tier`；Windows 发布改用 `current.json` 指针。
- 增加 profile/referral 机器注册表、source registry schema、source version 绑定和 ID ledger。
- 修复后需重新计算 Schema 哈希；尚未重新取得 APPROVED，不能开始实现。
## Round 8 — Independent re-review after third revision

### Verdict
`VERDICT: REVISE`

### Findings incorporated

- `valid_until`、lifecycle `event_id`、audit-event/ID ledger/current pointer 机器 Schema 已加入。
- 内容图节点/来源推导、F15 探针证据、NPC preview/author diagnostics DTO 边界已明确。
- Profile 文档控制字符、版本不一致和 SourceNature 枚举缺失已修复。
- 待新审查确认是否已达到可实现闭包；当前仍未开始实现。