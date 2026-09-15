# Review Log: 本周动态基础（P0）

## Round 1 — 2026-09-12

Scope: plan, direct callers, save contract, report/projection/menu code. Read-only; no build, sync or game launch.

Result: `VERDICT: REVISE`

| Finding | Severity | Revision made |
| --- | --- | --- |
| 报告生成仍被 Native Knowledge、读档恢复和 `EnableEventEngine` 门控 | P0 | Slice C 纳入 `ProbeExtension`；新增 storage-only 正式报告阶段，并将报告刷新移出旧事件引擎门控。 |
| v2 报告没有 schema、版本分派、fixture 或投影闭环 | P0 | Slice B 要求 v2 schema、正反 fixture、v1/v2 validator dispatch、投影与 UI 双读。 |
| 同一 `reportId` 的不同合法内容可覆盖已应用快照 | P0 | 报告状态与 v2 报告持久化内容指纹；相同幂等、不同冲突且保留原快照；补 commit-unknown 重读。 |
| 报告写入原子性及 commit-unknown 边界未定义 | P1 | 明确先核实 CAS；无 CAS 时采用单写者队列，不支持外部/跨进程竞争，并以重读指纹 fail closed。 |
| 内存 50 与持久化 200 条上限会破坏完整窗口 | P1 | P0 不以原始条数裁剪报告所需事实；长期压缩后置，并新增 >50/>200/失败语义测试。 |
| 菜单、ViewModel 和本地化仍走旧“世界周报”缓存路径 | P1 | Slice D 纳入 `WeeklyReportBrowserVM` 与中英文语言文件；正式/预览/不可用状态分开。 |
| 结构化日志承诺缺少字段契约与断言 | P2 | 定义日志字段并新增生成、复用、冲突、存储失败、延迟投影 smoke 断言。 |

Revision evidence: plan SHA-256 `949DF24B4FF7FBBD4FB6B5B0F4F3A700A2C4BCF9F7427EE9454022E982D2CA7E`.

Next action: independent read-only round 2. No runtime implementation is authorized before an `APPROVED` verdict and user sign-off.

## Round 2 — 2026-09-12

Scope: round-1 revision plus direct lifecycle, schema, persistence, report/projection and menu callers. Read-only; no build, sync or game launch.

Result: `VERDICT: REVISE`

| Finding | Severity | Revision made |
| --- | --- | --- |
| `RestoreCampaignStateAsync` 的启动点仍依赖 Native Ready | P0 | `ProbeExtension` 必须在 CampaignSessionReady 立即独立启动 storage/report 恢复；Native continuation 只负责 Native 依赖工作。 |
| v1 JSON schema 漏已存在的 `visibility` | P1 | 修复 schema，并用既有生成物的字节保留 fixture 锁定兼容。 |
| 损坏状态被补形状后可能变为空窗口 | P1 | 查询结果显式分为 missing/empty/success/corrupt/unavailable，损坏 fail-closed。 |
| 512KiB 单值上限使“无限制保留”不可实现 | P1 | 采用 v2 index + 每周分块键，384KiB 封块预算；完整窗口跨块读取。 |
| v1/v2 同窗口 ID 与指纹规则未定义 | P1 | v1 保持旧 ID，v2 使用版本化 ID；锁定 canonical JSON、字段集、SHA-256 和 UI 优先级。 |
| 合法空报告不能修复损坏 applied 快照 | P1 | 只有 null payload 不可修复；合法零来源报告可修复对应空周。 |
| 计划 BuildId 与实际工作树不一致 | P1 | 基线改为实际 working-tree BuildId + HEAD，并要求候选前记录 dirty-file hashes。 |

Revision evidence: plan SHA-256 `2A6668424BDAF9E0D81F45A1AAF19E204FAE52534C886BFE87E25D73A2400F31`.

Next action: independent read-only round 3, the bounded high-risk final round. No runtime implementation is authorized before an `APPROVED` verdict and user sign-off.

## Round 3 — 2026-09-12

Scope: terminal review of the revised plan, direct lifecycle, storage, contract and permission boundaries. Read-only; no build, sync or game launch.

Result: `VERDICT: REVISE`

| Finding | Severity | Revision made |
| --- | --- | --- |
| Plan HEAD and round state were stale | P1 | HEAD updated to `f8cfc22…`; plan and review state mark the review cap exhausted rather than a nonexistent pending round. |
| Index/chunk/report writes lacked a crash-recovery commit protocol | P1 | Removed global index; per-week manifest is the sole commit point, with chunks-first, readback verification, manifest-last and deterministic retry rules. |
| Global index would eventually exceed 512 KiB | P1 | Recent windows derive deterministic manifest keys; no growing global v2 index exists. |
| v2 fields/canonical input were not exact | P1 | Locked `policyVersion`, exact top-level/extension fields, canonical ordering and fixture requirements. |
| Campaign background path could request permissions | P1 | P0 requires an Evaluate-only storage-access seam; denied/unavailable state is noninteractive. |

Revision evidence: plan SHA-256 `EA739CA2D6CD5F73BB5A92E0EC4BC1446792DBE9145A244DE870CABA79699919`.

Review budget: high-risk maximum of three rounds is exhausted. The user directed a split on 2026-09-12. This combined plan is superseded and not approved; its storage findings are carried by `PLAN-WORLD-WINDOW-STORAGE-20260912.md`, while report/UI findings are carried by `PLAN-WEEKLY-DYNAMICS-20260912.md`.
