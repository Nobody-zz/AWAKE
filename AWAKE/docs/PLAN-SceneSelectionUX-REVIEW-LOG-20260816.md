# Plan Review Log: 场景选人体验增强

Act 1：用户确认方向与硬性红线（标记必须非常明显）；方案已锁定到 `PLAN-SceneSelectionUX-20260816.md`。MAX_ROUNDS=3。

## Round 1 — Codex（独立只读审查）

VERDICT: REVISE

主要问题：
1. 默认 `[` / `]` 键无法被现有 `Enum.TryParse<InputKey>` 解析，需映射 `OpenBraces` / `CloseBraces` 并加键解析测试。
2. `GetSceneTargets(64)` 在距离排序前截断，最终 48 可能漏掉更近 NPC，需先收集排序再截断。
3. 无名 NPC 的 `StableId` 内嵌 `AgentIndex`，Agent 回收后可能串台，需用 Mission + Agent 引用 + CharacterId 保留。
4. 场景模式不能把 `scene:current` 当普通 heroId，否则仍会读写 NPC 记忆/关系。
5. 场景喊话不能沿用允许 `awake.relationship.delta.v1` 的单个 NPC 输出契约。
6. `NpcDialogueVM` / Overlay 需要场景化标题、状态和提示文案。
7. 无候选 / 无附近人物 / 非场景上下文的可用性判定未定义。
8. SdkSmoke 不能直接测 UI/Mission 层，需抽出纯逻辑组件加入测试工程。
9. 候选集与扇形可视化需要统一使用已 APPROVED 的视锥 + LOS 判定。
10. 按住 T 期间每帧全量扫描有性能风险，需阈值化缓存。
11. 瞬时 `DisplayMessage` 不能满足“持续可见”，需要持久状态条兜底。
12. `TryOpenSceneShout` 的会话获取、失败清理、日志未定义。

### 修订

- 增加 `[` / `]` 到 `InputKey.OpenBraces` / `CloseBraces` 的映射与键解析测试。
- 候选改为先全量收集、过滤、排序，再截断 48，并加入视锥 + LOS。
- 选择状态改为 `(Mission, Agent, CharacterId)` 校验，避免 Agent 索引复用串台。
- `NpcDialogueService` 增加显式 `IsSceneShout` 分支，隔离记忆/关系/命令结算。
- 新增 `SceneShoutAvailability` 纯判定与 `SceneSelectionController` 纯逻辑，并加入测试工程。
- 新增持久 `SceneDialogueStatusOverlay`，作为持续状态提示与可见性兜底。
- 固定 `TryOpenSceneShout` 的 acquire/open/fail/close 生命周期与日志。

## Round 2 — Codex（同一只读会话复核）

VERDICT: REVISE

主要问题：
1. SdkSmoke 键解析测试需要 `TaleWorlds.InputSystem` 引用，或解析器保持无依赖。
2. 场景模式“不读记忆/关系、拒绝命令”的测试缝未命名。
3. `SceneShoutAvailability` 的未过滤人数来源未定义。
4. 提示文案硬编码 `[` / `]` / `C`，玩家改键后会误导。
5. 状态条不能让 `SceneShoutAvailability` 自锁，进入喊话前必须先关闭。
6. `SetContourColor` 可能静默无效，需要能力探测而不是只看异常。

### 修订

- 测试工程增加 `TaleWorlds.InputSystem` 引用，键解析测试可验证 `OpenBraces` / `CloseBraces`。
- 新增纯策略 `SceneDialogueModePolicy`，由服务调用并加入测试工程。
- 新增 `CountScenePeopleUnfiltered()`，允许注入人数以保持纯判定可测。
- 提示文案改用 `{NEAR_KEY}` / `{FAR_KEY}` / `{SHOUT_KEY}` 占位符，从 MCM 当前值解析。
- 明确进入喊话前先关闭 `SceneDialogueStatusOverlay`，状态条不进入阻塞判定。
- 新增 `SceneDialogueVisualCapabilities`，由 `awake.scene.visual_selftest` 探测 `ContourCapable` 后才启用文字兜底。

## Round 3 — Codex（同一只读会话复核）

VERDICT: APPROVED

Round 2 六项均已处理，未发现新的 material blocker。保留两条实现级提醒：
- 自检对 `ContourCapable` 需要可观测的确认手段，不能仅从 void 调用无异常推断。
- 候选排序需要对相同距离做确定性 tie-break（例如按 `CharacterId` / `StableId`）。

### 最终状态

- Act 1：用户确认方向与“标记必须非常明显”的红线，方案锁定。
- Act 2：独立只读审查 3 轮收敛，Round 3 `VERDICT: APPROVED`。
- 待用户签收后进入实现。
