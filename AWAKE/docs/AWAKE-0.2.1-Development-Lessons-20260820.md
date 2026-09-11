# AWAKE 0.2.1 开发日志与测试教训复盘

> 日期：2026-08-20
> 范围：回顾 2026-08-16 至 2026-08-17 围绕 `0.2.1` 验收目标完成的开发、修复、SdkSmoke、发布检查与游戏日志。
> 原则：严格区分“代码/离线测试通过”“旧构建真机通过”“当前构建已验证”。

## 1. 结论摘要

`0.2.1` 阶段最大的收获不是新增了多少功能，而是暴露了 AWAKE 的四类系统性薄弱点：

1. Prompt、输出 schema、运行时解析器与 Provider 实际输出没有作为同一份契约维护。
2. 异步注册、存储 readiness、写队列 drain 和读档回读之间缺少明确的依赖门禁。
3. 早期日志重错误、轻成功，导致“用户已经完成操作”仍无法从日志证明完整链路。
4. 多轮修复都保留 `v0.2.0` 版本号，运行日志不带构建哈希；同一版本下 DLL 多次变化，旧日志无法可靠对应当前二进制。

因此，当前不能直接宣布 `0.2.1` 已完成。历史日志证明若干链路曾经通过，但当前源码/dist DLL、游戏目录 DLL 与任务队列记录已经发生漂移，必须先统一候选构建，再做一次短而完整的真机验收。

## 2. 本次核查使用的证据

- `docs/AWAKE-Task-Queue-20260816.md`
- `docs/archive/AWAKE-Task-Queue-History-20260816-to-20260817.md`
- `docs/AWAKE-Internal-Test-Gate-20260817.md`
- `docs/AWAKE-Repair-Plan-20260817.md`
- `docs/Awake-StorageAndMemory-Verification-20260816.md`
- `BUILD_VERIFICATION.txt`
- `_houkai_merge/AWAKE.Tests/Program.cs`
- 游戏日志：
  - `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\Logs\Awake.log`
  - `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\Logs\AwakeProbe.log`
  - `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\MarcusAIFramework\log\framework.log`
  - `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\MarcusAIFramework\log\companion.log`

日志最后写入时间为 2026-08-17；当前游戏 DLL 最后写入时间为 2026-08-18，源码构建/dist DLL 最后写入时间为 2026-08-19。因此这些日志全部早于当前二进制，属于“历史构建证据”，不能直接给当前 DLL 背书。

## 3. 当前实际状态

### 3.1 二进制与部署已经漂移

| 位置 | SHA-256 | 最后写入 | 判断 |
|---|---|---|---|
| `_build_out/1.3.15/Release/Awake.dll` | `F863D8EF5D53B6FA44BA6B4311BA16FE53AADCA17EF3B8D5BA5F5AC1269D1CE4` | 2026-08-19 09:20 | 当前源码构建 |
| `dist/Modules/AWAKE/.../Awake.dll` | `F863D8EF5D53B6FA44BA6B4311BA16FE53AADCA17EF3B8D5BA5F5AC1269D1CE4` | 2026-08-19 09:20 | 与源码构建一致 |
| 游戏 `Modules/AWAKE/.../Awake.dll` | `FF98881E58102E992CFFE2B959222B406165762BEA2E2E3819E1647AF8F61021` | 2026-08-18 17:44 | 落后于源码/dist |
| 队列原记录 | `D2EA36815EC262E4C540D33CC55AD63393DF7B066651C03088D3FDAC95953BD7` | 旧记录 | 已失效 |

三个 `SubModule.xml` 仍然都是 `v0.2.0`。这说明“0.2.1”目前只是验收目标，不是可由日志直接识别的发布版本。

### 3.2 世界书当前静态状态

源码、dist 和游戏目录当前均为：

- 335 个规则 JSON；
- 335 个 ID；
- 335 个唯一 ID；
- 0 个缺失 ID；
- 0 个解析错误；
- 0 个重复 ID。

历史真机日志在 2026-08-17 后续会话显示 `worldbook_runtime_initialized rules=334 personas=415 warnings=0`。它证明当时的旧构建已经消除了重复 ID 警告，但不能证明当前 335 文件集合在当前 DLL 下仍会得到同样结果。文件数与历史运行时加载数的差异必须由新日志解释，不能自行假定正常。

### 3.3 历史真机已经证明的链路

以下结论只适用于产生 2026-08-17 日志的历史构建：

- 多次出现 `CampaignSessionReady`，启动和进入战役链路曾经通过。
- 地图/NPC 对话 Provider 请求曾返回 `Result: completed`。
- `effects` 省略导致的 `ai.output_schema_invalid` 修复后，对话可正常完成。
- 记忆摘要任务曾使用 `awake.npc.memory.summary.output.v1` 返回合法摘要并显示 `Result: completed`。
- 写信链出现完整四段成功事件：
  - `transcript_write_persisted`
  - `contact_index_persisted`
  - `letter_send_succeeded`
  - `letter_history_reloaded`
- 世界书启动警告曾降为 `warnings=0`。
- 多次出现 `world_state_final_drain_complete`。
- Companion 启动期的 pipe broken / EndOfStream 在数秒后自动恢复，后续 AI 请求可完成；它属于短暂重连噪音，不等同于整轮网络故障。

### 3.4 尚未被证明的闭环

即使按历史构建计算，以下事项仍缺少充分证据：

- 记忆摘要生成完成后，`PatchMemorySummaryAsync` 是否真实持久化成功。
- 保存、退出、读档后，同一 NPC 是否能回读这条摘要。
- 当前游戏 DLL是否包含 2026-08-19 源码/dist 的全部 Persona 与后续修复。
- 当前 335 条世界书规则在当前 DLL 下是否仍为 `warnings=0`，以及为什么旧日志只加载 334 条。
- 地图 `source=map` transcript 在当前构建中的正向持久化事件；旧日志只明确记录过修复前的 `transcript_turn_rejected source=map`。
- 联系人身份标签调整后，同名人物的名称、家族、效忠王国和城镇归属显示是否符合玩家观察。
- `give_gold` 的 pending/complete/compensated 恢复路径与 promise 状态机是否在真实存读档中通过。
- Hub、Esc、焦点恢复和多入口切换是否在长时间游玩后无输入锁。

## 4. 主要故障与对应教训

### 4.1 “可选字段”必须在四层同时可选

故障：Prompt 将 `effects` 描述为可选，但注册的输出 schema 仍把它当必填。DeepSeek 返回合法 `reply`/`mood`、省略 `effects` 时，框架报 `ai.output_schema_invalid`。

修复：schema 只要求 `reply`、`mood`；运行时 validator 将缺失 `effects` 归一化为空数组。

教训：每个 AI 输出契约必须同时审查四层：

1. Prompt 示例与自然语言说明；
2. 注册给框架的 JSON schema；
3. AWAKE 解析/验证器；
4. 缺省值归一化和后续业务使用。

任何一层的 required/optional 不一致，都应在测试中失败。以后每个 Provider 契约至少测试：字段齐全、合法省略可选字段、字段顺序变化、额外空白、空数组和无 command。

### 4.2 Prompt/schema 注册是硬依赖，不是 best-effort 装饰

故障：记忆任务引用 `awake.npc.memory.summary.output.v1`，但没有等价注册入口，导致 Provider 已返回合法摘要，框架仍报 `ai.output_schema_not_found`。

进一步审查发现，旧逻辑在注册完成前就写入“已尝试/已注册”状态；暂时失败、取消或并发初始化可能污染整个进程，之后不再重试。

修复：增加记忆 schema 注册；引入按 prompt key 的 single-flight 协调器；只有成功或 `prompt.revision_conflict` 才缓存 usable；失败和取消允许重试；注册不可用时禁止提交依赖任务。

教训：注册流程必须满足：

- `await registration -> usable -> submit`，不得先提交后等结果；
- “尝试过”不等于“可用”；
- 幂等冲突与真实冲突分开；
- 并发调用共享同一注册任务，但单个等待者取消不能污染其他等待者；
- 注册失败的错误码、类别、retryable、correlation 必须进入日志。

### 4.3 存储成功的定义必须是“已确认落盘”

故障一：首次读取 `storage.key_not_found` 被当成写入失败，反复重试后 dropped。实际上首次不存在是正常空状态，写路径应初始化文档后继续。

故障二：`EnsureWorldStateReadyAsync` 只要 12 个命名空间中任意一个打开成功就返回 true；写信真正依赖的 transcripts/contacts 可能没有打开。

故障三：写操作排队并 drain 后不检查 `WorldDrainSummary`，即使最终 Set 失败，外层仍返回 true，UI 可能显示成功。

修复：缺失 key 在写路径初始化；按调用方要求检查精确 namespace；写入 bool 依据 owner drain summary；联系人索引失败会使写信整体失败；增加 single-flight readiness 缓存。

教训：存储 API 的状态必须明确区分：

- accepted：进入本地队列；
- persisted：框架确认写入完成；
- reloaded：从存储重新读取到预期内容；
- save-committed：与游戏存档锚点绑定完成。

面向 UI 的“成功”至少应达到 persisted；涉及读档可靠性的验收必须达到 reloaded/save-committed。

### 4.4 只写失败日志会让成功也无法验收

故障：用户已经发信，但旧日志只有通用 `letter_rejected` 或没有成功事件，无法判断是空文本、幂等键、store 未就绪、transcript 失败还是联系人失败。

修复：增加写信四段成功事件和分层拒绝原因。

教训：关键闭环需要正向可观察事件，而不只是错误日志。推荐统一事件顺序：

`entry_opened -> request_accepted -> provider_completed -> parsed -> persisted -> reloaded -> ui_observed`

每一段携带稳定但不敏感的 `heroId`、`conversationId`、`source`、`route`、`correlationId` 和构建指纹。不要记录 API Key 或完整隐私正文。

### 4.5 离线 Smoke 不能替代真实生命周期

SdkSmoke 已覆盖大量纯逻辑，包括 transcript roundtrip、失败写入返回 false、Prompt registration single-flight、可选 effects、世界书解析、存储管道和命令校验。它的价值很高，但当时仍遗漏或无法模拟：

- Marcus Companion 的真实启动与重连时序；
- Provider 对“可选字段”的真实生成差异；
- Bannerlord Campaign 生命周期与覆盖层焦点；
- 游戏存档锚点、退出、读档后的跨进程回读；
- 当前发布目录是否真的加载了刚构建的 DLL；
- 用户通过具体入口看到的显示名称和视觉顺序。

教训：以后必须采用三层门禁：

1. 单元/Smoke：确定性逻辑和错误分支；
2. 本地集成：真实框架 FakeHost/Companion 契约、文件同步和哈希；
3. 游戏真机：按入口完成一次完整闭环并收集新日志。

“PASS ALL”只能写成“离线门禁通过”，不能单独作为“功能完成”。

### 4.6 开发测试入口不能代替正式入口

历史后续成功日志中出现 `source=dev_test`；修复前正式地图入口出现过 `source=map` 拒绝。即使两者最后调用同一服务，也可能经过不同 target 解析、会话来源、transcript 白名单和 UI 生命周期。

教训：每个对话功能至少建立入口矩阵：

- 地图对话；
- 场景对话；
- 遭遇菜单；
- 通讯录；
- 主动对话；
- 开发者测试。

共享核心服务可以只测一次重逻辑，但每个入口必须有最小 wiring smoke 和至少一次真机验收，不能用 `dev_test` 的成功替代 `map`。

### 4.7 稳定 ID 与显示标签必须分离

地图历史能够写入后，用户观察到 NPC 名称不正确或同名难以区分。后续讨论确定：长期主键必须使用稳定 CharacterId，显示标签根据角色类型组合真实游戏状态，例如领主可显示“名字 · 家族 · 当前效忠王国”，无王国角色显示“名字 · 家族”，流浪者仅显示名字，城镇要人/固定无名 NPC 才显示稳定城镇。

教训：

- 不用显示名做存储键；
- 不把贵族当前位置误当固定归属；
- 当前效忠王国是动态身份，应从游戏实时读取；
- 历史联系人需要保存稳定 ID，同时允许显示标签随身份变化重算；
- 重算显示标签不能改写历史事实或拆分同一个联系人。

### 4.8 世界书门禁要同时检查文件、ID 和运行时加载数

故障：两个泛化词条造成重复 Rule ID，启动日志为 `warnings=2`。静态文件数、唯一 ID 数与运行时加载数曾出现不同口径。

修复：删除泛化词条，只保留两个指定词条；静态审计增加重复 ID 门禁。

教训：世界书发布至少同时记录：

- JSON 文件数；
- 成功解析数；
- 唯一 ID 数；
- 重复/冲突数；
- 被拒绝数及原因；
- 运行时实际加载数；
- manifest/内容哈希。

仅看到 `warnings=0` 仍不足以解释“为什么文件 335、运行时 334”；加载器应输出 skipped/rejected 计数。

### 4.9 启动重连噪音不能误判为功能故障

Marcus 日志在启动和会话切换时出现 pipe broken / EndOfStream / profiles list 未连接，约数秒后恢复连接，后续任务完成。

教训：连接状态至少分为：

- startup_transient：启动窗口内自动恢复；
- recovered：发生过断开但当前可用；
- blocking_transport：超过门槛仍不可用；
- provider_failure：已连接但上游返回错误；
- user_cancelled：用户或客户端取消。

只有影响请求提交或超过时间门槛时才升级为阻断问题，避免把短暂重连当成“网络一直坏”。

### 4.10 同一版本下反复换 DLL 会破坏证据链

本阶段多个修复批次都保留 `v0.2.0`，DLL 哈希多次变化。运行日志只有 `module_load version=0.2.0`，无法从日志判断具体是哪个修复批次。

教训：不一定每次修复都正式提版，但每个候选构建必须带可观测指纹：

- `InformationalVersion` 或内部 `BuildId`；
- Git/源码快照标识（本工作区无 Git 时用时间戳 + 源文件清单哈希）；
- DLL SHA-256 前 12～16 位；
- 世界书 manifest 哈希；
- 运行时启动日志打印这些值。

之后所有真机结论都必须绑定 `BuildId + DLL hash + log session`。

### 4.11 不要在真机门禁未关闭时继续堆离线功能

`0.2.1` 阶段在多个 `fixed_pending_game` 尚未关闭时，又并行推进了 onboarding、统一会话、持久队列、承诺、give_gold、Hub、写信 UI 等大量离线工作。结果是待验收面不断扩大，难以判断一次新日志对应哪个变化。

教训：每个短批次只推进一个可验收纵切：

`入口 -> 调用 -> 结算/持久化 -> 正向日志 -> 真机复验`

若 P0/P1 阻断已经等待真机，不应继续扩大同一运行时的机制改动；可以做文档、测试设计或独立工具，但不能让候选 DLL继续漂移。

## 5. 对现有测试体系的改进要求

### 5.1 新增发布候选指纹门禁

- 构建时生成 `BuildId`。
- `module_load` 打印版本、BuildId、DLL hash 短码和世界书 manifest hash。
- 同步脚本生成三地哈希表。
- 游戏日志与候选清单不匹配时，验收自动判无效。

### 5.2 新增 AI 契约矩阵

每个 route 至少验证：

- schema 已注册后才能提交；
- required/optional 与 Prompt 完全一致；
- Provider 省略每个可选字段仍可解析；
- 不合法字段得到明确错误；
- `prompt.revision_conflict` 幂等；
- 注册暂时失败后可重试；
- 并发提交只注册一次。

### 5.3 新增存储闭环矩阵

每个持久化功能至少验证：

- key 不存在时首次写入；
- namespace 部分不可用；
- SetAsync 失败；
- drain 失败；
- 写入成功后立即回读；
- 新进程/读档后回读；
- 幂等重放不重复；
- 当前时间线只读取 AcceptedSequence 以内内容。

### 5.4 新增入口矩阵

每个正式入口都要记录 source，并断言最终 transcript/会话使用同一稳定 heroId。开发者入口只能作为附加诊断，不能替代正式入口。

### 5.5 新增正向日志契约

对话、记忆、信件、世界效果和交互账本都应有有限状态机式日志，并给日志事件写静态 smoke，防止后续重构再次失去可观察性。

## 6. 0.2.1 重新收口建议

### 批次 A：冻结并统一候选构建

1. 以当前源码/dist 的 `F863D8...` 为候选基线重新执行双版本构建与 SdkSmoke。
2. 确认没有 Bannerlord 进程后，按同步脚本安全同步游戏目录。
3. 核对 root/dist/game 三地 DLL、世界书和 manifest 哈希一致。
4. 增加或至少记录本候选的 BuildId；没有 BuildId 时将完整 DLL hash 写入验收清单。

### 批次 B：最小真机验收

只测以下闭环，不继续新增机制：

1. 启动旧存档并进入大地图。
2. 从正式地图入口完成一次 NPC 对话，确认回复、`source=map` transcript 与联系人显示。
3. 关闭对话触发记忆摘要，确认 Provider completed、摘要 persisted。
4. 保存退出并读档，重新进入同一 NPC，确认记忆回读。
5. 发送一封信，确认四段成功事件和历史回读。
6. 确认世界书 `warnings=0`，并解释运行时 loaded 数与 335 文件是否一致。
7. 退出时确认 final drain，无 dropped。

### 批次 C：只修复新日志确认的问题

- P0：崩溃、卡死、存档污染、持久化丢失、重复结算、输入锁。
- P1：正式入口断链、记忆/世界书未命中、身份标签错误。
- P2：短暂重连噪音、提示和视觉细节。

不根据旧日志继续猜测，也不在本轮加入 `0.3.0` 新机制。

## 7. 当前阶段判定

- 离线逻辑基础：较完整，但验证边界需要更严格。
- 历史真机：部分关键链路曾通过。
- 当前部署：不一致，必须先同步候选。
- `0.2.1` 状态：`blocked_by_candidate_reconciliation_and_fresh_game_validation`。
- Level 0：暂不放行。

最重要的下一步不是继续写新功能，而是把“当前到底运行哪一个 DLL”变成可证明事实，然后用一轮短真机测试关闭记忆持久化、地图历史、联系人身份和世界书加载四个核心闭环。