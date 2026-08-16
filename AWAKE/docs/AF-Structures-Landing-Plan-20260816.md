# AF 成熟结构落地方案

> 日期：2026-08-16
> 状态：规划文档，按批次实施；每批走项目规则和独立 PLAN。
> 原则：只借数据模型、交互流程、状态机与 UI 思路；代码全部自建，不引用 AF DLL，不复制 Harmony/反射/ONNX。

## 0. 总目标

把 AF 里成熟的“玩法结构”逐步落进 AWAKE，让 AWAKE 从“被动对话模组”升级为“会主动、会记忆、会汇报、有稳定 UI 的 AI 世界运行时”。

每批完成定义：

- 主工程构建 0 警告 / 0 错误
- SdkSmoke PASS
- 本地化校验 OK
- `_build_out / dist / 游戏 Modules` 哈希一致
- 游戏内验收：入口 → 调用 → 结算 → 可观察结果

## 1. A1：NPC 主动聊天状态机（P0）

### 现状

- AWAKE 目前没有 `NpcProactiveBehavior`，也没有主动聊天会话。
- 入口只有命令台深谈、场景 T/Y 选人、事件对话动作。

### 目标

把 AF 的 `Pending / Opening` 状态机、动机分类、冷却和动机疲劳重写成 AWAKE 自己的版本。

### 分步

1. 数据模型
   - 新增 `NpcProactiveCandidate` / `NpcProactiveSession`
   - 字段：`HeroId`、`MotiveType`、`Urgency`、`State`、`Day`、`ExpiresAtDay`、`CooldownDay`、`Fatigue`
   - 存储 namespace：`awake.npc.proactive`
   - 验证：SdkSmoke 状态转换、冷却、过期、疲劳、持久化

2. 动机来源
   - 关系状态：信任/爱意/敌意/私人信任
   - 队伍快照：伤员、士气、俘虏、负重
   - 事件记录：`WorldEventLedger` 最近事件
   - 对话跟进：上次深谈后的 follow-up
   - 验证：每个来源有纯函数，可单测

3. 触发评估
   - 每日/每小时评估一次
   - 前置门：战役地图、覆盖层未打开、无输入框、冷却未到
   - 概率由 `Urgency + Affinity + ChancePercent` 决定
   - 验证：SdkSmoke 断言门控和概率边界

4. 弹出与进入对话
   - 主动弹窗 → 接受 → 复用 `NpcDialogueLauncher`
   - 拒绝/超时/失败都结算，不残留 Pending
   - 验证：游戏内主动弹窗后能进入深谈

5. 持久化与清理
   - 过期清理、容量上限、会话结束 final drain
   - 验证：读档后冷却和 Pending 仍在

## 2. A2：记忆生命周期（P1）

### 现状

- `NpcMemoryService` 已有：对话事实、AI 摘要、top-k 召回块。
- 没有：日结封存、总览压缩、重试队列、同类记忆合并。

### 目标

把 AF 的“日结草稿 → 压缩记忆块 → 总览摘要 → 重试队列”概念重写成 AWAKE 记忆管道。

### 分步

1. 日结任务
   - 每个英雄按游戏日汇总当天事实/摘要
   - 生成 `daily draft`，写入 `awake.npc.memories`
   - 验证：SdkSmoke 日结生成与幂等

2. 总览压缩
   - 周/月总览：同类事件合并，保留权重和日期
   - 字节预算、条数上限、优先级排序
   - 验证：SdkSmoke 压缩结果和预算

3. 重试队列
   - 失败任务保留 `pending`，最多重试 N 次
   - 会话结束前 final drain
   - 验证：SdkSmoke 失败重试与最终放弃

4. 后续增强
   - 记忆分级、承诺账本、同类记忆合并
   - 验证：游戏内读档后记忆仍可回读

## 3. A3：事件收件箱 + 周报（P1）

### 现状

- `WorldEventLedger` 是内存队列，容量 50，未持久化。
- `NarrativeReportBuilder` 只生成文本，没有 UI，也没有实际周报触发。

### 目标

- 世界事件持久化
- 收件箱 UI
- 周报生成与展示

### 分步

1. 持久化
   - 新增 `awake.world.events` namespace
   - 记录 `Day / Kind / Text / Seen`，容量上限 200
   - 验证：SdkSmoke 读写、滚动、未读计数

2. 收件箱 UI
   - `WorldEventInboxVM` + Gauntlet prefab
   - 命令台根菜单加入“事件收件箱”
   - 大地图通知显示未读数量
   - 验证：游戏内打开收件箱、标记已读

3. 周报
   - 每周从持久化 ledger 读取记录
   - `NarrativeReportBuilder` 生成文本
   - 可选 AI 周报路由 `AWAKE.route.world.report`，失败走本地兜底
   - 验证：SdkSmoke 周报滚动和重试

## 4. A4：覆盖层长等待解锁（P0）

### 现状

- `NpcDialogueOverlay` 的 Esc 立即关闭。
- 没有“等待 AI 响应超过阈值才允许取消”的状态门控。

### 目标

- 空闲/已结束时 Esc 可随时关闭。
- 已发送、等待首包超过 60 秒时，Esc 变成“取消生成并关闭”。

### 分步

1. 生成状态暴露
   - `NpcDialogueService` 暴露 `Sending / Waiting / Streaming / Idle`
   - 验证：SdkSmoke 状态转换

2. 覆盖层计时
   - 记录等待开始时间，超过 60 秒显示“可取消”提示
   - 验证：SdkSmoke 用假延迟覆盖边界

3. 取消链路
   - Esc → `AiTaskGateway.CancelRoute` → 关闭覆盖层
   - 验证：游戏内长时间等待可 Esc 退出，不残留任务

## 5. A5：命令台增强（P1）

### 现状

- `AwakeTerminalBehavior` 已有热键、前置拦截、根菜单。
- 根菜单目前是“通讯录 + 开发者检查”。

### 目标

- 根菜单扩展：深谈、通讯录、事件收件箱、周报、开发者检查。
- 记录“为什么没打开”的拦截原因。

### 分步

1. 根菜单扩展
   - 按 `EnableDeveloperMenu` 和功能可用性显示条目
   - 验证：游戏内菜单条目正确

2. 拦截日志
   - `CanOpenTerminal` 每次失败记录原因
   - 验证：SdkSmoke 拦截原因枚举

3. 模块拆分
   - 避免把命令台做成下一个 AF 单体
   - 拆为 `TerminalIntercept` / `TerminalMenu` / `TerminalCommands`

## 6. A6：UI 资产与交互移植（P1）

### 目标

参考 AF 的 UI 资产和交互，但全部用 AWAKE 的 prefab/VM 重写。

### 分步

1. 审计 AF GUI
   - 命令台、场景选人、对话覆盖层、事件收件箱
   - 只记录布局、层级、交互，不复制代码
   - 验证：产出 UI 清单

2. AWAKE 重写
   - 复用现有 `GauntletLayer` 模式和命名规范
   - 卡片圆角 ≤ 8px，文本不重叠，移动端不溢出
   - 验证：游戏内截图检查

3. 交互一致性
   - 场景内 T/Y、场景外 U
   - 覆盖层 Esc 语义统一
   - 验证：快捷键冲突回归

## 7. A7：提示词守卫与回复规范化（P2）

### 目标

吸收 AF 的守卫/重试/可见回复清洗概念，但使用 AWAKE 自己的配置和实现。

### 分步

1. 输出契约
   - 回复正文、指令、效果分离
   - 验证：SdkSmoke 输出解析

2. 可见回复规范化
   - 去空标签、控制字符、重复换行
   - 验证：SdkSmoke 边界用例

3. 失败重试提示
   - 超时/404/权限失败返回角色内提示
   - 验证：游戏内失败路径

## 8. A8：性能看门狗（P2）

### 目标

轻量 Freeze/Perf 探针，只做超时和日志，不做本地模型推理。

### 分步

1. `AwakePerfProbe`
   - 记录对话生成耗时、路由耗时、世界书命中率
   - 验证：SdkSmoke 计时边界

2. 超时告警
   - 超过阈值写日志，不弹窗
   - 验证：日志断言

## 9. 批次顺序

| 批次 | 内容 | 优先级 |
| --- | --- | --- |
| Batch 1 | A1 主动聊天 + A4 长等待解锁 | P0 |
| Batch 2 | A5 命令台增强 + A3 收件箱/周报 | P1 |
| Batch 3 | A2 记忆生命周期 | P1 |
| Batch 4 | A6 UI 资产整理 | P1 |
| Batch 5 | A7 守卫 + A8 性能探针 | P2 |

## 10. 反模式红线

- 不引用 `AnimusForge.dll`
- 不复制 Harmony patch 表
- 不反射游戏私有 UI
- 不引入 ONNX / embedding / rerank
- 不把对话主链路绑死在 mission/scene
- 不把 AF 的巨型单体结构搬进 AWAKE
