# Plan: 交互动作代码保底（C4）

_Locked via grill — Round 9 `VERDICT: APPROVED`。审查记录见 `PLAN-Interactions-REVIEW-LOG-20260816.md`。_

## Goal

在联系人中心提供“代码保底”交互，v1 只做两个可严格校验动作：

- `awake.action.give_gold.v1`：给金币。
- `awake.action.promise_request.v1`：请求/承诺账本。

AI 只能生成动作建议，必须渲染为确认按钮；执行只能通过命令适配器，不能由 AI 直接改状态。

评估轴：玩家每次动作都有确定反馈；开发者只维护少量可测试命令，不写自由文本动作解析。

## Approach

### 1. 可行性前置

- 先做 1 个实现 spike：验证 Bannerlord 1.3.15/1.4.8 读取/修改玩家金币的原生 API。
- 若金币 API 不可稳定获取，则 v1 只实现 promise/request，金币延后。

### 2. 数据契约

- `AiTaskConstants.InteractionsNamespace = "awake.interactions"`。
- `WorldStateKind.Interaction`，schema `awake.interactions.v1`。
- per-hero key：`campaign.interactions.v1.<canonicalContactKey>`。
- 记录：`id/day/commandId/targetId/amount/items/result/correlation/idempotencyKey/reason`。
- 承诺记录：`promiseId/status(pending|accepted|kept|broken|rejected)/text/day/playerHeroId/targetHeroId/obligor/obligee`。
- 已解决承诺归档到 per-contact key `campaign.interactions.archive.v1.<canonicalContactKey>`，活动账本保持小体积；active + archive 不放在同一个全局单值。
- `awake.action.give_gold.v1` 输入 schema：
  `{"playerHeroId":"<identifier>","targetHeroId":"<identifier>","sessionId":"...","generation":1,"amount":100,"reason":"..."}`；identifier 是归一化前的任意稳定 hero 标识，适配器用 `NormalizeHeroId()` 归一化后再比较。
- 内部生命周期命令：`awake.action.give_gold.pending.v1` / `awake.action.give_gold.complete.v1` / `awake.action.give_gold.compensated.v1`，全部 internal-only。
- `awake.action.promise_request.v1` 输入 schema：
  `{"playerHeroId":"<identifier>","targetHeroId":"<identifier>","sessionId":"...","generation":1,"text":"...","obligor":"player|npc"}`；text ≤240 字符，identifier 与 give_gold 一样经 `NormalizeHeroId()` 归一化。
- `awake.action.promise_update.v1` 输入 schema：`{"promiseId":"...","newStatus":"accepted|kept|broken|rejected","reason":"...","eventId":"..."}`。
- `awake.action.archive.v1` 输入 schema：`{"canonicalContactKey":"...","promiseIds":["..."],"phase":"archive|confirmed"}`。
- typed error catalog：`awake.action.insufficient_gold`、`awake.action.snapshot_mismatch`、`awake.action.target_mismatch`、`awake.action.session_expired`、`awake.action.ledger_full`、`awake.action.permission_denied`。
- 身份归一化：`AwakeRuntime.CurrentHeroId` 可能是 `main_hero` 等框架 ID，不能假定 `hero:` 前缀；新增 `NormalizeHeroId()`，preflight/execute 比较归一化后的 player/target ID。

### 2.1 存储集成

- 更新 `WorldStateKind`、`NewState`、Apply switch、`AwakeStorageContract`、`AiTaskConstants.StorageNamespaceIds`。
- 新增 `EnsureInteractionShape/ApplyInteraction`：写活动账本、归档已解决承诺、硬字节上限、保留 `promiseId`/interaction ID 作为长期幂等。
- 承诺权威源：`awake.interactions.v1` 为 canonical，旧 `awake.npc.memory.v1.promises` 做一次性迁移/对账，禁止双写。
- 迁移标记 key `awake.interactions.migration.v1`：记录已完成/失败状态，幂等复制、可重试；旧数据不删除。
- `WorldCommandBridge.CategoryForHardFailure` 增加 `awake.action.*` 的 code→category 映射，不能全部落成 InternalFailure。
- 归档原子性：已解决承诺先保留在活动文档并标记 `archiving`，archive 命令确认后再从活动文档移除；任何一步失败可重试，不丢记录。
- archive 确认命令：`awake.action.archive.v1`（internal-only），或把归档 phase 并入 `promise_update.v1` 的显式 phase 字段；必须在命令注册表中出现。
- 每个 per-contact archive 设上限与压缩策略：超过上限时把最旧已解决条目压缩为一条摘要，不直接硬失败。
- 新增 `AwakeInteractionRecoveryService`：只在 interaction namespace 打开后运行，动作按钮启用前完成对账。
- recovery 索引 key `awake.interactions.recovery_index.v1`：记录 pending/completed 的 canonical key 列表；恢复服务优先扫索引，缺失时回退扫描联系人列表/transcript metadata 得到的 canonical keys。

### 3. 命令适配器

- `AwakeGiveGoldAdapter`：
  - 校验目标 hero 与当前 session 一致、余额、金额 `1..cap`、schema、快照 token。
- 分三阶段：先持久化 `pending` 命令 → 主线程原生金币扣减 → 持久化 `complete` 或 `compensated`；不在一个同步 adapter 内同时改金币和等 ledger drain。
  - Execute 使用 `AwakeInteractionSnapshot`（sessionId/targetId/playerBalanceBefore/playerBalanceAfter/targetBalanceBefore/targetBalanceAfter/day/deadline）在 game thread 重新校验后执行；v1 若只扣玩家金币，则 target 平衡段写 null 并声明 audit-only。
  - 若原生金币 API 不可用，适配器返回 `awake.action.unsupported` 并禁用按钮。
- `AwakePromiseAdapter`：
  - 创建 pending promise，校验目标/文本/方向，不直接改变关系数值。
  - 关系变化由代码在结算后推导。
- `awake.action.promise_update.v1`：internal-only 命令，由代码侧服务驱动 `accepted/kept/broken/rejected` 状态转换，AI 只能创建 pending；同样注册到命令/权限/风险/manifest。
- internal-only 路径：`CommandRiskPolicy` 增加 internal 标记；internal 调用走内部 authority token，不依赖玩家 PermissionCatalog，不弹权限框；`WorldCommandBridge` 和 AI output allowlist 直接拒绝该命令。

### 4. 会话与快照

- 新增 `sessionToken`/`sessionId`/`generation`，命令 preflight/execute 必须携带。
- 明确：`sessionToken` 不新增到 `CommandRequest` 字段，而是作为 `CommandAdapterPreflight.SnapshotToken` 的一部分，在 Execute 中重新校验。
- 快照 token 包含 `sessionId/targetId/balance/day/deadline`，执行前重新校验。
- 禁止依赖 CapabilityBroker；全部用当前已确认 API 或自建主线程适配器。
- `AwakeDialogueSessionCoordinator` 暴露 `ActiveSessionId` 与 `ActiveTargetId`，preflight/execute 用它比较 code-injected 身份，不能只依赖当前 UI 选择。
- 快照 token 不再使用 `SnapshotFromArguments` 的 args-only hash；改为 state-bearing token。
- 快照 token 包含 `playerBalanceBefore/playerBalanceAfter/targetBalanceBefore/targetBalanceAfter`；v1 audit-only 时 target 字段为 null，且必须在 token 中显式标记 `targetMutation=false`。

### 5. UI

- 联系人中心增加动作条，按联系人/距离/状态动态启用。
- 给金币：数量输入；请求：文本输入；提交时由代码构造 `CommandRequest`，不复用 AI JSON。
- v1 audit-only 模式必须显式写进 UI/结果文案：“只扣除你的金币并记录账本，不改变对方钱包”；游戏内验收也按此口径。
- `NpcDialogueService.HandleCompleted` 对 interaction 命令不再自动调用 `ExecuteCommandAsync`；改为入队 `PendingActionSuggestion`。
- AI 动作建议只显示“确认”按钮；渲染时生成稳定 `suggestionId`，确认点击期间禁用按钮，幂等键由 `suggestionId + typed args` 派生。
- 建议随 turn/contact/session/save 变化清空。
- 本 PLAN 的 UI 前置依赖 `PLAN-ContactHubHistory-20260816.md` 落地；若未落地，动作条先挂到现有 `AwakeMessengerOverlay`。

### 6. 权限与风险

- 命令注册清单：
  - `awake.action.give_gold.v1`：public，player confirm，R2Gameplay。
  - `awake.action.give_gold.pending.v1`：internal，R1Interface。
  - `awake.action.give_gold.complete.v1`：internal，R1Interface。
  - `awake.action.give_gold.compensated.v1`：internal，R1Interface。
  - `awake.action.promise_request.v1`：public，player confirm，R1Interface。
  - `awake.action.promise_update.v1`：internal，R1Interface。
  - `awake.action.archive.v1`：internal，R1Interface。
  - `awake.interactions.index.update.v1`：internal，R1Interface。
  - 全部同步加入 `AiTaskConstants.NewCommandIds`、`PermissionCatalog`、`CommandRiskPolicy` 和 manifest。
- give_gold 风险 R2Gameplay；promise 风险 R1Interface。
- 玩家确认用 `PermissionGate.EnsureAsync`；AI 建议路径不请求权限、不执行。

### 6.1 结算与恢复服务

- 新增 `AwakeGoldSettlementService`：由 `AwakeInteractionRuntime` 在 `Ready` 状态调用 `ProcessPending()`，读取 recovery index 中的 pending interaction，在主线程执行原生金币扣减，再写 complete/compensated；不依赖事件订阅，全部由该轮询/恢复入口驱动。
- 内部命令 schema 统一为 `{phase, interactionId, sessionId, generation, snapshotToken, expectedBalances}`；不再为每个 phase 维护独立参数结构。
- recovery index entry：`{canonicalContactKey, pendingInteractionIds[], updatedUtc}`；上限 200 条，超出后把最旧已解决条目移除；index 写入失败视为 retryable pending，不丢账本。
- index 双写顺序：先写 `awake.interactions.index.update.v1`，再写 active ledger pending；index 缺失时恢复服务回退扫描 canonical keys。
- index 是独立内部命令/applier，带 retryable update 与 reconciliation；单值上限 64KB，超限裁剪最旧已解决项。
- index schema：`{canonicalContactKey, pendingInteractionIds[], expectedLedgerKeys[], updatedUtc}`，幂等键 `index|<interactionId>`；重复提交返回 duplicate，不覆盖 newer state。
- 恢复分支：index-only（index 有、ledger 无）→ 以 index 为准重建 pending；ledger-only（index 无、ledger 有）→ 回补 index；两者不一致 → 以较新 `updatedUtc` 为准并记录 repair。
- 回退源：`Hero.AllAliveHeroes` 的 canonical keys + transcript metadata + recovery index 中最近 200 个 canonical keys；不在三者中的目标标记 fail-closed。
- 生命周期：新增 `AwakeInteractionRuntime`，在 CampaignSessionReady 后的安全 tick 启动；状态 `WaitingNamespace -> Recovering -> Ready`，只有 `Ready` 才启用动作按钮。

### 7. 测试

- 命令 schema/数字边界/负数/浮点/超量/余额不足。
- 快照 token 过期、目标不匹配、会话不匹配。
- promise 状态机：pending→accepted/kept/broken/rejected。
- AI 建议不自动执行、双击幂等、save/load 后不重复扣款。
- `NpcPromptTemplate` / output schema / `NpcDialogueOutputValidator` 增加 interaction 命令 allowlist 与 action-suggestion 预算。
- prompt 注入固定 `npc_interactions` 块：只含 pending/recent 承诺与最近动作摘要，上限 600 字节，不注入完整账本。
- 双版本构建和 SdkSmoke。

## Key decisions & tradeoffs

- v1 只做给金币和请求承诺：玩家能得到可信反馈，开发者命令矩阵最小。
- 物品/交易延后：避免在原生 API 未证明前写第二套实现。
- AI 建议确认制：玩家有掌控感，防止模型幻觉。

## Risks / open questions

- 金币 API 未证明前是否只发 promise？PLAN 默认：spike 失败则砍金币。
- 物品/交易是否在后续内容包接入后再做？默认是。

## Out of scope

- 物品交付、交易、赠送装备、俘虏/人口操作。
- 自由文本动作执行。
- 不依赖 AF/爱与恨状态。

## Verification

- 双版本构建 0 警告 0 错误。
- SdkSmoke 覆盖全部 8 个 public/internal 命令、promise 状态机、确认制、幂等、快照、recovery-index 失败、index-only/ledger-only/不一致恢复分支。
- 游戏内验收：给金币后钱包与账本一致；请求承诺进入账本且 AI 对话可见状态。
