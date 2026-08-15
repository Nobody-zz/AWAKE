# AWAKE 对话方案落地可行性核验

> 日期：2026-08-16
> 结论：三批方案在当前架构上均可落地，但都有明确的改造点，不是“直接就能跑”。

## 1. 总体判断

现有代码已经具备：

- `AwakeNpcTarget`：统一目标模型
- `NpcDialogueService` / `NpcDialogueOverlay`：场景/面谈 AI 对话
- `AwakeMessengerService` / `AwakeMessengerOverlay`：通讯录
- `WorldStateStore`：存储命名空间与幂等命令框架
- `NpcMemoryService`：记忆管道
- `AiTaskGateway`：AI 路由

所以三批不是从零开发，而是把已有骨架补完。

## 2. Batch A 可行性

可行。

需要做：

- 把 `NpcDialogueService` 作为共享会话服务，供 Immersive 与 Messenger 两个视图复用。
- `NpcDialogueLauncher` 增加“屏幕中心 + 距离权重”候选排序。
- `T` 循环选择 + 高亮 + `Y` 确认。
- 遭遇面谈增加 `PlayerEncounterState.Begin/Wait` 与敌意门。

技术依据：

- `Agent.Position.AsVec2`
- `Agent.Main.LookDirection.AsVec2`
- `Agent.AgentVisuals.SetContourColor`
- `Team.IsEnemyOf`

风险：

- 屏幕中心选人的判定需要真机调参。
- 场景候选可能包含大量 Agent，需要限制距离和数量。

## 3. Batch B 可行性

可行。

需要做：

- `AwakeNpcTarget` 增加 `SessionKey` / `MemoryKey`
- `AwakeUnnamedProfileService` 增加种子化人格指纹
- 无名 NPC 记忆按 `ephemeral / party / promoted` 分级
- 成年判定改成职业白名单
- 无名 NPC 命令默认拒绝

技术依据：

- `CharacterObject.Occupation`
- `CharacterObject.IsSoldier`
- 现有 `NpcMemoryService` 可扩展 heroId 之外的 targetKey

风险：

- 场景会话 ID 需要稳定，不能直接用随机 Guid。
- 年龄不明的普通平民会不可对话，需要内容包后续补年龄。

## 4. Batch C 可行性

可行。

需要做：

- `WorldStateStore` 增加 `awake.conversations` 命名空间
- `WorldStateKind` 增加 Conversation
- 新增消息/未读/最近消息 schema
- 每日 tick 处理信使送达与 NPC 主动来信
- Messenger 增加写信模式和来信列表

技术依据：

- `AiTaskConstants.StorageNamespaceIds` 可扩展
- `CampaignEvents` 可监听每日 tick
- `AiTaskGateway` 可复用生成 AI 回复
- `AwakeConfig` 可加 MCM 开关

风险：

- AI 写信/回信需要新的输出契约或复用现有 NPC 对话路由。
- 信使送达与主动来信必须幂等，防止读档重复投递。

## 5. 落地顺序

1. Batch A：先统一共享会话服务与场景选择。
2. Batch B：再补无名 NPC 记忆键与年龄/命令门。
3. Batch C：最后做通讯录写信与主动来信。

每批完成后跑：

- `dotnet build -c Release -p:BannerlordApi=1.3.15`
- `Awake.SdkSmoke`
- 本地化校验
- 游戏内日志验证

## 6. 当前阻塞项

- 新马库斯框架已更新到游戏目录，但 AWAKE 仍引用 `SDK_20260815`。
- 如果新框架公开 API 有变化，需要先拿到新 SDK 再验证。
- 存储权限与 Companion pipe 尚未在新版本游戏内验证。
