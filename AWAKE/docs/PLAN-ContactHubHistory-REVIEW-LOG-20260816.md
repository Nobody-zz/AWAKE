# Plan Review Log: 联系人中心 + 对话历史

MAX_ROUNDS=5。

## Round 1 — Codex

VERDICT: REVISE

要点：512KB 上限、无名 NPC key、delete/clear 范围、迁移、prompt 原文注入、UI 同步加载、开发者权限、卡片聚合 key、schema 一致性、直接对话未落 transcript。

## Round 2 — Codex

VERDICT: REVISE

要点：chunk 元数据/拆分原子性、pinnedIds 位置、scene-shout 目标、audit 契约、roll 触发器。

## Round 3 — Codex

VERDICT: REVISE

要点：metadata/audit 缺 appliedKeys、跨 chunk roll、bounded_session_summary 可能泄漏原文。

## Round 4 — Codex

VERDICT: REVISE

要点：canonical 联系人行与 live target 解耦、roll audit 两阶段顺序。

## Round 5 — Codex

VERDICT: REVISE

要点：canonical 历史行可能没有 live target，需支持无 target 的只读历史/固定；roll 需要 audit-intent 先行，再逐 chunk 幂等更新，最后 metadata/audit 提交。

## 最终状态

- 用户要求继续 GRILLME 后补跑 Round 6/7。

## Round 6 — Codex

VERDICT: REVISE

要点：transcript-only 联系人重载后无法枚举；audit-intent/audit-commit 未进入 audit schema。

## Round 7 — Codex

VERDICT: APPROVED

结论：新增 `campaign.contacts.v1` 索引、audit 增加 `phase/chunkIndexes/status` 后无新增 material blocker。

## 最终状态

- Round 7 APPROVED，可进入签收与实现。
- 实现时补充：`campaign.contacts.v1` 带 `schema/appliedKeys`；upsert 与 append/migration 共用幂等/恢复路径；`AwakeContactInfo.TargetId` 在 `Target == null` 时使用 canonical key。
