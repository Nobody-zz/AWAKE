# Plan: Worldbook Studio 批量作者工作流 V1

- date: 2026-08-24
- status: revision_13_pending_independent_review
- authority: `docs/superpowers/specs/2026-08-24-worldbook-studio-batch-authoring-design.md` 与同目录 contracts.v2.json（Revision 13）
- code_gate: 未取得 `VERDICT: APPROVED` 前不修改批量实现代码

## 目标

在不改变现有单份世界书契约和游戏运行时的前提下，为 Worldbook Studio 增加可恢复、可缓存、可逐项失败隔离的批量事实整理流程，人工审核后创建 `needs_review` 作者草稿。

## Gate 顺序

1. Revision 13 设计、机器契约和计划三者一致。
2. 独立只读审查必须由当前主模型返回 `VERDICT: APPROVED`。
3. 取得批准后才建立代码写入租约；`REVISE`、`REVIEW_ERROR` 或本地 Worker 结构筛查结果均不得进入实现。

## 实施顺序（仅在 `VERDICT: APPROVED` 后）

1. 建立代码写入租约，锁定 Revision 13 契约 hash；未经批准只允许文档和机械自检。
2. 实现 contract loader/self-check、workspace 级 create reservation、预批次 `ScanRepository`、正式 `BatchRepository`、atomic promotion、WorkspacePolicy 隔离和恢复器。
3. 实现 manifest/item/result-stage 状态机、revision/CAS、journal、quarantine、lease/fence/claim_generation 和 unknown-result 占位语义。
4. 实现 source unit、normalized snapshot、evidence 三重绑定、locator/quote/binding hash 和服务端重新计算。
5. 实现跨进程 idempotency reservation、facts/metadata 独立结果、needs_review 建档和 cache-materialization sidecar。
6. 实现 Provider 批量适配层、cache-hit attempt、local=1、cloud=2/最大4、120 秒 logical deadline、Retry-After 和三次总 attempt。
7. 实现 Web/API/UI：scan、create、get、consent、start、pause、cancel、claim、retry-item、review、item-detail、source-unit、prebatch-source-unit、create-documents、report；所有载荷按契约校验。
8. 新增并接入 batch contract/repository/recovery/provider/API/cache 测试，再运行现有 Studio、Draft、Launcher、Worker/Provider Smoke、package/release-check；未通过不得发布。

## 专项测试

- contracts JSON parse、schema required/enum/limit、Canonical JSON golden vectors、create reservation 与 error_ref 一致性；
- 50+ item 建立/恢复、每个 journal phase 崩溃窗口、孤儿 temp quarantine、revision/commit_seq 不一致、schema migration；
- 状态转移矩阵、过期 lease、旧 fence 晚到、metadata_pending/metadata_running 双阶段、metadata failed/unknown_result → retry-item → metadata_pending 回边、facts-only/facts-and-metadata consent、单项失败隔离；
- reservation CreateNew 竞争、崩溃半提交、稳定 document_id、sidecar/正式档案关联和冲突输入拒绝；
- batches 不进入 ListDocuments/Snapshot/Validate/Compile/Export；multipart 路径碰撞、reparse/设备路径、TOCTOU、200 文件/64 MiB/16 MiB/80,000 字符边界；
- evidence source/locator/quote 验证、UTF-16 offset、NFC/LF、伪造证据和错误码；
- Provider 并发、deadline、Retry-After 秒数/HTTP-date、fallback 同 logical attempt、永久错误、unknown_result、3 次总 attempt；
- cache 每个 key 字段变化逐项失效、等价规范化输入命中、损坏/旧 schema quarantine、无负缓存；
- Web scan→create→consent→start→review→item-detail/source-unit/prebatch-source-unit→create-documents→report、prebatch promotion journal/idempotency、consent CAS、CSRF/Origin/owner/session 重启、claim_generation、cache entry/payload/marker、expected_revision/scan_hash 409、authoring projection、expressions 硬拒绝；
- 现有 Studio 101/101、Draft、Launcher、离线 Worker/Provider Smoke 和 package/release-check 回归。

## 非目标

- 游戏内世界书读取器、周报系统、时间线、存档兼容和游戏目录同步；
- AI 自动发布、自动覆盖、自动冲突裁决、人物/家族绑定；
- 批量 expressions、CLI 批量命令、联网抓取和运行时学习系统。

## 完成定义

入口→上传→扫描→任务持久化→Provider 调度→事实审核→元数据选择→作者草稿创建→报告查询完整可达；所有关键失败都有可见状态；正式档案始终为 `needs_review`；不修改冻结 AWAKE 候选。

## Gate

Revision 13 必须重新通过独立只读审查并返回 `VERDICT: APPROVED`，之后才建立代码写入租约。`REVISE`、`REVIEW_ERROR` 和仅本地 Worker 的结果均不得开始实现。
## Revision 13 修订重点

- API 路由绑定 request/response/error schema、HTTP 状态、重试语义和 CAS 载荷；`consent-issue` 只返回一次原始 token，`create-documents` 返回逐项结果与批量摘要。
- 预批次目录与正式 batch promotion 进入契约；测试覆盖 promotion 中断、hash 不匹配、重复 promotion 和 quarantine。
- 新增 source unit、typed fact、item-detail/source-unit 读取和 evidence 细分错误码；服务端验证 fact → evidence → source snapshot 三重关系。
- `facts_result_ref`、`metadata_result_ref`、`result_stage` 明确两阶段状态；Provider/cache-hit 都必须有可审计 attempt。
- evidence 明确 locator、quote、binding 三类 hash 公式；attempt 绑定 batch/item/provider/lease/fence/deadline/claim_generation。
- consent 由 workspace marker 持久识别，重启后通过 CAS claim 恢复并撤销旧 generation 的未消费 consent。
- cache 使用 workspace 级共享 provider payload 的 entry/payload/commit-marker 三文件提交；命中后重新物化当前 batch 的 result/evidence。
- 测试入口固定为 `tools/worldbook-studio/tests/Awake.WorldbookStudio.BatchTests/`，由 `tools/worldbook-studio/scripts/test.ps1` 调用，并由 `tools/worldbook-studio/scripts/release-check.ps1` 汇总；现有单份测试保持不变。
- `common.id` 不允许路径分隔符或 `..`；批量 schema 对 facts/metadata 限界，expressions、身份、人物绑定继续硬拒绝。

- Revision 13 额外覆盖 start/claim journal、创建前完整恢复映射、Provider fingerprint 公式、严格 cache payload、public API projection、路径段拒绝和 release/test 门禁。

- Revision 13 收口 scope/path 约束、promotion 唯一恢复权威、创建前幂等映射、token known/null、cache 三文件恢复、needs_review 强制和 consent snapshot 二次校验。


## Revision 13 门禁修订

1. 先完成 allocation_pending/allocation_id 崩溃恢复、promotion owner/fence 接管和 journal 终态映射。
2. 明确 `accepted → metadata_pending → metadata_running` 的事实审核、consent scope、metadata 调度和失败重试回边。
3. 明确 batch-level start 的 target_item_ids、授权子集、全量 CAS、部分 Provider 结果和按 item 的 consent 结算。
3. 增加 public metadata projection，封闭 report、last_error 和 report_summary 的公开字段边界。
4. 重新运行契约机械自检；只有当前主模型独立只读审查返回精确 `VERDICT: APPROVED` 后才建立代码写入租约。


## Revision 13 审查修订记录

- P1-1：加入 `allocation_id` 与 `allocation_pending`，定义 batch_id 分配到 prepared journal 之间的恢复锚点；batch_id 一旦落盘永久保留。
- P1-2：加入 promotion claim-rebind CAS、claim_generation 递增和旧 fence 拒绝规则。
- P1-3：补齐 journal `prepared/copying/verified/renamed/committed/aborted/quarantined/needs_reconcile` 到 reservation/API 终态的完整矩阵。
- P1-4：加入 `public_metadata_result`，纳入 item-detail public projection，编辑者可读取 AI 标题、摘要、主分类和二级分类候选。
- P1-5：明确 review accepted、facts-only/facts-and-metadata consent 与 metadata_pending 的触发/重试/提交条件。
- P1-8：拆分 metadata_pending（等待，active=none）与 metadata_running（执行中，active=metadata），并将 retry-item(stage=metadata) 定义为唯一失败回边。
- P1-9：为 batch-level start 增加 authorized_item_ids/target_item_ids、目标子集校验、全量 CAS 和按 item 的部分结果/consent 结算。
- P1-10：将通用 failed/unknown_result 回边改为带 stage 的 facts retry 与 metadata retry，禁止 metadata 失败隐式回 queued。
- P1-6：新增封闭的 public report、公开错误码/摘要和 report_summary counts projection；内部 report 与任意 last_error 不再作为 API 响应。
- P1-7：统一契约、设计和计划为 Revision 13。
- P2：BatchTests、统一路径 validator 和 release-check 接线继续作为获批后的实现门禁，不在批准前修改业务代码。
- P0 修订：删除 metadata result-time consent 消费语义，固定 schedule journal commit 后、Provider 前消费，并加入机械断言。
- P1 修订：明确通用 batch journal 与独立 promotion journal 使用不同状态枚举，避免 copying/verified/renamed 泄漏到通用恢复器。
- P2-2：metadata_pending/metadata_running 同时要求 accepted_fact_ids，已提前硬化为 conditional-required。
