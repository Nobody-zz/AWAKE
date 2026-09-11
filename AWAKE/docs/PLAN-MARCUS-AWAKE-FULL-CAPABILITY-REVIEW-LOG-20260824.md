# Plan Review Log: Marcus-Awake 全功能继承版

## 当前状态

- Act 1：`BASELINE_EXPANDED`
- Act 2：`REVISE`
- 结论：独立只读审查返回 `VERDICT: REVISE`；已把 7 项实质缺口写回 P1.5 契约草案，暂不授权运行时代码迁移。

## Act 1 结果

- 用户要求：Marcus 的核心功能和突出优势必须继承，并且全部 Marcus 功能都要纳入考虑。
- 已将“全部考虑”解释为：每项能力都有归属、继承方式、阶段、证据、失败降级和验收边界；不要求第一版同时向玩家开放所有媒体、Provider 或开发者功能。
- 已明确四个 AuthorSource 扩展的全部业务行为纳入 AWAKE 适配验收，但不原样进入 Framework Core。
- 已锁定玩家配置、AI Runtime Service、Framework Core、DevTools、SDK 五层边界。

## 已完成的只读审查

- F-001–F-066 连续性检查：通过，无缺项、无重复。
- Marcus 设计大纲 00–18 覆盖检查：已在全功能清单建立逐文档覆盖表。
- 现有 AWAKE P0/P0.5 数据、存档键、Storage namespace、旧 Companion/IPC 入口：已与迁移计划对齐。
- 未修改 `AWAKE.csproj`、`SubModule.xml`、运行时代码、dist、游戏目录或冻结候选。

## 外部审查尝试

1. 已有只读代理会话未返回正文，无法作为审查证据。
2. 重试只读代理返回本地服务 `502 Bad Gateway / upstream 503`，无法生成审查结论。
3. 本机 `codex exec` 重试受当前 Windows shell/运行时包装限制，未取得有效审查输出。

本次通过本机 `codex exec --skip-git-repo-check --sandbox read-only` 成功取得独立审查结果。

## 独立审查结论（2026-08-24）

1. IPC 握手缺少 Named Pipe ACL、对端身份、用户 SID、父进程关系和防重放条件；已补入握手安全条件。
2. 消息幂等、重放、ACK 状态和 dedupe window 未闭合；已补入 `idempotency_key`、四态 ACK、receipt 和 sequence 规则。
3. 凭据引用缺少 scope、轮换、撤销、删除和 Service 读取边界；已补入 `credential_ref` 契约。
4. Service 生命周期缺少 instance lock、崩溃重启、孤儿清理、停止 drain 和升级隔离；已补入最低生命周期契约。
5. 旧存档/旧 DB 与新物理根的导入、冲突、备份和幂等迁移未定义；已补入迁移事务和 `storage_conflict` 语义。
6. 云端外发门没有唯一执行点和媒体/诊断覆盖；已补入统一 `ExportPolicyEvaluator`。
7. F-063–F-066 只有清单登记，没有延期状态、禁用默认值和未接线结果；已补入 deferred contract 表。

审查结论：`VERDICT: REVISE`。上述修订完成后需要再次只读复审，不能直接把本轮 `REVISE` 当作通过。

## 第二轮复审结论（2026-08-24）

第二轮确认前七项方向基本正确，但仍有 6 项字段级阻断：

1. IPC 没有 `user_sid`、`parent_pid`、bootstrap proof、challenge-response 和认证失败流程。
2. sequence 窗口没有乱序、断线重连、持久化、窗口清理和并发接收算法。
3. ACK 与 Receipt 的职责、终态优先级、取消/过期/恢复语义存在冲突。
4. Service instance lock、锁失效、父进程检测、重启预算和升级隔离仍未具体化。
5. `credential_ref` 缺少读取请求/响应、lease、轮换并发和重启恢复契约。
6. F-063–F-066 缺少统一返回类型、诊断字段和 deferred→available 兼容门槛。

以上问题已写回 P1.5 草案：双向认证、sequence 算法、ACK/Receipt 分工、实例锁规则、凭据 lease 和 `DeferredResult<T>`。

第二轮结论：`VERDICT: REVISE`。

## 待审查重点

- `MarcusAwakeFramework.dll` 是否只作为 AWAKE 单模块程序集加载，避免伪 SubModule 和重复 Host。
- AI Runtime Service 启停、多实例、父进程、旧 Marcus 冲突和 session invalidation。
- `platform.db`、`campaign.db`、sidecar、RAG、Timeline、AWAKE Native SaveDefiner 的权威与迁移。
- 六类 Provider、媒体/CAS、结构化输出、外发 policy、凭据引用的版本化和 unavailable/degraded 语义。
- 四个参考扩展是否全部通过 AWAKE 适配层接线，而不是被误塞入框架核心或被遗漏。

## 进入代码前条件

1. 获得独立只读审查的明确 `VERDICT: APPROVED`。
2. 用户签收全功能清单、继承矩阵 v2 和 Phase 0.5/1.5 迁移顺序。
3. 形成 API surface、IPC envelope、Service 配置、Storage ownership、Failure Matrix 和迁移映射表。

## P1.5 契约草案状态

- `MARCUS-AWAKE-P1.5-CONTRACT-LOCK-DRAFT-20260824.md` 已建立并按审查意见修订。
- 草案已覆盖程序集加载、旧 API 映射、版本轴、IPC 信任/幂等、MCM 配置事务、CredentialBroker、12 个 Storage namespace、4 个 Native Save key、权威矩阵、Save barrier、MigrationManifest、两条世界知识路径、统一 EgressBroker、Service 生命周期、延期能力和失败降级矩阵。
- 第四轮独立只读审查返回 `VERDICT: REVISE`，发现四项 P0 和多项 P1；第五轮复审确认这些概念还需要唯一签发者、当前源码迁移门和可执行恢复矩阵。
- 已完成第四、第五轮修订：补入 Bootstrap 私有启动通道与 canonical ACL、Epoch/Fence Authority、全局 sequence、IdempotencyScope/Receipt 恢复矩阵、CredentialBroker/EgressBroker 唯一路径、Native anchor/Save 事件时序、Migration commit marker、SessionLease、当前 AWAKE 迁移阻断清单和 F-063–F-066 deferred 门。
- 当前状态为 `REVISE_REQUIRED_BEFORE_NEXT_INDEPENDENT_REVIEW`；第六、七轮问题已在本文件末尾记录并已完成对应文档修订，仍需下一轮独立只读复审明确返回 `VERDICT: APPROVED` 后，才能建立首批代码写集。

## 第五轮复审结论（2026-08-24）

三路独立只读复审均返回 `VERDICT: REVISE`。本轮没有否定总体分层，而是确认上一轮补入的概念仍有部分没有落到唯一签发者、当前源码迁移门和可执行恢复矩阵。

| 第五轮问题 | 修订位置 | 当前处理状态 |
| --- | --- | --- |
| Epoch/fence/connection/sequence 没有唯一签发者、持久化和失效传播 | P1.5 §5.1.4、§10.1–§10.2.1 | 已补 `Epoch/Fence Authority`；下一轮核对字段是否足以实现 |
| Service 身份与 ACL 的“当前用户/服务身份”双重表述 | P1.5 §5.1、§5.1.3 | 已冻结为与 AWAKE 同一交互用户 SID 运行，canonical DACL 只允许目标 SID + SYSTEM |
| CredentialBroker/网络出口仍可能被绕过 | P1.5 §6.1.1、§8.1–§8.2 | 已补唯一 owner、secret transport、受保护 callback、EgressBroker 及静态边界拒绝 |
| Receipt、SettlementReceipt、CommandLedger、SaveAnchor 的恢复权威不唯一 | P1.5 §5.4.3、§9 验收 15–18 | 已补字段/发布者/查询权威和恢复矩阵 |
| Native anchor、Save barrier、Lifecycle 与当前源码入口不闭合 | P1.5 §7.1.1–§7.1.2、§10.3–§10.3.1 | 已冻结 `AwakeFrameworkSaveBehavior`、Bannerlord Save 事件时序、SessionLease 和当前迁移阻断清单 |
| 迁移 commit marker 与崩溃恢复没有权威切换算法 | P1.5 §7.4.1 | 已补 staging/backup/commit 目录、marker、启动扫描和旧根不可自动复权规则 |
| F-063–F-066 deferred gate、Permission 唯一权威、MCM 旧入口仍未接线 | P1.5 §10.3.1、§10.4；Plan Phase 1.5/Phase 6 | 已明确为代码迁移门；尚未修改运行时代码，不能宣称已接线 |

本轮仍为 `REVISE` 的原因：当前代码尚未实现上述迁移门，但这正是下一批 Framework Core 代码必须完成的范围；下一轮审查将只检查契约是否已经给出唯一、可测试、无矛盾的实现边界，不把“旧代码尚未迁移”误判为契约缺陷，除非契约没有定义目标入口或失败语义。

## 第六轮独立只读审查结论（2026-08-24）

本机捕获的第六轮审查仍返回 `VERDICT: REVISE`。主要问题是：Service 生命周期、唯一外发门、F-063–F-066 deferred contract、ACK/幂等状态机、凭据 scope/轮换和旧 DB 迁移回滚尚未达到可直接编码的字段级契约；同时指出 RAG 物理 owner、阶段范围和测试矩阵需要进一步明确。

## 第七轮独立只读审查结论（2026-08-24）

本机捕获的第七轮审查仍返回 `VERDICT: REVISE`。主要问题收敛为：IPC 认证字段/流程、序号窗口算法、ACK 终态优先级、Service instance lock/父进程规则、`credential_ref` 受控读取接口，以及 F-063–F-066 统一返回类型和 deferred→available 门槛。

以上两轮输出已作为历史审查证据保留；没有把“原则上提到”当作“契约已闭合”。

## 修订轮（2026-08-24）

针对第六、七轮问题，本轮完成以下文档修订，仍未修改运行时代码、项目入口、发布目录或游戏目录：

1. 新增 `MARCUS-AWAKE-CAPABILITY-OWNERSHIP-MAP-v1-20260824.md`，逐项覆盖 F-001–F-066 的 owner、artifact、fixture、phase 和当前状态。
2. 把 SQLite/FTS5、Embedding、Rerank、物理 RAG DB 和索引重建唯一归 Runtime Service；Framework Core 只保留 API/IPC/权限/预算，AWAKE 保留世界书语义和知识权限。
3. 固定 `FrameworkCoreVerticalSmokeFixture`，明确 Framework Core Migration Baseline、AWAKE Gameplay Baseline、Full Capability Completion 三种验收口径。
4. 将 F-064 关系只读投影、F-066 代码事实观察定义为 `partial`，并为 `DeferredCapabilityStatus`/`DeferredResult<T>` 增加 partial、available_parts、deferred_parts。
5. 写死三层权限顺序：AWAKE 世界书只缩小 → Framework PermissionGate 唯一授权 → ExportPolicy/Egress 最终拒绝或裁剪且不能扩大。
6. 增加 P0–P7 阶段退出门；fixture、artifact、owner 或 evidence 缺失时禁止转阶段。

当前结论仍不是批准：需要下一轮独立只读复审确认上述修订已形成唯一、可测试、无矛盾的实现边界。下一步不改代码、不构建迁移候选、不启动游戏。

## 第八轮复审尝试（2026-08-24）

已按三路独立只读复审分别提交协议安全、RAG/权限边界、阶段/写集三个审查节点；三路均因上游 `429 Too Many Requests` 超过重试限制而未返回正文或 verdict。

- 本轮不能视为 `APPROVED` 或 `REVISE`。
- 按限流规则停止当前轮次，不重复提交或并行重试。
- 第八轮审查输入已固定为本轮修订后的 P1.5 契约、ownership map、迁移计划、CURRENT 和 checkpoint。
- 已完成但尚未独立复审的修订包括：`WorldbookQueryScope` 检索前权限门、类型化三层权限结果、`fence_proof`/HKDF、sequence 安全重传与冲突复用、CommandLedger 中间态、partial 副作用白名单、P1/P2/P3 时序、A2 集合和 P1 文件级写集。

当前仍不得修改运行时代码、`AWAKE.csproj`、`SubModule.xml`、发布目录、游戏目录或冻结候选。恢复后唯一下一步是重新启动一次独立只读复审，不得跳过 verdict。

## 第八轮限流后的继续信号（2026-08-24）

- 用户发送“批准”，解释为允许在限流窗口恢复后重新启动一次独立只读复审。
- 该信号不替代审查结果，不构成 `VERDICT: APPROVED`，也不授权建立 P1 代码写集。
- 下一次审查继续使用已固定的 P1.5 契约、ownership map、迁移计划、CURRENT 和 checkpoint；若再次出现 429、502、503 或结果不确定，立即停止并更新 checkpoint，不自动重放。

## 第九轮独立只读复审结论（2026-08-24）

本轮在用户继续信号后仅启动一次独立只读复审，未修改代码、未构建、未启动游戏、未同步或改变冻结候选。结论：`VERDICT: REVISE`。

| 编号 | 阻断问题 | 修订动作 |
| --- | --- | --- |
| P1-1 | Sequence 重放语义中“重复一律拒绝”与“相同 payload 安全重传返回原 ACK”缺少优先级 | 增加 session/epoch/nonce → window → replay cache 的固定判定顺序；仅同一帧完全一致时允许安全重传 |
| P1-2 | Receipt.completed 对非游戏效果任务是否需要 SettlementReceipt 未闭合 | 增加 `settlement_requirement=required\|not_applicable`；游戏效果必须绑定 SettlementReceipt，非效果任务由 Service durable completion 结束且不得声称游戏结算 |
| P1-3 | F-063/F-065 fixture ID 在契约与 ownership map 不一致 | 统一为 `NpcDialogueCapabilityFixture`、`DiplomacyCapabilityFixture` |
| P1-4 | F-063–F-066 混用 `disabled/deferred/partial` | 固定 F-063/F-065 为 `deferred + feature_enabled=false`，F-064/F-066 为 `partial`；不以 disabled 表示未接线 |
| P1-5 | Save anchor contract owner 与 Native adapter owner 未分层 | 固定 Framework Core 拥有 SaveAnchor contract/schema/recovery，AWAKE 仅拥有 Native adapter 写入 |
| P1-6 | F-046 feature owner 与 CredentialBroker 组件 owner 混在一起 | 固定 AWAKE MCM 为端到端 feature owner，Runtime Service 为 CredentialBroker 组件 owner |

非阻断对齐：A2 集合补齐 `F-050`、`F-051`；P1 写集仍未建立。

### 第九轮修订后的下一步

- 重新检查契约、ownership map、carryover matrix 和 P1 写集的交叉一致性。
- 通过新的独立只读复审并取得明确 `VERDICT: APPROVED` 前，继续禁止建立 P1 写集。
## 第十轮独立只读复审结论（2026-08-24）

本轮未修改代码、未构建、未启动游戏、未同步或改变冻结候选。第九轮六项字段修订被确认有效，但发现以下四项交叉一致性阻断，已完成修订：

| 编号 | 阻断问题 | 修订动作 |
| --- | --- | --- |
| P1-1 | Receipt 恢复矩阵无条件把非效果任务的完成 Receipt 判为 `receipt_without_settlement` | `ServiceReceipt` 增加 `settlement_requirement`；恢复矩阵按 `required/not_applicable` 分支，非效果任务使用 Service durable result 校验 |
| P1-2 | A2 集合在 contract、carryover matrix、inventory、ownership map 不一致 | 统一 canonical A2 集合为 `F-016/F-024/F-026/F-049/F-050/F-051/F-065`，并同步 inventory、ownership map 与矩阵声明 |
| P1-3 | Plan Phase 1 F-ID 范围宽于 §9.7 文件级写集 | Plan 改为逐字引用 §9.7 的 17 项 P1 F-ID；其他能力明确排除出本批次写集 |
| P1-4 | Plan 仍使用 `disabled/deferred/partial` 状态词 | 改为 `deferred + feature_enabled=false` 与 `partial` 的契约术语，并统一 ACK/Receipt 表述 |

本轮复审同时确认：sequence 优先级、非效果 Receipt 规则、fixture 名称、Save Anchor 双层 owner、F-046 双层 owner、Permission/RAG/IPC/Service/Storage 边界已基本闭合。P1 写集仍未建立。

### 第十轮修订后的下一步

- 重新启动一次独立只读复审，重点检查 A2、P1 F-ID 和 Receipt 分支是否已在所有权威文档中唯一一致。
- 通过明确 `VERDICT: APPROVED` 且完成用户签收前，不建立 P1 文件写集。
## 最终独立只读复审与签收（2026-08-24）

- 最终复审结论：`VERDICT: APPROVED`，无 P0/P1 blocker。
- 已核对：Receipt 双分支、canonical A2 七项集合、P1 §9.7 F-ID 写集、F-063–F-066 状态/fixture、Save Anchor owner 分层、CredentialBroker owner 分层以及 Permission/RAG/IPC/Service/Storage 边界。
- 用户此前的“批准”记录为本批次用户签收；允许建立 P1 Framework Core 独立写集。
- P1 允许路径仅为 `framework/MarcusAwakeFramework/**`、P1 checkpoint 和 API evidence；禁止修改 AWAKE 现有入口、Runtime Service、MCM、DevTools、世界书、发布目录、游戏目录、冻结候选及既有存档/数据库。
- 非阻断修订已完成：ACK 状态图加入 `recovery_required`；计划将“禁用默认值”改为 `` `feature_enabled=false` 默认值``。
## P1 Framework Core 实现结果（2026-08-24）

- P1 写集已建立并完成：独立 `MarcusAwakeFramework` net472 项目、公共 API、typed result、Fake/in-memory contract doubles、7 项契约 Smoke。
- Release build：0 warnings / 0 errors；测试：`PASS ALL: 7 Framework Core contract tests`。
- API evidence：`docs/evidence/MARCUS-AWAKE-FRAMEWORK-CORE-API-20260824.json`；P1 checkpoint：`docs/checkpoints/MARCUS-AWAKE-P1-FRAMEWORK-CORE-20260824-checkpoint.md`。
- P1 未修改 AWAKE 主工程、SubModule、现有运行时代码、Runtime Service、MCM、DevTools、世界书、发布目录、游戏目录或冻结候选。
- P1 最高证据等级为 E2 离线验证；P2 纵向接入、真实 Service/IPC、Bannerlord、E3/E4/E5 均未开始。