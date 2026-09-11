# Worldbook Studio AI 生成管线持续红队——第二十轮收敛审计

> 日期：2026-09-06  
> 范围：前十九轮红队发现去重、根因合并、处置证据核对。  
> 状态：`NO_NEW_P0_P1_BUT_NOT_COMPLETE`  
> 本轮性质：只读收敛审计；未修改实现。

## 1. 审计目的

本轮不新增攻击面，避免把同一根因的不同表现重复计数。逐项检查：

- 前十九轮报告是否存在重复；
- 同一根因是否可以合并为稳定问题簇；
- 每个 P1 是否已经有实现处置证据；
- 连续无新增 P0/P1 的轮次是否满足；
- 是否可以宣布红队目标完成。

## 2. 统计

前十九轮报告共记录 `56` 个攻击条目，其中包含：

- 同一 CandidateSet lifecycle 根因在 create/review/UI 多处表现；
- 同一 evidence binding 根因在 quote hash、duplicate locator、normalized match 多处表现；
- 同一 Quick Authoring intent 缺口在 mode、hash、prompt、UI、retry 多处表现；
- 同一 local draft identity 根因在固定 key、恢复、timer、stale 多处表现。

去重后形成 `18` 个稳定问题簇：

| 编号 | 问题簇 | 严重度 |
|---|---|---|
| C01 | Quick/Semantic mode 与 user intent 未进入正式契约 | P1 |
| C02 | user fields 未进入完整 hash/fingerprint/lineage | P1 |
| C03 | Prompt data block 与用户输入隔离策略未代码化 | P1 |
| C04 | unknown content tier 默认 base | P1 |
| C05 | domain/subdomain/related domains authority 与落盘保真 | P1 |
| C06 | warnings/unresolved/coverage 生命周期和落盘 | P1 |
| C07 | proposition/evidence 语义覆盖不足 | P1 |
| C08 | evidence exact/normalized/hash/locator 状态混淆 | P1 |
| C09 | fact certainty/inferred 与 expression perspective/inferred 丢失 | P1 |
| C10 | CandidateSet lifecycle、stale、superseded、discarded 建档/审查边界 | P1 |
| C11 | review decision expected generation/CAS 缺失 | P1 |
| C12 | review decision 幂等与重复物化 | P1 |
| C13 | candidate/fact/expression ID 冲突与 merge 静默去重 | P1 |
| C14 | split derived candidate 的 source span 归属 | P1 |
| C15 | Draft state schema/migration/duplicate record/recovery | P1 |
| C16 | local draft identity、并行隔离、服务端 Draft 续接和 timer | P1 |
| C17 | Batch facts 空结果与 source/cache binding | P1 |
| C18 | 输入字段独立资源上限 | P1 |

## 3. 处置证据审计

当前工作区没有针对上述 C01–C18 的实现修复提交、修复回归或用户签收证据。现有 `33/33`、`21/21` 和 `113/113` 基线只证明旧契约没有回归，不证明这些问题已处置。

因此：

```text
P0 = 0
去重后待处置 P1 = 18
P1 处置证据 = 0/18
```

## 4. 红队收敛审计

第十八轮和第十九轮，以及本轮收敛审计，都没有新增 P0/P1。连续无新增轮次已满足：

```text
3 轮
```

但“连续无新增”只是收敛条件的一部分。目标还要求：

```text
P1 已有处置证据
```

该条件尚未满足，不能调用 `update_goal complete`。

## 5. 结论

本轮不新增问题，也不重复计数；但红队目标仍保持 active：

```text
REDACTION_COMPLETE = true
P0_CLEAR = true
NO_NEW_P0_P1_ROUNDS = 3
P1_REMEDIATION_EVIDENCE = false
GOAL_COMPLETE = false
```

下一步不是继续无限扩张红队，而是进入一个明确授权的 Worldbook Studio 修复批次，按 C01–C18 分批处理；每个修复批次完成后重新进行定向红队。

## 6. 边界声明

本轮未：

- 修改 Worldbook Studio 实现；
- 修改 AWAKE 模组本体；
- 修改语义迁移候选；
- 修改真实源目录；
- 访问真实 Provider、API Key、Token、Worker 或网络；
- 启动 Bannerlord；
- 同步游戏目录。

