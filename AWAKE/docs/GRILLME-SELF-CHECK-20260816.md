# AWAKE 代码自查

> 日期：2026-08-16

## 发现

### P1：Messenger 历史缓存未随战役重置

- `AwakeMessengerHistory` 的 `_loaded` 和 `Chats` 是静态缓存。
- `SubModule.ResetCampaignState` 没有调用 `AwakeMessengerHistory.ClearForTesting()`。
- 新战役可能读到上一场战役的聊天历史。

### P1：持久化回读在存储未就绪时会永久跳过

- `WorldEventLedger.LoadFromStoreAsync` / `AwakeMessengerHistory.LoadAsync` 在 `WorldStateStore == null` 时直接 `_loaded = true`。
- 如果玩家在存储就绪前打开收件箱/通讯录，本次战役内不会再回读。

### P2：WorldEvent/Messenger fire-and-forget 写任务可能产生未观察异常

- `_ = store.AppendWorldEventAsync(...)` / `_ = store.AppendMessengerMessageAsync(...)` 的 Task 若异步失败，异常未观察。
- 需要统一安全 fire-and-forget 包装。

### P2：周报触发日未持久化

- `AwakeEventBehavior._lastWeeklyReportDay` 只在实例内存中。
- 读档后同一天可能重复生成一次周报。

### P2：MCM AI 自检只检测宿主存在

- `AwakeMcmActions.RefreshAiStatus` 只判断 `FrameworkHostLocator.TryGetHost`。
- 不检测路由、模型或真实 Provider 状态，文案可能误导。

## 结论

以上问题列入任务队列，不自动开修。
