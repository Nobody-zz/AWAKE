# 世界事实结构化持久化桥接：修复后审查

- 范围：`WorldFactCapture -> WorldEventLedger -> WorldStateStore.records[].fact -> LoadFromStoreAsync`
- 证据：`WorldFactCapture.cs`、`AwakeWorldFactCollectorBehavior.cs`、`WorldEventLedger.cs`、`WorldStateStore.cs`、`SmokeStubs.cs`
- 验证：Runtime smoke 通过；主工程此前 Release 构建通过；未同步、未启动游戏。

## 复审结论

- structured fact 已纳入内存和持久化重复/冲突比较；同 key 异内容不覆盖旧值。
- smoke stub 已保存 fact，并覆盖保存、重复比较和读回路径。
- collector 捕获的 campaign generation 会传入 `QueueFact`，最终由 `RecordAsyncForCampaign` 校验，避免 reset 竞态。

未发现新的 P0/P1。

`VERDICT: APPROVED`
