# Plan: 联系人中心 + 对话历史（C1/C2/C3）

_Locked via grill — Round 7 `VERDICT: APPROVED`。审查记录见 `PLAN-ContactHubHistory-REVIEW-LOG-20260816.md`。_

## Goal

把现有 `AwakeMessengerOverlay/VM/Prefab` 扩展成“联系人中心”：

- 左侧联系人列表，右侧人物卡片，中间保留实时对话流。
- 对话原文升级为 `awake.transcript.v1`，与 NPC 压缩记忆、关系状态严格分层。
- 普通玩家可固定/查看原文；删除/清空/撤销等高级操作默认开发者菜单。
- 不复制 Alice/AF/HistoryManager 资产和实现，资产边界用 lint 强制。

评估轴：玩家一眼能看懂“原文/记忆/关系”的区别；开发者只维护一套面板和一套 transcript 存储。

## Approach

### 1. 数据契约

- 新增 `AiTaskConstants.TranscriptNamespace = "awake.transcripts"`。
- 新增 `WorldStateKind.Transcript` 与 `AwakeStorageContract.TranscriptSchema = "awake.transcript.v1"`。
- transcript 按联系人分 key：`campaign.transcript.v1.<canonicalContactKey>`；`canonicalContactKey` 只使用稳定 hero/troop 身份，不使用含 `:a<agentIndex>` 的运行时 StableId。
- 新增 `AwakeNpcTarget.CanonicalContactKey`：hero 为 `hero:<id>`；无名 NPC 为 `npc:<CharacterId>`（同兵种默认合并），运行时 agent locator 只用于场景选择。
- 单条结构：`id/day/location/speaker/text/source/conversationId/kind`；固定状态存文档级 `pinnedIds`，不在行内。
- 枚举：`source` 固定为 `messenger|scene|encounter|event|proactive|letter|system`；`kind` 固定为 `player|npc|system|failed|hidden`。
- 单条 text 最大 1200 UTF-8 字节；每条联系人页面上限 200 条或 30 游戏日；固定上限 20 条。
- 每个 chunk 最大 128KB，per-contact 可多 chunk；任何 chunk 序列化后不得超过 512KB，写前强制字节校验。
- 每个联系人新增元数据文档 `campaign.transcript.meta.v1.<contactKey>`：保存 `lastChunk`、`pinnedIds`、`nextChunk`；chunk 文档只保存 `chunkIndex/entries/appliedKeys`。
- 元数据文档与审计文档都包含 `schema` 和 `appliedKeys`，pin/roll/audit 幂等键绑定到对应文档，避免 `TryApplyAsync` 丢失幂等。
- chunk 创建与元数据更新拆成独立幂等命令；先提交新 chunk，再更新元数据 `lastChunk`；恢复时按 `nextChunk`/`lastChunk` 重放，避免孤儿 chunk。

### 2. 命令与审计

- `awake.transcript.append.v1`：追加原文，带逐行幂等键；由统一 session-owned sink 调用，`NpcDialogueService` 完成回合时直接落 transcript，不再由 VM 手动追加。
- `awake.transcript.pin.v1`：固定/取消固定，固定状态存 `pinnedIds` 元数据，不改写原文。
- `awake.transcript.roll.v1`：只在记忆写入 + 审计成功后才滚动未固定原文，带 `rollId` 幂等。
- roll 触发器：每个游戏日的有界后台任务，在 `NpcMemoryService.CloseConversationAsync` 或每日 consolidation 成功并返回 `memoryWriteId` 后执行；retry 使用同一个 `memoryWriteId`。
- 跨 chunk roll：按 `rollId|chunkIndex` 逐 chunk 执行，全部成功后最后提交元数据/审计；任一步失败按同 key 重试，不提前删除原文。
- roll 两阶段审计：先写 durable `audit-intent`（含 `rollId/memoryWriteId/chunkIndexes`），再执行 chunk 删除，全部 chunk 成功后写 `audit-commit`；崩溃恢复时先检查 audit-intent，未 commit 则重跑 chunk 幂等删除，已 commit 则停止。
- `awake.transcript.delete.v1`、`awake.transcript.clear.v1`：本 PLAN 不实现；Phase B 另立契约，必须先定义逆操作记录和 Execute 层开发者权限检查。
- 新增 `awake.history.audit.v1`：namespace `awake.history.audit`，key `campaign.history.audit.v1`，`WorldStateKind.Audit`，append-only 记录 `rollId/lineId/action/day/correlation/memoryWriteId/conversationId/phase(intent|commit)/chunkIndexes/status`；注册到 `AwakeStorageContract`、`AiTaskConstants.StorageNamespaceIds`，保留上限 1000，禁止整文档快照回滚。
- 所有命令同步登记到 `AiTaskConstants.NewCommandIds`、`PermissionCatalog`、`CommandRiskPolicy` 和框架 manifest。

### 3. 迁移

- 新增一次性只读迁移 `MigrateMessengerV1ToTranscriptV1`。
- 旧行 ID 确定性生成：`legacy:<canonicalContactKey>:<index>`；同兵种无名 NPC 按 canonical key 合并，避免 `:a<agentIndex>` 漂移。
- chunk 编码：canonical key 用 URL-safe base64，chunk key 为 `campaign.transcript.v1.<encodedKey>.<chunkIndex>`。
- 迁移状态写入 `awake.transcript.meta.v1`；旧数据不删除，迁移失败可重试；schema 不匹配按硬错误处理，不静默继续。

### 4. UI

- Phase A 保持“左联系人 + 中对话流 + 右侧小卡片”，不直接上三栏大面板。
- 右侧卡片：头像、身份、所在地、附近/远方、关系快照、记忆摘要计数。
- 卡片数据使用 `CanonicalContactKey` 聚合，无名 NPC 只显示 troop 级身份与通用记忆，不伪造 hero 关系。
- `AwakeMessengerService.BuildContacts` 去重与分组改为 `CanonicalContactKey`，同兵种无名 NPC 合并为一行。
- 联系人行与 live target 解耦：`AwakeContactInfo.Target` 可空；transcript metadata 中出现的 canonical key 即使当前无 live target 也显示为历史联系人，允许查看/固定历史，不允许发送新消息。
- 发送新消息前解析最近可用 live target；无可用实例时显示“当前无法对话”，不自动创建空会话。
- 新增 `campaign.contacts.v1` 索引（namespace `awake.contacts`，`WorldStateKind.Contacts`）：记录出现过的 canonical keys；append/migration/pin/roll 同步维护，联系人列表用它发现 transcript-only 历史联系人，避免重载后无法枚举。
- `NpcMemoryService`/`WorldStateStore` 的卡片聚合 key 使用 canonical 映射：hero 用 `hero:<id>`，无名 NPC 不读取 hero 关系/记忆。
- `conversationId` 在 `NpcDialogueService.Initialize` 时预留，同一个 id 贯穿 transcript append 与 memory close。
- `IsSceneShout` 不进入 per-contact transcript；写入 `scene:current` 非记忆 key 或跳过持久化，不允许落 undefined canonical key。
- 头像使用游戏原生 `CharacterImageIdentifier` / `ImageIdentifier`；无名 NPC 用 AWAKE 通用占位。
- Tab：对话、历史、写信（占位）、交互（占位）。历史 Tab 只读 + 固定按钮；写信/交互按钮禁用。
- 流式输出沿用现有 `NpcDialogueUiEvent` 和 `OnFrameTick`，不新建第三套对话引擎。
- 移除 `AwakeMessengerVM` 构造器同步 `.GetResult()`；联系人列表和选中联系人的历史/卡片数据懒加载，历史列表分页或虚拟化。

### 5. 资产边界

- 新增 `tools/asset_boundary_lint.ps1`，扫描 `src/`、`GUI/` 中的 Alice/AF/HistoryManager 名称、prefab ID、brush、sprite、纹理路径、VM 命名。
- 直接复制、改名复制、重绘、重描边均视为违规，除非来源与授权记录明确。
- lint 纳入 `release_check.ps1`。

### 6. 测试

- Transcript schema normalize、per-contact append/pin/roll、幂等、chunk 字节上限。
- 迁移 v1 到 transcript，旧数据不变。
- 压缩记忆写入不触碰 transcript，roll 只在记忆+审计成功后才执行。
- NPC prompt 断言不包含原始 transcript 文本；`NpcDialoguePromptPipeline` 的 `dialogue_history` 替换为有界 `bounded_session_summary`，不再注入原始行。
- `bounded_session_summary` 从内存会话历史生成，上限 1200 UTF-8 字节；同步更新 `NpcPromptTemplate.RequiredVariables` 与 pipeline 截断顺序（summary 最先截断，memory/knowledge 保留预算）。
- summary 必须是确定性非逐字摘要（抽取角色/主题/事实条目），不得只截断原文；prompt 断言为“不含存储 transcript 行 ID 与超过 40 字符的连续原文”。
- 固定行跨滚动/压缩保留；delete/clear 不在本 PLAN 验收范围。
- 直接对话、场景对话、遭遇、事件、主动对话都通过统一 transcript sink 落账，不再只由 Messenger VM 追加。

## Key decisions & tradeoffs

- 扩展现有 Messenger 而非新建 overlay：玩家入口不变，开发者只维护一套 UI。
- 新增 transcript schema 而非直接复用 messenger：关系清晰，但需要迁移；接受一次性迁移成本。
- 固定用 `pinnedIds` 而不是改每行：减少 undo/迁移复杂度。
- 删除/清空放开发者菜单：普通玩家操作面小，避免误删。

## Risks / open questions

- 旧存档如果 `awake.messenger.v1` 很大，迁移可能超过单值上限；需要 per-contact 分页迁移。
- 头像原生 API 在不同 Bannerlord 版本可能不同；需在实现前做 API probe。
- 删除/撤销若不使用全快照，需明确逆操作语义；本 PLAN 先只实现 append/pin，delete/clear 在 per-contact 存储稳定后再落地。

## Out of scope

- 不合并事件收件箱/周报。
- 不做信件系统（Phase C 另行 PLAN）。
- 不做交互命令（Phase D 另行 PLAN）。
- 不复制 Alice/AF/HistoryManager 资产。

## Verification

- 双版本 `dotnet build` 0 警告 0 错误。
- `Awake.SdkSmoke` PASS ALL，包含新 transcript/pin/migration/prompt 断言。
- `validate_localization.ps1` 全绿；`release_check.ps1` 含 asset lint。
- 游戏内验收：联系人卡片显示头像/地点/关系；历史 Tab 能看原文与固定状态；NPC prompt 日志不出现原文行。
