# Batch B 审查日志

> 审查对象：无名 NPC 对话
> 日期：2026-08-16
> 状态：Round 1 REVISE

## Round 1 - Codex 对抗审查

### 发现 1：无名 NPC 稳定 ID 与记忆可能串人

当前 `UnnamedKey` 只按 `CharacterObject.StringId + culture + troop + rank + gender` 生成。同一场景、同一兵种的多个人会共享同一个记忆键。

修订：记忆键必须加入会话标识（场景会话 / AgentIndex / 队伍 ID），不允许同兵种 NPC 互相串记忆。

### 发现 2：年龄未知时默认成年

当前 `AwakeNpcTarget.ResolveAge` 在 `character.Age <= 0` 时默认 30 岁。这会把年龄不明的无名 NPC 当成年人放行，不符合内容红线。

修订：年龄未知时只允许明显成年身份进入，例如士兵、商贩、酒馆老板等；普通平民如果年龄不明，不进入对话。

### 发现 3：无名 NPC 命令边界没定

当前运行时命令白名单为空，但计划没有说明无名 NPC 是否允许关系/身体/发情命令。

修订：无名 NPC 默认不允许 Hero 关系命令；只有内容包显式注册的通用命令可以执行。

### 发现 4：无名 NPC 记忆持久化策略缺失

计划没有回答“路边村民是否永久记住玩家”。

修订：无名 NPC 默认只有会话级/队伍级短期记忆；不生成每个路边村民的永久独立人格。

## 结论

`VERDICT: REVISE`

修订后的锁定决策写入 `PLAN-Awake-Dialogue-BatchB-20260816.md`。
