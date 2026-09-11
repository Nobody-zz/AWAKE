# Worldbook Studio AI 修复 C01–C18 证据矩阵

> 日期：2026-09-07
> 范围：Worldbook Studio AI 生成链路
> 结论：C01–C18 均已实现、定向验证并纳入全量离线回归

| 批次 | 处置内容 | 主要证据 |
|---|---|---|
| C01 | Quick Authoring / Semantic Migration 模式契约 | `AuthoringDraftContracts.cs`；DraftTests mode cases；Semantic Migration 501 拒绝 |
| C02 | intent 进入 request hash / fingerprint / lineage | `AuthoringDraftRequestFactory`、`AuthoringLifecycleContracts`；hash 回归 |
| C03 | prompt data block 与系统规则隔离 | `AuthoringDraftRequestSerializer`；source prompt injection test |
| C04 | unknown content tier 不静默变 base | `ResolveDraftContentTier`；tier red-team；Quick HTTP smoke |
| C05 | domain/subdomain/related domains authority 保真 | draft create validation；taxonomy/HTTP smoke |
| C06 | warnings/unresolved/coverage 生命周期 | Parser、Normalizer、Store；跨阶段合并测试 |
| C07 | proposition/claim/target span 语义图 | Parser、Normalizer、CandidateSet；绑定与 coverage 测试 |
| C08 | evidence exact/normalized/hash/locator 状态 | evidence normalization；verified/unverified/red-team cases |
| C09 | certainty/inferred/perspective 落盘 | document provenance annotations；DraftTests provenance assertions |
| C10 | CandidateSet lifecycle 与失效候选建档门 | `EnsureCandidateCanBeCreated`；discarded/superseded/stale tests |
| C11 | review expected generation/source/packet CAS | review endpoint baseline checks；review regression |
| C12 | review operation 幂等与重复物化防护 | operation id/request digest；review replay tests |
| C13 | candidate/fact/expression ID 唯一性与 merge 去重 | CandidateSet identity validation；duplicate identity tests |
| C14 | split 派生候选 source span 归属 | `AttributeSplit`；split attribution test |
| C15 | Draft state schema/migration/duplicate/recovery | server state v2；legacy/future/duplicate quarantine tests |
| C16 | local task identity、服务端续接、timer/flush | `draftLocalTaskId`、`draftRestoreServer`、pagehide flush；VM harness |
| C17 | Batch 空结果与 source/cache binding | `no_candidate` item/report contract；BatchTests `23/23` |
| C18 | 输入字段独立资源上限 | accepted facts/evidence/metadata/registry/request byte guards；budget red-team |

## 综合证据

- Release solution build：`0 warnings / 0 errors`。
- DraftTests：`55/55 PASS`。
- BatchTests：`23/23 PASS`。
- Worldbook Studio harness：`113/113 PASS`。
- Batch contract check：`1133/1133 PASS`。
- 前端 editor/session/safety/draft/batch/customer harness：全部通过。
- Draft HTTP smoke：staged、resume、幂等、伪造 evidence、候选切换、Quick Authoring semantic closure 全部通过。
- Authoring Save、Public Contract、Workstation handoff、Batch HTTP smoke：全部通过。
- 定向红队 Round22：无新增 P0/P1。
- 定向红队 Round23：无新增 P0/P1。

## 非目标与限制

- 不把 Quick Authoring 宣称为高保真 Semantic Migration。
- 不把 Batch facts-only 宣称为 proposition-level migration proof。
- 不修改语义迁移候选。
- 不访问真实 Provider、API Key、Token、Worker 或网络服务。
- 不启动 Bannerlord，不同步游戏目录。
- 不产生 `approved`、`canon`、`compiled`、`published` 或 `runtime-ready` 结果。
