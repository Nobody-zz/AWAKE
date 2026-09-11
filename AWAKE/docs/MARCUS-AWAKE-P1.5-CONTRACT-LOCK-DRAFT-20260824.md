# Marcus-Awake P1.5 契约锁定草案

- 日期：2026-08-24
- 状态：`P1.5_CONTRACT_LOCKED`
- 目的：把全功能继承方案落成可实现的程序集、API、IPC、Service、Storage、版本和失败契约。
- 本文不是跨阶段代码授权；P1.5 已完成独立审查和用户签收，后续每个阶段仍必须按本文件规定建立自己的精确写集、独立审查和证据门。

外部只读审查于 2026-08-24 多轮返回 `VERDICT: REVISE`；最终复审已确认修订闭合并返回 `VERDICT: APPROVED`，用户签收记录见 `checkpoints/MARCUS-AWAKE-FULL-CAPABILITY-20260824-checkpoint.md`。以下内容是进入代码迁移前的强制契约，不是可选优化；P1/P2 仍须分别通过自己的阶段审查，不能把本文件的批准当作后续写集的自动授权。

本轮把剩余问题收口为可执行的 ownership、fixture、阶段和权限契约。`contract_locked` 只表示字段与边界已锁定，不表示代码、构建、同步或游戏内验证已经完成。

## 1. 已确认事实

| 项目 | 当前事实 | 迁移含义 |
| --- | --- | --- |
| AWAKE 编译引用 | `AWAKE.csproj` 引用 `MarcusAIFramework_Reference\SDK_20260815\ref\v$(BannerlordApi)\MarcusAIFramework.dll` | 不能机械替换 using；必须建立 API surface 映射和契约测试。 |
| AWAKE 模组依赖 | `SubModule.xml` 声明外部 `MarcusAIFramework` | 内置后移除外部依赖，不创建伪 SubModule。 |
| 当前公共使用 | `IMarcusAiFrameworkHost`、`FrameworkHostLocator`、`RequestContext`、`PermissionGate`、`FrameworkErrors`、`host.Storage`、`IAiGateway` 相关调用 | 迁移必须逐调用点核对入口、结果和失败语义。 |
| Native 存档 | 当前已验证 `awake_last_weekly_report_day`、`awake_worldbook_overlay_v1`、`awake_worldbook_activation_v1` 三个 key；内置迁移新增 `awake_framework_anchor_v1` | 现有三个 key 不改名；第四个 anchor 只在迁移写集落地后启用，读档和新建存档都要验证。 |
| Framework Storage | 12 个 AWAKE 逻辑 namespace，已有 `awake.*.v1` schema | 逻辑 owner/key/schema 保留；物理根独立迁移。 |
| 世界知识 | 旧通用 RAG `awake.knowledge` 与 Worldbook v2 档案路径并存 | 新 Framework 只提供检索/存储基础设施，AWAKE 保留世界书语义和权限。 |
| 旧进程 | 外部 `MarcusAIFramework.Companion.exe` 与旧 `%LOCALAPPDATA%\MarcusAIFramework` | 不共享 Provider、IPC、DB 或凭据；冲突必须可见并 fail closed。 |

## 1.1 责任边界总表

| 层 | 唯一拥有 | 明确不拥有 |
| --- | --- | --- |
| Framework Core | 游戏侧公共 API、Host、SessionLease、PermissionGate、Context/Command、Save anchor、IPC Client 和 typed result | Provider HTTP、SQLite/FTS5、Embedding/Rerank 实现、凭据明文和 Service 物理数据库 |
| Runtime Service | Provider、Gateway、Prompt Registry 运行时、SQLite/FTS5、Embedding/Rerank、Timeline、CredentialBroker、ExportPolicyEvaluator、EgressBroker、IPC Server | Bannerlord 实时对象、原版事实、玩家主配置 UI和 AWAKE 关系/知识权限语义 |
| AWAKE | Bannerlord 事实适配、世界书语义、NPC 知识权限、关系/事件/周报、MCM 玩家入口和游戏侧结算 | Framework 公共权限第二实现、Provider 凭据、RAG 物理数据库和任意网络出口 |
| DevTools / SDK | 作者维护、FakeHost、Analyzer、脱敏诊断和开发资源 | 玩家运行时依赖、游戏事实权威、明文 Key 和外发绕过路径 |

当前源码与目标契约的差异是迁移门，不是已完成实现：`AWAKE.csproj`/`SubModule.xml` 仍引用旧 Marcus；当前只有三个 Native key；`ProbeExtension` 仍有生命周期阻塞等待；本地 `PermissionGate`/`PermissionCatalog` 和 F-063–F-066 路由/命令仍存在。除文档审查外，不得把这些旧入口计为新契约已接线。

## 2. 稳定身份与程序集加载

### 2.1 玩家发布包

```text
Modules/AWAKE/
├─ SubModule.xml
├─ bin/Win64_Shipping_Client/
│  ├─ Awake.dll
│  └─ MarcusAwakeFramework.dll
└─ Runtime/
   └─ Marcus-Awake AI Runtime Service 及其受控依赖
```

约束：

- `MarcusAwakeFramework.dll` 是 AWAKE 包内程序集，不是额外 Bannerlord 模组，不新增 `<DependedModule>`。
- AWAKE 只拥有一个 `SubModule.xml`、一个游戏入口和一个 Host 注册权威。
- 发布包不携带旧 `MarcusAIFramework.dll`、旧 Companion、SDK reference、TaleWorlds DLL 或测试 fixture。
- 旧 `MarcusAIFramework` 同时启用属于不支持组合；AWAKE 必须检测、提示并在有风险时关闭 AI 能力，不随机选择服务。

### 2.2 身份迁移

| 旧身份 | 新身份 | 规则 |
| --- | --- | --- |
| `MarcusAIFramework` 程序集 | `MarcusAwakeFramework` | 不提供旧程序集运行时兼容。 |
| `MarcusAIFramework.Api` | `MarcusAwakeFramework.Api` | 新公共 API 的唯一运行时命名空间。 |
| `MarcusAIFramework.Companion` | `Marcus-Awake AI Runtime Service` | 新 Service、pipe、日志和物理存储根独立。 |
| `%LOCALAPPDATA%\MarcusAIFramework` | `%LOCALAPPDATA%\MarcusAwakeFramework` | 不自动复用旧数据库、凭据、RAG 或资产。 |
| 旧 `MarcusAIFramework.Api` owner/route | AWAKE owner/route | AWAKE 逻辑 namespace、key、schema 保持语义；公共身份重新声明。 |

## 3. 公共 API 迁移映射

以下是迁移入口，不是允许直接批量替换的文本表。每行都必须有编译证据、行为测试和失败对照。

| 现有调用/能力 | Marcus-Awake 公共面 | 必须保留的语义 |
| --- | --- | --- |
| `FrameworkHostLocator.Register(...)` | `MarcusAwakeFrameworkHostLocator.Register(...)` | 轻量注册；不访问 `Campaign.Current`、不做 I/O、不启动 AI。 |
| `IMarcusAiFrameworkHost` | `IMarcusAwakeFrameworkHost` | 当前 session、能力发现、Storage、Gateway、生命周期和诊断状态。 |
| `RequestContext` | `MarcusAwakeFramework.Api.RequestContext` | correlation、campaign/timeline/session、deadline、取消和 owner。 |
| `PermissionGate` | `MarcusAwakeFramework.Api.PermissionGate` | manifest 不等于授权；调用点再次 Evaluate；未知权限 fail closed。 |
| `FrameworkErrors` / `FrameworkError` | `MarcusAwakeFramework.Api.FrameworkErrors` / `FrameworkError` | code/category/owner/correlation 稳定；展示文本独立本地化。 |
| `IAiGateway` | `MarcusAwakeFramework.Api.IAiGateway` | 逻辑 Route、结构化结果、流式事件、取消和 typed degraded。 |
| `host.Storage.OpenCampaignNamespaceAsync` | `host.Storage.OpenCampaignNamespaceAsync` | namespace、campaign/timeline 绑定、版本、配额、deadline 和异步写入。 |
| Context Provider | `ContextContribution` / `ContextPlanner` 公共契约 | 来源、scope、TTL、排除原因、token/字节预算和不可外发分类。 |
| Command Preflight/Receipt | `CommandDescriptor`、`CommandRequest`、`Preflight`、`Receipt` | R0–R3、快照复核、幂等、过期、撤销和可观察结算。 |

不允许公共 API 暴露：TaleWorlds 实时对象、HTTP client、SQLite connection、Named Pipe handle、Provider DTO、API Key、真实文件路径或内部 Prompt 原文。

## 4. 版本轴

以下版本独立协商，不能用一个总版本号代替：

```text
AWAKE ModVersion
Marcus-Awake FrameworkApiVersion
AI Runtime ServiceProtocolVersion
AI Runtime ServiceImplementationVersion
ProviderProfileSchemaVersion
StorageSchemaVersion
WorldbookSchemaVersion
SDKVersion
BannerlordApiVersion
```

最低规则：

- Framework API major 不兼容时拒绝注册，不做隐式反射兼容。
- IPC Protocol major 不匹配时 Service 进入 `protocol_incompatible`，不接收任务。
- Schema 只允许显式 migration；未知 major 不覆盖原数据。
- Provider 能力报告必须带版本和能力声明；能力不等价时不能静默 fallback。
- `campaign_guid`、`timeline_id`、`session_id` 必须同时参与请求、存储、日志和晚到结果判定；`campaign_id` 只允许作为旧盘点资料中的迁移别名，不进入新协议。

## 5. IPC Handshake 与 Envelope

### 5.1 握手最小字段

```text
protocol_id
protocol_major
protocol_minor
client_instance_id
service_instance_id
framework_api_version
bannerlord_api_version
requested_capabilities
session_nonce
user_sid
parent_pid
client_bootstrap_proof
service_bootstrap_proof
challenge_id
challenge_response
auth_key_id
```

握手成功的条件：协议主版本兼容、服务身份匹配、session nonce 新鲜、能力交集可计算、服务未被标记为冲突或停止。

额外安全条件：

- Runtime Service 固定以启动 AWAKE/Bannerlord 进程的同一交互式 Windows 用户 SID 运行，不使用独立服务账户；Named Pipe 的 canonical DACL 只允许该用户 SID 与 `SYSTEM`，拒绝 `Everyone`、其他用户、远程客户端和任意同名 pipe。
- 客户端和 Service 在握手中互相确认实例身份、用户 SID、父进程关系和一次性 session nonce；不能只依赖 pipe 名称。
- Service 对客户端连接使用 impersonation/ACL 校验；客户端对 Service 校验服务实例标识和启动凭据，不把 `checksum` 当作认证。
- 每个会话建立发送/接收方向的 nonce 与单调 sequence；旧 session、旧 nonce 和无法证明为同一帧的重复 sequence 进入 `replay_rejected`；安全重传例外按 §5.1.2 的判定优先级处理。

#### 5.1.2 Sequence 窗口算法

- Client→Service 与 Service→Client 使用独立的 `direction_nonce`、连接 epoch、连续接收水位和有界 in-flight window；序列号属于方向和 session，不属于单个 request。
- 每一帧使用 `sequence` 单调递增；接收端只接受连续水位之后的序号，窗口内乱序进入有界暂存，不得无限缓存。相同 `sequence + message_id + payload_hash` 的安全重传返回原 ACK；相同 sequence 但 message、payload 或 hash 任一不同返回 `replay_rejected`，不能把冲突帧当作普通重复。
- 同一 request 的并发响应使用 `event_index` 去重和排序，但仍占用该方向的全局 sequence；不允许通过 request 私有序列绕过方向序列。
- Sequence 判定优先级固定为：先校验 session/connection epoch/direction nonce；再检查是否为当前连续水位或窗口内乱序；最后对已记录 sequence 比较 `message_id + payload_hash`。只有同一 session、同一 connection epoch、同一方向 nonce、同一 sequence 且 message/payload 完全一致时，才走安全重传并返回原 ACK；其余重复或冲突一律 `replay_rejected`。
- 断线重连建立新的 connection epoch、方向 nonce 和序列起点；旧 session、旧 epoch、旧 nonce 的所有帧均拒绝。请求状态只能通过原 `message_id`/`idempotency_key` 查询或重发，不能复制新任务。
- 跳号、窗口内乱序、旧窗口、冲突复用和 nonce 不匹配分别返回 `sequence_gap`、`sequence_out_of_order`、`replay_rejected` 或 `nonce_mismatch`；安全重传返回原 ACK，不静默修正冲突帧。
- 接收状态按 `session_id + connection_epoch + direction_nonce` 持久化连续水位；Service 重启后只能恢复 `durably_recorded` 的水位，无法证明的帧进入 `recovery_required`。
- 接收端只保留有界的当前窗口；窗口满载、跳号超出上限和并发接收冲突进入 `ipc_backpressure`，不无限缓存。

#### 5.1.1 双向认证流程

1. AWAKE Bootstrap 创建一次性 `challenge_id` 和随机 challenge，写入当前启动事务，不写入普通日志。
2. Bootstrap 启动 Service 时通过受控父子进程启动通道传递 `client_bootstrap_proof`；Service 返回 `service_bootstrap_proof` 和 `service_instance_id`。
3. 双方使用本机受保护的 `auth_key_id` 对 `challenge_id + session_nonce + user_sid + parent_pid + instance_id` 做带域分隔的 HMAC challenge-response；Key 正文不得进入 envelope、日志或导出。
4. Service 通过 Pipe impersonation 取得实际连接用户 SID，并要求它与 `user_sid`、Bootstrap 事务 SID 和 ACL 一致；不一致返回 `ipc_identity_mismatch`。
5. Service 校验 `parent_pid` 仍属于同一 AWAKE Bootstrap 启动事务；父进程不存在、PID 重用或 proof 过期返回 `ipc_parent_invalid`。
6. 客户端只接受与当前启动事务绑定、协议/版本匹配且 challenge 未使用过的 Service proof；失败返回 `ipc_auth_failed`，不进入可重试任务状态。

`auth_key_id` 只标识受保护密钥，不允许调用方自行提供密钥字节。密钥轮换时旧 key 只能在明确的迁移窗口内验证，不能扩大访问权限。

#### 5.1.3 Bootstrap 信任根与 Pipe ACL

- AWAKE Bootstrap 是 `MarcusAwakeFramework.dll` 内唯一允许启动 Runtime Service 的代码路径；它在内存中生成本次启动专用的 256-bit `bootstrap_secret`、`launch_nonce` 和 `launch_transaction_id`，不写入命令行、普通日志、数据库、存档或导出包。
- Bootstrap 通过一次性继承句柄的私有父子启动通道把 `bootstrap_secret` 交给 Service；Service 只能在启动事务内读取一次，读取后立即关闭句柄并清除临时缓冲。`auth_key_id` 仅标识该启动秘密的域，不是长期 Provider 凭据。
- Service 启动证明必须绑定 `launch_transaction_id`、`parent_pid`、父进程创建时间、Service PID/创建时间、`user_sid`、实现版本和 Service 可执行文件 SHA-256；客户端拒绝缺失任一绑定字段的 proof。PID 数字相同但创建时间不同视为 PID 重用，返回 `ipc_parent_invalid`。
- 双向 challenge-response 使用域分隔 HMAC：`client-auth-v1` 与 `service-auth-v1` 分开计算，transcript 包含协议版本、实例 ID、launch nonce、session nonce、用户 SID、父进程证明和 pipe identity；challenge 只能消费一次。
- Named Pipe 使用动态 owner SID 的 canonical DACL：仅目标用户 SID 和 `SYSTEM` 具有完整控制权，拒绝 `Everyone`、远程客户端和其他用户；创建时启用当前用户限制、异步 I/O 和单实例 pipe identity，旧 Marcus pipe 名称不接受连接。ACL 校验先于协议解析，proof 校验随后进行，二者都通过后才创建 `connection_epoch`。
- Service 必须通过 Pipe impersonation 校验连接用户 SID，并同时校验 Bootstrap proof、实例 epoch 和 ACL；任一不匹配立即关闭连接并记录脱敏诊断。
- 实例锁、Bootstrap proof、PID 创建时间、父进程链、旧 challenge 重放、同 SID 伪造客户端、跨用户连接和旧 pipe 连接都必须有确定性 fake/Windows fixture。

#### 5.1.4 Epoch/Fence Authority

| 字段 | 唯一签发者 | 持久化/生命周期 | 规则 |
| --- | --- | --- | --- |
| `instance_epoch` | AWAKE Bootstrap 持有 instance lock 时签发 | `%LOCALAPPDATA%\MarcusAwakeFramework\runtime\epoch.state`；单调递增、永不回退或复用 | 每次接受新的 Service 启动事务先原子递增，再创建新的 instance lock；PID 不能代替 epoch |
| `fence_token` | 同一 Bootstrap 启动事务 | 原始值仅存在内存和一次性父子启动通道；不写盘、不进 envelope。instance lock 只保存 `fence_token_hash` | 旧 epoch 被替换或父进程失效时立即撤销；所有内部写调用必须带当前 `fence_proof` |
| `connection_epoch` | 当前已认证 Service 为每条 Pipe 连接签发 | 仅当前连接；断线、重连、Service 重启立即失效 | 不能跨连接复用；与方向 nonce、sequence 和 session 绑定 |
| `sequence` | 发送方向的当前连接 | 当前连接的帧序列；durable watermark 可写入 receipt/恢复账本 | 只按 `session_id + connection_epoch + direction_nonce` 验证；不能按 request 私有序列替代 |

- `FenceAuthority` 是 AWAKE Bootstrap/Framework Runtime 的唯一 epoch/fence 权威；Service、Provider Adapter、Storage Writer、Command Adapter 和 DevTools 不得自行签发、递增或延长 epoch/fence。
- 验证顺序固定为：进程/ACL 身份 → `instance_epoch` 当前性 → `fence_proof` 与 `fence_token_hash` → `session_generation` → `connection_epoch`/sequence → payload hash → 权限和业务条件。
- `fence_proof` 是唯一跨边界表示：`HMAC-SHA256(fence_token, domain + instance_epoch + connection_epoch + direction_nonce + sequence + message_id + payload_hash)`；原始 `fence_token` 不进入日志、数据库、envelope 或普通 API。
- 当前 fence 的权威状态同时记录为 instance lock 中的 `fence_token_hash` 和受保护 runtime fence record；所有 Storage 写入、Provider 任务、Credential lease、Egress decision、Command Execute 和 `SettlementReceipt` 都必须验证当前 epoch/fence，失效统一返回 `instance_fenced`。
- 新 Bootstrap 取得更高 epoch 后，旧 Service 即使 PID、pipe 或 session 仍存在，也只能进入 `fenced/draining`，不得继续写 `platform.db`、`campaign.db`、receipt、Provider 连接或游戏状态。

`auth_key_id` 的 HMAC 密钥派生固定为：

```text
auth_hmac_key = HKDF-SHA256(
  ikm = bootstrap_secret,
  salt = launch_nonce,
  info = auth_domain + "|" + launch_transaction_id + "|" + auth_key_id)
```

`auth_domain` 只能是 `client-auth-v1` 或 `service-auth-v1`；Bootstrap 负责生成并清除 `bootstrap_secret`，双方只在本次启动事务使用派生结果。`auth_key_id` 仍只是受保护启动秘密的标签，不是 Provider 凭据，也不允许调用方提供密钥字节。

### 5.2 消息 envelope

```text
message_type
message_id
correlation_id
campaign_guid?
timeline_id?
session_id
owner_id
deadline_utc
instance_epoch
connection_epoch
direction_nonce
sequence
fence_proof?
payload_schema
payload
checksum
```

约束：

- 所有请求、流事件、取消、完成、错误和 ACK 都使用同一 envelope 语义；`fence_proof` 对所有写请求、Provider 任务、凭据 lease、外发 decision、命令和 receipt 必填，纯只读健康查询可以省略。
- ACK 只表示 Service 已接收或持久化，不表示 Provider 成功或游戏侧已结算。
- 断线后旧 session 的晚到结果只能进入诊断，不能更新当前 UI、NPC、存档或世界状态。
- Pipe 读写、序列化、校验和和重连都不能阻塞游戏主线程。

### 5.3 幂等、重放与 ACK

- 每个可产生效果的请求必须带稳定的 `IdempotencyScope`：`owner_id + campaign_guid + timeline_id + command_id + command_schema_version + idempotency_key + payload_hash`。同一 scope 只允许一个最终结算；同一 key 配不同 payload、command 或 schema 必须返回 `idempotency_conflict`，不能选择其中一个静默执行。
- Service 必须先记录接收状态，再执行可重试工作；ACK 只允许 `accepted`、`durably_recorded` 两种状态，`completed`、`rejected`、`cancelled`、`expired` 和 `recovery_required` 只能由最终 Receipt 表示。
- 客户端超时重试时，Service 返回原始 receipt 或 `in_progress`，不能重新执行同一命令。
- 流式事件按 session/sequence 去重；重复完成、取消后完成和过期完成必须是可测试的终态冲突。
- receipt、dedupe window、重放拒绝和 ACK 超时都必须进入脱敏审计。

### 5.4 ACK / 幂等状态机

```text
received
  → accepted
  → durably_recorded
  → in_progress
  → completed | rejected | cancelled | expired | recovery_required
```

- `idempotency_key` 由请求发起方生成；Framework 对命令请求负责校验格式和作用域，Service 不自行替换。`campaign_guid` 是存档血统的唯一字段名，禁止在新协议中再使用 `campaign_id`。
- 产生世界/关系/经济/知识效果的请求，其去重记录绑定完整 `IdempotencyScope`，保留到 receipt 归档且对应 Save anchor 已确认后才能清理。
- 纯流式文本的去重记录绑定 `session_id + message_id`，只在会话 TTL 内保留；媒体任务绑定 `task_id`，直到完成、拒绝或清理策略允许删除。
- Service 重启时，`durably_recorded` 继续返回原记录；`in_progress` 必须根据 executor receipt 恢复、完成或转为 `recovery_required`，不能无条件重跑。
- ACK 超时只允许查询原状态或重发同一 `idempotency_key`；不允许创建新的效果请求替代原请求。
- dedupe window、状态迁移和重启恢复都必须有 fixture，重复完成、取消后完成和过期完成必须得到确定结果。

游戏效果命令的 `CommandLedgerEntry` 必须在第一次执行前 durable 创建，并由同一 `ledger_entry_id` 原地更新：

```text
prepared → applied
prepared → rejected
prepared → unknown
```

- `prepared` 表示幂等 scope 已记录、效果尚未得到最终代码结算；`rejected` 表示 Preflight 或 Execute 明确拒绝且未改变游戏状态；`applied` 表示 Execute 已完成并记录效果摘要 hash。
- 进程在 Execute 后、ledger 更新前崩溃时，entry 保持 `prepared`，恢复扫描将其确定为 `unknown` 并发布 `recovery_required`；不得根据“可能已执行”自动重放或自动补发。
- `SettlementReceipt.completed` 只能引用 `applied` entry；`prepared`/`unknown`/`rejected` 分别只能产生恢复、待人工核对或明确未结算结果。所有状态转换都绑定同一 `IdempotencyScope`、snapshot token、epoch/fence 和 effect hash。

#### 5.4.1 ACK 与 Receipt 的唯一分工

| 类型 | 允许状态 | 含义 | 是否代表效果已结算 |
| --- | --- | --- | --- |
| `ACK` | `accepted` / `durably_recorded` | 传输层与持久接收确认 | 否 |
| `ProgressEvent` | `in_progress` / `cancel_requested` | 当前任务进度或取消意图 | 否 |
| `Receipt` | `completed` / `rejected` / `cancelled` / `expired` / `recovery_required` | 业务任务唯一终态 | 按 `settlement_requirement` 判定，不能单凭 `completed` 推断游戏效果 |

- 每个 Route/请求契约必须声明 `settlement_requirement = required | not_applicable`。产生游戏内效果、命令或存档状态变化的任务必须为 `required`；只读查询、文本生成、配置校验、能力 Probe 和其他不产生游戏效果的任务必须为 `not_applicable`。
- `ACK` 不承载 `completed` 等业务终态；业务终态只由一个最终 `Receipt` 发布。`Receipt.completed` 在 `settlement_requirement=required` 时必须绑定已验证的 `SettlementReceipt`；在 `not_applicable` 时可由 Service 在自身结果 durable 后发布，且 `settlement_id` 必须为空并明确不声明游戏效果已结算。
- 终态优先级固定为：`completed`、`rejected`、`cancelled`、`expired`、`recovery_required` 中只能出现一个；先持久化的终态获胜，后到终态转为 `duplicate_terminal_event` 并只记审计。
- `cancel_requested` 不是终态；若任务已完成则返回原 `completed` receipt，若未完成且取消成功才发布 `cancelled`。
- `settlement_requirement=not_applicable` 的任务不得伪造 `SettlementReceipt`；`settlement_requirement=required` 的任务如果没有匹配 `SettlementReceipt`，只能返回 `recovery_required` 或稳定业务拒绝，不能发布表示完成的 Receipt。
- `recovery_required` 表示 Service 重启后无法证明 executor 是否执行，禁止自动重跑；必须由恢复流程以同一 receipt 处理。
- 查询同一 `idempotency_key` 永远返回当前唯一 ACK/Progress/Receipt 状态，不生成第二任务。

#### 5.4.2 游戏效果结算边界

- 对 `settlement_requirement=required` 的 Service 请求，`Receipt.completed` 只能在收到 AWAKE 游戏侧 `SettlementReceipt` 后发布；Provider 完成、文本生成完成或命令已排队都不能直接代表游戏效果已结算。对 `not_applicable` 的只读/非效果请求，Service 可在结果 durable 后发布 `Receipt.completed`，但必须携带 `settlement_requirement=not_applicable` 且不得声称游戏状态发生变化。
- `SettlementReceipt` 的唯一权威者是 AWAKE 的 Command Adapter/游戏主线程；它必须绑定完整 `IdempotencyScope`、当前 `session_id`、snapshot token、`instance_epoch` 和效果摘要 hash。
- 结算顺序固定为：`durably_recorded`（创建 `CommandLedgerEntry=prepared`） → 主线程 Preflight → 主线程 Execute → 更新同一 ledger entry 为 `applied` 或 `rejected` → 返回 `SettlementReceipt` → Service 发布业务 `Receipt`。`durably_recorded` 之前禁止执行效果。
- 游戏内效果与外部 Service 数据库不能依赖跨进程假原子提交。AWAKE 必须在 Save barrier 生成 `committed_anchor_sequence`；若游戏在 Execute 后、Save anchor 提交前崩溃，下一次读档把未被 anchor 覆盖的 receipt 标记为 `recovery_required`，禁止自动重放或补发效果。
- `SettlementReceipt`、Save anchor 和 Service receipt 的恢复关系必须能区分“效果已执行但 receipt 丢失”“receipt 已记录但效果未执行”“两者均已提交”；每种情况都返回稳定诊断，不用文本猜测。

#### 5.4.3 Receipt 权威与恢复矩阵

| 类型 | 唯一发布者 | 唯一 ID/关联键 | 表示什么 | 查询权威 |
| --- | --- | --- | --- | --- |
| `CommandLedgerEntry` | AWAKE Command Adapter | `ledger_entry_id` + 完整 `IdempotencyScope` | 主线程 Execute 前后对游戏效果的准备、应用或拒绝记录 | `campaign.db` command ledger |
| `SettlementReceipt` | AWAKE Command Adapter | `settlement_id`，引用 `ledger_entry_id`、snapshot、epoch/fence、`ledger_sequence` 和 effect hash | 游戏状态已由代码结算，或明确未结算 | AWAKE 游戏侧 receipt/ledger |
| `ServiceReceipt` | Runtime Service Receipt Store | `receipt_id`，引用 `message_id`、`IdempotencyScope`、`settlement_requirement` 和 `settlement_id?` | Service 请求生命周期终态，不自行证明游戏效果 | `platform.db`/Service receipt store |
| `SaveAnchor` | AWAKE Save Coordinator | `anchor_hash` + `committed_anchor_sequence` | 某个 Bannerlord 存档中的外部状态恢复边界 | Native `awake_framework_anchor_v1` |

- 对 `settlement_requirement=required` 的任务，恢复优先级固定为：当前 Native Save anchor 确定存档边界 → 游戏侧 CommandLedger/SettlementReceipt 确定效果是否已结算 → ServiceReceipt 仅补齐请求生命周期；ServiceReceipt 不能覆盖缺失或冲突的游戏侧结算证据。
- 对 `settlement_requirement=not_applicable` 的只读/非效果任务，不要求 CommandLedger 或 SettlementReceipt；恢复权威是 ServiceReceipt 与 Service 结果的 durable 记录，必须验证结果存在、hash 和 scope 一致，但不得把该 Receipt 解释为游戏效果已结算。
- `ledger_sequence` 是 `campaign.db` command ledger 的唯一单调提交序号，由 AWAKE Command Ledger 在主线程 Execute 前成功写入 durable ledger 时签发；同一 `campaign_guid + timeline_id` 只递增、不回退、不跨 timeline 复用。
- `settlement_sequence` 是 `SettlementReceipt` 的字段别名，必须等于对应 `ledger_sequence`，不允许另行生成或重新排序；`committed_anchor_sequence` 是 SaveCoordinator 捕获的该 timeline 已 durable `ledger_sequence` 水位。
- `SettlementReceipt.completed` 且其 `ledger_sequence <= committed_anchor_sequence`：效果属于当前存档，可重建缺失的 ServiceReceipt。
- `SettlementReceipt.completed` 但其 `ledger_sequence > committed_anchor_sequence`：效果只属于未提交的旧运行会话，读档时不得自动重放；标记 `settlement_after_anchor`/`recovery_required`。
- `settlement_requirement=required` 且 `ServiceReceipt.completed` 但不存在匹配 `SettlementReceipt`：标记 `receipt_without_settlement`，禁止补发效果；需要同一 scope 的人工恢复或显式新命令。
- `settlement_requirement=not_applicable` 且 `ServiceReceipt.completed`、`settlement_id` 为空并且 Service 结果 durable：这是正常的非效果完成，不标记 `receipt_without_settlement`；若结果缺失、hash/scope 不一致，则标记 `service_result_integrity_error`/`recovery_required`，仍不得补发游戏效果。
- Native SaveAnchor 显示已提交但对应 command ledger 缺失或 hash 不匹配：标记 `anchor_integrity_error`，停止 AWAKE 外部写入，允许原版游戏继续，等待诊断/恢复。
- Save 操作失败不发布新的 committed SaveAnchor；`OnSaveOver(false, ...)` 只记录失败诊断，不把本次 anchor 视为已保存。

#### 5.4.4 SaveAttempt 状态机

```text
prepared → serialized → committed
prepared → failed
serialized → failed
```

- `prepared` 表示 SaveCoordinator 已冻结新效果并捕获当前 `ledger_sequence` 水位；`serialized` 表示四个 Native key 已从 canonical in-memory snapshot 序列化；`committed` 只在 `OnSaveOverEvent(true, save_name)` 后成立。
- 任一校验、序列化或 Bannerlord 保存失败：丢弃候选 anchor，恢复最近一份 `committed` anchor，解除冻结，写入 `save_recovery_required` 诊断；不得把候选状态留作当前权威。
- 重试只能创建新的 `SaveAttempt`/`attempt_id`，不能复用失败候选或递增 `committed_anchor_sequence`；重复 `OnSaveOver` 只记录审计。
- `AwakeFrameworkSaveBehavior` 的稳定行为 ID 为 `awake.framework.save.v1`，由 `SubModule.OnGameStart` 在 `AwakeTerminalBehavior` 之后只注册一个实例；新档缺失 anchor 使用 `schema=awake.framework.anchor.v1` 的空安全初始值，旧档损坏或未知 major 进入 `anchor_integrity_error`，不自动覆盖。

## 6. MCM 到 Service 的配置事务

```text
AWAKE MCM
  → 输入校验与显示状态
  → AWAKE ConfigTransaction
  → ProtectedCredentialWriter（只写入受保护凭据）
  → AI Runtime Service profile apply
  → typed result / 脱敏状态
```

### 玩家可操作字段

- Provider 类型/预设。
- API URL。
- API Key：当前输入控件可见；保存后只显示掩码或“已保存”。
- 拉取模型、选择模型、手动模型回退。
- 测试连接。
- AI 功能总开关。
- 允许云端外发开关；Route、世界书知识权限和字段分类不在玩家界面自由编辑。
- 保存并应用。

### 绝不写入 MCM JSON、存档、普通日志或导出包

- 明文 API Key。
- `Authorization` header。
- Provider 原始请求/响应中的凭据。
- Service 真实数据库路径、pipe 名称、内部 Route 和 Prompt 原文。

“MCM 属性变化”不等于“Provider 已应用”；只有收到配置事务成功结果才更新为已连接/已应用。

#### 6.0.1 配置与凭据权威

- `ConfigTransactionCoordinator` 是 MCM 配置事务的唯一权威，拥有 `profile_version`、并发比较、apply receipt 和回滚结果；MCM 只提交用户输入和期望版本，不直接改写 Service profile。
- `ProtectedCredentialWriter` 是游戏侧唯一凭据写入者：只在当前输入控件和一次性写入调用的内存范围内接触明文 API Key，使用当前 Windows 用户的 OS protected store 写入/替换/删除，返回 opaque `credential_ref`；它不读取旧 secret，不把明文放进 MCM JSON、存档或 IPC envelope。
- Runtime Service 的 `CredentialBroker` 是唯一凭据读取/使用者；`IProtectedCredentialStore` 是版本化接口契约，不是第三套授权权威。写入者、读取者和 Broker 的 scope、user SID、profile/provider 绑定必须一致。
- 配置 apply 顺序固定为：校验输入 → 写入新 credential → 取得新 `credential_ref` → 以 `profile_version` 提交 Service profile → Service 通过 CredentialBroker 做最小连接/模型验证 → 成功才提交新 profile；任一步失败都撤销新引用、保留旧已提交 profile，并返回原子配置事务失败。
- 并发提交使用 `profile_version + canonical_config_hash + request_id`；版本过期返回 `config_conflict`，同一请求同一 hash 返回原 receipt，不得覆盖后来配置。
- 删除/轮换 credential 先冻结新外发，再撤销旧引用和连接缓存；若新 credential 验证失败，旧 profile/旧引用仍保持可用，不能出现“配置显示成功但 Service 仍使用未知旧状态”。

### 6.1 凭据引用契约

- `credential_ref` 只是不透明引用，不包含 API Key、可逆密文、Authorization header 或 Provider secret。
- 引用至少绑定 Windows 用户 SID、AWAKE profile、Provider、用途 scope 和 schema version；跨用户、跨 profile、跨用途复用默认拒绝。
- 保存、轮换、撤销、删除、Service 重启恢复和配置导出分别有独立结果；删除后旧引用立即失效。
- Service 只能通过受控凭据读取接口取得一次性使用的 Provider 凭据，不得自行扫描凭据目录或缓存明文。
- 凭据读取、轮换、撤销和失败只记录引用 ID、Provider、结果和 correlation，不记录秘密正文。

#### 6.1.1 Scope 与生命周期

允许的 scope 采用固定枚举，不接受任意自由文本：

```text
provider.connect
model.list
route.invoke
embedding.invoke
rerank.invoke
media.generate
devtools.diagnose
```

`credential_ref` 状态机：

```text
active → rotating → active
active → revoked
active → deleted
rotating → recovery_required
```

- 新旧引用在 `rotating` 期间只能按明确的 profile/Provider policy 过渡；不能自动扩大 scope。
- `revoked` 和 `deleted` 引用不可恢复；Service 重启恢复失败进入 `credential_recovery_required`，不回退到旧明文或旧引用。
- scope 匹配必须同时满足用户 SID、AWAKE profile、Provider、用途和 schema major；任一不匹配即 `credential_scope_denied`。
- 凭据后端通过版本化的 `IProtectedCredentialStore` 提供保存、读取、轮换、撤销、删除和恢复结果；Service 不直接访问后端文件。

`IProtectedCredentialStore` 的最小调用契约：

```text
CredentialReadRequest
  credential_ref / requested_scope / profile_id / provider_id
  user_sid / request_id / expires_utc

CredentialReadResult
  status = granted | scope_denied | revoked | deleted | expired | recovery_required
  lease_id? / lease_expires_utc?
  provider_id / scope / correlation_id
```

- `granted` 返回一次性 `lease_id`，只在绑定的 request/session/route/provider 内有效；Service 不通过普通 IPC envelope 取得秘密正文，而只能把 lease 交给同一进程内唯一的 `CredentialBroker`/`EgressBroker` 受控调用。
- Provider Adapter 只能通过 `CredentialBroker.UseAsync(lease_id, operation_scope, callback)` 执行一次绑定的外发操作；秘密正文只存在于短生命周期的受保护缓冲区，操作结束、取消、异常或超时后立即清除，不进入通用对象、连接池、诊断、receipt 或缓存。
- `CredentialBroker` 是唯一可把 Provider 凭据交给 EgressBroker 的内部路径；任何 Adapter、Gateway、DevTools 或 AWAKE 游戏侧代码自行读取凭据都属于契约违规。
- `CredentialBroker` 的唯一 owner 是 Runtime Service 进程；Provider Adapter 只能收到一次性 opaque lease 和受控 callback，不得收到 secret string、环境变量、文件路径、OS credential blob 或可复制连接对象。
- MCM 保存流程由游戏侧 `ProtectedCredentialWriter` 将当前输入直接写入 OS protected store，并只把 `credential_ref` 返回给 Service；API Key 不经普通任务 envelope 进入 Service，也不由 AWAKE 玩法代码持有。
- Provider 使用流程固定为：`credential_ref` scope 校验 → CredentialBroker 创建一次性 lease → EgressBroker 在同一 Service 进程内向 CredentialBroker 请求短生命周期受保护缓冲 → EgressBroker 建连/上传 → callback 结束后清除缓冲和 lease。禁止文件、环境变量、普通 IPC、持久内存缓存、连接池缓存和 Adapter 直读。
- Service/Provider/DevTools 程序集的引用与静态检查必须保证只有 CredentialBroker 能引用 `IProtectedCredentialStore`，只有 EgressBroker 能引用网络/DNS/HTTP/WebSocket 客户端；绕过路径返回 `credential_transport_violation` 或 `egress_bypass_rejected`。
- F-046 的端到端 feature owner 是 AWAKE MCM：它负责可见 Key 输入、校验、事务提交和 `credential_ref` 绑定；Runtime Service 是 CredentialBroker 组件 owner：它负责秘密读取、lease、轮换/撤销和向 EgressBroker 的唯一受控传递。两者不能互换，也不能形成第二条凭据读取路径。
- 轮换并发时，新引用只对新 request 生效；已发出的 lease 不延长、不回写旧引用，旧引用按其原状态完成或失败。
- Service 重启无法恢复 lease 时返回 `recovery_required`，配置事务进入待重新验证，不自动重试 Provider；旧进程内存中的 lease 必须全部作废。
- 读取、保存、轮换、撤销和删除都必须使用 `request_id + canonical_request_hash` 去重；同一 `request_id` 配不同输入返回 `credential_request_conflict`，并能查询原始结果。
- 撤销或删除立即阻止新 lease，并使未消费 lease、Provider 重试任务、连接缓存和待发送任务进入确定的 `credential_revoked`/`credential_deleted` 状态；不得继续使用旧凭据。

## 7. Storage 与世界知识映射

### 7.1 AWAKE 逻辑状态保留

以下 namespace、key、schema 在迁移中保持逻辑语义：

- NPC memory：`hero.<heroId>.v1` / `awake.npc.memory.v1`。
- Relationship：`hero.<heroId>.v1` / `awake.relationship.state.v1`。
- Event meta：`campaign.event_meta.v1` / `awake.event_meta.v1`。
- NPC proactive：`NpcProactiveConstants.Key` / `awake.npc.proactive.v1`。
- World events：`campaign.world_events.v1` / `awake.world_events.v1`。
- Messenger：`campaign.messenger.v1` / `awake.messenger.v1`。
- Transcript、Contacts、Audit、Onboarding、Dialogue Queue、Interactions 及其 recovery index。

### 7.1.1 Native Save anchor 与权威矩阵

当前 v1.5 仍使用 Bannerlord `CampaignBehavior.SyncData`，不新增伪 SubModule；Native Save key 的唯一 Native adapter owner、类型和职责冻结如下。Framework Core 拥有 `SaveAnchor` 契约、schema、恢复判定和跨存储一致性；AWAKE 的 `AwakeFrameworkSaveBehavior` 只拥有 Bannerlord `CampaignBehavior.SyncData` 的适配写入，不得创建第二套 anchor 权威：

| Native key | 类型/schema | 唯一 Native adapter owner | 权威内容 |
| --- | --- | --- | --- |
| `awake_last_weekly_report_day` | `int` / `awake.weekly_report.day.v1` | `AwakeEventBehavior` | 周报结算日游标 |
| `awake_worldbook_overlay_v1` | `string` / `awake.worldbook.overlay.v1` | `AwakeTerminalBehavior` | 玩家导出的世界书 Overlay |
| `awake_worldbook_activation_v1` | `string` / `awake.worldbook.activation.v1` | `AwakeTerminalBehavior` | 当前存档启用的内容包/档案选择 |
| `awake_framework_anchor_v1` | `string` / `awake.framework.anchor.v1` | 新增独立 `AwakeFrameworkSaveBehavior` | `campaign_guid`、`timeline_id`、`session_generation`、`committed_anchor_sequence`、最近结算 receipt hash、Storage schema epoch、migration ID 和 worldbook revision 摘要 |

权威关系固定为：

| 数据层 | 权威范围 | 不是其权威的内容 |
| --- | --- | --- |
| Bannerlord Native campaign/save | 原版角色、家族、关系、经济、外交、地图和战役事实 | AWAKE 的 AI 叙述、Provider 状态和派生检索索引 |
| AWAKE Native Save keys | Overlay、Activation、周报游标和跨存储 anchor | 完整 NPC 记忆、RAG 文本和 Provider 凭据 |
| `campaign.db` | AWAKE 逻辑 namespace、command ledger、SettlementReceipt、Timeline 和战役级运行状态 | 原版游戏事实；不能用数据库值覆盖 Native campaign |
| `platform.db` | Provider profile、协议/能力注册、诊断、迁移 receipt、审计和平台级状态 | 战役业务状态和 API Key 明文 |
| 扩展 sidecar | 单一扩展声明的私有可迁移数据 | 共享 Framework namespace、原版事实和其他扩展状态 |
| RAG/Embedding/Rerank | 可重建的语料索引、向量和检索缓存 | 事实权威、权限结算和存档进度 |
| Timeline | 事件/请求/receipt 的血统、分支和恢复记录 | 直接改写游戏状态的授权 |

`awake_framework_anchor_v1` 是跨 Native Save、`campaign.db` 和 `platform.db` 的恢复指针，不是第二套游戏状态。Framework Core 的 `SaveAnchor` contract 是 canonical owner；`AwakeFrameworkSaveBehavior` 仅负责把已准备好的 contract snapshot 映射到 Native key，并把读回值交还 Framework Core 做校验。任何数据库 sequence 超过 anchor 的记录都必须进入新 timeline 或 `recovery_required`，不得回写旧存档事实。`AwakeFrameworkSaveBehavior.SyncData` 只能读写已经在内存中准备好的 canonical snapshot，不得在 `SyncData` 内进行文件、数据库、网络、Overlay 导入或其他运行时副作用。

#### 7.1.2 Native Save 事件时序

当前 v1.3.15 证据确认 `CampaignEvents.OnSaveStartedEvent`、`OnBeforeSaveEvent`、`OnSaveOverEvent` 存在；目标实现固定使用以下顺序，不在生命周期回调中阻塞等待外部 I/O：

```text
OnSaveStartedEvent
  → SaveCoordinator.FreezeNewEffects()
  → 捕获已 durable 的 command/timeline watermark
OnBeforeSaveEvent
  → 仅在内存中生成 anchor snapshot
  → AwakeFrameworkSaveBehavior.SyncData 写入四个 Native key
实际 Bannerlord 保存
OnSaveOverEvent(success, save_name)
  → success: 记录 save confirmation 诊断
  → failure: 保留旧 anchor，不发布本次 committed anchor
```

- `OnSaveStartedEvent` 只改变主线程中的写入门和快照状态；正在进行的外部写入继续按原 receipt 完成或转为 `recovery_required`，不在事件回调中同步等待。
- 所有会改变游戏状态的 Command 在 Execute 前必须已有 `durably_recorded` 的 `CommandLedgerEntry`；因此 Save barrier 只需冻结新效果并读取已提交 watermark，不用把异步数据库 flush 强塞进 `SyncData`。
- `OnBeforeSaveEvent` 只能读取 `SaveCoordinator` 的内存快照；如果 watermark、hash 或 fence 校验失败，写入 `save_recovery_required` 状态和上一份安全 anchor，禁止假报新 anchor 已提交。
- 读取存档后先由 `AwakeFrameworkSaveBehavior.SyncData` 恢复内存 snapshot，再由 `OnSessionLaunched`/`CampaignSessionReady` 建立 `SessionLease`；完成 anchor 校验前不得打开新的 `campaign.db` namespace 或 Timeline 写入。

物理存储根迁移为 `%LOCALAPPDATA%\MarcusAwakeFramework` 下的独立平台/战役/时间线结构；不复制旧 DB 内容作为新权威。

### 7.4 旧存档与新物理根迁移

- 打开旧 AWAKE 存档时，先读取 Native Save key 和 anchor，再检查新物理根、旧物理根和数据库摘要；不因缺少新 DB 自动覆盖旧状态。
- 旧 DB 存在且校验通过时，只能以显式迁移事务导入新根；导入前生成精确备份，导入后写入 migration receipt 和 source fingerprint。
- 旧 DB 缺失、损坏或与 Save anchor 冲突时，默认进入 `storage_migration_required`/`storage_conflict`，保留游戏载入和代码侧状态，不自动删库或静默拼接。
- 同一 source fingerprint 的重复迁移必须幂等；不同 fingerprint 不得覆盖已导入数据，必须创建待处理迁移记录。
- timeline/session 重建必须从当前存档 anchor 开始；数据库 sequence 超前时创建新 timeline，禁止把未来事件写回旧存档。
- 迁移必须先处理 Native anchor，再打开 `campaign.db` namespace；RAG/Embedding 只在事实数据提交后重建，不参与事实冲突裁决。

#### 7.4.1 迁移状态机与回滚

```text
detected
  → verified
  → backup_created
  → importing
  → validating
  → commit_intent
  → committed

verified → blocked
importing → rolled_back | blocked
validating → rolled_back | blocked
commit_intent → committed | rolled_back | migration_recovery_required
```

- `detected` 只记录来源路径/指纹/版本摘要；`verified` 才允许创建备份。
- `backup_created` 必须包含受影响 DB/WAL/SHM、manifest、Save anchor 摘要和 migration plan；缺一项不得进入 `importing`。
- `validating` 必须验证 namespace owner、schema major、timeline/campaign、sequence、引用完整性和 receipt 去重结果。
- 失败先回滚到 `backup_created`，保留失败原因和原始 fingerprint；重试只能从 `verified` 重新开始，不能覆盖未处理的失败证据。
- 迁移目录固定为同一父目录下的 `target.staging.<migration_id>`、`target.backup.<migration_id>` 和 `target.commit.<migration_id>`；manifest 的权威副本先写入 staging，再通过原子 rename 写入 commit marker。
- `migration.commit.marker` 必须包含 `migration_id`、source/target fingerprint、mapping version、Native anchor hash、target generation 和 canonical manifest hash；只有 marker durable 且 target 校验通过后，新根才可被标记为 `current_root_generation`。
- 启动恢复顺序固定为：读取 current-root marker → 校验 target generation/fingerprint → 若 marker 完整则继续新根 → 若 staging 未提交则删除或隔离 staging → 若切换中断但 backup 完整则恢复旧 current marker 并进入 `migration_recovery_required` → 任何不确定状态都不自动选择新旧数据。
- `commit_intent` 是唯一允许切换 current-root pointer 的阶段；持久化顺序固定为：写 staging manifest → 完成 target 校验 → 写 backup marker → 写 `commit_intent` marker → 原子替换唯一 current-root pointer → 写 `committed` marker。只有 `committed` marker 完成后，新根才成为当前权威。
- 启动恢复只能读取唯一 current-root pointer 和 marker 状态，不能根据目录存在性猜测权威：`commit_intent` 无 `committed` marker 时回滚到原 current pointer 并进入 `migration_recovery_required`；`committed` marker 完整时继续新根；staging/backup 只读隔离。旧根一旦被标记为 backup，不得重新成为权威，除非人工执行带新 migration ID 的回滚事务。
- 新旧 DB 同时存在且 fingerprint 不同，必须进入 `storage_conflict`，由玩家/开发者选择导入方案；不能按时间戳或文件名自动选边。

`MigrationManifest` 最小字段固定为：

```text
migration_id / mapping_version / source_root / target_root
source_fingerprint / backup_fingerprint
campaign_guid / timeline_id / native_anchor_hash
schema_map[] / namespace_map[] / receipt_id
status / created_utc / committed_utc?
```

- 迁移前必须 quiesce 外部写入并对 SQLite 使用一致性备份，覆盖 DB、WAL、SHM、manifest 和 Native anchor 摘要；禁止只复制主 DB 文件。
- 导入写入临时目录，完成 schema、namespace、timeline、sequence、引用和 receipt 去重校验后，才以原子目录切换成为新根；切换前新根不是权威。
- 同一 `migration_id + source_fingerprint + mapping_version` 只能产生一个最终 receipt；输入变化返回 `storage_migration_conflict`，不得复用旧 receipt。
- 迁移恢复状态统一返回 `storage_migration_required`、`storage_migration_conflict`、`migration_recovery_required` 或 `storage_read_only_degraded`，并带 `diagnostic_id`、是否允许继续游戏、是否允许写入和人工恢复入口；不返回空数据库或静默选边。

### 7.2 世界知识两条路径继续分层

| 路径 | 权威 | Framework 提供 | AWAKE 提供 |
| --- | --- | --- | --- |
| 通用 RAG `awake.knowledge` | Runtime Service 的 collection、索引、指纹和检索结果 | 版本化查询/导入 API、IPC、权限、scope、预算和 typed result；不拥有 SQLite/FTS5/Embedding/Rerank 实现 | 语料来源、用途边界和事实语义 |
| Worldbook v2 档案 | AWAKE/内容包的 manifest、身份、表达、授予/拒绝、Overlay、Activation | 受权限和预算约束的查询/索引接口；不拥有世界书语义和物理 RAG DB | 档案语义、知识权限、NPC 表达、玩家编辑/导出 |

不能把两条路径粗暴合成一张“AI 知识表”；也不能把完整世界书文件直接注入 Prompt。

RAG 的实现所有权固定为：Runtime Service 拥有 SQLite/FTS5、Embedding、Rerank、索引重建、物理数据库和检索缓存；Framework Core 只拥有公共 API、IPC、权限、上下文边界和预算；AWAKE 只拥有世界书语义、身份/阶层知识过滤和结果用途。游戏 DLL 不直接引用 SQLite、模型推理或 RAG 物理路径。Service 不得把检索结果当作 Bannerlord 事实权威，所有事实与权限仍由 AWAKE/Framework 代码结算。

#### 7.2.1 WorldbookQueryScope 与检索前权限门

所有 Worldbook v2 检索必须先由 AWAKE 代码生成不可变的 `WorldbookQueryScope`，再交给 Runtime Service；没有 scope 的 Worldbook 检索一律返回 `worldbook_scope_required`。最小字段为：

```text
worldbook_package_id
collection_id = worldbook.v2.<package_id>
archive_ids[]
entry_ids[]?
grant_rule_ids[]
identity_profile_hash
content_revision
overlay_revision
allowed_domains[]
manifest_hash
corpus_fingerprint
retrieval_mode = keyword | hybrid | semantic
max_chunks / max_bytes / max_tokens
policy_epoch
```

- Service 必须在 FTS5、Embedding、Rerank 之前应用 `collection_id + archive_ids/entry_ids + grant_rule_ids + content_revision + overlay_revision` 的 scope 过滤；未授权条目不得进入候选、排序、向量缓存、检索日志或诊断包。
- Worldbook collection 与通用 `awake.knowledge` collection 物理/逻辑隔离；通用 RAG 查询不能借用 Worldbook scope，Worldbook 查询不能省略包、manifest 或权限版本。
- 查询必须绑定 `manifest_hash + corpus_fingerprint + overlay_revision + retrieval_mode`。任一索引/语料/Overlay 不匹配返回 `rag_index_stale`，不得静默使用旧索引、降级后伪装成 semantic，也不得把旧索引结果混入当前包。

每个 `WorldbookQueryResult` chunk 和所有由 chunk 派生的 Prompt/context 字段必须携带：

```text
source_entry_ids[]
grant_rule_ids[]
classification
manifest_hash
corpus_fingerprint
overlay_revision
policy_epoch
```

来源缺失、多个来源合并后无法追溯、字段分类变化或派生 payload hash 改变时，必须重新经过 `ExportPolicyEvaluator`；不能把“已检索”解释为“已获准外发”。

### 7.3 Raw SQL 与只读视图

- 扩展自有复杂数据只能进入自己的 sidecar；不能获得共享 DB connection。
- 共享查询只能访问已发布、版本化、带 access scope 的只读视图。
- `query_only`、authorizer、参数化、行/字节/deadline/慢查询预算必须保留。

## 8. 失败与降级矩阵

| 状态 | 游戏侧保留 | 禁止行为 |
| --- | --- | --- |
| `service_unavailable` | 原版游戏、代码侧查询、已持久化档案、简化 UI | 阻塞等待、静默启动多个 Service、假报 AI 成功。 |
| `provider_unconfigured` | MCM 配置向导、离线功能 | 发送默认/旧 Key、自动外发。 |
| `provider_unauthorized` | 重试前的配置修正提示 | 无限重试、把认证错误当模型不存在。 |
| `model_unavailable` | 手动模型输入、其他明确可用 Route | 静默更换任务语义或模型能力。 |
| `timeout` / `cancelled` | 当前对话可取消、原状态不变 | 把半截结果结算为最终结果。 |
| `protocol_incompatible` | 原版游戏、框架诊断、离线查询 | 通过降级字段猜测协议。 |
| `session_stale` | 当前 session 继续 | 晚到结果写入当前 NPC/UI/存档。 |
| `storage_missing` | 游戏可载入，声明降级 | 自动删库重建、覆盖旧证据。 |
| `cloud_export_denied` | 本地 Route、脱敏/无外发任务 | 绕过最终外发门。 |
| `schema_invalid` / `tool_rejected` | 保留原文诊断、无效果结算 | 执行未验证的关系、战争、经济、知识权限变化。 |

## 8.1 唯一云端外发门

- 所有文本、工具参数、Embedding、Rerank、图片、TTS、Provider 诊断和错误原文在进入任何外部网络 Adapter 前，必须经过同一个 `ExportPolicyEvaluator`。
- 字段 classification 的权威来源是由 Schema/Route owner 管理的 `FieldClassificationRegistry`；调用方提交的分类只作为声明，必须与注册表逐字段匹配。字段缺失、未知、过期或试图降级分类时直接返回 `classification_invalid`/`cloud_export_denied`。
- Evaluator 输入为字段 ID、注册表版本、当前 campaign policy、玩家开关、Route/Provider 能力和凭据 scope；输出为允许的最小字段集合或拒绝原因。
- 所有云端 HTTP、WebSocket、Embedding、Rerank、图片、TTS、Provider 诊断、Management HTTP 和 DevTools 外发统一经 `EgressBroker`；Adapter 不得拥有独立网络出口，不得自行创建 socket 或绕过 Gateway。
- 每次允许、裁剪或拒绝都记录不含正文的 policy receipt：分类、Route、Provider、字段计数、结果和 correlation。
- 本地 Provider 也必须声明是否为 `local_only`；不能因“本地”而跳过字段分类和审计。

### 8.2 ExportPolicyEvaluator 契约

```text
ExportPolicyRequest
  task_id / owner_id / route_id / provider_id
  locality / purpose / credential_scope
  campaign_policy / player_policy
  fields[] = field_id + classification + byte_length

ExportPolicyDecision
  decision = allow | trim | deny
  allowed_field_ids[]
  reason_code
  policy_epoch
  receipt_id
  canonical_payload_hash
  endpoint_id / credential_ref / request_nonce
  expires_utc
  consumption = unused | consumed | expired
```

- `FieldClassificationRegistry`、Route/Schema owner 和 registry version 必须进入 decision hash；没有权威 registry 的字段不允许外发。
- `ExportPolicyEvaluator` 是 AI Runtime Service Gateway 的唯一最终执行点，`EgressBroker` 是唯一实际网络出口；所有 Provider Adapter 只能接收已通过且尚未消费的 `ExportPolicyDecision`。
- Gateway、Embedding、Rerank、图像、TTS、Management HTTP、DevTools 诊断导出都必须先提交同一请求模型；没有 receipt 不得发出外部请求或写出外发包。
- `trim` 必须按字段 ID 裁剪后重新计算字节预算和 `canonical_payload_hash`；不能只在 UI 层隐藏字段。实际发送 payload、endpoint、credential scope、request nonce 和 policy epoch 任一变化都必须重新评估。
- decision 只能被绑定的 request/session/instance epoch 消费一次；payload 被修改、decision 过期、旧 epoch、receipt 缺失或重复消费都返回确定错误，不发送网络请求。
- 测试必须使用拒绝型 fake adapter 验证每条入口都经过 evaluator，并覆盖恶意降级分类、复用旧 decision、修改 payload、绕过 Adapter、策略 epoch 过期、分类未知、媒体字段和错误原文。

### 8.3 三层权限顺序

任何 NPC 知识、世界书检索、Prompt 上下文或外发请求都必须按以下固定顺序处理，后层只能继续缩小范围，不能扩大前层结果：

1. **AWAKE 世界书知识策略**：按身份、阶层、地域、时代、专业、亲历性、关系和内容包规则筛出候选知识。该层只能删除候选，不得把 NPC 不应知道的条目加入结果。
2. **Framework `PermissionGate`**：唯一框架权限权威，校验扩展、Route、Storage、Context、Command 和云端能力授权。它不能绕过或扩大 AWAKE 已筛出的知识范围；未知权限直接 fail closed。
3. **`ExportPolicyEvaluator` / `EgressBroker`**：作为最终外发门，按字段分类、玩家策略、Route/Provider、凭据 scope 和当前 policy epoch 做 `allow`、`trim` 或 `deny`。它可以最终拒绝或裁剪，但不能把被世界书或 Framework 排除的字段重新加入。

三层结果不是同一种集合，必须使用不同类型：

```text
WorldbookKnowledgeSet
  = approved entries/chunks + source_ids + grant_rule_ids + classification

FrameworkPermissionDecision
  = granted capability/field IDs + denied IDs + reason_codes

ExportGateDecision
  = allow | trim | deny | not_applicable
  + allowed_field_ids + policy_epoch + receipt_id
```

有效上下文只在类型化结果完成后计算：

```text
effective_context = WorldbookKnowledgeSet
  ∩ FrameworkPermissionDecision.granted_fields
  ∩ ExportGateDecision.allowed_field_ids
```

纯本地、无外发任务时只能返回 `ExportGateDecision.decision=not_applicable`；这表示没有外发门需要执行，不表示“允许全部”。`allowed_field_ids=[]` 永远表示拒绝全部字段。三层都必须记录不含正文的来源、排除原因、policy epoch 和 correlation；`PermissionOrderAndEgressFixture` 必须证明顺序、裁剪、未知权限、分类未知和绕过出口均 fail closed。

## 9. P1.5 契约验收

在进入 Framework Core 代码迁移前，P1.5 只要求完成以下契约定义、映射和审查准备；这些条目中的 `fixture` 在 P1/P2/P3 按阶段执行，不要求在未授权代码前伪造运行结果：

1. 新旧 API surface 对照表和 AWAKE 27 个源码依赖文件、2 个项目入口文件（`AWAKE.csproj`、`SubModule.xml`）的逐调用点/配置映射。
2. 双版本 API/程序集加载检查；确认不新增伪 SubModule。
3. IPC handshake、envelope、checksum、断线、取消、晚到结果和协议版本 fixture。
4. MCM 配置事务 fixture：URL/Key/模型拉取/测试/保存/失败/重复点击/取消/超时。
5. 12 个 Storage namespace、4 个 Native Save key（含 `awake_framework_anchor_v1`）、worldbook 两条路径的保留与隔离测试。
6. provider unavailable、cloud denied、schema invalid、session stale、storage missing 的 typed error 对照。
7. 外部旧 Marcus 模块/旧 Service 存在时的冲突检测和 fail-closed 行为。
8. IPC ACL/用户 SID/父进程/实例身份/防重放的最小安全矩阵。
9. ACK/幂等状态机、dedupe 持久化、Service 重启恢复和重复完成 fixture。
10. `credential_ref` scope 枚举、轮换/撤销/删除/恢复状态机和并发竞态 fixture。
11. `ExportPolicyEvaluator` 对文本、工具、Embedding、Rerank、图片、TTS、诊断和管理接口的统一入口测试。
12. Storage migration 状态机、备份/回滚、fingerprint 冲突、重试和 Save anchor 选择测试。
13. F-063–F-066 各自的 deferred/partial status、feature flag、failure code、fixture 定义和入口→观察结果验收描述。
14. Service lifecycle 状态机、instance lock、崩溃退避、drain、升级隔离和孤儿实例 fixture。
15. `Epoch/Fence Authority` fixture：旧 epoch、旧 fence、旧 connection、旧 credential lease、旧 egress decision、旧 command 和旧 SettlementReceipt 在升级/重启后全部被拒绝。
16. Receipt 恢复矩阵 fixture：效果已执行但 ServiceReceipt 丢失、ServiceReceipt 已记录但效果未执行、Save anchor 未提交、anchor/ledger hash 冲突和重复提交均得到确定终态。
17. Save/Session 入口 fixture 定义：`OnSaveStartedEvent → OnBeforeSaveEvent → SyncData → OnSaveOverEvent`、`CampaignSessionReady → SessionLease → namespace`、`SessionEnding → Unregistered` 均无阻塞等待、无 `CancellationToken.None` 逃逸、无晚到写入；具体执行分配到 P2/P3。
18. `CredentialBroker`/`EgressBroker` 边界 fixture 定义：文件、环境变量、普通 IPC、Adapter 直读、第三方网络客户端和绕过 policy 的路径均被静态或运行时拒绝；具体执行分配到 P3/P4。
19. `FrameworkCoreVerticalSmokeFixture` 定义：`SubModule → Bootstrap/Host → SessionReady → SessionLease → RequestContext → typed result → FrameworkProbeReceipt/诊断`，每一步都有调用记录和失败分支；E1 属于 P2，E2 属于 P3 后置证据。
20. `PermissionOrderAndEgressFixture` 定义：证明世界书只缩小、Framework Gate 唯一授权、Export/Egress 只能拒绝或裁剪且不能扩大；执行分配到 P3/P4。
21. `MARCUS-AWAKE-CAPABILITY-OWNERSHIP-MAP-v1-20260824.md` 的 `F-001`–`F-066` primary owner、artifact ID、fixture ID、phase 和状态无空档、无双主责、无未登记状态。
22. 通过独立只读审查并获得 `VERDICT: APPROVED` 后，才允许建立代码写集。

## 9.1 F-063–F-066 延期能力契约

延期不表示能力被删除。每项必须在框架迁移期间保持明确的 `deferred/partial` 状态，并且不能出现“入口存在但未接线”的假完成。对 F-063–F-066，`deferred` 表示尚未接线，`partial` 表示只开放明确列出的子能力；不使用 `disabled` 表示当前实现状态。玩家或内容策略主动关闭已接线能力时，使用独立的 `feature_enabled=false` 和原因码，不改变能力状态。

本表区分三种状态：`deferred` 表示该能力当前没有可用子能力；`partial` 表示代码侧的一部分可用、另一部分仍受 deferred gate 阻断；`available` 只表示对应子能力的调用、结算/持久化、fixture 和证据均已完成。

| 能力 | 当前阶段 | 当前状态 | `feature_enabled` | 可执行子能力 | 禁止副作用 | 未接线时的可观察结果 |
| --- | --- | --- | --- | --- | --- | --- |
| F-063 NPC 对话与档案 | P6 | `deferred` | `false` | 无 | 不得发起 Route、Provider、Memory 写入或档案变更 | 游戏内显示“NPC AI 适配尚未启用”，不发送请求。 |
| F-064 关系系统 | P6 | `partial` | `true`（仅只读投影） | `GetRelationshipProjection` | 不得执行 Proposal、Command、ledger 写入或关系/家族好感变更 | 原版关系继续工作；AI 写回提案返回 deferred。 |
| F-065 外交分析 | P6/A2 | `deferred` | `false` | 无 | 不得生成分析任务、提案、审阅或外交命令 | 不生成外交候选，不改变外交状态。 |
| F-066 世界事件 | P6 | `partial` | `true`（仅事实观察） | `ObserveFact` | 不得调用 AI 叙述、传播、`KnowledgePatch`、Provider 或世界效果命令 | 代码事件继续记录；AI 叙述/传播关闭。 |

每项能力都必须有 feature flag、状态、`feature_enabled`、未接线诊断和最小 fixture；对 F-063–F-066，状态固定为 `deferred` 或 `partial`，不使用 `disabled`。`feature_enabled=false` 时任何玩家/DevTools 开关都只能返回“尚未接线”，不能改变运行时状态。partial 只能执行表中列出的子能力和副作用白名单。

### 9.2 DeferredCapabilityStatus 最小公共面

```text
DeferredCapabilityStatus
  feature_id
  state = deferred | partial | available | rejected
  feature_enabled = true | false
  reason_code
  route_id?
  command_ids[]
  fixture_id
  can_enable
  contract_version
  diagnostic_id
  available_parts[]
  deferred_parts[]
  side_effect_allowlist[]
  side_effect_denylist[]
  transition_requirements[]
```

| 能力 | 最小入口 | 初始状态 | 未接线失败码 | 最小 fixture | 验收闭环 |
| --- | --- | --- | --- | --- | --- |
| F-063 NPC 对话与档案 | `GetNpcDialogueCapabilityStatus`、`BeginConversation` | `deferred`, `feature_enabled=false` | `awake.npc.feature_deferred` | `NpcDialogueCapabilityFixture` | 打开 NPC 对话 → 返回 deferred 状态 → 不调用 Provider → UI 显示原因。 |
| F-064 关系系统 | `GetRelationshipProjection`、`PreflightRelationshipProposal` | `partial`, `feature_enabled=true` 仅 projection | `awake.relationship.write_deferred` | `RelationshipProjectionFixture` | 读取原版关系 → 生成只读投影；Proposal 直接 deferred、无 ledger/命令写入、原版数值不变。 |
| F-065 外交分析 | `GetDiplomacyCapabilityStatus`、`RequestDiplomacyAnalysis` | `deferred`, `feature_enabled=false` | `awake.diplomacy.feature_deferred` | `DiplomacyCapabilityFixture` | 请求分析 → 返回 deferred → 不生成提案 → 无外交命令。 |
| F-066 世界事件 | `GetWorldEventCapabilityStatus`、代码侧 `ObserveFact` | `partial`, `feature_enabled=true` 仅 fact observation | `awake.world_event.narration_deferred` | `WorldEventObservationFixture` | 观察不可变事实 → 代码存档；不调用 AI、不生成 KnowledgePatch、不传播、不执行世界效果命令。 |

`can_enable=false` 时任何玩家/DevTools 开关都只能返回“尚未接线”，不能改变运行时状态。只有当对应 Route、Schema、调用方、结算/持久化路径和 fixture 全部存在时，状态才可变为 `available`。

### 9.3 Deferred 统一结果契约

所有 F-063–F-066 的未接线调用统一返回：

```text
DeferredResult<T>
  status = deferred | partial | available | rejected
  feature_status: DeferredCapabilityStatus
  value?: T
  error?: FrameworkError
  correlation_id
```

- `deferred` 必须带 `feature_status.diagnostic_id` 和稳定 `reason_code`；禁止只返回空列表、null 或通用 Provider 错误。
- `partial` 必须同时返回 `available_parts[]` 和 `deferred_parts[]`；调用方只能使用已列入 `available_parts` 的子能力，不能把部分可用解释为整项能力可用。
- `available` 的转换条件必须同时满足：Route/Schema major 已冻结、调用方已接线、结算/持久化 receipt 存在、最小 fixture 通过、feature flag 默认策略已更新。
- `deferred → available` 只能通过新的 capability/schema epoch 迁移，不允许同一版本运行时热切换后改变旧存档语义。
- `rejected` 表示能力已接线但请求不满足权限/事实/风险条件，不得与 `deferred` 混用。
- DevTools 必须能以 `diagnostic_id` 查询状态、依赖、未满足条件和最近一次调用；玩家只看到自然语言说明。

### 9.4 FrameworkCoreVerticalSmokeFixture

这是 Framework Core 首批代码唯一认可的最小纵向 fixture，固定为；P1.5 只锁定其定义和预期证据，不在未授权代码前执行完整 fixture：

```text
SubModule.OnSubModuleLoad
  → Bootstrap/Host 注册
  → CampaignSessionReady
  → SessionLease.ready
  → RequestContext
  → 只读 Framework Probe
  → typed result
  → receipt / 脱敏诊断
```

- `Framework Probe` 只验证 Host、Session、Context、IPC/health 和 typed error，不调用 NPC 对话、关系写回、外交提案、世界事件叙述、Provider 外发或 SQLite 写入。
- P2 执行 E1：使用 Fake Service/FakeHost 验证顺序、取消、session stale、typed result 和诊断字段；P3 执行 E2：使用真实 `Marcus-Awake AI Runtime Service` 做进程/协议 smoke。E2 是 P3 的后置证据，不是 P2 的前置条件；Fake fixture 不能替代真实 Service 或游戏内验证。
- `FrameworkProbeReceipt` 是本 fixture 专用的诊断 receipt，不是 `CommandLedgerEntry`、`SettlementReceipt`、`ServiceReceipt` 或 `SaveAnchor`，不代表任何游戏效果，也不要求 SQLite 写入。它只能记录 fixture 步骤、correlation、session/epoch、typed result、diagnostic ID 和 side-effect=`none`。
- 固定证据文件分别为：`docs/evidence/MARCUS-AWAKE-FRAMEWORK-CORE-VERTICAL-SMOKE-E1-20260824.json` 与 `docs/evidence/MARCUS-AWAKE-FRAMEWORK-CORE-VERTICAL-SMOKE-E2-20260824.json`。文件必须记录 `fixture_id`、调用序列、correlation、session/epoch、结果 code、`FrameworkProbeReceipt`、失败分支和未验证项。
- 任一步失败、调用顺序改变、出现阻塞等待、缺少 receipt/诊断或存在晚到写入时，P2 不得退出，后续 P3/P4/P6 不能以“类已存在”继续推进。

### 9.5 两种“首版”与完整能力口径

为避免“Framework 首版不可缩水”和“F-063–F-066 当前 deferred”互相矛盾，项目固定使用三个不同验收口径：

| 口径 | 覆盖范围 | 允许的延期 | 完成条件 |
| --- | --- | --- | --- |
| Framework Core Migration Baseline | Framework Core、IPC Client/Server 契约、Session/Save anchor、权限、Context、Command、typed result 和 Service 基础边界 | 不宣称 AWAKE NPC/关系/外交/世界事件已经可用 | `FrameworkCoreVerticalSmokeFixture` + 对应安全/存储/生命周期 fixtures 通过 |
| AWAKE Gameplay Baseline | MCM、世界书/知识策略、NPC/关系/外交/世界事件适配和原版事实保留 | 只允许契约中写明的 `deferred`/`partial`，且玩家得到自然语言状态，代码路径不产生假效果 | 每项有入口→调用→结算/持久化→观察结果；延期项有最小 fixture |
| Full Capability Completion | F-001–F-066 全部能力的最终继承状态 | A2 能力必须有独立后续阶段、owner、artifact、fixture 和 release 标记，不能被省略 | ownership map 无空档，所有非 A2 项达到 available 或已通过对应实机/Provider 门 |

因此，Framework Core 首版通过不等于 AWAKE 完整游戏首版通过；AWAKE Gameplay Baseline 也不能把 `partial/deferred` 包装成完整功能。

### 9.5.1 A2 后续能力集合

当前明确标记为 A2、可在 Full Capability Completion 中按后续阶段完成的能力只有：`F-016`、`F-024`、`F-026`、`F-049`、`F-050`、`F-051`、`F-065`。任何新增 A2 必须先更新全功能清单、继承矩阵、ownership map 和阶段退出门；不能只在某行的 phase 字段临时写 `A2`。A2 可以延期，但必须保留稳定契约、owner、artifact、fixture、失败降级和未来迁移门。

### 9.6 阶段退出门

| 阶段 | 必须提交的统一证据 | 失败处理 |
| --- | --- | --- |
| P0 | 来源 commit、许可证、包/程序集白名单 | 停止，不建立代码写集 |
| P0.5 | 配置、Storage、Native Save、旧入口盘点和冲突清单 | 保持 `needs_review`，不得改存档入口 |
| P1.5 | 本契约、ownership map、API/IPC/Storage/Failure fixture 定义、独立审查 `VERDICT: APPROVED` | 禁止建立 Framework Core 写集 |
| P1 | Framework Core 独立项目骨架、API surface diff、Release build；不接入 AWAKE 现有入口 | 保持独立项目，不修改 `AWAKE.csproj`/`SubModule.xml` |
| P2 | `FrameworkCoreVerticalSmokeFixture` 和 Framework Core 构建/契约报告 | 禁止进入 P3 |
| P3 | Service lifecycle、RAG ownership、Credential/Egress、Storage/IPC fixtures | 保持离线降级，禁止接入 MCM/游戏写回 |
| P4 | MCM 配置事务和可见 Key/模型/连接测试证据 | 玩家配置入口不宣称可用 |
| P5 | DevTools、SDK、Analyzer、脱敏导出和包检查 | 不进入发布包 |
| P6 | AWAKE 适配逐项 fixture、partial/deferred 状态和原版事实回归 | 不得宣称完整游戏首版 |
| P7 | 构建、同步、哈希、用户提供的 E4/E5 游戏证据 | 仅停留在离线验证，不发布候选 |

任何阶段的 fixture、artifact、owner 或 evidence 缺失都阻止阶段推进；不能用下一阶段的静态文件反向补齐上一阶段的退出门。

### 9.7 `VERDICT: APPROVED` 后的首批 P1 写集

P1.5 通过后只允许建立以下**第一批 Framework Core 独立写集**；它不授权修改现有 AWAKE 入口，也不授权 Runtime Service、MCM、DevTools 或游戏目录迁移：

**允许新增/修改**

```text
_houkai_merge/AWAKE/framework/MarcusAwakeFramework/MarcusAwakeFramework.csproj
_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/**/*.cs
_houkai_merge/AWAKE/framework/MarcusAwakeFramework/tests/**/*.cs
_houkai_merge/AWAKE/docs/checkpoints/MARCUS-AWAKE-P1-FRAMEWORK-CORE-20260824-checkpoint.md
_houkai_merge/AWAKE/docs/evidence/MARCUS-AWAKE-P1-FRAMEWORK-CORE-API-20260824.json
```

P1 写集只覆盖 F-001/F-002/F-003/F-004/F-005/F-009/F-010/F-011/F-012/F-013/F-038/F-040/F-041/F-042/F-043/F-048/F-057/F-058 的独立 API surface、typed result、FakeHost/FakeService 契约和静态检查；不得把这些能力接入当前 `Awake.dll`。

**明确禁止修改**

```text
_houkai_merge/AWAKE/AWAKE.csproj
_houkai_merge/AWAKE/SubModule.xml
_houkai_merge/AWAKE/src/**/*.cs
_houkai_merge/AWAKE/GUI/**
_houkai_merge/AWAKE/ModuleData/**
_houkai_merge/AWAKE/dist/**
_houkai_merge/AWAKE/_build_out/**
游戏目录 Modules/AWAKE/**
Runtime Service、MCM、DevTools、世界书内容、冻结候选和既有存档/数据库
```

P1 的 `API surface diff`、Release build 和静态 fixture 通过后，才可以创建新的 P2 写集；P2 是否允许修改 `AWAKE.csproj`、`SubModule.xml` 或现有 `src` 文件，必须在 P1 checkpoint 中重新列出并经过新的边界审查，不能由本次 P1.5 批准自动继承。

## 10. 实施约束与待冻结技术细节

以下事项仍需以当前游戏/运行环境证据冻结，不能凭设计文档猜测；它们不再改变本契约已经冻结的权威边界和状态机：

- 精确的 Service 自包含发布目录、打包清单和启动器安装动作；父进程所有权、fencing 和 drain 语义已在本契约冻结。
- Windows 凭据保护 API 的最终实现和升级/多用户行为。
- Named Pipe 库、序列化库和 checksum/framing 的具体实现。
- v1.3.15 当前可验证环境与未来 v1.4.8 环境的四前置矩阵。
- ComfyUI、Player2、TTS、Managed GGUF 的真实实例协议和许可。
- Gauntlet/MCM 生命周期、关闭、输入焦点和跨版本 API 证据。

### 10.1 Service 生命周期最低契约

- Service 由 AWAKE 唯一 Bootstrap 按 OS mutex + 原子 instance lock 启动；旧/重复实例只能报告冲突，不能接管或共享存储租约。
- Service 必须声明 parent process PID/创建时间、user SID、implementation version、instance epoch、pipe identity 和 shutdown deadline，并持有父进程死亡通知/心跳租约。
- 所有 IPC 写请求、Storage 写入、Provider 任务和终态 Receipt 都必须携带并验证 `instance_epoch/fence_token`；失去当前锁的旧实例进入 `fenced`，不得继续写入。
- 崩溃后只允许有界重启；连续失败进入 `service_crash_loop`，不无限重启、不阻塞游戏。
- 停止流程必须先拒绝新任务、等待有限 drain、取消可取消任务、写入 shutdown receipt，再关闭 pipe。
- 升级、卸载和父进程退出必须清理孤儿实例；清理失败进入诊断，不杀死未知进程。只有通过当前 instance proof 认证的旧实例才允许被受控终止。

### 10.2 Service 生命周期状态机

```text
stopped
  → starting
  → ready
  → stopping
  → draining
  → stopped

starting → conflict | protocol_incompatible | crashed | orphaned
ready → crashed | conflict | draining | stopping | fenced
draining → stopped | recovery_required | upgrade_failed
stopping → draining | stopped | recovery_required
crashed → starting（仍有重启预算）| stopped（预算耗尽）
orphaned → stopping（proof 有效）| recovery_required（proof 无效）
fenced → stopped | recovery_required
```

实例锁记录必须包含：

```text
instance_id / owner_sid / parent_pid / start_utc
implementation_version / pipe_identity / heartbeat
shutdown_deadline / restart_count / lock_schema
instance_epoch / fence_token / parent_creation_time
```

- 只有 AWAKE Bootstrap 能创建 `starting` 实例；MCM/DevTools 只能发起受控启动/停止请求，不能直接生成第二实例。
- `conflict`、`protocol_incompatible`、`service_crash_loop`、`credential_recovery_required`、`recovery_required` 和 `upgrade_failed` 都是终态诊断状态，必须由玩家/开发者明确处理后才能恢复。
- 默认运行参数固定为：heartbeat 2 秒、stale grace 10 秒、drain deadline 5 秒、单小时自动重启预算 3 次、退避 1/2/4 秒；达到上限后停止尝试，游戏继续离线运行。
- `draining` 拒绝新任务，等待有界写入和可取消任务结束，写入 shutdown receipt 后关闭 pipe。
- `stopping → stopped` 只允许在停止意图确认后且没有待处理任务、未消费 lease、未提交 receipt 或持有的 storage lease 时直接发生；否则必须先进入 `draining`。`draining` 到期仍有不确定任务时进入 `recovery_required`，不得伪造 clean stop。
- 升级顺序固定为：`upgrade_prepare → old_draining → old_stopped → storage_lease_released → new_starting → new_ready`。旧实例 drain 超时且 proof 有效时才允许受控停止；旧实例未停止或存储租约未释放时，新实例不得 ready。新实例启动失败则保持离线或回退已验证旧版本，禁止新旧双写。
- 实例锁、状态转换、父进程退出、旧实例晚到完成、升级失败、PID 重用和孤儿判定都必须有状态机 fixture；不能只用“进程是否存在”判断健康。

#### 10.2.1 实例锁与父进程规则

- instance lock 的权威位置为 `%LOCALAPPDATA%\MarcusAwakeFramework\runtime\instance.lock`；只允许当前用户 SID 创建和修改。
- 锁内容必须使用原子创建，包含第 10.1 节全部字段和 `lock_nonce`；已存在的锁只有在 owner proof、PID 创建时间、heartbeat 和 parent process 都失效时才可标记为 stale。
- stale 判定必须同时满足：PID 不存在或不属于 AWAKE Service、heartbeat 超过 shutdown grace、且无法通过 owner proof challenge；不能只按时间删除锁。
- Bootstrap 启动失败、ACL 拒绝、锁冲突、父进程退出和 PID 重用分别返回稳定错误码；不删除未知进程或未知锁。
- 升级时新 Service 使用新 `implementation_version`、新 pipe identity 和更高 `instance_epoch`；旧 Service 先进入 `draining`，旧实例未完成 drain、释放存储租约并进入 `stopped` 前新实例不得进入 `ready`。
- 父进程退出后 Service 进入 `draining`，在 shutdown deadline 到期后拒绝所有新任务并退出；未完成任务按 `recovery_required` 处理。旧实例失去 fence 后，所有迟到写入和终态 Receipt 统一返回 `instance_fenced`。

### 10.3 Session、Save barrier 与异步任务

- `campaign_guid` 表示存档血统，`timeline_id` 表示事件分支，`session_id` 表示本次进程中的战役会话；三者命名在 API、DB、日志、迁移 receipt 和 Save anchor 中保持一致。
- `CampaignIdentityAdapter` 是唯一身份映射者：读取当前 SDK `SessionRef.CampaignId`/等价原始字段后，规范化为新协议的 `campaign_guid`；该映射只在会话建立时生成并写入 anchor，复制存档、回滚或分支不得通过显示名、时间戳或 PID 重新猜测血统。
- 身份输入优先级固定为：有效 Native anchor pair → 明确的旧存档迁移映射 → 新建存档生成器；`SessionRef.CampaignId` 只能作为 opaque 校验输入，不能单独覆盖 anchor。首次新建存档生成新的随机 opaque `campaign_guid`；复制/另存为继承存档血统但创建新的 `timeline_id`；显式 fork 才能创建新的 timeline；读档恢复原 anchor 中的 pair。无法证明映射时返回 `campaign_identity_unavailable`，不打开外部写入。
- `SessionLease` 状态机固定为：`created → ready → closing → drained`，异常转移为 `closing → recovery_required`。重复 `CampaignSessionReady` 返回当前 ready lease；closing 期间再次 ready 返回 `session_closing`；重复 `BeginClosing` 返回同一 closing/drain task；已 drained 的旧 generation 不能重新打开；Unregistered 未完成 drain 只能返回 `session_drain_incomplete`。
- `CampaignSessionReady` 创建新的 `SessionLease`：包含 `session_id`、单调 `session_generation`、session CTS、campaign/timeline 绑定、`instance_epoch/fence_token` 摘要和当前 Native anchor 摘要。读档必须先恢复 anchor，再完成 identity/sequence 校验，最后打开 `campaign.db` namespace、Timeline 和 RAG 查询。
- `SessionEnding`/`Unregistered` 的顺序固定为：调用唯一 `SessionCoordinator.BeginClosing()` → 原子递增 generation → 取消 session CTS → 拒绝新请求/写入 → 隔离晚到结果 → 在安全调度点进行有界异步 drain。任何 Storage、Event、Command 最终写入口都再次校验 generation、session token 和 `instance_epoch`。
- `SessionLease` 是所有后台任务的唯一 cancellation owner；禁止使用 `CancellationToken.None` 启动 session-owned Storage、AI、RAG、Memory、Event 或诊断任务。Unregistered 未取得 drain 完成证明时，只能返回 `session_drain_incomplete`，不能伪造已排空。
- 不得在生命周期回调中使用 `.Wait()`、`.Result` 或 `GetAwaiter().GetResult()` 等待外部 Storage/AI；回调只发出停止意图，完成证明由安全调度点和 typed receipt 提供。
- Save barrier 不通过同步等待实现，顺序固定为：`OnSaveStartedEvent` 冻结新效果并捕获已 durable watermark → `OnBeforeSaveEvent` 在内存中生成 anchor snapshot → `SyncData` 写入四个 Native key → Bannerlord 执行保存 → `OnSaveOverEvent` 记录成功/失败。Save snapshot 不能访问外部 I/O；校验失败进入 `save_recovery_required`，保留上一份安全 anchor。

### 10.3.1 当前 AWAKE 迁移阻断清单

以下是必须在代码迁移批次中完成的可观察门，不是当前已完成声明：

| 当前入口 | 目标变化 | 验收要求 |
| --- | --- | --- |
| `src/ProbeExtension.cs` Session lifecycle | 改用统一 `SessionLease`/CTS/安全 drain；移除同步等待和 `CancellationToken.None` | SessionReady、SessionEnding、Unregistered、迟到结果和 drain receipt fixture |
| `src/AwakeTerminalBehavior.cs` Overlay/Activation | 只保留两个 worldbook key；anchor 移到 `AwakeFrameworkSaveBehavior` | `SyncData` 无 I/O/副作用，四 key 读写和读档顺序 fixture |
| `src/SubModule.cs`/`AWAKE.csproj` | 改用新 Framework/Bootstrap 身份，移除外部 Marcus | 真实 SubModule → Host → SessionReady 纵向 fixture |
| `src/PermissionGate.cs`/`PermissionCatalog.cs` | 改为 Framework 公共 Gate 的无授权适配器或删除 | 未知权限 fail closed；无第二授权状态 |
| `src/AiTaskConstants.cs`、`src/ProbeExtension.cs`、`src/AwakeWorldCommandAdapters.cs` | F-063–F-066 注册/调用/执行三层接入 deferred gate | 四项 capability fixture 均遵守各自状态：F-063/F-065 不发 Provider 请求、不写游戏效果；F-064 仅读投影；F-066 仅记录不可变事实观察。 |

当前代码未通过这些门前，不能把“类存在、路由注册或旧 smoke 通过”记作新迁移完成。

### 10.4 Permission 唯一权威与当前 AWAKE 迁移门

- Framework Core 的 `PermissionCatalog`/`PermissionGate` 是权限唯一权威；AWAKE 只声明 manifest、请求权限并在调用点调用公共 Gate，不保留第二套可独立授权的 `PermissionGate.cs`/`PermissionCatalog.cs`。
- 迁移期间 AWAKE 本地权限类只能作为临时适配器或删除；它们不得绕过 Framework 结果、改变未知权限的 fail-closed 语义或自行持久化授权。
- F-063–F-066 的唯一当前状态为：F-063/F-065 `deferred` 且 `feature_enabled=false`；F-064/F-066 `partial` 且仅开放各自列明的只读子能力。`disabled` 不作为这四项能力的状态值；未接线统一使用 `deferred` 加 `feature_enabled=false`。现有 `AWAKE.route.npc.dialogue`、关系/世界效果命令若尚未具备完整调用→结算路径，必须由 feature flag 阻断并返回 deferred；partial 只能开放已列明的只读/事实观察子能力，不能一边注册可调用入口、一边声称整项能力可用。
- 首批 Framework API 迁移的最小纵向闭环固定为：`SubModule.OnSubModuleLoad → Bootstrap/Host 注册 → CampaignSessionReady → RequestContext → 公共 Gateway/Storage 调用 → typed success/error 或 deferred → 脱敏日志/诊断 receipt`。只有这条链路有 fixture 和调用证据，才算迁移完成。
