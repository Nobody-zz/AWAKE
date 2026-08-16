# Plan: 场景可视化选人 + AI 场景对话独立接口

_Locked via grill — AWAKE 设计方 + 用户，待独立审查后签收_

## Goal

AWAKE 第一版同时具备两种 AI 对话方式：

1. **场景 AI 对话（独立接口）**：场景内按住 `T` 显示轻量可视化范围扇形，候选 NPC 金色轮廓、主目标品红轮廓，`Y` 循环切换，松开 `T` 只打开 AWAKE 自己的 `NpcDialogueOverlay`，不沿原版对话接口。
2. **原版对话窗口 AI 模式（共存接口）**：玩家进入原版对话后，AWAKE 在原版对话窗口上提供可切换的 AI 模式；开启后使用 AWAKE 自己的输入/回复 UI 接管当前对话目标，关闭后恢复原版对话选项，不破坏任务/强制对话。

## Approach

1. 新增 `SceneDialoguePreviewMath`（纯数学，无 Mission/Agent 依赖）：
   - 距离曲线由调用方传入当前范围标量，不直接依赖 `SceneDialogueSelection`。
   - 角度曲线：按住时间 0~4 秒，总角从 90° 增长到 300°，半角 = 总角 / 2。
   - 提供 `CurrentHalfAngle`、`BuildFanSegments`、`IsWithinCone`。
   - 常量：候选上限 48、地面标记上限 180、每 tick 新建上限 16。
2. 新增 `SceneDialoguePreview`（Mission 运行时）：
   - 用标准物品 `sling_leadammo`（回退 `throwing_stone`）通过 `Mission.SpawnWeaponWithNewEntity` 生成地面标记实体。
   - 生成时使用 `Mission.WeaponSpawnFlags.CannotBePickedUp`，随后 `DontSaveToScene | PhysicsDisabled`、`Stationary`、`SetPhysicsState(false)`，并放大帧 `ApplyScaleLocal(2.4f)`、青色轮廓/染色/光泽，作为纯视觉实体；若该 spawn 组合失败，自动降级为“只高亮候选、不画地面”。
   - 标记实体复用池：不足时按每 tick 16 个补建，超出隐藏；结束/退出时统一 `Remove`。
   - 候选高亮：全部候选金色 `(1, 0.84, 0.2)`，主目标品红 `(1, 0.22, 0.72)`；状态字典同时保存 Agent 引用、Mission 引用、StableId，应用/清除前验证仍匹配当前候选，防止 AgentIndex 复用导致高亮串台。
3. `AwakeTerminalBehavior` 接线：
   - `T` 按住期间每 tick 更新范围/角度，调用 `SceneDialoguePreview.Update`。
   - 只有当范围/角度/候选集变化超过阈值时才重建标记与高亮，避免每帧实体抖动。
   - 候选过滤 = 距离 + `IsWithinCone` + 基本视线检查；视线检查失败的目标不进入候选，防止隔墙选人。
   - `Y` 在过滤后的候选中循环；每次候选集更新都重算当前索引并 clamp，候选为空时保持无选中。
   - 松开 `T` 先 `SceneDialoguePreview.Clear`，再调用 `NpcDialogueLauncher.TryOpenDialogue(target, "scene")`。
   - 松开 `T` 时如果没有有效选中目标，直接结束，不打开对话也不报错。
   - 取消路径（Escape、离开场景、战斗/竞技场等模式、覆盖层打开）必须清空标记与高亮。
4. `NpcDialogueLauncher` 硬规则：
   - `entrySource == "scene"` 时，覆盖层打开失败直接返回 `None`，不再调用 `NpcDialogueStarter.TryOpenConversation`。
   - 保留 `NpcDialogueStarter` 供其他非场景入口使用，本轮不改其英雄/无名逻辑。
5. 原版对话窗口 AI 模式：
   - 新增 `NativeConversationAIMode` 检测器：完全通过 `Campaign.Current.ConversationManager.IsConversationInProgress` 状态跃迁检测；启动/读档时先同步当前状态，不使用 Harmony 拦截 `StartConversation`。
   - 对话中每次 tick 重解析 `OneToOneConversationCharacter`，但目标必须先连续稳定 3 tick 才允许切换/重建会话，避免目标经 null 或多人瞬态导致反复重建。
   - 对话目标通过 `Campaign.Current.ConversationManager.OneToOneConversationCharacter` 解析，映射为 `AwakeNpcTarget`；目标为 null 或无法唯一解析时禁用 AI 模式按钮。
   - 新增 Gauntlet 切换层：折叠态只显示角落的 AWAKE 按钮，不抢原版焦点；展开态显示 AI 输入/回复覆盖层并全屏遮挡原版答案区；关闭后恢复原版交互。
   - 展开态由覆盖层层捕获鼠标/键盘/手柄输入并阻挡原版答案区；折叠态恢复原版输入。
   - 上下文门谓词 `NativeConversationAIModeContextGate.CanShowAiButton`：以下任一条件为真则隐藏按钮：`IsConversationFlowActive`；`OneToOneConversationCharacter` 为 null/不唯一；`Mission.Current.Mode` 为 Battle/Deployment/Duel/Stealth/Tournament；`PlayerEncounter.Current` 非空且 `EncounterState` 不在 Begin/Wait；`ConversationManager.Handler` 类型名包含 `Quest`/`Issue`/`Barter`；`ConversationContext` 反射探测可用且值不是 `Default`/`PartyEncounter`。实现拆成纯判定类并补 SdkSmoke 用例。
6. MCM 与本地化：
   - 新增 `EnableSceneVisualSelection`（默认 true），关闭时回到现有文字提示 + 单目标高亮行为。
   - 新增 `EnableNativeConversationAIMode`（默认 true）与 `NativeConversationAIAutoOpen`（默认 false）双语 key。
   - 场景提示复用现有 `awake.scene.hold_hint` / `awake.scene.select_hint`；原生对话 AI 模式新增按钮与状态文案。
7. SdkSmoke 与游戏内自测：
   - `SceneDialoguePreviewMath` 测试：角度曲线单调、扇形段数不超过上限、`IsWithinCone` 前/后判定正确。
   - `NativeConversationAIMode` 测试：对话开始/结束状态跃迁、目标解析、目标稳定 debounce、上下文门禁判定。
   - 开发者自测命令 `awake.scene.visual_selftest`：游戏内真实 spawn 标记实体并记录成功/降级，覆盖最大距离可见性；不要求 SdkSmoke 做无法运行的游戏内 spawn。
   - 构建后本地化校验全绿。

## Key decisions & tradeoffs

- **AI 场景对话独立接口**：场景来源永不回退原版对话。代价是覆盖层失败时没有原版兜底，因此必须先修好 overlay 焦点（本轮已完成 focus_pending 放宽）。
- **原版对话窗口 AI 模式不替换原版**：AWAKE 以“切换层”共存，不反射写原生 DialogText，不隐藏原生答案区；开启 AI 模式时才用覆盖层遮住原版选项。这是比 AF 更干净的做法。
- **AF 式但简化**：保留“地面扇形 + 候选高亮 + 主目标高亮”的视觉核心，但标记数从 AF 的 760 降到 180，避免性能风险。
- **扇形只在前方**：候选过滤加入视锥角，与视觉一致；不再选中背后 NPC。这是行为变化，用户已倾向 AF 风格。
- **标准资产**：只使用游戏自带 `sling_leadammo` / `throwing_stone`，不复制 AF 自定义金币物品与 prefab。
- **MCM 默认开**：如果真机性能或视觉干扰严重，玩家可一键退回旧交互。

## Risks / open questions

- `SpawnWeaponWithNewEntity` 在部分场景（竞技场、室内）可能受物理/生成限制；失败必须降级为“只高亮候选、不画地面”，不能崩溃。
- 视锥角可能让玩家需要转身才能选到后方 NPC；若真机体感差，后续可把角度上限提高到 360° 并改画圆环。
- overlay 焦点放宽后尚未游戏内验证；本轮依赖该修复保证场景独立接口可用。

## Out of scope

- 不做 AF 的完整喊话/模式菜单。
- 不复制 AF 自定义美术资产、prefab、命名。
- 不反射修改原版对话 VM，不 Harmony 拦截 `ConversationManager.StartConversation`。
- 不改 `NpcDialogueOverlay` / `AwakeMessengerOverlay` 的会话模型；原生对话 AI 模式使用独立轻量切换层。
- 不新增屏幕候选列表 UI。
- 不修改英雄在非场景入口的原生对话回退。

## Verification

- `dotnet build -c Release -p:BannerlordApi=1.3.15` 0 警告 0 错误。
- `Awake.SdkSmoke` 全 PASS，包含新的预览数学测试。
- `validate_localization.ps1` 全绿。
- 游戏内验收：T 显示扇形与候选高亮、Y 切换主目标、松开 T 打开 AWAKE 覆盖层；场景来源日志出现 `scene_native_skipped`，无原生对话堆栈。
- 游戏内验收：进入原版对话出现 AWAKE 切换按钮；开启 AI 模式后鼠标/键盘/手柄输入被 AWAKE 捕获，能对话并返回原版；任务/强制对话不出现按钮。
- 游戏内验收：覆盖层 `focus_pending` 后输入仍可到达 AI 输入框，场景对话不依赖原生回退。
- 游戏内验收：`awake.scene.visual_selftest` 在最大距离下标记可见、不可拾取、不碰撞。
