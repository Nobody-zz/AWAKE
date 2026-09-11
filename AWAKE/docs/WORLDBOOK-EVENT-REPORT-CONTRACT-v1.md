# AWAKE Event Record / Weekly Report Contract v1

## 目的

本文件固定 AWAKE 世界书平台中“世界事件记录”和“机械周报”的运行时边界。它不新增一套事件系统，也不要求 NPC 逐个主动学习；所有生产事件必须进入同一个 recorder，所有周报必须由同一个 report service 生成。

## 唯一运行路径

```text
生产者
  -> WorldEventServices.Recorder (IWorldEventRecorder)
  -> WorldEventLedger
  -> WorldStateStore: awake.world.events / campaign.world_events.v1

查询、周报、界面
  -> WorldEventServices.Recorder.SnapshotWeek
  -> WorldEventServices.Reports (IWeeklyReportService)
  -> WeeklyReportService
```

`WorldEventLedger` 和 `WeeklyReportService` 是唯一实现；生产代码不得绕过 `WorldEventServices` 直接写入或生成报告。`WorldEventContract` 只负责 v1 投影和离线结构校验，不持有第二份事件状态。

## event-record.v1 映射

契约文件：`tools/worldbook-contract/v1/event-record.schema.json`。

| Contract 字段 | 运行时来源 | 规则 |
|---|---|---|
| `schemaVersion` | 常量 | 固定为 `awake.worldbook.event-record.v1` |
| `eventId` | `WorldEventRecord.EventId` | 稳定三段式 ID；同一事件重试必须保持不变 |
| `eventKey` | `WorldEventRecord.EventKey` | 来源幂等键；同键即使异常产生不同 ID，也只保留一次 |
| `occurredAt` | `WorldEventRecord.OccurredAt` | 机械生成；存储层同时保留该值，旧记录缺失时按 `day` 重建 |
| `eventType` | `WorldEventRecord.Kind` | 小写并归一化为 `[a-z][a-z0-9_.-]*` |
| `domain` | `WorldEventRecord.Domain` | 仅允许 `politics/economy/culture/war`；缺失或未知值按事件类型推断 |
| `visibility` | v1 保守投影 | 当前无结构化可见性输入时固定为 `scope=local`、`min_detail=summary`，不伪造贵族/秘密权限 |
| `facts[0].factId` | 事件 ID 的稳定 SHA-256 前缀 | 每个事件至少投影一条事实 |
| `facts[0].text.zh-CN` | `WorldEventRecord.Text` | 不调用 AI，不扩写、不补事实 |
| `extensions.awake:gameDay` | `WorldEventRecord.Day` | 保留 Bannerlord 游戏日；不污染核心 Contract 字段 |

`actorIds`、`locationIds`、`identity_ids` 和 `causationId` 在当前 Ledger 输入中没有可靠来源，因此不虚构；未来只有在生产者能提供结构化值时才加入对应字段。

## weekly-report.v1 规则

契约文件：`tools/worldbook-contract/v1/weekly-report.schema.json`。

- 报告窗口是包含 `nowDay` 的七个游戏日：`nowDay - 6` 到 `nowDay`。
- `period` 使用半开区间 `[start, end)`；`end` 为 `nowDay + 1` 日的零点，避免把最后一天截成空日。
- 事件先按稳定 `eventId` 去重，再按 `day`、`eventId` 稳定排序。
- 报告固定生成四个领域段落，顺序为政治、经济、文化、战争；没有事件的段落保留空数组。
- `sourceEventIds` 必须与段落条目的来源 ID 完全闭合：不能漏源，也不能引用未渲染事件。
- `generatedBy` 固定为 `awake:system:weekly-report-generator`。
- `BuildText` 只是当前界面的本地化文本投影，不能反向作为事实源。

## 幂等、重放与持久化

- 显式 `eventKey` 生成稳定 `eventId`；没有 `eventKey` 时由游戏日、事件类型和文本生成确定性 ID。
- 内存 Ledger、存储应用、存储重载和周报生成都按 `eventId` 去重；内存重载额外按 `eventKey` 去重，防止损坏或旧数据出现“同键不同 ID”。
- 同一文本但 `eventKey` 不同，代表两次合法发生，不能合并。
- 存储保留 `domain`、`occurredAt` 和 `eventKey`；旧 `awake.world_events.v1` 记录缺少新字段时使用确定性回退，不阻塞读档。
- 事件写入仍然异步进入 `WorldStateStore`，UI、Campaign tick 和周报生成不新增阻塞网络或 AI 调用。

## 验证范围

离线 Runtime smoke 覆盖：

- façade 记录/周报调用路径；
- event-record 投影与结构校验；
- weekly-report 结构、窗口边界和来源闭包；
- 同键重试、同键异 ID 重放、同文本异键、并发提交；
- 存储重载后的 `domain/occurredAt` 保留；
- 事件容量和近期 ID/键保留窗口。

本批不启动 Bannerlord、不覆盖游戏目录、不创建或同步新 BuildId；冻结候选继续保持原哈希。
