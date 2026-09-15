# AWAKE 对话生命周期与可观测性修复

- 批次：`AWAKE-DIALOGUE-LIFECYCLE-OBSERVABILITY-20260913`
- 状态：`revised_after_review_round_2`
- 来源：`docs/AUDIT-AWAKE-DIALOGUE-CHAIN-010-20260911.md` 的 P1
- 范围：仅 AWAKE 运行时人物对话链；不涉及世界书正文、角色卡编辑器、UI Lab、游戏目录或同步。

## 目标

让一轮人物对话在本地证据中能明确区分“尚未可用”“已成功完成”“因关闭而取消”，并保证用户拒绝指令提案时不会发生结算写入。用户已经确认的指令视为已提交：关闭对话窗口不撤销其结算；未确认提案则随关闭取消。

当前最小闭环：

`NpcDialogueLauncher -> NpcDialogueService 初始化/发送 -> AI 完成 -> 输出校验 -> 指令确认或普通完成 -> UI 状态/完成事件 -> 转录与可追溯日志`。

## 已确认问题

1. 初始化调用 `EnsureCurrentHeroBoundAsync` 后未消费失败结果，可能先显示准备就绪，再在发送时失败；面板关闭后，初始化任务也可能继续完成。
2. 输出校验成功并派发 UI 完成事件后，没有稳定的正向完成日志；直答、指令确认和拒绝路径也不能遗漏该日志。
3. `NpcDialogueVM` 发送使用的生命周期取消语义不足；关闭、换目标、重开面板时，早期准备工作可能继续。

## 修复边界

修改候选文件：

- `src/NpcDialogueService.cs`
- `src/NpcDialogueModels.cs`
- `src/NpcDialogueConfirmedSettlementRunner.cs`（新增，仅承接已确认指令的后台结算）
- `src/NpcDialogueVM.cs`
- `src/NpcDialogueOverlay.cs`（仅当需要由关闭动作传递取消）
- `tools/worldbook-runtime-production-smoke/ProductionSmokeTestsDialogue.cs`
- 必要时对应 smoke 项目文件，但不增加外部依赖。

不修改：AI route/输出契约、世界书加载、Persona 内容、命令允许列表、存档模型、游戏目录、MCM、版本号。

## 决定

| 问题 | 方案 |
| --- | --- |
| 服务生命周期 | `NpcDialogueService` 在 `Initialize()` 前创建自己的 lifetime CTS；初始化和发送都链接它。关闭或 `Dispose()` 必须先取消它，且每个 await 后、设置 `_ready`、写 ready 状态/日志前都检查服务仍有效。 |
| 初始化失败 | 绑定失败、取消、会话失效均不得写 `npc_dialogue_ready`；返回明确的现有错误路径。 |
| 正向完成 | 所有有效回合只通过一个幂等的完成出口派发 `TurnCompleted` 和 `npc_dialogue_turn_completed`。覆盖普通 AI 回复、直答、确认后的结算与拒绝；日志包含 hero、generation、correlation、completion_kind，且不记录玩家原文、回复正文或提示词。已关闭面板的确认结算只写最终日志，不回投 UI 事件。 |
| correlation 规则 | AI 回合保留 gateway 的 correlation。创建 `NpcDialogueCommandConfirmation` 时必须冻结该 correlation；确认、拒绝和确认后的结算均复用它，禁止重新生成 GUID。直答在进入直答分支前预留仅服务内递增的 `directTurnId`，使用 `dialogue:{heroId}:direct:{directTurnId}`；该编号不复用 gateway generation，连续两次直答不得重复。 |
| UI 生命周期取消 | VM 拥有面板会话 CTS，作为服务 lifetime CTS 之外的调用方 token；关闭/换目标/重开时先取消 VM token，再调用服务取消/释放。晚到事件按 generation 忽略。 |
| 指令确认 | 保持现有“模型提案 -> confirmation event -> 用户确认才 ExecuteCommandAsync”；拒绝不得进入命令桥或写入状态。确认动作将不可变的 proposal、原始 correlation、目标与会话代际交给独立 `NpcDialogueConfirmedSettlementRunner`。Runner 不依赖 UI service 存活，不使用 `AwakeDialogueSessionCoordinator`，只受运行时会话终止/代际失效取消；它按 correlation 去重、调用既有命令桥，并唯一写出最终完成日志。 |
| 关闭后的结果 | 本批不持久化“上次结算结果”，也不承诺下次打开面板显示它。若窗口已关闭，已确认结算的成功/失败仅在结构化日志中可见；若会话已失效，则不得写入游戏状态，也不得记录成功。 |

## 验收

| 场景 | 预期本地证据 |
| --- | --- |
| 初始化后关闭 | 初始化 CTS 被取消；之后不得设置 `_ready`、派发 ready UI 状态或写 `npc_dialogue_ready` |
| 玩家绑定失败 | 无 `npc_dialogue_ready`，发送被拒绝且无 AI 提交 |
| 普通有效回复 | 恰有一次 `npc_dialogue_turn_completed`，其 generation/correlation 与本轮一致，随后 UI 完成事件可见 |
| 直答 | 使用规则化 direct correlation，恰有一次完成日志和完成事件 |
| 指令提案 | 确认前没有结算；拒绝后仍无结算；确认后才开始现有结算 |
| 指令最终状态 | 确认与拒绝各自产生恰有一次完成日志；确认沿用原始 AI correlation，拒绝不进入命令桥；不得产生新的 GUID correlation |
| 直答序列 | 两次连续直答的 correlation 均符合规则且彼此不同；各自产生恰有一次完成日志 |
| 已确认后关闭 | 确认后立即关闭/释放 UI service，再完成受控的延迟命令任务：命令桥恰执行一次、最终日志恰一次、不会向已关闭 UI 投递事件；关闭不能撤销已确认结算 |
| 会话失效 | 已确认任务在 campaign/session 代际失效后不得写入游戏状态或记录成功 |
| 关闭/换目标 | VM 与服务会话 token 均已取消；服务取消路径执行；晚到 UI 事件不进入已关闭/新会话 |
| 重复关闭 | 幂等，不重复取消、结算或释放服务 |

## 证据与限制

- E1：编译与结构化日志字段检查。
- E2：focused production smoke 覆盖上述状态转移；复用现有 `CapturedLogs` hook 对日志次数、字段、顺序与敏感文本缺失做断言。
- E4：用户在匹配 BuildId 的游戏中确认真实 Provider 回合、关闭/重开和指令确认显示。
- E5：不在本批范围；不因本地 smoke 宣称存档或长期会话通过。

## 非目标

- 不改变真实指令的游戏效果数值。
- 不在本批引入新 Gauntlet 测试宿主或本地网页 UI。
- 不修复旧候选 010 的部署证据缺失；新候选单独记录自己的离线证据。
