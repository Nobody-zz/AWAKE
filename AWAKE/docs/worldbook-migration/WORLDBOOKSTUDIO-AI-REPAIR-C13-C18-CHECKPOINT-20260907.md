# Worldbook Studio AI 修复批次 C13–C18 Checkpoint

> 日期：2026-09-07
> 状态：已实现并通过离线回归；总 Goal 仍保持 active
> 范围：Worldbook Studio AI 生成链路、Batch/cache、Draft 状态与离线测试

## 本批完成

- C14：split 派生候选按事实证据、source origin、claim 绑定过滤 `source_spans`、`target_spans`、`claims`、`propositions`；无法归属的 source span 生成 blocking unresolved，不再复制到所有派生候选。
- C15：服务端 Draft 状态文件加入显式 v2 schema；旧 v1 可读取并在下次写入时升级；未知 schema 和重复 Draft/Consent/Attempt 记录隔离到 quarantine 文件。
- C16：本地 Draft key 加入浏览器 task identity，避免同工作区并行任务串线；旧 key 提供兼容回退；保存 `draftId/serverSourceContentHash`，恢复后可安全接回服务端 Draft；源文修改会解除旧 Draft 绑定；关闭/离页立即 flush。
- C17：Batch 空 facts 结果合法结算为 `no_candidate`，零 evidence；Batch item/report contract 同步声明 `no_candidate`；公开报告和 UI 汇总计数、状态文案同步；cache key 继续绑定 source content hash，空 cache payload 可读取。
- C18：Draft request 工厂对 accepted facts、evidence、metadata、perspectives、registry summary、candidate id 和 canonical request bytes 施加独立资源上限。

## 已有相关安全链路

- Quick Authoring 与 legacy staged 入口按模式分流。
- Semantic Migration 仍显式拒绝，不静默降级。
- `propositions → claims → target_spans` 有 ID、source origin、coverage 和 quote 边界校验。
- `must_not_invent` 阻断新增人物、年份、战争和未登记正式 ID。
- rumor/history/perspective/quote drift 会进入 blocking unresolved。
- `review_only`、`pending/kept`、CAS、consent、retry/recovery 和 create-document 门保持不变。

## 验证结果

- Release solution build：0 warnings / 0 errors。
- DraftTests：`55/55 PASS`。
- BatchTests：`23/23 PASS`。
- Worldbook Studio harness：`113/113 PASS`。
- Frontend editor/session/safety/draft/batch/customer harness：全部通过。
- Draft HTTP smoke：staged、服务端续接、幂等、伪造 evidence、候选切换和 Quick Authoring semantic closure 通过。
- Authoring Save、Public Contract、Workstation handoff、Batch HTTP smoke：通过。
- C14 split source attribution、C15 state migration/quarantine、C16 server resume/task isolation、C17 no-candidate report、C18 independent budgets 均有 focused 回归。

## Batch 合同

- Revision 保持 `14`，因允许 `no_candidate` 更新 approved contract hash。
- 当前权威合同 hash：`df19cc670db082bed8702c2cd95c60c22c91b5340a27d841d915514810bd8ffb`。
- 核心常量、package/release 校验脚本已同步。

## 仍待处理

- 需要对已修复 C01–C18 做连续两轮定向红队复测并封存报告。
- 需要完成 P1 逐项处置证据矩阵，确认没有只通过旧基线而未覆盖的条目。
- Batch 尚未承担完整 proposition-level Semantic Migration；其 `batch_facts_only` coverage 明确标记为非迁移证明。
- Semantic Migration 独立工作流仍不在本 Goal 实现范围内。

## 硬边界声明

- 未启动 Bannerlord。
- 未同步游戏目录。
- 未访问真实 Cloud Provider、API Key、Token、Worker 或网络服务。
- 未修改 AWAKE 模组本体、ModuleData、dist 或世界书迁移候选。
- 未将任何候选标记为 `approved`、`canon`、`compiled`、`published` 或 `runtime-ready`。
