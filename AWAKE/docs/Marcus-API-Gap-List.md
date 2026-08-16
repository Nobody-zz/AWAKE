# Marcus API 缺口清单（SlaaneshsEmbrace）

> 文档状态：提交给马库斯作者确认的能力缺口清单
> 日期：2026-08-13
> 背景：当前 SDK 公共 API 为 0.1 Preview；本清单只记录会阻碍 SlaaneshsEmbrace 消重的契约缺口，不是承诺。

## 1. IAiGateway 缺少 route 级任务治理

当前公共 API：

- `IAiGateway.SubmitAsync(AiTaskRequest, RequestContext, CancellationToken)`
- `IAiTaskHandle.Subscribe / SnapshotEvents / CancelAsync`

缺少能力：

- `GetTask(taskId)` / `Stream(taskId)` 查询或继续读取已提交任务；
- `CancelByRoute(routeId)` 或 route session 级取消；
- route 内“新任务取代旧任务”的 generation 语义。

需要替代的本地实现：

- `AiTaskGateway` 的 `_active`、`_generations`、single-flight、`CancelRoute`、`FinishTurn`。

验收标准：

- 扩展可按逻辑 route 查询活动任务；
- 同 route 新提交可取消旧任务，且迟到事件不会进入新回合；
- 扩展不再需要维护路由级任务字典。

## 2. IEventService 缺少 durable 消费能力

当前公共 API：

- `IEventService.Subscribe(string, Action<EventEnvelope>)`
- `IEventService.Publish(EventEnvelope, EventDelivery, RequestContext)`

缺少能力：

- durable `Replay / Ack / cursor`；
- consumer 级幂等与断点恢复；
- gap、背压、dead-letter 的公开策略；
- 可选 `IEventStore` 或 durable subscription 句柄。

需要替代的本地实现：

- `WorldStateStore` 的 `_pendingEvents`、`_resultLedger`、`appliedKeys`、重试/丢弃/最终 Drain。

验收标准：

- 事件以 durable 方式投递后，扩展可在重启后按 cursor 回放；
- 同一 `event_id` 不重复生效；
- 扩展不再需要本地 outbox 队列。

## 3. IPermissionService 缺少请求去重与批量语义

当前公共 API：

- `IPermissionService.Evaluate(permissionId, context)`
- `IPermissionService.RequestAsync(permissionId, purpose, context, cancellationToken)`
- `IPermissionService.Revoke(permissionId, extensionId)`

缺少能力：

- 同一权限并发请求的共享审批句柄或框架侧去重；
- 每个等待者都能拿到自己的结果、reason 与 correlation；
- 可选批量请求，避免 AI 回合内多次弹授权。

需要替代的本地实现：

- `PermissionGate` 内基于 `TaskCompletionSource` 的同权限合并逻辑。

验收标准：

- 同权限并发 `RequestAsync` 只触发一次玩家审批；
- 每个调用方都能收到独立 typed result；
- 扩展不再需要自行维护共享请求字典。

## 迁移顺序

1. 马库斯确认并发布缺口 1 后，迁移 `AiTaskGateway`。
2. 马库斯确认并发布缺口 2 后，迁移 `WorldStateStore`。
3. 马库斯确认并发布缺口 3 后，瘦身 `PermissionGate`。

迁移前不围绕上述本地实现继续扩展治理能力。
