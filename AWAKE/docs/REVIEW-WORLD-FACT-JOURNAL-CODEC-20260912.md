# 世界事实分段 journal codec 审查

- 范围：`src/WorldFactJournal.cs` 及其与事实模型、smoke 工程的直接关系。
- 证据：主工程 Release 构建 0 警告/0 错误；既有 Runtime smoke 通过；本轮未同步、未启动游戏。

## 本轮最终阻断项

1. P1：codec 尚无生产调用者，采集器仍写旧 200 条 records，不能宣称完整历史 journal。
2. P1：root/chunk 仍未校验事实是否落在 chunk bounds、factId/eventKey 确定性关系和实体语义，损坏数据可能被当成成功。
3. P1：普通 smoke 目前只编译 journal codec，没有调用 Build/Read root/chunk 的实际断言。

已关闭：capture 对 NaN/Infinity、跨日 timeSlot、重复 faction 的正常构造路径拒绝已补齐。

`VERDICT: REVISE`
