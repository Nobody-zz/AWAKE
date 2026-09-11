# Plan: Marcus-Awake 内置框架迁移（全功能继承版）

_Locked discovery baseline — 2026-08-24；本文件当前仍为契约审查基线，未授权代码迁移。_

全功能基线：`MARCUS-AWAKE-FULL-CAPABILITY-INVENTORY-v1-20260824.md`。
逐项归属与证据：`MARCUS-AWAKE-CAPABILITY-OWNERSHIP-MAP-v1-20260824.md`。

## Goal

将公开源码基础上的 Marcus 框架改造成 AWAKE 自己维护的独立 `Marcus-Awake AI Framework`：玩家只安装 AWAKE；游戏内框架核心、后台 AI Runtime Service、AWAKE MCM 玩家配置和开发者 DevTools 明确分层；不再依赖外部 `MarcusAIFramework` 模组；不把原有 NPC、关系、外交、世界事件示例扩展误装进框架核心。

## Current evidence

- AWAKE 当前 `AWAKE.csproj` 仍通过 `MarcusAIFramework_Reference\SDK_20260815\ref\v$(BannerlordApi)\MarcusAIFramework.dll` 编译引用外部框架 API。
- AWAKE 当前 `SubModule.xml` 仍声明 `MarcusAIFramework` 为外部依赖。
- 本地 AuthorSource 中的四个项目 `MarcusAIDiplomacy`、`MarcusAINpc`、`MarcusAIRelationships`、`MarcusAIWorldEvents` 是独立玩法/示例扩展，不是框架核心。
- 原框架文档把游戏 Runtime、后台 Companion、管理页、SDK 和示例扩展放在同一发布叙事中，迁移前必须先拆层。
- 公开仓库采用 Apache-2.0；迁移时必须保留 LICENSE、NOTICE、版权与修改说明，并锁定来源 commit。
- Marcus 的全部框架能力已逐项纳入全功能清单：游戏 Runtime、SDK/Capability、GameData、Events、Storage、Timeline、Command、Gateway、六类 Adapter、Prompt/Structured Output、RAG、IPC、凭据、媒体/CAS、UI、DevTools、观测、双版本和迁移。
- 四个 AuthorSource 参考扩展的全部业务功能也纳入 AWAKE 适配验收，但不原样并入 Framework Core；它们是功能行为参考与纵向切片，不是通用框架边界。
- RAG 的物理实现和数据库 owner 已明确归 Runtime Service；Framework Core 只提供 API/IPC/权限/预算，AWAKE 只拥有世界书语义和知识权限。

## Selected architecture

1. **Game Runtime Core**：独立程序集 `MarcusAwakeFramework.dll`，只承载游戏内 Host、API、权限、Context、Command、Event、Storage anchor、UI bridge 和 IPC client。
2. **AI Runtime Service**：独立后台进程，负责 Provider、Route、Prompt、请求生命周期、IPC server、RAG/SQLite、Timeline、Assets/Media；不提供玩家主配置界面。
3. **AWAKE MCM**：玩家配置 API URL、API Key、模型拉取、模型选择、联通测试、保存应用和简化状态。
4. **DevTools**：独立开发者工具，负责原始日志、Prompt/Route/Provider/Token/Worker/Storage/Timeline/世界书诊断和导出。
5. **SDK**：独立开发资源，不进入玩家发布包；四个 Marcus 示例扩展也不进入框架核心。

## Migration phases

### Phase 0 — Source and license freeze

- 记录公开仓库 commit、Apache-2.0 LICENSE、NOTICE、第三方清单。
- 将公开源码归档为 AWAKE 的上游快照，不直接跟随 main。
- 生成 source inventory 和程序集/目录白名单。
- 明确 `BannerlordApi=1.3.15` 为当前可验证目标；1.4.8 只有在本机匹配游戏根和引用集存在时才构建。

**验证：**源码清单、commit、许可证、文件白名单可复核；无运行时代码修改。

### Phase 0.5 — Full capability and evidence inventory

- 以全功能清单 F-001–F-066 为迁移基线，不再只按“核心能力摘要”安排批次。
- 为每个能力补齐 owner、API/Protocol/Schema/Storage 边界、失败降级、版本要求和证据等级。
- 将“框架能力”“AWAKE 玩法适配”“玩家 MCM”“DevTools”“SDK”“媒体生态”分开列入允许写集。
- 将四个参考扩展的功能作为 NPC、关系、外交、世界事件的纵向验收场景登记，不把它们误标为框架缺失。

**验证：**F-001–F-066 无未归属项；每项均有阶段、证据和非目标；未修改运行时代码。

### Phase 1.5 — API, protocol, storage and failure contract lock

- 冻结 `MarcusAwakeFramework.Api`、程序集加载、Named Pipe envelope/handshake、Service 配置协议和独立物理存储根。
- 冻结版本轴、session invalidation、deadline/cancellation、typed error、permission、cloud export policy 和 command receipt。
- 为六类 Provider、媒体 AssetHandle、Prompt/Structured Output、RAG、Timeline 和 DevTools 定义可演进 Schema。
- 冻结 Bootstrap 信任根、Pipe ACL、instance epoch/fence、Service 生命周期状态机、Save anchor/权威矩阵、MigrationManifest、Save barrier 和 CredentialBroker 唯一路径。
- 冻结 F-063–F-066 的当前 `deferred/partial` 状态，并把现有 AWAKE 路由/命令的阻断门写入迁移验收：F-063/F-065 deferred，F-064/F-066 partial。
- 建立“能力存在但暂不开放”的降级语义，禁止用未实现或不可用伪装成成功。
- 固定 `FrameworkCoreVerticalSmokeFixture`，并建立 F-001–F-066 的 owner、artifact、fixture、phase 映射。
- 明确 Framework Core Migration Baseline、AWAKE Gameplay Baseline 和 Full Capability Completion 三种验收口径，避免把框架首版与游戏完整首版混为一谈。

**验证：**API surface、协议 fixture、信任根/ACL、Service 状态机、Save anchor/迁移、CredentialBroker、RAG ownership、三层权限顺序、Schema 兼容、失败矩阵、存储隔离、ownership map 和外部 Marcus 冲突策略通过独立只读审查；未修改运行时代码。

### Phase 1 — Framework Core extraction

- 从公开源码抽取游戏内框架核心，但首批只建立独立项目骨架和公共 API，不接入当前 `Awake.dll`。
- 建立 `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/MarcusAwakeFramework.csproj` 和独立程序集身份。
- 将公共 API 命名空间迁移到 `MarcusAwakeFramework.Api`。
- 不保留旧 `MarcusAIFramework.Api` 运行时兼容；如需要迁移期编译辅助，只能存在于临时测试/参考项目，不得打进玩家包或被 SubModule 加载。
- 建立独立框架测试、FakeHost/FakeService 和 API surface 检查；不修改 `AWAKE.csproj`、`SubModule.xml` 或现有 `src/**/*.cs`。
- 本阶段严格只覆盖 P1.5 契约 §9.7 固定的 F-ID 集合：F-001/F-002/F-003/F-004/F-005/F-009/F-010/F-011/F-012/F-013/F-038/F-040/F-041/F-042/F-043/F-048/F-057/F-058；F-006–F-008/F-014/F-032/F-059–F-061 不属于本次 P1 文件写集，后续按 ownership map 阶段处理。

**验证：**Framework Core 独立 Release build、API surface diff、FakeHost/FakeService 静态 fixture 和 P1 文件 allowlist；不要求 AWAKE 编译，不移除外部依赖。

### Phase 2 — Framework Core vertical integration

- 在 P1 独立框架通过后，建立新的 P2 写集，接入 Host/Bootstrap、SessionLease、RequestContext、IPC/health 和 `FrameworkProbeReceipt`。
- 执行 `FrameworkCoreVerticalSmokeFixture` E1：`SubModule → Bootstrap/Host → SessionReady → SessionLease → RequestContext → typed result → FrameworkProbeReceipt/诊断`。
- 是否修改 `AWAKE.csproj`、`SubModule.xml` 或现有 `src/**/*.cs`，必须由新的 P2 checkpoint 明确列出；P1.5 批准不自动授权这些修改。

**验证：**E1 FakeHost/FakeService smoke、无阻塞等待、无 `CancellationToken.None` 逃逸、session stale/取消/typed error/诊断证据。

### Phase 3 — AI Runtime Service extraction

- 将 Provider、Route、Prompt、IPC、Storage/RAG/Timeline/Assets/Media 从历史 Companion 目录中整理为 AI Runtime Service。
- 统一后台服务名称、安装目录、配置协议和日志目录。
- 服务不再承担玩家 UI；玩家配置通过 AWAKE MCM 进入统一配置接口。
- 保留管理协议给 DevTools，但不把管理页当作玩家必经路径。
- 覆盖全功能清单中的 F-015–F-031、F-033–F-039、F-042–F-053；暂不可用的 Provider/媒体能力必须返回 typed unavailable/degraded。

**验证：**服务独立启动/停止、IPC 契约、Provider profile apply、模型拉取和连接测试离线/本地 loopback 测试。

### Phase 4 — AWAKE MCM player configuration

- 在 AWAKE MCM 建立中文优先、一步一步的 AI 配置页。
- API Key 编辑时可见，提供显示/隐藏切换；保存后默认掩码。
- 支持 URL、Key、拉取模型、选择模型、手动模型回退、测试连接、保存并应用。
- 所有网络/IPC 操作异步，不在 MCM 回调或游戏 tick 中阻塞。
- 明确错误：地址不可达、Key 无效、模型不存在、服务未启动、Provider 返回错误。

**验证：**MCM seam tests、脱敏测试、重复点击/取消/超时测试、模型列表和连接测试契约。

### Phase 5 — DevTools separation

- 建立 `Marcus-Awake DevTools` 独立入口。
- 迁移详细日志、Provider 请求诊断、Prompt/Route/Schema、Token、Worker、Storage/RAG/Timeline、世界书检查和导出。
- 诊断包脱敏；API Key、Authorization header、完整凭据和敏感路径不得进入导出。
- 玩家 MCM 只保留简化状态和诊断包导出，不展示原始内容。

**验证：**日志脱敏、导出包白名单、开发者工作流 smoke；不纳入玩家 MCM。

### Phase 6 — Remove external dependency

- AWAKE 工程改为引用 `MarcusAwakeFramework`。
- `SubModule.xml` 移除外部 `MarcusAIFramework` 依赖；不创建伪 Framework SubModule，`MarcusAwakeFramework.dll` 由 AWAKE 唯一 SubModule 伴随加载。
- 发布包改为 AWAKE 单包双程序集，并随包携带匹配 AI Runtime Service。
- 对已有外部 Marcus 安装做冲突检测和明确提示；第一阶段不提供双轨运行。

**验证：**干净环境安装测试、外部 Marcus 存在/不存在测试、程序集加载唯一性、SubModule 解析、包白名单和哈希。

### Phase 7 — AWAKE integration regression

- 迁移当前 AWAKE 所有 `using MarcusAIFramework.Api` 和 Host/Route/Storage/Prompt 调用。
- 重新运行 AWAKE Release、SdkSmoke、世界书 Runtime Smoke、MCM seam tests。
- 检查现有 B1/B2 等批次不被框架迁移误改。
- 不在本迁移批次内实现 NPC 世界知识、关系写回或旧四版世界书迁移。
- 参考扩展的功能不直接复制；按 F-063–F-066 重构为 AWAKE 自己的 NPC、关系、外交和世界事件适配层。
- 在上述适配尚未形成完整调用→结算路径前，现有 NPC Route、关系/世界效果命令按契约状态阻断：F-063/F-065 使用 `deferred + feature_enabled=false`，F-064/F-066 仅开放已声明的 `partial` 只读关系投影或代码事实观察；不得用 `disabled` 作为未接线状态，也不得出现注册成功但仍可实际外发/写回的双重语义。

**验证：**入口→调用→结果闭环、0 warnings/0 errors、focused smoke、包哈希和未验证项报告。

## Phase exit gates

- P0/P0.5：来源、许可证、配置/存档/Storage 盘点齐全；失败则不改入口。
- P1.5：契约、ownership map、统一 fixtures 齐全，并取得独立只读审查 `VERDICT: APPROVED`；失败不得建立代码写集。
- P2：`FrameworkCoreVerticalSmokeFixture` 通过，证明 `SubModule → Bootstrap/Host → SessionReady → SessionLease → RequestContext → typed result → receipt/诊断`；失败不得进入 Service 迁移。
- P3：Service lifecycle、RAG ownership、Credential/Egress、IPC/Storage fixtures 通过；失败保持离线降级。
- P4/P5：MCM 事务、DevTools/SDK、Analyzer、脱敏导出和包检查通过；失败不宣称玩家配置或作者工具完成。
- P6：AWAKE 适配逐项 fixture 通过，明确 F-063/F-065 deferred、F-064/F-066 partial；失败不宣称完整游戏首版。
- P7：构建、同步、哈希以及用户提供的 E4/E5 证据齐全；缺失时只能报告离线验证。

任何阶段的 fixture、artifact、owner 或 evidence 缺失，都将阶段保留在 `needs_review`/`blocked`，不得用下一阶段静态文件反向补齐上一阶段退出门。

## Key boundaries

- 框架核心不拥有 AWAKE 世界书、NPC 玩法、成人内容、关系规则或世界事件业务。
- AI Runtime Service 不拥有玩家 UI；AWAKE MCM 是玩家 AI 配置入口。
- DevTools 不成为游戏运行依赖；它是作者维护与诊断工具。
- Companion 作为旧统称逐步淘汰；正式名称按职责拆为 `Marcus-Awake Framework Runtime`、`Marcus-Awake AI Runtime Service`、`Marcus-Awake DevTools`。
- 不做旧 `MarcusAIFramework.Api` 的永久运行时兼容，不做外部/内置双轨。
- 不把 SDK、示例扩展、历史构建产物和第三方游戏 DLL 放入玩家发布包。
- “不放入玩家包/不进入 Framework Core”不代表不继承：对应功能必须在全功能清单中有契约、阶段和验证记录。

## Risks / open questions

- 公开仓库当前 main 的真实源码版本、commit 与本地参考快照可能不同，必须先锁定来源再迁移。
- 现有 AWAKE API 调用规模较大，命名空间改名会涉及大量文件和测试。
- Bannerlord v1.3.15 可验证；v1.4.8 本机是否具备匹配游戏根仍需单独确认。
- MCM API Key 与 Runtime 配置的权威边界已由 P1.5 契约冻结；仍需以当前 Bannerlord/MCM API 证据选择具体实现，不得另建第二套凭据、网络或配置权威。
- 外部 Marcus 与内置框架同时存在时的冲突检测、迁移提示和旧存档处理需要独立验收。
- 现有框架文档和用户新决策存在冲突时，以本计划中已确认的 AWAKE 玩家体验为准，但必须在实现前更新旧文档引用。

## External review corrections

独立只读审查于 2026-08-24 连续多轮返回 `VERDICT: REVISE`。下一轮复审前必须完成以下修订：

1. Named Pipe ACL、对端身份、用户 SID、父进程关系、session nonce 和防重放。
2. `idempotency_key`、sequence、dedupe window、ACK（`accepted/durably_recorded`）、ProgressEvent、业务 Receipt 和晚到终态冲突。
3. `credential_ref` 的绑定 scope、轮换、撤销、删除、重启恢复和 Service 读取边界。
4. Service instance lock、崩溃重启上限、孤儿清理、停止 drain、升级隔离和启动/停止权限。
5. 旧 DB/新物理根/Save anchor 的显式迁移事务、备份、fingerprint、冲突和幂等回退。
6. 文本、工具、Embedding、Rerank、图像、TTS、诊断和错误原文统一经过唯一 `ExportPolicyEvaluator`。
7. F-063–F-066 的 deferred contract、`feature_enabled=false` 默认值、未接线诊断和最小 fixture。
8. Epoch/Fence Authority、Receipt 恢复矩阵、Native Save/Session 事件时序和迁移 commit marker。
9. CredentialBroker/EgressBroker 的唯一 owner/传输路径、静态边界和绕过拒绝。

对应实现契约见 `MARCUS-AWAKE-P1.5-CONTRACT-LOCK-DRAFT-20260824.md`；任何一轮 `REVISE` 不能作为代码迁移批准。

## Out of scope

- 不迁移四个玩法示例扩展为 AWAKE 核心。
- 不重做 AWAKE 世界书编辑器。
- 不在本计划内实现 B2 世界知识查询硬门。
- 不修改游戏目录、不启动 Bannerlord、不改变冻结候选。
- 不自动删除用户已有的外部 Marcus 安装。
- 不把 Companion/DevTools 的原始日志、Prompt、Token 或 Provider DTO 直接暴露给普通玩家。

## Gate before implementation

本文件当前是规划基线。正式写代码前仍需：

1. 用户确认本拆分矩阵、全功能清单和迁移阶段顺序；
2. 独立只读对抗审查返回 `VERDICT: APPROVED`；
3. 建立 Phase 0 独立 checkpoint 和允许写集；
4. 先做源码/许可证/程序集清单，不能直接改 `AWAKE.csproj` 或 `SubModule.xml`。
