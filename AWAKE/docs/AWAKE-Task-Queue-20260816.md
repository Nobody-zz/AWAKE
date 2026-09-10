# AWAKE 任务队列

> 最后整理：2026-08-17
> 当前权威：本文件只保留有效状态和推进方向；旧的逐轮记录已归档到 `docs/archive/AWAKE-Task-Queue-History-20260816-to-20260817.md`。
> 连续性规则：每轮开始重读 `awake-task-continuity/SKILL.md` 与本文件；同一时间最多一个 `in_progress`；“继续/做”只延续当前锁定任务，不自动选择新功能。
> 验证纪律：编译、SdkSmoke 和发布检查不等于游戏内通过；真机结论必须来自用户运行当前 DLL 后产生的新日志。

## 1. 当前总览

- 开发阶段：P1-P2 已批准的离线批次 C-I 已全部实现，当前没有未完成的已批准离线开发项。
- 当前锁定任务：`VAL-20260817-1` 真机验收与 P0 反馈修复；`ROADMAP-20260817-1` 已完成，路线图进入已确认状态。
- 当前状态：`in_progress`；版本路线图已完成并经用户确认，当前只处理 0.2.1 真机阻断项，不切换到 0.3.0。
- 内测门槛：Level 0 尚未放行；必须先完成本轮真机验收并处理其中的阻断反馈。
- 当前版本：AWAKE `v0.2.0`，未正式提版。
- 当前 1.3.15 DLL SHA-256：`D2EA36815EC262E4C540D33CC55AD63393DF7B066651C03088D3FDAC95953BD7`。
- 部署状态：修复 DLL 已同步到 `_build_out/1.3.15`、`dist` 与游戏 `Modules/AWAKE`，三处哈希一致；1.4.8 是独立兼容构建，哈希不同属于预期。
- Git 状态：`AWAKE-Repo` 已同步批次 I 与路线图文档但尚未提交；未经用户明确要求不提交、不推送。
- 路线图决策：采用 AWAKE 核心 + 官方内容包双轨；1.0 以“完整稳定可玩的 AI 世界模组”为第一成功标准；按可玩闭环分版、按测试阶段设门槛。
## 1.1 当前待进行计划（按版本目标）

| 计划/任务 | 版本目标 | 状态 | 下一动作 |
|---|---|---|---|
| `VAL-20260817-1`：批次 I、ContactHubHistory 与既有反馈真机验收 | `0.2.1` | `pending_game` | 用户重新启动游戏后复验记忆摘要、写信与世界书启动警告 |
| `PLAN-Awake-AF-Batch1`、`Batch2to5` | `0.2.1` | `pending_game` | 与 Level 0 一起验收，不重复离线开发 |
| `PLAN-DevTestTools`、`PLAN-EventInboxUI`、`PLAN-WeeklyReportBrowser` | `0.2.1` | `pending_game` | UI 和诊断真机验收 |
| `PLAN-WorldEventPersistence`、`PLAN-MarcusMcmConfig`、`PLAN-MessengerPersistence` | `0.2.1` | `pending_game` | 存读档、MCM、历史持久化验收 |
| `PLAN-ContactHubHistory`、`PLAN-Interactions`、`PLAN-UnifiedDialogueSession` | `0.2.1 → 0.3.0` | `pending_game` | 先完成当前验收，再作为 0.3 社交基座扩展 |
| `PLAN-SceneVisualSelection`、`PLAN-ProactiveLogic` | `0.3.0` | `pending_game` | 场景入口、主动对话和解释性触发真机复验 |
| B1 双模式对话（原版窗口 AI 模式） | `0.3.0` | `decision_needed` | 按路线图范围单独锁定实施 PLAN，不自动开工 |
| 记忆分级、关系阶段、承诺深化、流言基础 | `0.4.0` | `queued` | 以现有记忆/账本为基线重新写 PLAN |
| 事件内容批次、世界效果、周报后果链 | `0.5.0` | `queued` | 先选 20–40 条可验收内容，再走 PLAN/审查 |
| Messenger 来信送达、群体议事、社会传播 | `0.6.0` | `queued` | 新建通信与传播 PLAN；媒体/TTS 非硬门槛 |
| 国家态度、称号、借贷、阴谋和刺杀 | `0.7.0` | `queued` | 命令与风险结算稳定后分批立项 |
| 世界书管理、作者工具、公开 API、内容包 manifest | `0.8.0` | `draft` | 合并 API/世界书/内容工具计划后审查 |
| 性能预算、迁移矩阵、Beta/RC、正式发布 | `0.8 → 1.0` | `queued` | 在玩法闭环稳定后进入发布计划 |

当前优先顺序不是“立即做最高版本号”，而是：`0.2.1` 真机证据 → 阻断问题修复 → `0.3.0` 双模式对话与社交闭环 → `0.4.0` 记忆关系 → `0.5.0` 事件世界反应。

## 2. 当前锁定任务

### VAL-20260817-1：当前版本真机验收

- 优先级：P0。
- 状态：`pending_game`。
- 前置条件：用户使用当前游戏目录 DLL 启动游戏并生成新日志。
- 完成定义：入口可用、调用链真实执行、结算或持久化成功、存在玩家可观察结果，并有本次运行日志佐证。

#### 验收组 A：启动与基础回归

- 存档能进入大地图，`CampaignSessionReady` 正常出现。
- 首启向导只在 `MapState` 安全弹出，不再阻塞读档。
- `worldbook_runtime_initialized` 正常，启动阶段无持续 CPU 空转或日志停滞。

#### 验收组 B：通讯录与历史

- 地图、场景、遭遇和通讯录对话写入统一 transcript。
- 通讯录历史 Tab、固定、transcript-only 联系人和关系摘要正常。
- 远方联系人显示写信按钮；发送后 `source=letter` 内容出现在历史中。
- 复验 `FB-20260817-10`：地图 AI 对话不再被 transcript source 校验拒绝。

#### 验收组 C：引导与覆盖层

- Welcome → AI Config → Command Deck → First Dialogue → Contact History → Complete 顺序推进并持久化。
- 本局跳过只影响当前战役会话；永久跳过跨会话保持。
- NPC 对话、通讯录、事件收件箱、周报、开发者检查能由统一 Hub 正确判断和关闭。
- Esc、焦点恢复和场景回退不残留输入锁。

#### 验收组 D：AI 路由、存储与记忆

- 复验 `FB-20260817-11`：记忆日结不再出现 `ai.cloud_export_denied`。
- 复验 `FB-20260817-12`：提示词示例 `heroId` 不再出现双重引号。
- 复验 `FB-20260817-7`：实际使用 `AWAKE.route.*`，无 Slaanesh 路由残留或错误 Provider 配置。
- 复验 `FB-20260817-8`：首次缺 key 不刷错误噪音，真实写入不丢失，退出时 final drain 完成。
- 记忆摘要成功后可以落盘，重载后仍可读取。

#### 验收组 E：交互与恢复

- `give_gold` 成功扣除正确金币并写入 interaction ledger。
- 正常保存/重载后不会重复扣款。
- pending 状态按 expected balance 正确完成、补扣或 fail-closed 补偿。
- 账本完成写入失败时金币能退款并记录 compensated。
- promise_request / promise_update 状态转换和去重结果可观察。

#### 验收组 F：既有体验反馈

- 复验 `FB-20260816-1`：场景近/远循环、公开喊话、扇形和高亮足够明显。
- 复验 `FB-20260817-1`：地图对话入口完整，不再与场景/通讯录入口割裂。
- 复验 `FB-20260817-2`：旧 C/U 配置归一化为 V，MCM 显示与运行时一致。
- 复验 `FB-20260817-3`：主动对话由关系、事件、身份、地点或需求驱动，日志能解释触发原因。
- 复验 `FB-20260817-5/6`：开发者检查与世界书管理入口可操作，不是静态占位。

#### 下一动作

1. 用户运行当前版本并完成上述最小验收路径。
2. 收集本次运行的 `Awake.log`、`AwakeProbe.log` 以及 Marcus/Companion 对应日志。
3. Codex 先登记新反馈，再按 P0 → P1 → P2 修复；无新日志时不根据旧日志重复改代码。

## 3. 推进方向与优先级

### P0：稳定当前版本并放行 Level 0

1. 完成 `VAL-20260817-1` 真机验收。
2. 修复新日志确认的崩溃、卡死、存档损坏、重复扣款、持久化丢失和输入锁死。
3. 对修复项重新执行双版本构建、SdkSmoke、本地化、资产边界、XML、release check 和同步哈希检查。
4. 真机复验全部阻断项后，更新内测门槛状态。

### P1：核心机制下一阶段

1. **B1 双模式对话**：PLAN 已有审查基础；开始实现前重新核对锁定范围和用户的明确实施指令，状态 `decision_needed`。
2. **运行时相关性与记忆闭环**：世界书真实命中、TextMappings/persona、记忆摘要落盘和读档回读，优先用真机证据驱动修复。
3. **事件内容与世界效果**：在现有事件引擎、`awake.world.effect.record.v1` 和命令风险策略上增加真实内容批次；属于新内容/机制时重新走 PLAN 与审查。
4. **通讯录交互闭环**：在现有写信、承诺和给金币基础上评估回复延迟、未读和通知；不得绕过统一 transcript 和 interaction ledger。
5. **平台配置诊断**：如新日志仍显示路由或云外发异常，再检查 Marcus `platform.db` / Companion 配置，不凭旧日志推断。

### P2：体验与扩展

1. Messenger 群聊、媒体、TTS、群聊整理：必须新建 PLAN 并通过 `grill-me-codex`；Media 能力不可用时先定义降级路径。
2. 通讯录联系人列表头像、更多关系/身份信息和来信通知。
3. 开发者检查的测试触发、状态刷新、日志跳转和命令诊断增强。
4. 世界书检索调试、重载、校验和管理体验增强；编辑能力需明确内容同步与安全边界。
5. 对话等待动画、状态提示、未读计数和大地图通知。
6. 社区候选：信使距离/时间、善恶与身份差异、记忆整理、借贷、国家态度、阴谋/刺杀、区域文化提示词。

### 暂停或不迁移

- 女神人格和成人内容机制：独立内容包/插件路线，不并入 AWAKE 核心。
- 旧 AF / 爱与恨实现代码：只作设计参考，不建立运行时依赖。
- 未批准的内容注入、背景知识和内容包 RAG 扩展。
- 不具备框架能力或没有明确降级方案的媒体/TTS 实现。

## 4. 反馈状态归一

| ID | 内容 | 当前状态 | 后续动作 |
|---|---|---|---|
| `FB-20260817-9` | 读档无法进入大地图 | `done` | 已有真机日志证明关闭 |
| `FB-20260817-10` | 地图对话不写历史 | `fixed_pending_game` | 验收组 B |
| `FB-20260817-11` | 记忆日结云外发拒绝 | `fixed_pending_game` | 验收组 D |
| `FB-20260817-12` | `heroId` 双重引号 | `fixed_pending_game` | 验收组 D |
| `FB-20260816-1` | 场景选人和喊话不明显 | `fixed_pending_game` | 验收组 F |
| `FB-20260816-2` | 通讯录关系中心 | `fixed_pending_game` | 核心阶段已实现，验收组 B/E；高级来信另列 P1/P2 |
| `FB-20260817-1` | 地图对话入口割裂 | `fixed_pending_game` | 验收组 F |
| `FB-20260817-2` | C 键冲突 | `fixed_pending_game` | 验收组 F |
| `FB-20260817-3` | 主动对话纯概率 | `fixed_pending_game` | 验收组 F |
| `FB-20260817-4` | 通讯录头像与信息不足 | `queued` | 已交付人物卡/关系摘要，剩余列表增强列入 P2 |
| `FB-20260817-5` | 开发者检查缺少操作 | `fixed_pending_game` | 验收组 F；增强项列入 P2 |
| `FB-20260817-6` | 世界书管理不足 | `fixed_pending_game` | 验收组 F；编辑/调试增强列入 P2 |
| `FB-20260817-7` | 路由残留与 Provider 疑点 | `decision_needed` | 先用新日志确认，再决定是否查平台数据库 |
| `FB-20260817-8` | 缺 key、写丢与 final drain | `fixed_pending_game` | 验收组 D |

## 5. 已完成离线基线

### 运行时和存储

- Messenger 缓存重置、存储回读重试、安全写任务、周报触发日持久化。
- transcript/contact/audit schema、历史迁移、持久对话队列和统一会话 token 生命周期。
- 结构化记忆、衰减、承诺账本、互动 ledger、give_gold 可恢复结算。
- 重复 ID 拒绝、晚注册重载、内容注册预校验、条件解析 fail-closed、容量裁剪。

### AI、世界书和事件

- `AWAKE.route.*` 路由、Prompt/schema 注册、云端对话配置入口。
- 世界书加载、索引、查询、相关性过滤、TextMappings 回退、占位符审计和运行时注入。
- 数据驱动事件引擎、关系命令、世界效果记录命令和事件持久化/周报。
- 主动聊天动机注册与解释性触发基础。

### UI 和体验

- NPC 对话、通讯录、事件收件箱、周报、开发者检查和统一 Hub 生命周期。
- 通讯录人物卡、关系摘要、历史 Tab、写信入口。
- 多步首启引导、跳过与持久化。
- 场景选人循环、公开喊话、扇形/候选/目标高亮和状态提示。

### 最新离线验证

- Bannerlord API 1.3.15 / 1.4.8：0 警告、0 错误。
- SdkSmoke：PASS ALL。
- 本地化：`source=226 en=259 cn=259`。
- 资产边界：`ASSET_BOUNDARY_OK files=111`。
- Prefab 和语言 XML：可解析。
- 发布检查：`RELEASE_CHECK_OK`。
- 游戏目录无误同步 `src`。
- Marcus 确定性 extension validator：因本机缺少同时包含 `src/MarcusAIFramework/Api` 与 `sdk/manifest.json` 的框架源码根目录，未运行，不得宣称通过。

## 6. 开发与切换门禁

- 新功能、新机制、新内容批次：先 PLAN → `grill-me-codex` → 独立只读审查 → `VERDICT: APPROVED` → 用户明确签收 → 才能实现。
- 完成定义统一为：入口 → 调用方 → 结算/效果 → 可观察结果；只有类、路由、命令或 JSON 存在不算完成。
- 当前任务未完成时，新建议只入队，不自动切换；用户明确重定向才允许暂停当前任务。
- 游戏反馈先登记编号和证据，再判定 `blocking_current` / `non_blocking`。
- 不根据旧日志重复修复，不把取消事件描述为网络错误，不在客户端断开后自动续跑。
- 不启动 Bannerlord、不修改启动器、不终止游戏进程；游戏运行时不覆盖模块。
- 普通修复不提版本；提交、推送和正式发布必须由用户明确要求。

## 7. 后续候选

1. `VAL-20260817-1`：当前版本真机验收；当前锁定，等待新日志。
2. 新日志反馈修复：只在验收产生证据后开始。
3. B1 双模式对话：`decision_needed`，需明确实施指令。
4. Messenger 群聊/媒体/TTS：需新 PLAN、能力门禁和审查。
5. 正式 Level 0 内测发布整理：仅在真机验收通过后执行。

## 8. 最新检查点

- 2026-08-17 已重新整理任务队列：旧逐轮记录归档，当前文件成为唯一推进权威。
- `ROADMAP-20260817-1` 已完成：版本重心、功能包、内容包并行轨、延期规则和升版门槛均已写入并确认。`VAL-20260817-1` 当前为 `in_progress`。
- 最近可验证事实：2026-08-17 19:47:40 与 19:48:32 两次地图 NPC 对话均由 DeepSeek 成功完成，`effects` 可选修复已通过真机链路；19:48:50 记忆日结返回合法摘要，但因 `awake.npc.memory.summary.output.v1` 未注册而被框架以 `ai.output_schema_not_found` 拒绝。
- 路线图文档：`docs/AWAKE-Version-Roadmap-0.2.0-to-1.0.0-20260817.md` 已写入并确认，状态 `approved`。
- 下一动作：继续当前锁定的 `VAL-20260817-1`，四项离线修复已完成并同步；下一动作是用户运行当前游戏目录 DLL 生成新日志，确认记忆摘要持久化、写信成功、世界书 warnings=0，不启动 0.3.0。
## 新增游戏反馈（2026-08-17）

- `FB-20260817-13`：启动与读档验收通过，但地图 AI 对话在第 2 项开始出问题。
  - 来源：用户本轮真机测试。
  - 证据：`Awake.log` 19:03:50 提交成功；`companion.log` 19:03:55 返回 `ai.output_schema_invalid`，响应含合法 `reply`/`mood` 但省略可选 `effects`。
  - 关联任务：`VAL-20260817-1`，验收组 B / D。
  - 优先级：P0，当前验收阻断。
  - 状态：`fixed_pending_game`；分类：`blocking_current`。
  - 修复：`NpcPromptTemplate` 不再把 `effects` 列为必填；`NpcDialogueOutputValidator` 缺省时归一化为空数组。
  - 离线验证：1.3.15 构建 0 警告/0 错误；`Awake.SdkSmoke` PASS ALL，新增 optional effects 回归通过。
  - 下一动作：新 DLL 已同步；与本轮新日志一起复验地图回复、transcript 与记忆日结。

- `FB-20260817-14`：地图 AI 对话修复通过，但记忆日结输出契约未注册。
  - 来源：`Modules\AWAKE\Logs\Awake.log` 与 `Modules\MarcusAIFramework\log\companion.log` 最新一轮日志。
  - 证据：2026-08-17 19:47:40、19:48:32 两次 `AWAKE.route.npc.dialogue` 均 `Result: completed`；19:48:48 提交 `AWAKE.route.memory.daily`，19:48:50 返回合法 `{"summary":"..."}`，但框架报 `ai.output_schema_not_found`，契约为 `awake.npc.memory.summary.output.v1`。
  - 关联任务：`VAL-20260817-1`，验收组 B / D。
  - 优先级：P0；状态：`fixed_pending_game`；分类：`blocking_current`。
  - 初步定位：`NpcMemoryService` 提交时引用 `awake.npc.memory.summary.output.v1`，当前代码未发现对应 Prompt/输出 schema 注册路径；对话 schema 由 `NpcPromptTemplate` 注册，记忆 schema 没有等价入口。
  - 下一动作：新 DLL 已同步；用户重新进入游戏并结束一次对话，确认摘要写入且不再出现 `ai.output_schema_not_found`。

- `FB-20260817-15`：世界书启动持续报告两个警告。
  - 来源：`Awake.log` 的 `worldbook_runtime_initialized rules=336 personas=415 warnings=2`。
  - 证据：离线按当前 Loader 规则审计出两个重复 Rule ID：`rule_巴旦尼亚水之女神` 与 `rule_中原`，各出现两次；337 个规则文件只有 335 个唯一 ID。
  - 关联任务：`VAL-20260817-1`，验收组 A / D。
  - 优先级：P1；状态：`fixed_pending_game`；分类：`non_blocking`。
  - 下一动作：按用户定案删除两个泛化词条后，335 个规则文件已同步；用户启动新日志确认 `warnings=0`。
  - 最终处理：删除 `巴旦尼亚水之女神` 与 `中原` 泛化词条，只保留 `比安芙` 与 `洛泰——贾尔马律斯平原`，并保留重复 ID 内容门禁。

- `FB-20260817-16`：通讯录写信被拒绝，但日志无法说明拒绝原因。
  - 来源：`Awake.log` 2026-08-17 11:00:24 `letter_rejected key=hero:lord_1_18`。
  - 证据：有效 contact key 已记录；`AppendLetterAsync` 将 store 未就绪、空文本和空幂等键统一记录为同一事件，现有日志无法判断是用户空提交、存储未就绪还是调用参数缺失。
  - 关联任务：`VAL-20260817-1`，验收组 B。
  - 优先级：P1；状态：`fixed_pending_game`；分类：`non_blocking`，仍需用户实际发送一封信确认历史写入。
  - 下一动作：已增加存储就绪和拒绝原因处理；用户实际发送一封信确认 `source=letter` 历史写入。

- `FB-20260817-17`：Prompt 注册冲突和 Companion 启动重连仍产生噪音。
  - 来源：`Awake.log` 与 `MarcusAIFramework\log\framework.log`。
  - 证据：最新会话出现一次 `prompt.revision_conflict`，但随后两次 NPC 对话均完成；框架启动后约三秒内出现 pipe broken / EndOfStream 与 profiles list 请求失败，19:47:09 已重新连接，未阻断 19:47:40 之后的 AI 请求。
  - 关联任务：`VAL-20260817-1`，诊断质量。
  - 优先级：P2；状态：`fixed_pending_game`；分类：`non_blocking`。
  - 下一动作：新 DLL 已同步；用户新日志确认 `prompt.revision_conflict` 不再作为 AWAKE 失败。
  - 原离线动作：Prompt 冲突按“已存在同 revision”降级为幂等成功或明确日志；Companion 重连仅在影响任务提交时升级，当前不归类为连接故障。

## 9. 潜在问题反向审查（2026-08-17）

- `AUD-20260817-1`：存储就绪与写入结果存在“假成功”链路。
  - 优先级：P0；状态：`fixed_pending_game`；关联：`VAL-20260817-1` 写信与 transcript 验收。
  - 证据：`EnsureWorldStateReadyAsync` 在 12 个命名空间中任意一个打开成功即返回 `true`，没有确认写信必需的 `awake.transcripts` 与 `awake.contacts` 均已就绪；`AppendTranscriptLinesAsync`、`EnsureContactAsync` 在 drain 后不检查 `WorldDrainSummary`，即使最终写入失败或重试耗尽仍返回 `true`；`AppendLetterAsync` 也忽略联系人写入结果。
  - 风险：UI 可提示“发送成功”，但历史或联系人索引实际未持久化；部分命名空间故障时现有日志不足以将成功提示与落盘失败对应起来。
  - 建议：增加指定命名空间 readiness；所有面向调用方的 bool 写入方法依据 owner drain summary 返回真实结果；补 transcript/contacts 缺失、SetAsync 失败、部分成功三类回归测试。

- `AUD-20260817-2`：Prompt 注册失败会污染整个会话的注册状态，并存在注册未完成即放行的竞态。
  - 优先级：P1；状态：`fixed_pending_game`；关联：`FB-20260817-14`、`FB-20260817-17`。
  - 证据：`NpcMemoryService` 在 `RegisterAsync` 前写入 `_promptRegistrationAttempted=true`；`NpcDialogueService` 在 await 前写入静态 `PromptRegistrationAttempts` 与实例 `_promptRegistered`。取消、暂时断连、权限失败或框架未就绪后不会重试；并发初始化时后续调用可在首个注册仍未完成时直接继续提交任务。
  - 风险：一次暂时失败可使本进程后续记忆摘要持续得到 `ai.output_schema_not_found`，或在注册完成前产生偶发 schema 失败。
  - 建议：改为按 prompt key 的 single-flight 注册任务；只在成功或 `prompt.revision_conflict` 后缓存 usable；失败/取消移除状态并按 `FrameworkError.Retryable` 决定后续重试；注册不可用时不提交依赖该 schema 的 AI 任务。

- `AUD-20260817-3`：记忆摘要失败诊断被吞掉。
  - 优先级：P1；状态：`fixed_pending_game`；关联：记忆日结验收。
  - 证据：`NpcMemoryService.SummarizeAsync` 对 `Failed` / `Cancelled` 只返回空字符串，不记录 `AiTaskEvent.Error`；30 秒超时也静默返回空；SDK 的 `AiTaskEvent` 明确提供 `Error.Code`、`Category`、`Retryable`、`CorrelationId` 与 `Details`。
  - 风险：AWAKE 日志无法区分 schema、Provider、权限、取消和超时，必须跨查 Companion 日志，且重试策略无法基于错误类型判断。
  - 建议：记录结构化错误字段；取消与失败分开；超时记录 route/hero/conversation/correlation；不要记录敏感正文。

- `AUD-20260817-4`：存储就绪检查存在重复 I/O 与并发重复打开。
  - 优先级：P2；状态：`fixed_pending_game`。
  - 证据：每次 `EnsureWorldStateReadyAsync` 都重新执行权限 Ensure，并遍历打开全部 12 个命名空间；对话初始化、EnsureReady、命令结算、事件引擎和每封信都会调用，且没有 single-flight/readiness 快路径。
  - 风险：增加 Companion 往返、日志与初始化延迟；并发入口可能重复打开同一组 namespace。
  - 建议：缓存逐 namespace readiness，并用 single-flight 任务合并并发初始化；调用方只要求自身依赖的 namespace 集合。

- 已排除：写信调用不传 `displayName` 不会清空已有联系人名称；`ApplyContacts` 仅在非空名称时覆盖 `contactNames`。
- 内容一致性：root、dist、游戏目录、AWAKE-Repo 四处世界书均为 335 文件 / 335 唯一 ID / 0 重复，两个泛化词条已删除，两个指定词条均保留。
- 本轮门禁：1.3.15 与 1.4.8 构建均为 0 警告 / 0 错误；SdkSmoke `PASS ALL`；release check `RELEASE_CHECK_OK`；MAF lint 为 316 条既有 warning、0 blocking；世界书占位符审计仍为 18 条既有内容问题。
- 修复结果：`AUD-20260817-1` 至 `AUD-20260817-4` 已完成离线修复；1.3.15/1.4.8 构建与 SdkSmoke 通过，当前等待用户用新 DLL 复验写信、记忆日结和异常恢复日志。