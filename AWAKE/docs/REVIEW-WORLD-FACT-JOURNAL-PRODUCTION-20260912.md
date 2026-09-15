# 世界事实 journal 生产接线审查

- 范围：`WorldFactCapture -> WorldEventLedger -> WorldStateStore -> journal chunk/root`
- 证据：主工程 Release 构建通过；Runtime smoke 通过；未同步、未启动游戏。

## 结论

`VERDICT: REVISE`

未发现 P0，发现 3 个 P1：

1. 生产写入手工构造 chunk，未调用 `WorldFactJournalCodec`，且缺少 codec 要求的 schema；读档也没有恢复 journal。
2. root/chunk 全部嵌套在旧的单一 `awake.world_events.v1` value 中，无法满足真正分段历史、容量和恢复协议。
3. journal 层幂等后仍继续追加旧 records，旧 records 淘汰后可能重新出现重复事实。

当前只能确认结构化 facts 已接入旧账本兼容桥，不能确认分段 journal 已完成。
