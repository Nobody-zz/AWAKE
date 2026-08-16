# AWAKE 任务队列

> 日期：2026-08-16
> 规则：每轮开始必须先读 `awake-task-continuity/SKILL.md` 与本文件；延续候选必须列出并等待用户决策，不自动选择下一项。
> 任务锁：同时只允许一个 `in_progress`；游戏测试反馈先入队，不打断当前任务；只有用户明确重定向或批准暂停当前任务时才切换。
> 当前状态：A 类与 B 类已完成；大审核通过并已推送；未提版。

## 当前检查点

- 当前任务：`PLAN-ContactHubHistory-20260816.md` 代码完成，状态 `pending_game`。
- 最近完成：Messenger 旧历史写入已弃用，通讯录聊天/历史统一走 transcript；`AwakeMessengerHistory` 仅保留旧档迁移读取；双版本构建、SdkSmoke、本地化、发布校验全部通过，DLL 已同步 dist/游戏目录。
- 下一步：用户进游戏验收通讯录/历史 Tab/固定/transcript-only 联系人；通过后切 `PLAN-Interactions`。
- 阻塞：游戏内验收需用户运行游戏。

### A 类当前状态

- B0-1 Messenger 缓存重置：完成。
- B0-2 存储回读重试：完成。
- B0-3 写任务安全包装：完成。
- B0-4 周报触发日持久化：完成。
- B0-5 MCM AI 自检补状态：完成。
- B0-6 release_check.ps1：完成并通过。
- B3 规则/提示词 schema 注册表：完成。
- B4 事件引擎数据驱动化：完成。
- B5 记忆结构化与衰减：完成。
- B6 主动聊天动机可注册：完成。
- B7 内容包公开 API：完成。
- Bug 扫描：重复 ID 拒绝、晚注册重载、内容注册预校验、条件解析 fail-closed、承诺去重与容量裁剪：完成。

### B 类当前状态

- B2 会话服务统一：完成。
- B8 Messenger 统一 UI：完成（统一会话入口）。
- B8 开发者检查面板：完成。
- B8 首启向导：完成。
- B9 日志/存储契约/性能探针：完成。
- 大审核：双版本构建、32 条 SdkSmoke、本地化 136/154/154、release_check OK、prefab XML OK：完成。

## 重新排列后的任务优先级

原则：需要游戏内实测才能验收的项目尽量后置；可离线开发、编译、SdkSmoke 验证的代码任务优先。

### A 类：不依赖游戏实测，可先开发

1. B0-1：Messenger 历史缓存随战役重置。
2. B0-2：持久化回读在存储未就绪时安全重试。
3. B0-3：WorldEvent / Messenger fire-and-forget 写任务安全包装。
4. B0-4：周报触发日持久化。
5. B0-5：MCM AI 自检补充路由/模型/Provider 状态。
6. B0-6：新增 `tools/release_check.ps1` 发布校验。
7. B3：规则/提示词 schema 注册表。
8. B4：事件引擎数据驱动化。
9. B5：记忆结构化与衰减。
10. B6：主动聊天动机可注册。
11. B7：内容包公开 API。

### B 类：代码可做，最终验收需要游戏内实测

1. B2：会话服务统一。
2. B8：Messenger 统一 UI。
3. B8：开发者检查面板。
4. B8：首启向导。
5. B9：日志、存储契约、性能探针（部分可离线）。

### C 类：强依赖游戏实测，尽量后置

1. B1：双模式对话（PLAN 已 APPROVED，待签收；落地后仍需完整游戏内验收）。
2. 世界书运行/命中/TextMappings/persona 验收。
3. Overlay 焦点与场景回退验收。
4. AF Batch 1：主动弹窗 → 深谈、拒绝冷却、60 秒 Esc。
5. AF Batch 2-5：收件箱/周报、日结日志、回复清洗、性能日志。
6. 世界事件/周报重启后持久化。
7. MCM 分组、预设、按钮。
8. 存储管道真机：Companion 读写、读档持久化。
9. NPC 记忆真机：读档回读、真实对话记忆。
10. 热键冲突检测真机。

## 游戏反馈隔离

用户进入游戏测试后反馈的崩溃、报错、行为异常统一按以下流程处理：

1. 先登记反馈到本队列，编号 `FB-YYYYMMDD-N`。
2. 记录来源、证据、关联任务、优先级。
3. 判定是否为 `blocking_current`：只有当前任务无法完成或无法验收时才可能打断。
4. 非阻塞反馈一律进入 `待修复` 或 `待用户决策`，不立即开工。
5. 当前任务完成后，再由用户从反馈清单选择下一项。

## 已完成（代码/文档）

### 运行时基础

- 场景 T/Y 三维距离选人
- 无内容事件引擎骨架
- `awake.relationship.delta.v1` 关系命令
- NPC 提示词关系命令接线
- 事件弹窗“参与话题”第三入口
- 事件类型清单、枚举与校验
- 事件选项接入关系命令结算
- 存储管道离线 SdkSmoke
- NPC 记忆逻辑 SdkSmoke
- AI 架构清单

### 世界书

- 世界书接口与格式规范
- 世界书 30 轮自审查
- 世界书加载 / 索引 / 查询服务
- 世界书运行时接入 NPC 对话
- 世界书附加目录数据加载
- TextMappings 全量 AF Kind 覆盖（22 种）
- 一次性迁移脚本 `tools/migrate_af_worldbook.ps1`
- 迁移规范文档
- `variantSelection` 显式解析与校验
- 卡拉迪亚编年史嵌入 `ModuleData/Worldbook`（759 文件，约 3 MB）

### AI / 框架 / 配置

- 路由 ID 命名空间修复（`AWAKE.route.*`）
- Companion 路由恢复
- `AwakeFeedback` 统一操作反馈
- MCM 菜单重排、预设与操作按钮
- 世界事件持久化与周报自动生成
- Marcus MCM 便捷配置（状态、一键打开 AI 设置/诊断、自动同步引导）
- 开发者测试工具（强制深谈、收件箱/周报、重置主动状态）
- 事件收件箱 Gauntlet UI（列表、滚动、Esc 关闭）
- 周报浏览器 Gauntlet UI（自建 prefab，不使用 AF 美术资产）

### AF 结构学习

- AF 结构落地方案
- AF 历史版本演进分析（v0.8.4-v1.3.2.1）：规则/后处理迭代、主动聊天动机化、世界玩法外扩、稳定性投入与单体膨胀教训
- AWAKE 改进方向文档：双模式对话、会话统一、规则 schema、命令层、事件引擎、记忆、主动聊天、发布纪律
- AWAKE 落地方案：B0-B9 分批执行，含依赖、验收、退出条件与版本纪律
- AWAKE 计划与文档总索引：77 个活跃文档按执行链/活跃计划/待验收/参考/AF 学习分类，205 个归档文档不再作当前权威
- AF Batch 1：主动聊天状态机 + 60 秒 Esc 解锁
- AF Batch 2-5：命令台/收件箱/周报、记忆日结、UI 审计、回复规范化、性能探针
- AF 五批次 grillme 复查
- UI 借鉴映射文档
- Messenger 会话历史持久化
- `awake-task-continuity` 任务连续性 skill

## 待游戏内验收（C 类明细，保留）

- 世界书：`worldbook_runtime_initialized`、NPC 命中、TextMappings、persona
- UI 排版/本地化：NpcDialogue 输入区不重叠、通讯录左右栏不压聊天区、收件箱/周报滚动条留白、命令台顺序、中英文按钮与状态文案
- Overlay 焦点与场景回退：四个面板不再因即时 no_focus 失败；场景非英雄不再回退原生对话（等待游戏内验收）
- AF Batch 1：主动弹窗 → 深谈、拒绝冷却、60 秒 Esc
- AF Batch 2-5：收件箱/周报、日结日志、回复清洗、性能日志
- 世界事件/周报：重启后收件箱仍有历史、周报自动生成
- MCM：分组、预设、按钮
- 存储管道真机：Companion 读写、读档持久化
- NPC 记忆真机：读档回读、真实对话记忆

## 延续候选（B/C 类候选，保留备查）

1. Messenger 统一会话：通讯录与 NPC 覆盖层统一入口调度，不删除任一层。
2. Messenger 写信/来信：远方联系人、回复延迟、未读、来信通知。
3. 事件收件箱 UI 升级：代码已完成（Gauntlet 列表），待游戏内验收。
4. 周报浏览器 UI：代码已完成（独立 Gauntlet 查看器），待游戏内验收。
5. 开发者检查面板：完整诊断 UI，而不是文本报告。
6. 对话覆盖层等待动画与状态提示增强。
7. Messenger 未读计数与大地图通知。
8. Marcus 一键配置：代码已完成（MCM 状态、AI 设置/诊断入口、自动同步引导），待游戏内验收；写入 Provider profile 不建议 AWAKE 直写。
9. 场景可视化选人 + 原版对话 AI 模式：AF 式地面扇形范围 + 候选高亮；同时第一版内置“原版对话窗口 AI 模式”切换层，AI 场景对话独立接口，原版对话窗口 AI 模式共存；方案见 `PLAN-SceneVisualSelection-20260816.md`。

## 待修复（A 类来源，保留明细）

1. P1：Messenger 历史缓存未随战役重置。
2. P1：持久化回读在存储未就绪时永久跳过。
3. P2：WorldEvent / Messenger fire-and-forget 写任务统一安全包装。
4. P2：周报触发日持久化，避免读档重复生成。
5. P2：MCM AI 自检补充路由/模型/Provider 状态。
6. P1：场景非英雄 NPC 回退原生对话触发 `GenerateUniqueNoFromParty` 空引用（E.htm 堆栈 + Awake.log 的 `npc:merchant_empire:a27` native 回退）；已加防护：场景模式跳过非英雄原生回退，待游戏内验收。

## 半成品（有入口但未闭环）

- `EventDialogueQueue`：引擎已生产，但无内容规则。
- 命令层：只有关系命令，世界效果未接。
- Preprocess / Postprocess 路由：已注册但无调用方。
- 无名 NPC：有身份回退，无永久记忆与命令边界。
- 遭遇面谈 / 场景 T/Y / 通讯录：统一会话未完成；通讯录历史已持久化。
- 开发者检查：仍是文本报告。

## 排队中（新想法/建议，不打断当前任务）

- Messenger 群聊、头像、媒体、TTS。
- 记忆分级、承诺账本、秘闻传播。
- `FB-20260816-2`：通讯录升级为“关系中心 / 对话历史管理器 / 人物卡片 / 写信 / 交互动作 / 统一对话入口”，方案见 `docs/AWAKE-ContactPanel-Concept-20260816.md`；状态 `queued`，等用户确定分阶段范围后走 grill-me。
- 2026-08-17 游戏引导增强：自动弹出 + 多步引导 + 持久化进度，只借鉴 AF 设计不复制代码/资产；方案见 `docs/AWAKE-Onboarding-Concept-20260817.md`，状态 `queued`，待用户批准后走 grill-me。
- 2026-08-16 群聊整理：信使距离/时间、善恶值/身份差异、避免公屏开麦、通讯录记忆管理器、事件动态、关键词调整、头衔/称号、记忆压缩清理、战俘转奴隶兵、借贷、国家态度/王国卡/宿敌、阴谋/刺杀、区域文化提示词；完整清单见 `docs/AWAKE-Community-Discussion-20260816.md`，状态 `queued`，不打断当前任务。
- 世界事件 / 政令。
- 内容包接入后的事件内容批次。
- 体验与测试改进建议：见 `docs/AWAKE-UX-Dev-Improvements-20260816.md`；含首启向导、收件箱/周报 UI、开发者检查面板、游戏内测试触发器、结构化日志等。

## 已暂停 / 不迁移

- 内容注入与背景知识接入。
- 知识检索语料与内容包 RAG 接入。
- 内容包公开 API 落地。
- 女神人格、情色机制。
- 旧 AF / 爱与恨兼容。

## 最新游戏反馈（2026-08-16）

- `FB-20260816-1`：场景选人范围框选不明显，难以选中具体对象；希望增加“近→远”和“远→近”两个循环键，并支持不选具体人物直接场景喊话。
  - 来源：用户实测反馈 + `Awake.log` 显示场景 overlay 多次成功打开（`a23`、`a20`），无崩溃。
  - 关联：`PLAN-SceneVisualSelection-20260816.md`（已 APPROVED，待用户签收；当前只落了 T/Y 基础循环，未落地面扇形与候选可视化）。
  - 优先级：P1；状态：`fixed_pending_game`；分类：`non_blocking`，不打断当前任务。
  - 硬性验收红线：标记必须非常明显。地面范围、候选高亮、当前目标高亮三者要能一眼分辨，不得出现“有标记但看不清、被环境色吞掉、远距离不可见”的情况。
  - 下一步：已签收并实施，代码完成待游戏内验收。
  - 本轮实施：双键往返、场景喊话、地面扇形 + 候选/目标高亮、持续状态条、场景模式服务与独立输出契约；双版本构建 0 警告 0 错误，SdkSmoke 34 条 PASS，本地化 156/180/180，release_check OK，DLL 已同步 dist/游戏目录。

## 评估原则反馈（2026-08-16）

- `FB-20260816-3`：设计出发点不能只问效率和可行性，必须同时考虑“玩家观感”与“开发者制作/维护难度”。
- 已纳入：`AWAKE-ContactPanel-Concept-20260816.md`、`Five-Corrections-Revised-20260816.md`、`docs/grill/Grill-Summary-20260816.md`。
- 后续 GRILLME / PLAN 审查统一加入两条轴；玩家可理解优先，高维护成本默认先做低维护版本。

- `FB-20260816-4`：重新判断 GRILLME 轮次，不再默认每条 50 轮。
- 新策略：默认 PLAN `MAX_ROUNDS=5`；统一会话/交互命令高风险项 `MAX_ROUNDS=8`；资产边界/历史命令低风险项 `MAX_ROUNDS=3`；APPROVED 提前终止，5 轮仍 REVISE 就交用户拆解。
- 已写入 `docs/grill/Grill-Summary-20260816.md`。

## 常驻原则（2026-08-17）

- 每个功能必须评估 MCM 菜单是否需要调整或新增调控项；评估结论写进 PLAN，不需要时写明理由。
- 已有玩家可调行为时必须提供 MCM 入口，默认值 fail-safe；改动同步检查分组、中英文、预设联动与 `Config.json` 兼容。
- 已写入 `AWAKE/AGENTS.md` 的 `MCM 菜单规则`。

## 最新游戏反馈（2026-08-17）

- `FB-20260817-1`：大地图 NPC 对话入口割裂。
  - 证据：用户实测；只读核查显示通讯录 `IsNearby` 依赖 `NpcDialogueLauncher.GetNearbyTargets`，遭遇菜单有“面谈（醒世）”，但相遇后缺少可直接调出 AI 对话菜单的入口。
  - 建议方向：先定范围 -> 定格画面 -> 选择具体人物/公开喊话（参考 AF），并统一场景/地图/通讯录入口。
  - 优先级：P1；状态：`queued`；分类：`non_blocking`。
- `FB-20260817-2`：C 键默认冲突。
  - 证据：`AwakeConfig.SceneShoutKey` 默认 `"C"`，`AwakeTerminalBehavior` 绑定 `InputKey.C`；游戏本身已占用 C。
  - 建议：更换默认键并保留 MCM 可配置；必要时加按键冲突探测。
  - 优先级：P1；状态：`queued`；分类：`non_blocking`。
- `FB-20260817-3`：主动对话无逻辑、纯概率触发。
  - 证据：`NpcProactiveService.EvaluateAsync` 目前是 `BaseChance + affinity` 随机抽取，motive 按权重随机，OpeningHint 通用，缺少“当前处境/事件/身份/关系事实”驱动。
  - 日志证据：16:20:27-38 连续出现 `npc_proactive_candidate_created hero=... motive=casual`，且同时大量 `world_state_relationship_load_failed code=storage.key_not_found`，说明没有关系/事件事实参与，纯随机高频触发。
  - 建议：先做确定性触发条件（关系阶段、近期事件、身份、地点、需求），概率只做最终扰动；并给触发写可解释理由。
  - 优先级：P1；状态：`queued`；分类：`non_blocking`。
- `FB-20260817-4`：通讯录 UI 继续补头像等信息。
  - 已有人物卡头像基础，继续扩展联系人列表/卡片信息。
  - 优先级：P2；状态：`queued`；分类：`non_blocking`。
- `FB-20260817-5`：开发者检查目前是摆设。
  - 证据：DeveloperCheckOverlay/VM 已有，但只有静态行展示，无可操作入口/刷新/跳转/测试触发。
  - 建议：补状态刷新、测试触发、日志跳转、命令诊断。
  - 优先级：P2；状态：`queued`；分类：`non_blocking`。
- `FB-20260817-6`：世界书管理功能几乎没有。
  - 证据：只有 WorldbookLoader/Service/Runtime 的加载与查询，无游戏内管理、重载、校验、编辑、检索调试面板。
  - 优先级：P2；状态：`queued`；分类：`non_blocking`。
- `FB-20260817-7`：Slaanesh 路由残留与对话路由疑点。
  - 证据：源码/游戏模块文本未找到 `Slaanesh` 路由文件，当前路由为 `AWAKE.route.npc.dialogue`；马库斯侧称仍残留 Slaanesh 文件名相关路由文件，需进一步查 `platform.db`/Companion 配置。
  - 日志证据：`[2026-08-17 00:18:38] AI task failed ... RouteId: AWAKE.route.npc.dialogue | Code: ai.cloud_export_denied`；AWAKE 日志同步出现 `npc_dialogue_turn_failed code=ai.cloud_export_denied category=ProviderFailure`，对话路由实际被云外发策略拒绝。
  - 优先级：P1；状态：`queued` / `decision_needed`；分类：`non_blocking`。
- `FB-20260817-8`：存储 key 缺失导致持久化失败/写丢。
  - 证据：AWAKE 日志大量 `world_state_memory_load_failed`、`world_state_relationship_load_failed`、`world_state_messenger_load_failed`、`world_state_proactive_load_failed` 均为 `storage.key_not_found`；`world_state_write_failed_dropped ... code=storage.key_not_found attempts=3`；会话结束 `world_state_final_drain_failed pending_writes=20 dropped=2`。
  - 优先级：P1；状态：`queued`；分类：`non_blocking`。

## 修复方案（2026-08-17）

- 已整理 `docs/AWAKE-Repair-Plan-20260817.md`，分 Batch 1（云外发、存储缺 key、Prompt 诊断、C 键）、Batch 2（主动对话逻辑化、地图对话入口）、Batch 3（开发者检查、世界书管理、通讯录 UI）。
- 状态：用户已批准；Batch 1 完成待游戏内验收；Batch 2 主动对话逻辑完成，地图对话已加“范围 -> 定格选择 -> 公开喊话”两步入口；Batch 3 开发者检查与世界书管理完成；统一会话与通讯录 UI 扩展仍待后续批次。

## 内测门槛（2026-08-17）

- 已整理 `docs/AWAKE-Internal-Test-Gate-20260817.md`：Level 0 技术内测 / Level 1 玩法内测 / Level 2 公开 Beta。
- 当前尚未放行 Level 0，需先完成 Batch 1 与 ContactHubHistory 的游戏内验收。

## 可行性核验（2026-08-16）

- API probe：`Hero.Gold` 可读可写；`PartyBase.ItemRoster` 存在；`HeroVM(Hero, bool)` 提供 `ImageIdentifier`。
- 三份 PLAN 均可行；给金币不因原生 API 阻塞，portrait 可用原生角色头像。
- 主要风险是范围/回归而非不可实现：建议按 ContactHubHistory → Interactions → UnifiedDialogueSession 分批实施。

## 排队中（新增）

- 场景选人体验增强：`FB-20260816-1`，双键往返选人 + 无目标场景喊话。

## 本轮实施（待游戏内验收）

- 场景选人 UX：`[` 近到远、`]` 远到近、`C` 场景喊话；按住 `T` 显示地面扇形、候选金色轮廓、当前目标品红脉冲。
- 无目标场景喊话：独立 `scene_shout` 会话、场景专用 prompt/output 契约、拒绝关系命令、不写 NPC 记忆/关系。
- 持续状态条与轮廓能力探测，不可用时走文字兜底。

## 新概念方案（未进入实现）

- `FB-20260816-2`：通讯录关系中心。参考 AIInfluenceHistoryManager 面板与 AliceMM 对话/写信结构，已拆成 Phase A-E：关系中心核心、历史管理、写信、交互动作、统一对话入口。
- 关键修正：原始对话、压缩记忆、关系状态分成三层；交互动作走代码保底而非自由文本；场景/地图/通讯录共用同一会话模型。
- 等待用户拍板：保留天数、固定上限、历史管理器可见性、第一批交互动作、信件送达规则、场景对话是否并入面板。

## 进行中（2026-08-16）

- `GRILL-20260816-2`：已完成两轮。五个关键修正每轮各用独立 Codex 只读审查生成 50 个对抗检查点，累计每项 100 点；第二轮五条均为 REVISE。
- 结论：概念文字无法收敛，必须转成 schema/命令/存储/UI/测试契约后再 APPROVED。修订版见 `docs/Five-Corrections-Revised-20260816.md`，两轮产物见 `docs/grill/`。
- 下一步：等用户拍板 Phase A 边界（是否 v1 只做给金币+请求承诺、历史是否升级 transcript v1、场景入口是否保留轻量覆盖层），然后分别写成 PLAN 契约再收敛。

- `PLAN-20260816-3`：三份 PLAN 全部 APPROVED。
  - `PLAN-ContactHubHistory-20260816.md`：Round 7 APPROVED。
  - `PLAN-Interactions-20260816.md`：Round 9 APPROVED。
  - `PLAN-UnifiedDialogueSession-20260816.md`：Round 5 APPROVED。
  - 用户已签收。当前任务：`PLAN-ContactHubHistory-20260816.md` 代码完成，状态 `pending_game`。
  - 实现顺序：ContactHubHistory → Interactions → UnifiedDialogueSession。
- `PLAN-ContactHubHistory` 当前进度：
  - 完成：canonical key、transcript/contacts/audit schema 与 applier、AwakeTranscriptService、chunk 字节上限、右侧人物卡片、Messenger VM 异步历史加载、联系人按 canonical 去重、回合双行单命令写入、NpcDialogueService transcript sink、asset boundary lint、历史 Tab、transcript-only 联系人发现。
  - 完成：Messenger 旧历史写入已移除，聊天/历史 Tab 均从 transcript 读取；`AwakeMessengerHistory` 保留为旧档迁移读取源。
  - 自检修复：ChunkIndex 支持跨 chunk 固定、turn 幂等键加序号、历史加载竞态防护、pin 只在实际成功后刷新、迁移分批防超限；首次历史读取等待迁移完成，避免旧档漏读。
  - 下一步：游戏内验收通讯录/历史 Tab/固定/transcript-only 联系人；通过后切 `PLAN-Interactions`。
  - 验证：双版本构建 0 警告 0 错误；SdkSmoke PASS ALL；localization 165/193/193；asset lint 107 文件 OK；release_check OK；DLL SHA-256 5578473BE803345485C78E514850758B932406F5B21CF0E1DF0DEE594F5E68D1 已同步 _build_out/dist/游戏目录。

## 远程同步

- 远端 `main` 已确认以本地最新为权威。
- 最近推送成功：`f16d4b0`（A 类 B0-B7 完成）。
- 最近推送成功：`d256e91`（B 类完成 + 大审核修复）。
- 最近推送成功：本批 ContactHubHistory + transcript 历史权威 + 场景选人 UX（含队列检查点）。
