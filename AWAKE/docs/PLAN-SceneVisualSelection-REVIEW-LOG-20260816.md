# Plan Review Log: 场景可视化选人 + AI 场景对话独立接口

Act 1（设计 grill）完成 — 关键决策已锁定：AI 场景对话独立接口、原版对话窗口 AI 模式共存、AF 式简化可视化、扇形视锥、标准资产、MCM 默认开。MAX_ROUNDS=5。

## Round 1 — Codex（独立只读审查）

VERDICT: REVISE

主要问题：

1. 方案存在“检测器不 Harmony 拦截”与“不 Harmony 拦截”的表述歧义；应只保留状态跃迁检测。
2. 只监听开始/结束跃迁会漏掉读档时已处于对话中、以及对话中切换目标的情况；应初始化当前状态并每次重解析目标。
3. `OneToOneConversationCharacter` 可能为 null 或多人对话中不唯一；需要空安全回退并在无法唯一解析时禁用 AI 模式。
4. `Y` 循环索引在候选集、范围、角度变化时会过期；应在每次候选更新时重算并 clamp。
5. 候选为空或松开 `T` 无选中目标时的行为未定义；应 no-op。
6. 每 tick 更新 + 每 tick 60 个实体有帧尖峰风险；应阈值化更新并降低每 tick 创建数。
7. `sling_leadammo`/`throwing_stone` 标记可能是可拾取/碰撞/过小的物理实体；应改为纯视觉、禁用物理拾取，并验证最大距离可见。
8. 视锥没有遮挡检查，可能隔墙选人；应加 LOS。
9. 任务/竞技场/战斗/强制剧情门谓词未定义，手动按钮在强制剧情中仍可能破坏脚本；应隐藏/禁用。
10. 标记生成失败只列为风险，未列为实现行为；应要求降级为只高亮。
11. “纯数学”仍依赖 `SceneDialogueSelection.CurrentRange`，并不纯；应改为调用方传标量。
12. 验收依赖 `focus_pending` 修复但未加入游戏内验证项；应显式验证焦点与输入捕获。
13. 展开态遮挡原版答案区但未定义鼠标/键盘/手柄输入如何被压制；应明确捕获方式并验证三种输入。

### Claude/Codex 的裁决与修订

- 采纳 1：明确检测器只使用状态跃迁，不 Harmony。
- 采纳 2：启动/读档时同步当前对话状态，每次 tick 重解析 `OneToOneConversationCharacter`。
- 采纳 3：目标 null/不唯一时禁用 AI 模式按钮。
- 采纳 4：候选集每次更新重算并 clamp `Y` 索引。
- 采纳 5：无有效选中目标时松开 `T` no-op。
- 采纳 6：更新阈值化，每 tick 新建上限降到 16。
- 采纳 7：标记实体禁用拾取/物理/保存，并做最大距离可见性验收。
- 采纳 8：候选过滤增加基本视线检查，失败不入选。
- 采纳 9：定义上下文门谓词，强制剧情/任务等隐藏按钮。
- 采纳 10：标记失败降级为只高亮，作为实现行为。
- 采纳 11：预览数学只接收标量参数。
- 采纳 12：验收清单增加 overlay 焦点与输入捕获。
- 采纳 13：展开态由覆盖层捕获鼠标/键盘/手柄输入，并纳入验收。

修订完成，进入 Round 2。

## Round 2 — Codex（独立只读审查）

VERDICT: REVISE

主要问题：

1. 上下文门谓词仍不可实现：需要给出每个上下文的精确检测字段/API，并补 SdkSmoke 分类用例。
2. 每次 tick 重解析目标会导致 `OneToOneConversationCharacter` 瞬态 null/多人状态时反复重建会话；需要稳定 debounce。
3. 按 `agentIndex` 维护高亮会在 Agent 离开后索引复用时串台；需要保存 Agent 引用 + Mission 身份并校验。
4. `sling_leadammo`/`throwing_stone` 在最大距离可能太小/不可见；需要明确 scale、tint、渲染路径和最大距离验收。
5. `WithStaticPhysics` + `PhysicsDisabled` 的 spawn 组合未被验证；需要明确支持 flag 集或用纯视觉 API，并验证真实 spawn。

### Claude/Codex 的裁决与修订

- 采纳 1：新增 `NativeConversationAIModeContextGate` 纯判定类，列出 `IsConversationFlowActive`、Handler 类型名、Mission Mode、EncounterState、ConversationContext 反射探测等具体源，并补 SdkSmoke 用例。
- 采纳 2：目标需连续稳定 3 tick 才允许切换/重建会话。
- 采纳 3：高亮状态保存 Agent/Mission/StableId，应用与清除前校验。
- 采纳 4：标记实体放大 2.4x、青色轮廓/染色/光泽，并增加游戏内 `awake.scene.visual_selftest` 最大距离可见性验收。
- 采纳 5：明确使用 `CannotBePickedUp` spawn，随后禁用物理/保存；游戏内自测命令验证真实 spawn，SdkSmoke 只测纯判定与常量。

修订完成，进入 Round 3。

## Round 3 — Codex（独立只读审查）

VERDICT: APPROVED

结论：上一轮问题均已处理，未发现新的 material blocker。

保留的次要实现提醒：
- 上下文门谓词中的反射字段应缓存，避免每帧反射。
- 类名匹配应只在没有稳定 API 时使用。
- `DontSaveToScene` 应在游戏内自测里覆盖一次存/读档验证。

### 最终状态

- Act 1 设计 grill：完成。
- Act 2 独立只读审查：3 轮收敛，Round 3 `VERDICT: APPROVED`。
- 待用户签收后才进入实现。
