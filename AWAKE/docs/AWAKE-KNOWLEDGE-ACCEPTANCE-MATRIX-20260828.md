# AWAKE 世界知识底层闭环验收矩阵

- `task_id`: `AWAKE-KNOWLEDGE-INDEPENDENT-20260828`
- `revision`: `2`
- `scope`: 内容无关的离线底层闭环
- `current_evidence_ceiling`: `E2`
- `current_status`: `offline_verified`
- `not_in_scope`: Marcus 共享框架实现、真实世界观内容、云端 Provider、游戏目录同步、Bannerlord 实机 E4/E5

## 验收原则

只有从入口到可观察结果完整跑通，才计为通过。编译成功、类型存在、JSON 可解析或单独调用某个服务，都不能替代入口闭环证据。本批的生产边界入口是离线 `ProductionSmoke`，不是 Bannerlord 实机。

动态知识必须与静态世界书进入同一查询和权限筛选路径。事件、周报、玩家 Overlay 不得在 NPC 对话层各自增加旁路。

## 入口矩阵

| 编号 | 入口 | 关键调用链 | 可观察结果 | 当前状态 |
|---|---|---|---|---|
| `WB-01` | 合法 v2 包初始化 | `WorldbookRuntime` → `ReadAndVerify` → `LoadVerified` → `WorldKnowledgeQueryService` | 合法包被加载，授权身份能查到对应表达 | 固定 fixture E2 已通过；生产边界 smoke 未启动真实包目录初始化 |
| `WB-02` | 包完整性失败 | 篡改 `manifest`、`runtime` 或 `index` 后重复初始化 | 返回明确 `WB2-*` 错误，不进入可查询状态 | 核心逻辑 E2 已覆盖；固定包三层哈希已通过 |
| `WB-03` | 身份权限过滤 | 身份适配 → 权限评估 → 查询 | 已授权内容可见；拒绝、未知身份和超出范围内容不可见 | 核心查询与生产动态投影 E2 已通过；Bannerlord 身份适配仍未做 E4 |
| `WB-04` | 关键词未命中 | 玩家问题 → 查询后备匹配 | 标题、摘要或正文存在且有权限时，不因缺少预设关键词直接判定未找到 | 不属于本批新增行为，保留为后续检索质量项 |
| `EV-01` | 事件结算 | 事件结算 → 稳定 `eventId/eventKey` → `WorldEventLedger` → 动态投影 → 查询 | NPC 查询能看到结算事实，并保留来源事件 ID | 生产源代码边界 smoke 已通过；真实游戏入口未验证 |
| `EV-02` | 重复事件重试 | 同一稳定事件再次提交 | 台账和知识投影只出现一份事实，不产生重复文本 | 生产边界 smoke 与 runtime smoke 均通过 |
| `REP-01` | 周报生成 | 完整 7 日窗口 → `WeeklyReportService` → 动态投影 | 查询能看到周报事实、`reportId` 和来源事件闭包 | 生产边界的快照修复与查询可见性通过；完整游戏入口未验证 |
| `REP-02` | 错过边界补报 | 战役日从 7 跳到 21，逐周处理尚未应用窗口 | 连续生成缺失的完整周；不漏周、不把未完成窗口提前发布 | runtime smoke 的逐周补报断言通过；真实存档恢复未验证 |
| `REP-03` | 周报重复触发/崩溃恢复 | 同一 `reportId` 重复触发或应用后进程重启 | 只应用一次；重载后仍能识别已应用报告 | runtime smoke 与生产快照修复断言通过；真实跨进程存档未验证 |
| `STO-01` | 事件持久化失败 | 台账接受 → 存储失败 → 重试 | 调用结果区分“内存接受”和“已持久化”；失败不能报告成功；后续可重试 | runtime smoke 的失败/重试桩通过；真实 Marcus Storage 未验证 |
| `STO-02` | 存储加载失败 | 启动/读档 → 加载失败 → 再次加载 | `_loaded` 不被错误锁死，后续重试能成功；不使用半加载状态 | production boundary smoke 的加载/替换路径通过；真实存档未验证 |
| `AI-01` | AI 上下文出口 | NPC 查询 → 结果状态判定 → Prompt builder | 只有 `known/partial` 的已筛选文本进入上下文；`blocked/referral/not_found` 不携带被拒绝正文 | 查询层动态权限已验证；`NpcDialogueService` 的完整 AI 请求出口不在本批 |

## 动态知识不变量

1. `WorldEventLedger` 是事件事实唯一来源；周报是可从台账重建的确定性投影。
2. `eventId`/`eventKey` 用于事件去重，`reportId` 用于周报投影幂等；重复调用不能产生第二份知识。
3. 周报只覆盖已经结束的完整 7 日窗口。若一次跨过多个边界，按窗口顺序逐周补报，不合并成一个不透明的大报告。
4. 应用状态必须落在现有战役存储边界中；不得在 `ModuleData` 或第二套自定义存档中伪造战役状态。
5. 持久化失败必须保留可重试状态；任何“内存已接收”都不能被包装成“已保存”。
6. 动态事实与静态事实共用身份、范围、详细度和拒绝规则；事件路径不得绕过权限。
7. UI/campaign tick 只发起非阻塞工作，不在 tick 中执行阻塞文件、网络或数据库调用。

## 证据分级

- `E0`：计划、契约和静态代码路径存在。
- `E1`：编译与结构解析通过。
- `E2`：固定 fixture、focused smoke 和失败路径断言通过。
- `E3`：root、`dist`、游戏目录和测试包文件哈希一致。
- `E4`：匹配 BuildId 的真实游戏入口闭环，由用户运行游戏并提供日志。
- `E5`：匹配 BuildId 的读档、重启和长时回归。

本批已取得 `E2` 离线证据，但仍不得把它包装成 `E3/E4/E5`。不得用旧 EXE、冻结候选或未匹配 BuildId 的日志替代当前源代码证据。

## 当前边界

本批已实际修改或验证：

- `AWAKE\src\` 中本批世界知识/生命周期生产代码；
- `AWAKE\tools\worldbook-runtime-smoke\` 与 `AWAKE\tools\worldbook-runtime-production-smoke\` 下的固定 fixture、测试代码和测试项目配置；
- 本文件与 `docs` 下本批专用审查/检查点文件。

本批仍未修改：

- `AWAKE.csproj`、`SubModule.xml`、`ModuleData`、`dist`；
- Marcus `framework`、游戏目录、冻结候选和同步发布目标。

Marcus P3D-A0 已释放本批 AWAKE 世界知识生产写集；本批没有修改 Marcus。正式 AWAKE 构建仍被 Marcus Framework 的 `netstandard, Version=2.0.0.0` 引用环境阻塞，详见 checkpoint。
