# AWAKE 分割 Phase 0：标识清单与边界表

> 日期：2026-08-15
> 依据：`PLAN-AwakeSplit-20260815.md`（已 APPROVED）
> 状态：盘点完成；spike 项未开始

完整逐文件清点与去向表见 `AwakeSplit-ContentClassification-20260815.md`；后续切割必须按该表执行，不再随手改动。

## 基线证据

- 主工程构建：0 警告 0 错误（`SlaaneshsEmbrace -> _build_out/1.3.15/Release/SlaaneshsEmbrace.dll`）。
- SdkSmoke：`PASS ALL SlaaneshsEmbrace.SdkSmoke`（约 68 条断言路径）。
- 本地化：`LOCALIZATION_OK source=35 en=45 cn=45`。
- 代码规模：`src` 78 文件 / 约 15051 行；测试 2 文件 / 约 3921 行。

## 模块身份

| 项 | 当前值 | 归属 | AWAKE 目标（方向） |
| --- | --- | --- | --- |
| SubModule Name/Id | `SlaaneshsEmbrace` | 运行时 + 内容混用 | 运行时 `AWAKE`；内容包 `SlaaneshsEmbrace` |
| DLL | `SlaaneshsEmbrace.dll` | 同上 | `Awake.dll` |
| SubModuleClassType | `SlaaneshsEmbrace.SubModule` | 同上 | `Awake.SubModule` |
| AssemblyTitle/Product | `SlaaneshsEmbrace` | 同上 | `Awake` |
| AssemblyVersion | `0.2.0.0` | 两者 | 保持 0.2.0 |
| namespace | `SlaaneshsEmbrace` | 两者 | 运行时 `Awake`；内容包保留内容 namespace |

## 路由

| ID | 归属 |
| --- | --- |
| `SlaaneshsEmbrace.route.dialogue` | 内容（女神人格）→ `slaanesh.*` |
| `SlaaneshsEmbrace.route.npc.dialogue` | 运行时 → `awake.*` |
| `SlaaneshsEmbrace.route.preprocess` | 运行时 → `awake.*` |
| `SlaaneshsEmbrace.route.postprocess` | 运行时 → `awake.*` |
| `SlaaneshsEmbrace.route.memory.daily` | 运行时 → `awake.*` |

## 上下文 Provider

| ID | 归属 |
| --- | --- |
| `slaanesh.embrace.player.context` | 运行时 → `awake.*` |
| `slaanesh.embrace.hero.context` | 运行时 → `awake.*` |
| `slaanesh.embrace.relationship.context` | 机制运行时 + 轴语义内容；ID 收敛到 `awake.*`，旧 key 别名 |

## 命令

| ID | 归属 |
| --- | --- |
| `slaanesh.embrace.bless.notify.v1` | 内容 |
| `slaanesh.embrace.oracle.record.v1` | 内容 |
| `slaanesh.embrace.favor.state.v1` | 内容 |
| `slaanesh.embrace.relationship.delta.v1` | 机制运行时 + 轴语义内容 |
| `slaanesh.embrace.body.develop.v1` | 内容 |
| `slaanesh.embrace.estrus.tick.v1` | 内容 |
| `slaanesh.embrace.offering.accept.v1` | 内容 |
| `slaanesh.embrace.boon.grant.v1` | 内容 |

## 存储 namespace / schema

| namespace | schema/key | 归属 |
| --- | --- | --- |
| `slaanesh.embrace.dialogue` | `history.v1`、`slaanesh.embrace.message.v1` | 内容（女神历史） |
| `slaanesh.embrace.relationships` | `slaanesh.embrace.relationship.state.v1` | 内容（三轴语义） |
| `slaanesh.embrace.body_state` | `slaanesh.embrace.body.state.v1`、`estrus.state.v1` | 内容 |
| `slaanesh.embrace.favor_state` | `slaanesh.embrace.favor.state.v1` | 内容 |
| `slaanesh.embrace.npc.memories` | `slaanesh.embrace.npc.memory.v1` | **运行时机制** → `awake.*` |
| `slaanesh.embrace.event_meta` | `slaanesh.embrace.event_meta.v1`、key `campaign.event_meta.v1` | **运行时机制** → `awake.*` |

## 菜单

运行时目标菜单（当前混在 `GoddessMenuBehavior`）：

- `slaanesh_embrace_npc_talk_<town/castle/village/town_keep>` → 运行时 `awake_npc_talk_*`
- `slaanesh_embrace_ai_probe_*` → 运行时 `awake_ai_probe_*`
- `slaanesh_embrace_dev_check_*` → 运行时 `awake_dev_check_*`

内容菜单（迁入内容包）：

- `slaanesh_embrace_goddess_<menu>`（神谕入口）
- `slaanesh_embrace_altar_<menu>`（祭坛入口）
- `slaanesh_embrace_altar`（祭坛主菜单）
- `slaanesh_embrace_body_edit` 与 `slaanesh_altar_*`、`slaanesh_body_edit_*` 全部选项

## 本地化

- 源 key 35、EN 45、CN 45，前缀全部为 `slaanesh.embrace.*` 与 `SlaaneshsEmbrace.*`。
- 运行时通用 key（AI 链路状态、云外发、开发者菜单、NPC 深谈、事件引擎开关）收敛到 `awake.*`。
- 内容 key（女神、祭坛、愿力、身体、发情、俘虏、预设档位）保留 `slaanesh.*`。

## GUI 与 ModuleData

| 文件 | 归属 |
| --- | --- |
| `GUI/Prefabs/GoddessDialogue.xml` | 内容 |
| `GUI/Prefabs/NpcDialogue.xml` | 运行时 |
| `ModuleData/Knowledge/slaanesh_knowledge.json` | 内容（世界观语料） |
| `ModuleData/Languages/*` | 按 key 归属拆分 |

## 日志

- `SlaaneshsEmbrace.log` → 运行时 `Awake.log`。
- `SlaaneshsEmbraceProbe.log` → 运行时 `AwakeProbe.log`；内容包日志另立。

## MCM

- `SettingsId` / `FolderName` = `SlaaneshsEmbrace`，拆分时做 copy-on-first-run 迁移。
- 运行时开关（AI 链路、云外发、玩家状态外发、开发者菜单、事件引擎、NPC 主动机制）→ `Awake` 设置页。
- 内容开关（愿力提案、身体/发情、预设档位语义）→ 内容包设置页。

## 事件（硬编码规则，全部内容）

- `slaanesh.embrace.event.test.calm`
- `slaanesh.embrace.event.test.peak`
- `slaanesh.embrace.event.test.chain.start`
- `slaanesh.embrace.event.test.chain.next`
- `slaanesh.embrace.event.world.captive.plead`
- `slaanesh.embrace.event.world.army.trust`
- `slaanesh.embrace.event.world.camp.fire`
- `slaanesh.embrace.event.demo.dialogue`

## 运行时旧 key 迁移表

| 旧 key | 新 key | 策略 |
| --- | --- | --- |
| `slaanesh.embrace.npc.memories` | `awake.npc.memories` | 每英雄惰性迁移 + marker + 重启对账（若存储无枚举能力） |
| `slaanesh.embrace.event_meta` | `awake.event_meta` | 单 key 迁移状态机，保留 `appliedKeys`/`versions` |
| `SlaaneshsEmbrace.route.*` | `AWAKE.route.*` | 旧 ID 别名或游戏内迁移 |
| `slaanesh.embrace.player.context` 等运行时 Provider | `awake.*` | 旧 ID 别名或一次性重注册 |
| MCM `SlaaneshsEmbrace` 设置 | `Awake` 设置 | copy-on-first-run |
| Companion route/profile | `AWAKE.route.*` | 游戏内一键同步/迁移，旧配置 fixture 验收 |

## Phase 0 spike 待办

- [ ] 旧档 fixture：含 `slaanesh.embrace.*` 数据的真实/合成旧档。
- [ ] owner 锚点 spike：内容包继承旧 `SlaaneshsEmbrace` ModId vs 兼容 stub 模块，Phase 0 结束前锁定。
- [ ] 存储 spike：`IKeyValueStore` 是否提供 key 枚举与多 key 原子/事务能力。
- [ ] 旧 MCM JSON 与 Companion route/profile fixture。
- [ ] 双工程工具链：主工程、内容包工程、项目引用、内容包 SdkSmoke、双模块构建/同步/哈希/maf-lint。

## Spike 结果（静态盘点，2026-08-15）

- `IKeyValueStore` 公开接口只有 `GetAsync` / `SetAsync` / `DeleteAsync`（测试工程 FakeKeyValueStore 与 SdkSmoke 使用同一契约），**没有 key 枚举，也没有多 key 原子/事务方法**。
- Marcus 另有 `OpenSidecarAsync` / `IRawSqlSession`，示例扩展用它做 SQL 迁移；能否枚举 KV namespace 内部表、是否提供事务，需要真机/真实 Companion 验证，当前静态盘点不能下结论。
- 因此运行时旧 key 迁移默认采用“marker + 幂等复制 + 重启对账”，`npc.memories` 在无枚举能力时按英雄惰性迁移；若后续 spike 证明 raw SQL 可安全枚举/事务，再升级为批量迁移。
- owner 锚点（内容包继承旧 ModId vs 兼容 stub 模块）无法纯静态确定，需要真实 Bannerlord 存档与 Marcus 存储绑定实测，属于待用户提供旧档后推进的项。

## 存档与存储位置（实测，2026-08-15）

- Bannerlord 存档目录：`C:\Users\26811\OneDrive\文档\Mount and Blade II Bannerlord\Game Saves\`，共 100+ 个 `.sav`。
- 最新档：`saveauto1.sav`（2026-08-14 02:21）；`save054.sav`（2026-08-12 19:46）内含 `SlaaneshsEmbrace` v0.1.2.0 模块元数据，但**不含** `slaanesh.embrace.*` 业务数据。
- Marcus 持久化不在 `.sav` 内，而是独立数据库：
  - `%LOCALAPPDATA%\MarcusAIFramework\platform.db`：路由 Profile、模型/连接 Profile、Prompt 定义。已确认含旧 `SlaaneshsEmbrace.route.*` 与 `slaanesh.embrace.goddess.v1` 等记录，可作旧 MCM/route fixture。
  - `%LOCALAPPDATA%\MarcusAIFramework\campaigns\<campaignId>\<timelineId>\campaign.db`：`managed_kv` 表（`extension_id/namespace_id/key/value_json`）、`durable_events`、`timeline_state`。当前两个 campaign 库 `managed_kv` 均为 0 行。
  - `campaigns\smoke-campaign-20260812\...\campaign.db`：测试语料库，含 `slaanesh` RAG 数据。
- 结论：当前磁盘上没有“真实旧档 + 已持久化 `slaanesh.embrace.*` 业务数据”的完整 fixture；现有存档的持久数据为空，与早期 storage 权限未授予、写入降级为内存态的现象一致。

### 旧档 fixture 方案（待定，二选一）

- **A. 合成 fixture**：按 `managed_kv` 表结构向一个 campaign.db 插入 `extension_id=SlaaneshsEmbrace`、`namespace_id=slaanesh.embrace.*` 的样本数据（memory、event_meta、relationships），配合 `save054.sav`/`saveauto1.sav` 的模块元数据做迁移与 owner 锚点测试。
- **B. 真实 fixture**：用户进游戏确保 storage 权限授权成功、产生真实对话/记忆/事件后保存，再同时拷贝 `.sav` 与对应 campaign.db。

优先做 A（不依赖用户重新游戏），B 作为 Phase 3 真机验收补充。

## 为什么数据库是空的（根因，来自日志）

- 2026-08-12（v0.1.2）：反复出现 `goddess_storage_open_degraded code=storage.permission_denied`，女神存储 namespace 从未打开成功。
- 2026-08-14（v0.2.0）：`world_state_namespace_open_degraded namespace=slaanesh.embrace.relationships/body_state/npc.memories/favor_state code=storage.permission_denied`，四个世界状态 namespace 全部被拒；`storage.namespace.write` 与 `data.player_known.read` 多次 `decision=Denied`。这就是“allow 后反复弹窗”的实际现象：部分权限（route/cloud）能授予，但 storage 写权限没有真正落地。
- 2026-08-15：`framework.log` 大量 `Companion pipe is not connected (message=storage.kv.get.request)`，即使权限解决，Companion 管道也未连接，KV 请求到不了数据库。
- 同日 `companion.log`：AI 任务失败于 `ai.cloud_export_denied`、`ai.output_schema_not_found`，对话没有真正完成，记忆摘要没有生成。

结论：不是“存档里没存”，而是**游戏内持久化链路从未成功过一次**——存储权限被拒 → 降级内存；随后 Companion 管道未连接 → KV 传输失败；对话输出不合规 → 记忆不生成。所以磁盘上不存在含 `slaanesh.embrace.*` 业务数据的真实旧档。

这同时是一个 P0 前置问题：无论切割还是重做，运行时都必须先让“storage 权限授予 + Companion 管道连接 + 对话完成”在真实游戏中成立，否则 Phase 3 的迁移验收没有真实输入，运行时记忆功能本身也不可用。

### 权限循环的代码级原因（补充）

- 马库斯框架规则：manifest 声明权限 ≠ 已授权；先 `Evaluate`，只有玩家主动操作的“安全 UI 时机”才 `RequestAsync`；授权按战役保存并可撤销。
- 当前实现：`GoddessMenuBehavior.AltarConsequence` 用 `_ = PrepareAltarAsync(host)` 异步准备，`PrepareAltarAsync` 内部 `ConfigureAwait(false)` 后调用 `EnsureWorldStateReadyAsync`，后者再 `EnsureAsync(storage.namespace.write)`。
- 后果：权限请求发生在线程池续体上，不在游戏主线程的安全 UI 时机；框架授权弹窗无法可靠完成，点击允许后授权不落盘/不生效，下一次打开菜单再次请求，形成“allow 后反复弹窗”。
- 日志佐证：`permission_gate_request permission=ai.route.invoke... granted=true` 能成功，而 `storage.namespace.write` 持续 `Denied`；不同调用点的线程时机不同，route 权限恰好成功、storage 权限未成功。

修复方向（Phase 0/1 P0）：权限请求必须 marshal 回游戏主线程，并在玩家手势直接触发的同步入口完成；先确认 Companion 管道已连接再请求；授权成功后再批量打开 namespace。已同步的 `SlaaneshUiDispatcher` 可复用做主线程 marshal。

### 权限修复进度（2026-08-15）

- 已实现：`SlaaneshUiDispatcher` 增加游戏线程识别（`InitializeGameThread`）与 `RunOnGameThreadAsync`；`SubModule.OnApplicationTick` 先初始化再 drain；`PermissionGate.RequestCoreAsync` 的全部 `Permissions.RequestAsync` 改走主线程 marshal。
- 已实现：SdkSmoke 新增 `ui dispatcher main thread smoke`，验证“非游戏线程发起 → 游戏线程 drain 执行 → 结果回传”，`PASS ALL`。
- 未完成：Companion 管道连接检查与“连接成功前不请求”的守卫，需要真机验证框架 API 与授权落盘；对话输出 schema 问题（`ai.output_schema_not_found`）属于独立问题，不在本修复内。
