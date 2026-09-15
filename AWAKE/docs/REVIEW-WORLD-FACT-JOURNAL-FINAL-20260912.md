# 世界事实 Journal 当前切片终审

- 范围：`WorldFactCapture -> WorldEventLedger -> WorldStateStore -> WorldFactJournal`，以及专门 SDK smoke 夹具。
- 结论：`VERDICT: APPROVED`
- 证据边界：完成静态只读终审；主工程 Release 构建、Runtime smoke、专门 journal storage smoke 均通过；未同步游戏目录、未启动游戏、未做真机存读档。

## 已确认

1. 结构化事实分流到独立 `awake.world.fact.journal` namespace，不写旧 `awake.world.events` 存储。
2. journal 使用独立 root 与不可变 revisioned chunk；chunk 写入后重读，校验 schema、key grammar、SHA-256、revision、bounds、ordinal 和事实结构。
3. `presentation.summary/domain` 随事实落库，读档恢复不会退化成仅有 `kind`。
4. journal duplicate 在 journal 路径内幂等返回，不重复写 legacy；无 legacy store 时也能完成 journal 写入。
5. 缺段、损坏、哈希不匹配和存储不可用保持 `corrupt/unavailable`；ledger 保留明确 `JournalStatus`，同时不破坏旧 records 的兼容读取。
6. 专门夹具覆盖首写、revision 递增、duplicate、journal-only、损坏 chunk fail-closed。

## 当前剩余边界

- 还没有真机 E4/E5、真实文件适配器故障注入、跨代 lease 夹具和长期容量压力测试。
- 选择性 `WorldFactQuery`/`SelectionPolicy` 及其报告、知识、人物记忆消费者接线仍是后续切片；本切片不写 Persona Storage、不接入对话。
