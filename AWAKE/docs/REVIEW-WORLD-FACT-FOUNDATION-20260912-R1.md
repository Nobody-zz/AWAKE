# 世界事实底座独立审查：Round 1

- 目标：`docs/PLAN-WORLD-FACT-FOUNDATION-20260912.md`
- 目标 SHA-256：`93562964D5F228F6F525B2902906413B8A5FA3590C303BEF03BA386FCB6029D2`
- 证据边界：计划、`WorldEventLedger`、`WorldStateStore`、采集器、报告/知识投影、事件合约、文件存储、`IKeyValueStore` 与项目准则；全程只读，未编辑、构建、同步或启动游戏。

## 已合并的阻断发现

| ID | 根因 | 修订处置 |
| --- | --- | --- |
| P1-01 | 事实字段、事件身份与五类回调映射不够精确 | 在计划第 3 节补充 fixed preimage、fingerprint、时间槽、role 枚举及回调逐字段表。 |
| P1-02 | 无 CAS 的 root 更新可能因并发 read-modify-write 丢失事实 | 明确一个 campaign generation 的 FIFO 单一 writer；无 writer/generation 不一致不得报成功。 |
| P1-03 | AWAKE 文件存储和生命周期尚不满足计划的 scope/取消/权限主张 | 将私有文件适配器和 Evaluate-only 生命周期最小修复纳入同一批次，明确不改 Marcus 公共 API/默认路径。 |
| P1-04 | 查询策略未绑定实际报告和知识消费者 | 逐一指定唯一 query 入口、legacy adapter 唯一例外，并要求调用链与静态旁路门禁。 |

## 结论

`VERDICT: REVISE`。上述四项均为 P1；修订后的计划进入仅针对变更和直接不变量的 Round 2 复审。
