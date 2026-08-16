# AWAKE · Messenger 持久化审查

## Round 1

- 写路径为 outbox append，不阻塞 UI。
- 打开联系人时同步回读，只阻塞一次用户操作。
- 历史按 TargetId 分组，容量上限避免膨胀。

`VERDICT: APPROVED`
