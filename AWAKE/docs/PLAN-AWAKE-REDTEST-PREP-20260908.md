# AWAKE 本体红测准备计划 — 2026-09-08

## 目标

为当前 AWAKE Runtime source candidate `awake-20260903-awake-runtime-repair-004`
建立可执行的红测计划，优先验证失败路径、竞态、禁用行为、重复提交、
生命周期隔离、UI 输入和存档边界。

本计划是**红测准备批次**，不是实机验收结果，也不是代码修复授权。

## 硬边界

- 不启动 Bannerlord。
- 不同步 `dist`、游戏目录或测试模组目录。
- 不访问真实 Cloud Provider、本地真实 Provider、API Key、Token 或网络服务。
- 不修改 Worldbook Studio、Persona Workbench、UI Workstation。
- 不修改当前 Runtime source candidate，除非另立修复批次并完成相应审查。
- 不把历史 `002`、`003` 证据当作当前 `004` 证据。
- 世界书迁移候选不属于本批次；只记录其为外部阻塞，不读取其正文作为红测依据。

## 当前候选身份

- BuildId：`awake-20260903-awake-runtime-repair-004`
- 工程版本：`v0.2.0`
- 当前状态：`source_only_pending_sync`
- 当前证据：E1/E2；没有当前 BuildId 的 Bannerlord 日志。
- `Awake.dll` SHA-256：
  `B25A5A4F1F7E95D7182BBD48FDC41A894E8366366440ECA4D27F2582AF74C17E`
- `MarcusAwakeFramework.dll` SHA-256：
  `00B34BF79DEFADBE4A946612244EA8631CE777495C2B99ADD11F2B10CE8C7F55`
- `MarcusAwakeTransport.dll` SHA-256：
  `DE81809AC08EA86BE90FD6F0AB77FA348B24B744588B0AF970DFFF2080E6DF48`
- 当前游戏日志路径（仅供用户后续红测采集）：
  `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\Logs\Awake.log`
- 探针日志路径：
  `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\Logs\AwakeProbe.log`

## 审查结论

### RT-LIFECYCLE：conditional pass，必须红测

- 发现 P1：`SubModule.OnGameStart` 每次 Campaign 分支都直接向
  `CampaignGameStarter` 添加三个新 Behavior，没有本体级去重门。
- `AwakeHostComposition` 的 session 启动有幂等和 drain 逻辑，但不能替代
  Bannerlord Behavior 注册去重。
- session generation、旧 Store 隔离、Provider cancellation/deadline 和
  离线终态幂等目前有静态或离线证据，但仍缺当前 BuildId 的游戏入口证据。

### SAVE-RECOVERY：reject for E5

- P0：`PersonaPersistenceEnvelope`、`PersonaRecoveryRecord` 和
  `PersonaPersistenceValidator` 尚未接入运行时存储、Bannerlord `SyncData`
  或读档后二次重建。
- P1：`PersonaStorageKey.TryBuild` 当前未将 `SaveId` 纳入键组成；跨存档/
  跨 timeline 隔离必须通过独立契约与红测证明。
- P0：当前 `SyncData` 没有 Persona recovery、ledger、receipt 或 save anchor
  的接线证据。
- P1：进程重启会丢失实例内的 result ledger、memory reservation 和 sequence
  恢复状态。
- P1：`Unknown` 尚未形成跨进程、跨存档、跨 session 的恢复闭环。
- 结论：可以准备 E5 用例和失败证据格式，但当前不能把 E5 红测宣称为可通过。

### UI-TICK：conditional fail，四个 P1

- P1：各 overlay 的打开入口没有统一 layer ownership/互斥门。
- P1：关闭后焦点恢复没有明确契约和统一可观察日志。
- P1：NPC 对话发送使用 `CancellationToken.None`，关闭后的后台结算取消边界
  不足。
- P1：UI tick 使用无上限 `while` 排空异步事件，有单帧阻塞风险。
- P2：列表重建后的顺序、滚动位置、详情返回和禁用入口缺少实机证据。

### CONFIG-PERMISSION：conditional fail，六个 P1

- P1：未发现 `Config.json` 兼容回退实现。
- P1：MCM 优先级、损坏字段和重启加载没有 contract smoke。
- P1：ContentPolicy/世界效果 gate 没有接入当前事件效果路径。
- P1：金额入口缺少明确的缺失、非整数、零、负数和溢出拒绝。
- P1：PermissionGate 有取消/过期/Unknown，但没有本地统一 timeout 策略。
- P1：未发现犯罪效果的独立入口、command、持久化和 smoke；必须标记
  `NOT_IMPLEMENTED` 或 `NOT_ENABLED`，不能按关系效果推断。

## 红测阶段

### R0 — 当前离线准备

状态：`completed`

必须完成：

1. 固定当前 BuildId、DLL 哈希和 source-only 边界。
2. 固定日志关键字、correlation/session/command 字段。
3. 为每个红测用例定义：前置、操作、观察、失败判定、证据文件。
4. 运行已有离线 smoke 作为基线，不把基线当作红测通过。
5. 确认任何代码修复都另立 P1/P0 修复批次。

### R1 — 离线失败路径红测

允许使用 fake Provider、fake storage、fake world state 和现有 harness；
禁止真实 Provider。

当前状态：`behavioral_offline_cases_executed_bannerlord_cases_pending`

当前静态门禁结果：

- `6 PASS / 1 PARTIAL / 8 RISK / 2 BLOCKED / 0 FAIL`
- 详细证据：`docs/evidence/AWAKE-REDTEST-R1-STATIC-20260908.json`
- 原始 smoke 输出：`docs/evidence/AWAKE-REDTEST-R1-SDKSMOKE-20260908.txt`

当前行为负路径红测结果（2026-09-10 执行，离线）：

- `19 PASS / 0 FAIL / 2 RISK / 1 BLOCKED / 7 NOT_ATTEMPTED`
- 原始证据：`docs/evidence/AWAKE-REDTEST-R1-BEHAVIORAL-20260908.json`
- 结论记录：`docs/evidence/AWAKE-REDTEST-R1-BEHAVIORAL-20260908.md`
- 入口：`Awake.SdkSmoke.exe --redtest-r1-behavioral`
- 已通过的离线负路径：权限 denied/unknown/expired/context-missing/evaluate-throws/cancelled
  fail-closed、事件关系效果越界与全零拒绝、事件选项门、金币入口边界矩阵、
  旧 session 生成号与旧 Store 隔离、session 结束后拒绝新工作；
  以及 `AiTaskGateway.SubmitAsync` 提交边界 7 例——context_missing、caller 取消短路、
  route_missing、已登记路由权限被拒、未知云外发分类、云外发关闭时 `player_state` 被拒、
  玩家绑定失败 `awake.player_unbound`。
- 离线可测边界已探明：`EnsureCurrentHeroBoundAsync` 位于云外发门之后、`host.Ai.SubmitAsync`
  之前；离线 fake 无法实现 framework internal 的 `ICompatibilityGameDataService`，
  英雄绑定必然失败，因此 Provider 提交后的失败路径（provider_error_unknown、
  duplicate_terminal_event_idempotency、deadline_cancellation）离线不可达，
  连同重复 `OnGameStart` 注册、overlay 归属/焦点、对话晚到隔离、UI drain 预算
  一并标记 NOT_ATTEMPTED，转入 R3。
- 仍未闭环：`config.json_fallback` 与 `event.contentpolicy_gate` 为 RISK（源码缺失）；
  `save.persona_recovery` 为 BLOCKED（P0 未接线）。

这里的 `PASS` 仅表示源码身份或已有离线覆盖检查通过，不表示相应
Bannerlord 行为红测已经通过。

重点用例：

- 重复 `OnGameStart` 注册。
- 旧 session 结果回写新 Store。
- caller cancellation 与 deadline cancellation 区分。
- Unknown Provider 错误和重复 terminal event。
- permission denied/unknown/cancelled/timeout fail-closed。
- 金额边界和关系增量边界。
- 事件效果失败不得记录为成功。
- UI 关闭后晚到结果不得更新新 VM/新 NPC。
- 高密度 UI event drain 不得无界阻塞测试 tick。

R1 通过条件：

- 每个用例都有明确断言，不接受只看日志“没有报错”。
- 失败路径不产生未授权 Provider 请求、状态写入、金币变化或关系变化。
- 终态只出现一次；重复提交只能得到 duplicate/conflict。
- 仍需保留发现的 P1/P0，不得为让测试通过而放宽断言。

### R2 — 当前候选同步前门禁

状态：`completed_20260910`（用户于 2026-09-10 明确授权同步）

必须先满足：

- R1 红测结果归档。
- 所有 P0/P1 结果分类为 product defect、test defect 或 environment defect。
- 若修复代码，生成新 BuildId 并重新验证。
- 经过明确授权后，Bannerlord 关闭时同步当前候选。
- 记录 source/dist/game/test package 四地哈希和 Manifest。

执行记录（2026-09-10）：

- P0/P1 分类：Persona persistence 未接线 = product defect（未修复，保持 E5 blocked）；
  `Config.json` 回退与 ContentPolicy 门缺失 = product defect（未修复，保持 RISK）；
  其余 P1 为需 Bannerlord 运行时的待验证项（environment，转入 R3）。未修改候选代码，
  因此不生成新 BuildId，工程版本保持 `v0.2.0`。
- 同步前置：`tools\package_embedded_runtime.ps1` 重建内嵌 Runtime（此前 dist Runtime
  落后于 Framework 构建，被 WhatIf 门禁拦下）。
- 同步：`tools\sync_module.ps1 -ConfirmGameSync` -> `state=verified`；
  报告 `docs/sync-reports/sync-20260910-200556708.json`（copied=9, removed=0）
  与 `docs/sync-reports/sync-20260910-200754788.json`（copied=2, removed=0，补发
  `BUILD_VERIFICATION.txt`）。
- 哈希：`Awake.dll` `B25A5A4F…F74C17E`、`MarcusAwakeFramework.dll` `00B34BF7…C8C7F55`、
  `MarcusAwakeTransport.dll` `DE81809A…0E6DF48` 在 source/dist/game 三地逐字节一致；
  `SubModule.xml` `378E9C0B…2AE80F66`、worldbook `manifest.json` `2115664E…9448468A`
  三地一致；Runtime `manifest.json` `FBBDF4A8…A9B2857`、`SHA256SUMS.txt`
  `2A21ED84…FF60F497` 在 dist/game 一致（197 文件）。
- 游戏目录由旧 `002` 候选（`Awake.dll` `D00478D7…957FEBE`）升级到 `004`；
  `Config.json` 原本不存在，无覆盖风险；`Logs` 保留。
- `AWAKE-release-staging` 未在本批次更新，仍为 `003` 制品，不作为当前 game 证据。

### R3 — E4 用户实机红测

只能由用户在匹配 `004` 的游戏目录执行。每次运行必须先记录：

- `BuildId`、module version、`Awake.dll` SHA-256。
- 游戏版本和当前启用模块列表。
- 新日志会话起止时间。
- 测试存档名称和稳定 NPC/hero ID。

E4 用例：

1. NPC 对话入口：入口 → Provider/fake path → memory/relationship settlement
   → UI/log observable result。
2. NPC proactive 入口：主动提示 → 接受/拒绝 → relationship result。
3. Event/Inbox/Weekly Report 入口：事件 → inbox/report → effect result。
4. Messenger/Inbox/Weekly UI 互斥、关闭、焦点恢复和重复打开。
5. NPC 对话发送后立即关闭、换人、重开，检查晚到结果隔离。
6. 配置关闭、host 未就绪、权限拒绝和目标失效的 fail-closed 行为。
7. 5–10 分钟长会话，观察 tick、输入和重复 layer。

E4 通过条件：

- 当前 BuildId 出现在日志中。
- 每条链路均有入口、调用、结算和可观察结果。
- 失败/取消/拒绝不会写入错误目标或改变原版结果。
- 没有重复 Behavior、重复 layer、输入穿透或晚到污染。

### R4 — E5 存档/恢复红测

当前状态：`blocked_by_P0_persona_persistence_wiring`

待 Persona persistence/projection 接线后执行：

1. 保存后退出、加载、二次进入同一 NPC。
2. 同 idempotency key 保存前后重复提交。
3. 同 key 不同 payload 冲突。
4. in-flight Unknown 的保存边界和恢复。
5. 未保存 reservation/记忆不得伪装为已保存。
6. Campaign A/B 相同 hero/key 的隔离。
7. 三轮加载 → 进入 → 写入 → 保存 → 退出 → 重载长时回归。

当前 E5 只允许输出：

- `blocked_not_wired`
- `not_attempted`
- `static_only`

不得输出 `passed`。

## 红测统一失败判定

以下任一项即为 P1 或更高：

- 当前 session 结果写入新 session 或新 Store。
- 同一命令/效果重复结算。
- `Unknown` 被当成成功或自动重放。
- 权限拒绝后仍发 Provider、写状态、扣金币或改关系。
- 功能关闭后仍执行 AWAKE 副作用。
- 旧 UI layer 继续接收输入。
- 关闭窗口后的晚到结果更新新目标。
- 单帧无界 drain 导致输入或关闭明显卡顿。
- 读档后跨 Campaign/Save 污染。
- 失败效果被记录为成功效果。
- 日志缺少 BuildId/correlation/error code，导致无法归属。

## 日志/证据要求

至少采集：

- BuildId、模块版本、DLL/Runtime hash。
- lifecycle：`module_load`、`game_start`、`host_campaign_session_ready`、
  `OnGameEnd`/drain 结果。
- session generation、session id、Store identity。
- command id、idempotency key、payload/effect hash、correlation id。
- permission id、decision、error code/category。
- Provider terminal state、cancel/timeout/Unknown/fallback。
- settlement phase、before/after、ledger sequence、duplicate/conflict。
- overlay open/close/focus、UI event drain 数量和晚到丢弃原因。
- save/load anchor、recovery status、rebuild key、Persona schema。

日志只有在包含当前 BuildId 并能连接完整链路时，才可用于 E4/E5 归属。

## 当前状态与下一步

- 红测准备状态：`in_progress`。
- 当前候选状态：`source_only_pending_sync`。
- R0：本计划完成后可进入离线红测执行。
- R1：先执行离线失败路径；不启动游戏、不同步。
- R2：等待用户明确授权和 P0/P1 处理决策。
- R3：用户运行匹配候选后提供日志/截图。
- R4：Persona persistence/projection 接线前保持阻断。
