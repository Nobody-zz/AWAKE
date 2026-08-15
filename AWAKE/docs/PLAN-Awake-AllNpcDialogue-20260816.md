# AWAKE 全 NPC 对话方案

> 日期：2026-08-16
> 状态：草案，待 grill-me 锁定后分批实现。
> 目标：达到类似 AF 的效果——有名英雄、无名士兵、平民、酒馆老板、守卫、商贩等都能进入 AI 对话。

## 1. 现状与差距

当前 AWAKE 深谈只支持：

- `Hero` 目标
- 附近的有名英雄
- 大地图/定居点菜单环境
- `Hero` 记忆和 `Hero` 关系命令

缺少：

- `CharacterObject` / `Agent` / `LocationCharacter` 目标模型
- 无名 NPC 候选列表
- 无名 NPC 的降级身份档案
- 无名 NPC 记忆与持久化
- 场景内对话入口
- 原版对话启动时的 AI 接管

## 2. 目标架构

引入统一的对话目标：

```csharp
public sealed class AwakeNpcTarget
{
    public Hero Hero;              // 有名英雄时非空
    public CharacterObject Character; // 所有目标都有
    public int AgentIndex;         // 场景代理，-1 表示无
    public LocationCharacter LocationCharacter;
    public bool IsHero;
    public string StableId;        // 稳定目标 ID
    public string DisplayName;
    public string CultureId;
    public string TroopId;
    public string UnnamedKey;
    public string UnnamedRank;
    public bool IsFemale;
    public float Age;
}
```

候选来源：

- 附近有名英雄：现有 `AliveHeroes` + 定居点/队伍过滤
- 当前场景代理：`Mission.Current.Agents`
- 当前地点角色：`LocationComplex.Current.GetListOfCharacters()`
- 队伍兵种/俘虏：`MobileParty.MainParty.MemberRoster` / `PrisonRoster`
- 无名 NPC：按 `CharacterObject.StringId + culture + troop + gender + rank` 生成 `UnnamedKey`

## 3. 分阶段

### Stage A：目标模型与候选入口

- 新增 `AwakeNpcTarget`
- `NpcDialogueLauncher` 增加 `GetNearbyTargets(limit)` / `FindTargetById` / `TryOpenDialogue(AwakeNpcTarget)`
- 命令台“深谈”候选列表同时展示英雄和无名 NPC
- 允许在场景内呼出命令台并选择附近代理
- 无名 NPC 先不持久记忆，只做会话内上下文

当前状态：代码已落地，游戏内场景候选与回退对话待验证。

验收：

- 命令台能列出当前场景/地点的英雄与无名 NPC
- 选择无名 NPC 后能打开 AWAKE 覆盖层或回退原版对话
- 不崩溃、不误伤原版对话

### Stage B：无名 NPC 身份档案

- 从 `CharacterObject`、文化、王国、兵种、性别、年龄、身份生成身份块
- 仿照 AF `NpcDataPacket`：名字、身份、性格、背景、角色描述
- 运行时优先查找已有档案，找不到时生成一次回退档案
- 档案保留在会话/存档存储，不写进提示词正文

验收：

- 同一兵种/文化/身份的无名 NPC 有稳定但不过度统一的说话方式
- 无名平民不会突然拥有领主知识或贵族权力

### Stage C：无名 NPC 记忆

- 无名 NPC 使用 `UnnamedKey + 场景/队伍/Agent 会话标识` 作为记忆键
- 同一场景会话内连续对话可引用上次记忆
- 离开场景后，队内无名 NPC 按队伍成员索引保留短期记忆
- 不做“每个路边村民永久独立人格”的膨胀存储

验收：

- 场景内连续对话有上下文
- 同名同兵种 NPC 不互相串记忆
- 存档重开后不产生明显错误

### Stage D：原版对话 AI 接管

- 通过安全钩子拦截 `CampaignMapConversation` / `ConversationManager.StartConversation`
- 玩家靠近任意 NPC 发起原版对话时，先进入 AWAKE 深谈
- 覆盖层不可用时回退原版对话
- 拦截必须带上下文门，不能影响任务、竞技场、战斗、强制事件

验收：

- 城镇/城堡/村庄/野外的英雄和无名 NPC 均能自然发起 AI 对话
- 原版任务对话不被误接管
- 对话退出后状态一致

## 4. 命令与语义边界

- 有名英雄：可执行关系、记忆、内容包命令
- 无名 NPC：初始只允许无主目标命令或由内容包显式声明的命令
- 任何命令仍走 `CommandRiskPolicy + PermissionGate + Preflight + 幂等`
- `heroId` 锁定仅适用于 Hero 目标；无名 NPC 使用 `target.stableId` 作为目标键

## 5. 不做的事

- 不照搬 AF 的 `[ACTION:...]` 标签
- 不引入 ONNX 检索
- 不把女神、发情、俘虏等内容语义写进 AWAKE 核心
- 第一阶段不做每个无名 NPC 的永久独立人格

## 6. 验证命令

- 构建：`dotnet build -c Release -p:BannerlordApi=1.3.15`
- SdkSmoke：`Awake.SdkSmoke.exe` PASS
- 游戏内：命令台列出英雄 + 无名 NPC；场景内选择无名 NPC 后进入深谈；覆盖层失败回退原版对话
