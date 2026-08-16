# PLAN: 主动对话逻辑化（Batch 2）

> 状态：已由修复计划批准，实施前独立只读复核；复核记录见 `PLAN-ProactiveLogic-REVIEW-LOG-20260817.md`。

## Goal

把 `NpcProactiveService` 从“纯概率随机触发”改为“确定性条件为主、概率只做最终扰动”，并记录可解释触发理由。

## Changes

1. `NpcProactiveCandidate` 新增 `TriggerReason`，序列化/反序列化兼容旧档。
2. `NpcProactiveService` 新增纯函数：
   - `BuildTriggerReason(affinity, hasRelationship)`：根据关系事实生成可解释理由。
   - `ComputeTriggerChance(affinity, hasRelationship, chancePercent)`：关系缺失时压低概率，高亲密度提高概率，最终受 MCM 概率缩放。
3. `EvaluateAsync`：
   - 关系缺失且无候选事实时使用低基础概率，避免日志里的高频 `casual`。
   - 高亲密度/高敌意使用不同动机与理由。
   - 创建候选时写入 `TriggerReason`，日志输出理由。
4. MCM 保持 `EnableNpcProactive` 与 `NpcProactiveChance`，仅更新提示：概率是最终扰动，不是主要触发条件。

## Verification

- SdkSmoke：触发理由、关系缺失低概率、高亲密度概率、JSON 持久化。
- 构建 0 警告 0 错误；release check 全绿。
- 游戏内验收：主动对话频率明显下降，触发时有可解释理由，不再连续刷 `motive=casual`。
