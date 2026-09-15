# control-plane · 已退役（2026-09-14）

> **本目录自 2026-09-14 起停止维护。**
> **当前状态一律读 [`../AWAKE-ROADMAP.md`](../AWAKE-ROADMAP.md) 的「现状」一节。**

## 为什么退役

本目录原本承载「当前批次」这一层状态，供 codex 时代的 `awake-task-continuity` 技能恢复上下文。
2026-09-14 全局主控线核实：这份状态早已失效，而且**没有任何东西在读它**——
在仓库的 `.ps1` / `.cs` 里零命中，所以它烂掉了也没人发现。

`CURRENT-20260911-frozen.json` 冻结时的实际状况：

| 字段 | 值 | 问题 |
|---|---|---|
| `updated_at_utc` | `2026-09-11T13:12:28Z` | 停更 |
| `active_batch` | `AWAKE-G3-B-IMPLEMENT-20260911` | 09-11 的批次，此后三天的工作完全没进来 |
| `task_status` | `implemented_approved_e2` | **不在 `CURRENT.schema.json` 自己的枚举里** |
| `checkpoint_path` | `docs/checkpoints/AWAKE-MOD-RETURN-20260911-checkpoint.md` | **该文件不存在** |

⇒ 指针指向空处、字段值不合自己的 schema、停更近一个月。
**它不是「过期的状态」，是一份没人读的遗物。**

## 现在往哪看

| 想知道 | 看 |
|---|---|
| 项目到哪了 / 离能玩还差什么 | `../AWAKE-ROADMAP.md` 的「现状」节 |
| 08-24 ～ 09-11 的批次记录 | `../AWAKE-STATE-HISTORY.md`（原 `AWAKE-CURRENT.md`，同日退役） |
| 证据等级定义（E0–E5） | `../AGENTS.md` 的「状态、候选与证据」节 |
| 各线的当日流水 | `D:\AWAKE-Dev\.workbuddy\memory\` 下的日期日志 |

## 目录里保留什么

- `CURRENT-20260911-frozen.json` —— 冻结快照，**仅作历史追溯，不要更新**。
- `CURRENT.schema.json` —— 格式定义，保留作参考。将来若要重建「当前批次」这一层，从它起步。
