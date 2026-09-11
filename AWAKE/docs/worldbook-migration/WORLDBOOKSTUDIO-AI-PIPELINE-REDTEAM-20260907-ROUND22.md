# Worldbook Studio AI Pipeline Red Team — Round 22

> 日期：2026-09-07
> 范围：C01–C18 修复后的 Worldbook Studio AI 生成链路
> 性质：独立静态红队 + 离线合同/回归复核
> 状态：`NO_NEW_P0_P1`

## 检查边界

- 只检查 Worldbook Studio、Batch/cache、离线测试和契约文档。
- 未启动 Bannerlord。
- 未访问真实 Cloud Provider、API Key、Token、Worker 或外网。
- 未修改 AWAKE 模组本体、ModuleData、dist、真实源目录或迁移候选。

## 攻击面

- Quick Authoring/Semantic Migration 模式旁路。
- `unknown` content tier 静默降级。
- candidate/fact/expression 生命周期与建档门。
- proposition/claim/target span 来源归属。
- warning/unresolved/coverage 丢失。
- Draft 状态 schema、重复记录、恢复和跨任务 key。
- Batch `no_candidate`、cache source binding 和 report 汇总。
- 输入字段独立资源上限。
- system prompt 与 source/user data 注入隔离。

## 结果

- 未发现新的独立 P0/P1。
- 发现的 `unknown/base`、`no_candidate`、Draft state、split source span 等旧问题均已有对应修复代码和 focused 回归，不重复计数。
- `Semantic Migration` 仍被显式拒绝，没有静默降级为 Quick Authoring。
- `create-document` 继续检查全局及所选 candidate 的 blocking unresolved、生命周期和人工采纳状态。
- `source_spans`、`target_spans`、claims 和 propositions 在 split 派生候选中按来源过滤，无法归属时进入阻断诊断。
- Batch `no_candidate` 已同时进入 item contract、public report counts 和 UI 状态文案。

## 证据

- `scripts/batch-contract-check.ps1`：`1133/1133 PASS`。
- 修复后全量 `scripts/test.ps1`：前端、Worldbook Studio、Batch、Draft、Workstation、HTTP smoke 全部通过。
- DraftTests：`55/55 PASS`。
- BatchTests：`23/23 PASS`。
- Worldbook Studio harness：`113/113 PASS`。
- Draft HTTP smoke：服务端续接、幂等、候选切换、Quick Authoring semantic closure 通过。

## 收敛计数

```text
P0 = 0
本轮新增 P0/P1 = 0
连续无新增 P0/P1 = 1 轮
P1 处置证据 = 已覆盖 C01–C18 修复批次
GOAL_COMPLETE = false
```

下一步：进行 Round23 独立复测；只有第二轮同样无新增 P0/P1，且完成项逐条证据审计后，才评估 Goal 是否可以结束。
