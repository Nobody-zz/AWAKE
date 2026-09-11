# Marcus-Awake 能力归属与证据映射表 v1

- 日期：2026-08-24
- 状态：`P1.5_CONTRACT_REVIEW_BASELINE`
- 用途：把 `F-001`–`F-066` 逐项绑定到唯一主责层、计划产物、最小 fixture 和阶段，防止能力无人负责或只有类没有调用证据。
- 权威清单：`MARCUS-AWAKE-FULL-CAPABILITY-INVENTORY-v1-20260824.md`
- 本表是契约和验收映射，不代表代码已经存在或已通过游戏内验证。

### Artifact resolution rule

- 本表“计划产物”列使用稳定 artifact ID/契约名称；它不是“文件已经存在”的声明。
- P1.5 只要求 artifact ID、owner、fixture、phase 和状态完整；实际 evidence 文件必须在对应阶段 checkpoint 中登记后，才能把状态从 `planned`/`contract_locked` 提升为已验证状态。
- 当前唯一已锁定的 P1 文件级写集与证据路径见 P1.5 契约 §9.7；其它 artifact 在进入对应阶段前不得自行扩展写集。

## 归属规则

1. **Framework Core** 负责游戏侧公共 API、生命周期、权限、Context、Command、Save anchor 和 IPC Client；不拥有 SQLite、Embedding、Rerank、Provider 或物理 RAG 数据库。
2. **Runtime Service** 负责后台 Provider、Gateway、Prompt Registry 的运行时编译、RAG/SQLite、Timeline、凭据 Broker、Egress、媒体和 IPC Server。
3. **AWAKE** 负责 Bannerlord 事实适配、世界书语义、NPC 知识权限、关系/事件/周报和 MCM 玩家入口。
4. **DevTools/SDK** 负责作者维护、诊断、脱敏导出、FakeHost、Analyzer 和第三方开发资源，不成为玩家运行时依赖。
5. `primary_owner` 必须是一个唯一 canonical owner；协作层、依赖层和发布层列在下方 `supporting_owners` 映射中，不得与 primary owner 并列承担同一验收责任。
6. `partial` 表示同一能力的一部分已有代码侧可用，另一部分仍明确 deferred；`deferred` 不等于遗漏，必须有入口、失败码和 fixture。
7. 跨边界能力必须区分端到端 feature owner 与组件 owner：feature owner 负责用户可观察的完整验收；组件 owner 只负责其内部契约和安全边界，不能改变 primary owner。Save Anchor 的 feature/contract owner 是 Framework Core，Native adapter owner 是 AWAKE；F-046 的 feature owner 是 AWAKE MCM，CredentialBroker 组件 owner 是 Runtime Service。

## 逐项映射

| 能力 | primary_owner | 计划产物 | 最小 fixture / 证据 | 阶段 | 当前状态 |
| --- | --- | --- | --- | --- | --- |
| F-001 稳定身份与版本轴 | Framework Core | `MarcusAwakeFramework` 身份、版本清单、包白名单 | `FrameworkIdentityFixture` | P1.5/P2 | contract_locked |
| F-002 Host / Extension 生命周期 | Framework Core | Host、Bootstrap、SessionCoordinator | `FrameworkCoreVerticalSmokeFixture` | P2 | contract_locked |
| F-003 Extension Manifest | Framework Core | Manifest Schema、注册校验 | `ExtensionManifestFixture` | P2 | contract_locked |
| F-004 Capability Broker | Framework Core | Capability registry/API | `CapabilityDiscoveryFixture` | P2 | contract_locked |
| F-005 依赖/循环/能力发现 | Framework Core | capability URI 协商和冲突诊断 | `CapabilityConflictFixture` | P2 | contract_locked |
| F-006 SDK 开发体验 | SDK | Reference DLL、XML、模板、双语文档 | `SdkPackageFixture` | P5 | planned |
| F-007 FakeHost / Test Kit | SDK | FakeHost、虚拟时钟、回放和断言 | `AwakeSdkSmokeFixture` | P5 | planned |
| F-008 Analyzer / Linter | SDK | `maf-lint` 扩展规则 | `AnalyzerRuleFixture` | P5 | planned |
| F-009 GameDataQuery | Framework Core | GameData DTO、分页和快照接口 | `GameDataSnapshotFixture` | P2 | contract_locked |
| F-010 EntityRef 与稳定身份 | Framework Core | EntityRef/稳定 ID Schema | `StableIdentityFixture` | P2 | contract_locked |
| F-011 数据可见性 Scope | Framework Core | PlayerKnown/ObservedHistory gate | `VisibilityScopeFixture` | P2 | contract_locked |
| F-012 Snapshot / 一致性 | Framework Core | Snapshot token、过期和 revalidation | `SnapshotExpiryFixture` | P2 | contract_locked |
| F-013 Context Provider / Planner | Framework Core | ContextContribution、Planner 和知识过滤器 | `ContextPlannerFixture` | P2 | contract_locked |
| F-014 Prompt Context 解释 | DevTools | included/excluded reason 诊断 DTO | `ContextExplainabilityFixture` | P2/P5 | planned |
| F-015 SQLite FTS5 RAG | Runtime Service | Service-owned SQLite/FTS5、collection、scope、fingerprint | `RagBoundedRetrievalFixture` | P3 | contract_locked |
| F-016 Embedding / Rerank | Runtime Service | Service-owned Adapter 和可选检索层 | `RagProviderFallbackFixture` | P3/A2 | planned |
| F-017 Prompt Registry | Runtime Service | Prompt registry、Schema、版本和 owner | `PromptRegistryFixture` | P3 | planned |
| F-018 AI Gateway | Runtime Service | Gateway、Route 和任务生命周期 | `AiGatewayRouteFixture` | P3 | planned |
| F-019 Connection / Model / Route | Runtime Service | Profile、Model、Route 解析和状态 | `ProviderProfileFixture` | P3/P4 | planned |
| F-020 Provider Capability Report | Runtime Service | Capability report 和等价性检查 | `ProviderCapabilityFixture` | P3 | planned |
| F-021 OpenAI-compatible Adapter | Runtime Service | 文本/工具/Embedding 适配器 | `OpenAiCompatibleFixture` | P3 | planned |
| F-022 Anthropic Adapter | Runtime Service | Messages/stream/tool 转换 | `AnthropicAdapterFixture` | P3 | planned |
| F-023 Ollama Adapter | Runtime Service | 本地模型列表和文本路由 | `OllamaAdapterFixture` | P3 | planned |
| F-024 Player2 Adapter | Runtime Service | 受版本门控的 Provider adapter | `Player2UnavailableFixture` | P3/A2 | deferred |
| F-025 ComfyUI Adapter | Runtime Service | Workflow descriptor、输出校验和取消 | `ComfyWorkflowFixture` | P6 | planned |
| F-026 Managed GGUF Adapter | Runtime Service | 托管本地模型能力报告 | `ManagedGgufUnavailableFixture` | P3/A2 | deferred |
| F-027 流式与取消 | Runtime Service | TextDelta/Completed/Cancelled 状态机 | `StreamingCancellationFixture` | P3 | contract_locked |
| F-028 重试、fallback、pinning | Runtime Service | 有界重试、路由切换和 pinning | `RouteFallbackFixture` | P3 | planned |
| F-029 配额与公平性 | Runtime Service | Route/扩展/Token/媒体预算 | `QuotaBackpressureFixture` | P3 | planned |
| F-030 Structured Output | Runtime Service | Schema 编译、校验和原文诊断 | `StructuredOutputFixture` | P3 | contract_locked |
| F-031 Tool Candidate 循环 | Framework Core | allowlist、轮次、风险和参数验证 | `ToolCandidateValidationFixture` | P3 | contract_locked |
| F-032 Runtime Event Bus | Runtime Service | 即时事件订阅和取消 | `RuntimeEventBusFixture` | P2 | planned |
| F-033 Durable Event Stream | Runtime Service | durable cursor、重放和 gap | `DurableEventReplayFixture` | P3 | planned |
| F-034 Durable Spool / Backpressure | Runtime Service | spool、采样、聚合和背压 | `DurableSpoolFixture` | P3 | planned |
| F-035 platform.db | Runtime Service | 新物理根 platform DB 与迁移 receipt | `PlatformStorageFixture` | P3 | planned |
| F-036 campaign.db | Runtime Service | campaign DB、namespace 和 timeline | `CampaignStorageFixture` | P3 | planned |
| F-037 Managed KV / Sidecar / Read View | Runtime Service | KV、sidecar、只读 view 权限 | `StorageOwnershipFixture` | P3 | planned |
| F-038 Save Anchor | Framework Core | `SaveAnchor` contract/schema/recovery + `AwakeFrameworkSaveBehavior` Native adapter | `SaveAnchorLifecycleFixture` | P2/P3 | contract_locked |
| F-039 Timeline Export/Import/Fork | Runtime Service | snapshot、hash、fork 和血缘 | `TimelineForkFixture` | P3/P5 | planned |
| F-040 R0-R3 Command Authority | Framework Core | CommandDescriptor、risk gate 和 adapter | `CommandRiskFixture` | P2/P3 | contract_locked |
| F-041 Preflight / Revalidation / Receipt | Framework Core | ledger、SettlementReceipt、幂等恢复 | `ReceiptRecoveryFixture` | P2/P3 | contract_locked |
| F-042 Named Pipe 协议 | Framework Core | handshake、envelope、sequence、checksum | `IpcHandshakeSequenceFixture` | P2/P3 | contract_locked |
| F-043 Session Invalidation | Framework Core | SessionLease、epoch/fence、late-result gate | `SessionInvalidationFixture` | P2/P3 | contract_locked |
| F-044 Service Process Management | Runtime Service | instance lock、drain、重启和升级隔离 | `ServiceLifecycleFixture` | P3/P5 | contract_locked |
| F-045 Management HTTP | Runtime Service | loopback、Bearer、CSRF、no-store | `ManagementHttpFixture` | P5 | planned |
| F-046 Credential Reference | AWAKE MCM | MCM credential-reference transaction + `ProtectedCredentialWriter`; Runtime Service CredentialBroker 组件契约 | `CredentialLifecycleFixture` | P3/P4 | contract_locked |
| F-047 Cloud Export Policy | Runtime Service | Field registry、Evaluator、EgressBroker | `PermissionOrderAndEgressFixture` | P3/P4 | contract_locked |
| F-048 离线与故障收缩 | Framework Core | typed degraded 状态和原版保留路径 | `DegradedModeFixture` | P2/P3 | contract_locked |
| F-049 AssetHandle / CAS | Runtime Service | CAS、opaque handle、quota/retention | `AssetHandleFixture` | P3/A2 | planned |
| F-050 Image Generation | Runtime Service | 图像路由、输出校验和资产落库 | `ImageGenerationUnavailableFixture` | P6/A2 | deferred |
| F-051 TTS / VoiceProfile | Runtime Service | TTS route、VoiceProfile 和 fallback | `TtsUnavailableFixture` | P6/A2 | deferred |
| F-052 Workflow / Asset Transform | Runtime Service | 受 Schema 约束的 transform | `AssetTransformFixture` | P6 | planned |
| F-053 Retention / Quarantine / Quota | Runtime Service | retention、隔离和配额清理 | `AssetRetentionFixture` | P6 | planned |
| F-054 MCM 快速配置 | AWAKE MCM | URL、可见 Key、模型、测试、保存应用 | `McmConfigTransactionFixture` | P4 | contract_locked |
| F-055 Gauntlet 诊断/控制台 | AWAKE MCM | 玩家状态页与必要确认面 | `McmStatusSurfaceFixture` | P4/P5 | planned |
| F-056 DevTools 管理与导出 | DevTools | 原始诊断、Prompt/Route/RAG 检查和脱敏导出 | `DevToolsExportFixture` | P5 | planned |
| F-057 Observability | Framework Core | correlation、health、latency、receipt 查询 | `ObservabilityCorrelationFixture` | P2/P5 | contract_locked |
| F-058 性能/背压/熔断 | Framework Core | 主线程预算、队列、熔断和恢复 | `PerformanceBackpressureFixture` | P2/P3 | contract_locked |
| F-059 CN/EN 本地化 | AWAKE | 双语稳定键、错误码和文档 | `LocalizationParityFixture` | P4/P5 | planned |
| F-060 包、许可证与来源 | Package | LICENSE、NOTICE、第三方清单和包 allowlist | `PackageProvenanceFixture` | P0/P7 | contract_locked |
| F-061 双版本兼容 | Build | v1.3.15/v1.4.8 隔离构建与包检查 | `DualApiBuildFixture` | P0/P7 | contract_locked |
| F-062 配置/存档/索引迁移 | Framework Core | MigrationManifest、backup、rollback、anchor 选择 | `StorageMigrationFixture` | P0.5/P1.5/P7 | contract_locked |
| F-063 NPC 对话与档案 | AWAKE Gameplay Adapter | NPC route/context/archive/取消和档案接线 | `NpcDialogueCapabilityFixture` | P6 | deferred |
| F-064 关系系统 | AWAKE Relationship Adapter | 原版关系只读投影；Proposal/AI 写回 deferred | `RelationshipProjectionFixture` | P6 | partial |
| F-065 外交分析 | AWAKE Diplomacy Adapter | 分析/报告只在 route/schema/审阅链完整后开放 | `DiplomacyCapabilityFixture` | P6/A2 | deferred |
| F-066 世界事件 | AWAKE World Event Adapter | 代码事实观察可用；AI 叙述/传播 deferred | `WorldEventObservationFixture` | P6 | partial |

## Supporting owners / dependencies

以下协作层不承担该 F-ID 的唯一最终验收责任；`primary_owner` 负责 artifact、fixture、失败语义和阶段退出。

| 能力 | supporting_owners |
| --- | --- |
| support:F-001 | Package |
| support:F-003 | SDK |
| support:F-011、F-013 | AWAKE |
| support:F-014 | Framework Core API |
| support:F-017 | AWAKE |
| support:F-019 | AWAKE MCM |
| support:F-027、F-030、F-037 | Framework Core API |
| support:F-032 | AWAKE |
| support:F-038、F-040、F-041 | AWAKE |
| support:F-042、F-043 | Runtime Service |
| support:F-044 | Bootstrap |
| support:F-045 | DevTools |
| support:F-046 | Runtime Service |
| support:F-047 | Framework Core; AWAKE |
| support:F-048 | Runtime Service |
| support:F-055 | DevTools |
| support:F-057 | Runtime Service; DevTools |
| support:F-058 | Runtime Service |
| support:F-059 | SDK |
| support:F-060 | Release |
| support:F-061 | SDK |
| support:F-062 | Runtime Service; AWAKE |

## 阶段退出规则

- 每个阶段必须同时提交本表对应 fixture 的结果文件、变更文件清单和未验证项；只有静态存在不能退出阶段。
- fixture 失败、owner 不唯一、artifact 未接线或 evidence 缺失时，阶段状态只能保持 `needs_review`/`blocked`，不得进入下一阶段。
- `contract_locked` 只表示契约字段和边界已经固定；它不表示 `E2`、`E3`、`E4` 或 `E5` 已通过。
- `partial`/`deferred` 能力必须在玩家界面显示可理解的状态，在 DevTools 中显示 `feature_id`、`reason_code`、`diagnostic_id` 和下一门槛。
