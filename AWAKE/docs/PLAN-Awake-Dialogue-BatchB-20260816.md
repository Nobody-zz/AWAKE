# Batch B：无名 NPC 对话

> 日期：2026-08-16
> 状态：待用户签收的修订 PLAN

## 1. 目标

让无名 NPC 能对话，但不串记忆、不冒充成年人、不越权执行 Hero 命令。

## 2. 目标身份

- Hero：`hero:<StringId>`
- 无名场景 Agent：`npc:<CharacterId>:a<AgentIndex>`
- 无名静态/领队：`npc:<CharacterId>:static`
- 记忆键：`unnamed:<CharacterId>:<culture>:<troop>:<rank>:<gender>:<session>`

`session` 由场景会话、AgentIndex 或队伍 ID 决定，保证同兵种 NPC 不串记忆。

## 3. 成年校验

- `Character.Age >= 18`：允许。
- 年龄未知但身份明确为成年职业（士兵、商贩、酒馆老板、工匠、守卫等）：允许。
- 年龄未知的普通平民：不允许进入对话。

## 4. 无名 NPC 身份

- 运行时只生成确定性回退身份，不做 AI 生成式人格。
- 内容包后续可以注册更丰富的无名 NPC 档案。

## 5. 命令边界

- 无名 NPC 默认不能执行 Hero 关系命令。
- 只允许内容包显式注册的通用命令。
- 运行时命令白名单为空期间，无名 NPC 只对话，不改世界状态。

## 6. 记忆策略

- 场景内会话：短期记忆，绑定场景会话。
- 队伍内无名 NPC：按队伍 ID 保留短期记忆。
- 不生成每个路边村民的永久独立人格。
- 无名 NPC 升格为 Hero 后，才获得长期记忆与关系命令。

## 7. 验收

1. 两个同兵种 NPC 不会互相引用对方记忆。
2. 年龄不明的平民不会进入对话。
3. 无名 NPC 无法执行 Hero 关系命令。
4. 无名 NPC 可以正常 AI 对话。

## 8. 参考 AF / Alice

- AF `NpcDataPacket`：名字、身份、角色描述、UnnamedKey、兵种、文化、性别、年龄。
- AF `UnnamedNpcProfiles`：无名 NPC 有回退档案，但不要求运行时每个都生成。
- Alice `NPCConversationManager`：历史按 `sessionType` 区分，提示词只读最近 N 条。

不照搬：

- 不复制 AF 全部无名档案 JSON。
- 不复制 Alice 的 SQLite 会话库。
- 不给每个路边村民生成永久独立人格。

## 9. AWAKE 改进点

1. 种子化人格指纹：
   - `fingerprint = hash(characterId + culture + occupation + sceneRole + sessionSeed)`
   - 同一场景、同一个人稳定。
   - 同一兵种、不同个体由 `sessionSeed` 区分。
   - 不需要给每个无名 NPC 建档案文件。
2. 记忆分级：
   - `ephemeral`：场景会话内短期记忆。
   - `party`：队伍内无名 NPC 短期记忆。
   - `promoted`：升格为 Hero 后才长期记忆。
3. 成年判定用职业白名单：
   - 士兵、守卫、商贩、酒馆老板、工匠等明确成年身份允许。
   - 普通平民年龄不明则拒绝。
   - 不再用“默认 30 岁”兜底。
4. 无名 NPC 命令默认拒绝，内容包显式注册后才放行。
