# AWAKE 周报运行时接入实施复审

日期：2026-09-12  
范围：CampaignSessionReady、storage-only readiness、正式 v2 周报生成/持久化、Native-only 知识投影、v1 读取兼容及对应 runtime smoke。  
证据等级：E2

## 结论

`APPROVED`

独立只读终审未发现 P0 或 P1。

## 已核实闭环

1. `CampaignSessionReady` 先启动 `EnsureWorldStateStorageReadyAsync`，后台权限路径只调用 `PermissionGate.Evaluate`；恢复函数消费当前已安装的 `WorldStateStore`，不再调用 `EnsureWorldStateReadyAsync`。
2. Native continuation 只进入 `EnsureKnowledgeReadyIfNativeReadyAsync → EnsureKnowledgeReadyAsync`，不会再次生成或写入正式报告。
3. 正式报告路径锁定为 `QueryCompletedWeeklyDynamicsAsync → WeeklyDynamicsInput → TryBuildV2FromFacts → UpsertWeeklyReportStateAsync`，拒绝 legacy fallback 作为正式报告来源。
4. v2 写入得到 `OwnerApplied` 后，会从存储重新读取并核对 reportId、schema、状态、窗口、contentFingerprint、非 corrupt 及 canonical JSON；读回不一致不会冒充成功。
5. v1 快照可读回并投影，schemaVersion 保持 v1；v2 不会覆盖同 ID 的既有 v1 快照。
6. v2 报告空 `visibility.identity_ids` 按公开摘要处理，且 v2 的日期、窗口边界、来源闭包和 fingerprint 有严格校验。

## 验证证据

- 生产运行时冒烟：18/18 通过，包含 Native 不可用时报告仍持久化、`RequestAsync` 次数为 0、读回和投影边界。
- 常规运行时冒烟：通过，包含 v2 生成/持久化、v1 读回投影和重复调用幂等。
- Release 构建：成功，Bannerlord API `1.3.15`。
- `git diff --check`：无错误。

## 限制

本复审不声明游戏目录同步、游戏启动、真实存档读写或 E4/E5 游戏内行为。全量 SDK smoke 仍有独立人物 DSL 断言失败，因此不能将全量测试称为全绿；该失败不属于本次周报运行时切片。

剩余 P2：`BuildCompletedWeeklyReportFromFactsAsync` 与 `TryBuildWeeklyReportFromQueryResult` 仍保留旧 preview/兼容入口，但当前没有正式生产 caller。
