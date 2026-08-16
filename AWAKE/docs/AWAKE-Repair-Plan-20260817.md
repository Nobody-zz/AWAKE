# AWAKE 修复方案（2026-08-17）

> 状态：方案待用户批准，未开始实现。
> 依据：08-17 游戏反馈 + `Awake.log` / `AwakeProbe.log` / Marcus `framework.log` / `companion.log` 日志核查。

## 已确认根因

1. 对话路由被云外发策略拒绝：`companion.log` 出现 `RouteId: AWAKE.route.npc.dialogue | Code: ai.cloud_export_denied`；`NpcDialogueService` 调用 AI 时硬编码 `CloudExportPolicy.None`，且框架侧路由 `allowCloudForNew=False`。
2. 存储写入丢数据：`WorldStateStore.TryApplyAsync` 把 `storage.key_not_found` 当写入失败重试，最终 dropped；日志出现 `pending_writes=20 dropped=2`。
3. Prompt 注册失败无细节：`npc_prompt_register_failed code=unknown`，当前只记录 code，无法定位权限/路由/注册问题。
4. C 键默认冲突：`SceneShoutKey` 默认 `"C"`，游戏已有 C 快捷键。
5. 主动对话是纯概率：`NpcProactiveService.EvaluateAsync` 只做 `BaseChance + affinity` 随机，缺少关系阶段、近期事件、身份、地点、需求等确定性条件。
6. 大地图 NPC 对话入口割裂：通讯录 `IsNearby` 依赖场景目标，地图相遇后没有可直接调出 AI 对话菜单的统一入口。

## Batch 1：P0/P1 代码修复（低风险，不走新机制 grill-me）

### 1. 云外发链路

- `NpcDialogueService.SendAsync` 不再硬编码 `CloudExportPolicy.None`，改为 `CloudExportPolicy.ResolveDialogueClassification(AwakeSettings.Current)`。
- 云 Provider 时，要求玩家在 MCM 启用“允许云外发 + 允许外发玩家状态”，并在 Marcus AI 设置台允许该 route 云外发。
- AWAKE 开发者检查增加“对话路由云外发策略”与“当前实际 classification”两行。

### 2. 存储写入路径

- `WorldStateStore.TryApplyAsync` 对写命令把 `storage.key_not_found` 视为“空状态”，初始化新 state 后继续写入，而不是重试后丢弃。
- 读取路径仍按未初始化处理，不伪造数据。
- 补充 SdkSmoke：缺失 key 时 proactive / relationship / transcript 写入能成功创建并回读。

### 3. Prompt 注册诊断

- `RegisterPromptBestEffortAsync` 记录完整错误 code/category/safe fallback，并标记是权限、路由还是框架内部失败。
- 同会话内同一 prompt 只注册一次，避免重复失败刷日志。

### 4. C 键默认冲突

- `SceneShoutKey` 默认改为 `V`，MCM 保留可配置。
- 开发者检查增加按键冲突探测：当前 AWAKE 快捷键与常用游戏键是否重复。

## Batch 2：需要 grill-me -> PLAN 的机制修复

### 5. 主动对话逻辑化

- 新建触发模型：关系阶段、近期事件、身份/角色、地点/场合、需求/动机、冷却与疲劳。
- 确定性条件先筛出候选并打分，概率只作为最终扰动；每次触发写入 `triggerReason`。
- 开场白由 motive/rule 生成，不再是通用文案。
- MCM 从单一概率改为模式：关闭 / 事件驱动 / 均衡 / 频繁；概率只做二级缩放。

### 6. 大地图对话入口统一

- 与 `PLAN-UnifiedDialogueSession` 合并：地图上先定范围 -> 定格画面 -> 选择具体人物或公开喊话。
- 通讯录、遭遇面谈、场景 T/Y、事件对话共用同一会话模型。
- 地图选人保留独立轻量入口，不新增第三套对话 UI。

## Batch 3：P2 体验补齐

- 开发者检查：增加状态刷新、测试触发、日志跳转、命令诊断，不再只是静态行。
- 世界书管理：游戏内加载状态、重载、校验、关键词/规则检索调试面板。
- 通讯录 UI：继续补头像、身份、位置、关系摘要与最近事件。

## MCM 评估

- Batch 1：云外发开关、场景喊话键、开发者检查入口保持/调整提示。
- Batch 2：新增 NPC 主动模式；地图对话入口与定格行为可配置。
- Batch 3：世界书管理入口、开发者诊断入口。

## 验证方式

- 双版本构建 0 警告 0 错误；SdkSmoke 覆盖存储缺 key 写入、云外发分类、Prompt 注册失败诊断。
- 本地化与 release check 全绿；DLL 同步 `_build_out` / `dist` / 游戏目录。
- 游戏内验收：对话能走通、主动对话有可解释触发、地图入口可调出 AI 对话、C 键不再冲突。
