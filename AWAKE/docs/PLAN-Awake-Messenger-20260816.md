# AWAKE Messenger 统一对话方案

> 日期：2026-08-16
> 状态：草案，待 grill-me 锁定后分阶段实现。
> 目标：结合 AliceMM 的社交聊天 UI 与 AF 的场景内即时对话，把“先选人再对话”改成“通讯录 + 消息应用”。

## 1. 现状问题

- 当前命令台打开后，深谈仍是一个“选择人物”弹窗。
- 没有联系人列表、未读消息、最近对话、时间线。
- 场景内对话与场景外对话没有统一入口。
- 对话历史只按会话存在，没有“通讯记录”的触感。

## 2. 从 AliceMM 学到的

### ChatPanel / ChatPanelVM

- 独立 Gauntlet 覆盖层，带输入框、发送按钮、消息列表、建议 chips、滚动和暂停游戏。
- `MBBindingList<ChatMessageVM>` 管理消息，支持玩家/NPC/系统三种消息形态。
- 左侧参与者头像栏、右侧图片栏，可折叠。

### NPCConversationManager

- 会话按 NPC 持久化，记录 `role / text / turn / gameDay / sessionId / sessionType`。
- `sessionType` 区分 `single / letter / multi`。
- 提示词历史从数据库按最近 N 条加载。

### LetterManager / LetterStorage

- 场景外对话用“信件”实现：按距离计费，发送后延迟一天回复。
- NPC 也有主动来信机制，每天限量。

### GroupConversationManager

- 群聊按参与者 ID 哈希生成 groupId，保存参与者、消息、更新时间。
- 群聊和单聊共用同一套历史提示词逻辑。

## 3. 从 AF 学到的

- 场景内对话要即时、流式、贴近角色。
- 覆盖层失败回退原版对话。
- `NpcDialogueLauncher` 统一入口，支持 Hero / CharacterObject / Agent。

## 4. AWAKE 目标形态

### 入口

- 命令台快捷键打开“AWAKE 通讯录”。
- 通讯录显示所有可对话对象：有名英雄、无名 NPC、场景内人物、远方熟识者。
- 不再弹“选择要深谈的对象”，而是进入联系人列表。

### 联系人列表

- 显示：头像/占位、名字、身份、最近消息、未读标记、状态。
- 状态分为：`附近可即时对话`、`远方可写信`、`忙碌/不可用`。
- 排序：最近对话优先，未读优先，附近人物优先。

### 对话窗口

- 统一聊天界面：消息气泡、时间、发送者、流式回复、输入框、发送按钮、建议 chips。
- 场景内：即时发送、即时回复。
- 场景外：进入“写信/发消息”流程，扣钱或消耗资源，回复延迟一天。
- NPC 主动来信：地图通知 + 通讯录未读标记。

### 会话模型

```csharp
public sealed class AwakeConversation
{
    public string ConversationId;
    public string TargetId;
    public string TargetName;
    public string SessionType; // "instant" / "letter" / "group"
    public int LastMessageDay;
    public int UnreadCount;
    public string LastMessagePreview;
}

public sealed class AwakeChatMessage
{
    public string ConversationId;
    public string SpeakerId;
    public string SpeakerName;
    public string Text;
    public int GameDay;
    public string Kind; // player / npc / system
}
```

### UI 结构

```text
AWAKE Messenger
├─ 左侧：联系人列表
│   ├─ 附近人物
│   ├─ 远方熟识
│   └─ 未读优先
└─ 右侧：对话窗口
    ├─ 消息流
    ├─ 建议 chips
    ├─ 输入框 + 发送
    └─ 状态栏（即时/信件/等待回复）
```

## 5. 分阶段

### M1：通讯录 + 会话模型

- 新增 `AwakeConversation` / `AwakeChatMessage` / `AwakeMessengerService`
- 命令台深谈改为打开通讯录
- 联系人列表可进入对话
- 场景内对话复用现有 `NpcDialogueService`

当前状态：通讯录与统一聊天面板已落地，命令台入口已切换；会话持久化和未读计数待 M2/M3。

验收：

- 不再出现“选择人物”弹窗
- 通讯录能看到附近和远方联系人
- 点击联系人进入统一对话窗口

### M2：聊天 UI

- 新增 `AwakeChatOverlay` / `AwakeChatVM`
- 消息气泡、时间、发送者、输入、发送、建议 chips
- 场景内流式回复接入

验收：

- 场景内和场景外使用同一套聊天 UI
- 文字不重叠、可滚动、可输入

### M3：场景外消息

- 远方联系人进入“写信/发消息”流程
- 距离计费、回复延迟、NPC 主动来信
- 未读标记与地图通知

验收：

- 玩家在大地图可以和远方 NPC 保持对话
- NPC 会主动来信
- 存档重开未读与历史仍可恢复

### M4：群聊与后续

- 多人会话
- 周报/事件进入通讯记录
- 后续内容包可注册“动作按钮”

## 6. 关键决策

- 保留命令台快捷键，但把它当成“打开 AWAKE 通讯录”的入口。
- 不照搬 AliceMM 的 SQLite；AWAKE 使用 Marcus Storage / `WorldStateStore`。
- 不照搬 AF 的原版对话接管；先从通讯录入口做起，后续再做原版对话拦截。
- 无名 NPC 使用 `AwakeNpcTarget.StableId` 作为联系人键。
- 场景外消息不是“即时短信”，而是符合骑砍时代的信件/信使节奏。

## 7. 不做的事

- 第一阶段不做群聊。
- 第一阶段不做头像/立绘资源系统。
- 不把内容包动作按钮写进核心。
- 不引入 AliceMM 的 SQLite 或数据库编辑器。

## 8. 验证

- 构建 0 警告 0 错误。
- `Awake.SdkSmoke` PASS ALL。
- 游戏内：快捷键打开通讯录 → 选择联系人 → 聊天窗口发送 → NPC 回复。
- 大地图：远方联系人写信 → 次日收到回复 → 未读显示。
