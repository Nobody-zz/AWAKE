# Plan: 场景选人体验增强（明显标记 + 双键往返 + 场景喊话）

_Locked via grill — AWAKE 设计方案 + 用户红线：标记必须非常明显。_

## Goal

把场景内 AI 对话选人从“按住 T 扩大范围、Y 单向循环”升级为可精确控制的体验：

1. 按住 `T` 时显示非常明显的地面扇形范围，所有候选 NPC 金色轮廓，当前目标品红轮廓。
2. 提供两个循环键：一个从近到远选择，一个从远到近选择，让玩家不用反复绕圈。
3. 提供“不选具体人物，向场景喊话”的入口，松开 `T` 后打开 AWAKE 自己的对话覆盖层，不依赖原生对话。
4. 标记在任何可操作距离、室内外、昼夜光照下都必须一眼可辨；看不清即验收不通过。

## Approach

### 1. 范围与候选

- 保持 `T` 按住扩大距离，范围曲线沿用 `SceneDialogueSelection.CurrentRange`。
- 候选来源新增 `NpcDialogueLauncher.GetSceneCandidates()`：收集全部当前 Mission 的合法 Agent 候选，不做 64 的预截断，过滤后按到主控人物的三维距离升序排列。
- 候选上限 48，超出只取最近 48 个；排序与截断必须发生在距离过滤之后，防止“先截断导致近处候选被丢弃”。
- 复用已 APPROVED 的视锥与视线规则：候选必须同时满足 `IsWithinCone` 和基础 LOS；地面扇形与可选候选集使用同一判定，避免视觉和交互不一致。
- 候选缓存：按住 `T` 期间不每帧全量扫描；仅在范围变化超过 1 米、候选数量变化、Mission 变化或每 0.2 秒一次时重建候选。
- 选择保持不使用 `StableId` 里的 `AgentIndex` 作为唯一依据：保存 `(Mission 引用, Agent 引用, CharacterId)`，应用高亮前核对 Agent 仍属于当前 Mission 且索引未复用，防止无名 NPC 因 Agent 回收而串台。

### 2. 双键往返选人

- 新增两个 MCM 可配置键：`SceneCycleNearToFarKey`（默认 `[`）与 `SceneCycleFarToNearKey`（默认 `]`）。
- 键解析必须支持 `[` / `]` 字面值：映射到 `InputKey.OpenBraces` / `InputKey.CloseBraces`，不能只依赖 `Enum.TryParse<InputKey>`；解析失败回退默认值，并加入 SdkSmoke 键解析测试。
- 近→远：当前索引 `+1`，超出末尾回绕到 0；未选中时默认选最近。
- 远→近：当前索引 `-1`，低于 0 回绕到末尾；未选中时默认选最远。
- 保留 `Y` 作为近→远循环的兼容别名，避免旧习惯失效；`Y` 行为与近→远键一致。
- 提示文案不硬编码键名：本地化键使用 `{NEAR_KEY}`、`{FAR_KEY}`、`{SHOUT_KEY}` 占位符，从 MCM 当前值解析，避免玩家改键后提示说错。
- 索引状态抽成纯逻辑 `SceneSelectionController`（候选列表、当前索引、方向移动、范围变化后 clamp），放入测试工程直接测，不依赖 Mission/UI。
- `AWAKE.Tests.csproj` 增加 `TaleWorlds.InputSystem` 引用，使键解析测试能验证 `InputKey.OpenBraces` / `CloseBraces` 映射。

### 3. 明显标记

- 地面扇形：按已 APPROVED 的 `PLAN-SceneVisualSelection-20260816.md` 实现，使用游戏自带投掷物实体池生成地面标记，最大 180 个，每 tick 最多新增 16 个；禁用拾取、物理、存档，放大并染色。
- 候选高亮：所有候选 NPC 使用高饱和金色轮廓 `SetContourColor(1, 0.84, 0.2)`，当前目标改为高饱和品红 `SetContourColor(1, 0.22, 0.72)`。
- 轮廓呼吸：选中目标轮廓每 0.25 秒在品红和亮红之间切换，形成明显脉冲；候选轮廓固定金色，不做全屏闪烁。
- 降级规则：地面实体 spawn 失败时仍保留候选金色轮廓 + 当前品红轮廓；若轮廓也不可用，显示持续存在的高对比状态条“范围内有 N 人，最近 XXX，最远 XXX”，不允许无声失败。
- 持续状态条：新建只读 `SceneDialogueStatusOverlay`（Gauntlet 薄层，不抢输入焦点），按住 `T` 期间持续显示“当前模式 / 选中目标 / 范围 / 候选数”，松开或取消后关闭；它同时作为降级可见性兜底，不依赖瞬时 `DisplayMessage`。
- 轮廓能力探测：新增 `SceneDialogueVisualCapabilities`，由游戏内 `awake.scene.visual_selftest` 对真实 Agent 执行一次 `SetContourColor` 得到 `ContourCapable`；`SetContourColor` 可能静默无效，不能只靠是否抛异常判断。状态条兜底只有在 `ContourCapable == false` 时才启用，并在日志记录 `scene_visual_contour_unavailable`。
- 验收红线：近距离多目标、最大距离、室内、室外、白天、夜晚都必须能一眼看出范围和当前目标。

### 4. 场景喊话

- 按住 `T` 期间按 `SceneShoutKey`（默认 `C`）进入场景喊话模式。
- 进入喊话模式后，目标选择清空，提示“向场景喊话”，候选高亮改为统一淡金色，表示“不是针对某个人”。
- 松开 `T` 时调用 `NpcDialogueLauncher.TryOpenSceneShout(sceneKeywords)`，打开 `NpcDialogueOverlay`，会话 source 为 `scene_shout`，target 为 `scene:current`。
- `TryOpenSceneShout` 生命周期固定为：`AwakeDialogueSessionCoordinator.TryAcquire("scene_shout", "scene:current")` → 创建场景模式 `NpcDialogueService` → `Initialize()` → `NpcDialogueOverlay.Open(...)` → 成功写 `scene_shout_open_success`；任何一步失败立即 `Dispose` 服务并 `Close` 会话，写 `scene_shout_open_failed`；overlay 关闭时写 `scene_shout_closed`。
- `NpcDialogueService` 增加显式 `IsSceneShout` 分支，不是把 `scene:current` 当成普通 heroId：
  - 不调用 `LoadMemoryBlockAsync`、不调用 `LoadNpcStateAsync`，`Dispose` 不进入 NPC 记忆保留/写入分支；
  - 使用独立的场景喊话 prompt/output 契约：上下文注入“附近人物名单 + 当前场景关键词”，输出契约不允许 `awake.relationship.delta.v1`，场景模式下 `ExecuteCommandAsync` 直接拒绝所有命令并提示“场景喊话不结算单条关系”；
  - 关闭时不生成 NPC 记忆记录，只记录 `scene_shout_closed`。
- 新增纯判定 `SceneShoutAvailability`：输入 Mission 状态、战役状态、Settlement 上下文、未过滤附近人数，输出 Available / NoPeople / WrongContext / BlockedByOverlay / ConversationActive。可用条件为：非战斗/部署/决斗/潜行/竞技场模式、无对话进行、无对话覆盖层打开，且（Settlement 上下文有效 或 附近人数 > 0）。`SceneDialogueStatusOverlay` 不是阻塞性对话覆盖层，进入喊话前先关闭它再判定，避免自锁。该判定放入测试工程。
- 新增 `SceneDialogueModePolicy` 纯策略类，由 `NpcDialogueService` 在场景模式下调用：`AllowsNpcMemory=false`、`AllowsRelationshipState=false`、`AllowsCommands=false`、`PromptContractId=scene_shout.v1`、`OutputContractId=scene_shout.output.v1`；该策略加入测试工程，SdkSmoke 直接断言门禁。
- 未过滤附近人数由 `NpcDialogueLauncher.CountScenePeopleUnfiltered()` 提供：只统计当前 Mission 中非玩家、存活的 Agent，不做距离/视锥/资格过滤；该函数为轻量遍历，并允许 `SceneShoutAvailability` 注入人数，保持纯判定可测。
- `NpcDialogueVM` / `NpcDialogueOverlay` 增加场景模式分支：标题显示“向场景喊话”，开场提示与发送按钮状态不再使用单个 NPC 的“对方正在回应”文案，而使用场景化文案。

### 5. 配置与本地化

- MCM 新增三个文本输入项：`[`、`]`、`C`，全部可改，解析失败回退默认。
- 新增/更新本地化键：双键提示、喊话提示、喊话标题、无目标喊话上下文、降级可见性提示。
- 中英文档同步更新。

### 6. 测试

- SdkSmoke：
  - 把 `SceneSelectionController`、`SceneShoutAvailability`、`SceneDialoguePreviewMath`、`SceneDialogueModePolicy`、键解析函数加入 `AWAKE.Tests.csproj` 的 Compile Include，纯逻辑层直接测试。
  - 候选排序、近→远/远→近索引回绕、范围变化后按候选身份 clamp。
  - `[` / `]` 字面键映射到 `InputKey.OpenBraces` / `CloseBraces` 并回退默认。
  - 场景模式可用性：无候选但城镇上下文可喊话；无候选且非场景上下文不可用；战斗/对话/覆盖层阻塞。
  - `SceneDialogueModePolicy`：`AllowsNpcMemory=false`、`AllowsRelationshipState=false`、`AllowsCommands=false`。
  - `CountScenePeopleUnfiltered` 返回注入值后，纯判定可重复测试。
- 构建：双版本 `dotnet build -c Release -p:BannerlordApi=1.3.15|1.4.8` 0 警告 0 错误。
- 本地化校验全绿。
- 游戏内验收：按住 `T` 看到明显扇形与候选高亮；`[`/`]` 可往返切换；按 `C` 松开后打开场景喊话；日志出现 `scene_shout_open_success`，无原生对话回退、无崩溃。

## Key decisions & tradeoffs

- 保留 `Y` 作为近→远别名，而不是移除：兼容旧操作习惯，代价是说明文字多一个键位。
- 默认键选 `[` / `]` / `C`，并全部进 MCM：降低与常用动作键冲突概率，同时允许玩家自定。
- 场景喊话走独立 overlay 而非原生对话：符合 AWAKE 场景来源永不回退原生对话的既有决策。
- 标记采用“地面扇形 + 全身轮廓 + 目标脉冲”三层：单靠轮廓不够明显，单靠地面实体在室内/远距离可能失效。

## Risks / open questions

- `SpawnWeaponWithNewEntity` 在部分室内或竞技场可能受限制；实现时必须有轮廓兜底和文字兜底，不能崩溃。
- 轮廓呼吸若每帧改色可能带来性能与闪烁问题；先按 0.25 秒阈值更新，游戏内实测后再调。
- 场景喊话的 AI 回复语义需要后续内容包细化；本版只保证入口、上下文和独立会话成立。

## Out of scope

- 不做 AF 的完整喊话模式菜单和美术资产；场景喊话状态条只显示当前状态，不做完整候选列表 UI。
- 不 Harmony 拦截原版 `StartConversation`，不反射写原版对话 VM。
- 不改 Messenger / 收件箱 / 周报现有 UI。
- 不新增屏幕候选列表 UI；可见性不足时先走文字降级。

## Verification

- SdkSmoke `PASS ALL`，包含新增索引/回绕/场景模式测试。
- 双版本构建 0 警告 0 错误。
- `validate_localization.ps1` 全绿。
- 游戏内验收条目见 Approach 6。
