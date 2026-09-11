# AWAKE 对话链路修复立项（010）— 2026-09-11

## 修订记录

- v1 2026-09-11：初稿（按 A/B/C 对比结论立项）。
- v2 2026-09-11：round 1 独立只读审查判定 `REVISE` 后修订。改动：D-5 重写（AWAKE 无法实现 internal 接口）；删除无可实现机制的结论（v1 的 D-2「上游优先」、D-3「CanDegrade 参与放行」）；D-4 同意默认值由「默认开启」收紧为「默认拒绝 + 显式授权」；F-8 事实定性修正（非「内容未产出」，而是「现有 v1 包被 v2 加载器整包拒绝」）；新增 D-9（最小存储并入本批）以消除验收与边界的矛盾；新增 F-10/F-11 与「会改变的调用点语义」章节。
- v3 2026-09-11：round 2 独立只读审查判定 `REVISE` 后修订。改动：F-9 事实定性修正（「曾到达 submit」≠「回合跑通」，实测 `turn_completed` 为 0）；**D-9 前提错误更正**（Runtime 侧存储后端已接线，见 F-13）；新增 F-14（世界书不可用会直接跳过 `SubmitAsync`，是实机「说不出话」的第一硬门）与 F-15（对话转录成功无正向日志）；D-2 保护面说明更正；验收 2 补 `verify_marcus_awake_mcm_contract.ps1`；验收 3 补前置条件并去掉不可观测项；「做」清单补转录正向日志与**世界书解封**（待用户选定方案，见 D-11）；风险节补 Runtime Provider 健康依赖与存储权威归属。
- v4 2026-09-11：用户否决「以旧格式兼容换取全量可用」（原话：「可用归可用，但是不能喧宾夺主，不然不是白更新了吗」）。据此**删除 A 案**，D-11 收窄为 B 案（worldbookstudio 出 v2 包）；新增 D-12「喧宾夺主红线」把该约束变成可验证门禁；新增 D-13 前置验证（先离线证明 studio 能出包，否则停下重议，不得静默退回 A）。代价如实记录：本批交付后**只有试点 NPC 会说话**，其余仍走「我没有可靠的说法。」。
- v5 2026-09-11：round 3 独立只读审查判定 `REVISE`（六项），全部修正。要点：验收 3 与契约的「出现 NPC 回复」**限定为试点包覆盖的目标 NPC**（原先无限定，与 D-12 代价正面冲突）；新增 **D-14 世界书投放与同步口径**（`sync_module.ps1` 会把 `ModuleData\Worldbook\manifest.json` 当受管文件从工程覆盖到游戏，会撤销试点包并触发 `Assert-TargetVerified` 哈希不符）；D-9 的「标记未启用」**降级为文档/注释声明**并点明真实隔离机制是客户端能力集（`RuntimeServiceClient.cs:23-34` 默认不含 `storage.*`/`rag.*`）；D-12 三条门禁改为可判定（运行日志对 registry.v1/v2 两分支共用，不能用作判据）；清理 v3/v4 残留措辞。
- v6 2026-09-11：用户确认「先做小样」（worldbookstudio 自身仍在修，不依赖它）。**D-13 前置验证已实际完成**：不改 studio、不改 mod 代码，新增独立自建出包器 `tools/worldbook-pilot-package/`（`<Compile Include>` mod 侧 `WorldbookPackageIntegrity` / `WorldKnowledgeLoader` / `WorldKnowledgeQueryService`，与游戏加载语义同源，stub 复用 `worldbook-runtime-smoke\SmokeStubs.cs`），产出最小 v2 试点包并通过全部校验（重跑哈希一致）。据此刻意更正 **D-11 的产出方口径**：本批试点包由**自建出包器**产出，**studio 仍是正式内容的产出方**，修复后由正式产物替换（校验口径不变）；**A 案（mod 内运行时翻译）仍保持删除**。代价与红线（D-12）不变。证据：`docs/evidence/AWAKE-DIALOGUE-CHAIN-010-PILOT-PACKAGE-20260911.json`。
- v7 2026-09-11：用户下令「都做」（= 按 v6 冻结方案实现 + 批准 D-14）。**本批已全部落盘并完成离线验证**：框架公开注入入口、AWAKE 四个真实现、MCM 显式授权开关、可观测性修复、公共 API 基线重生成、`sync_module.ps1 -SkipWorldbook`、红测（验收 1 ①②③④⑤ + 框架侧⑥）、离线回归全绿、构建 / 打包 / 同步完成、试点包已投放游戏目录并就地验证通过。BuildId 另立为 `awake-20260911-dialogue-chain-010`（D-8），工程版本仍 `v0.2.0`。执行结果与实测数值见下方「执行结果（2026-09-11）」，证据 `docs/evidence/AWAKE-DIALOGUE-CHAIN-010-DEPLOY-20260911.json`，构建/同步记录见 `BUILD_VERIFICATION.txt` 末节。**唯一未完成项：实机验证（需用户跑游戏）与用户签收。**

## 立项结论

- 目标：把「NPC 对话」从「面板能开、说不出话、也不报错」修到**能真正发出 AI 请求、产出回复并完成结算**。
- 触发：用户 2026-09-11 实机反馈（BuildId `awake-20260911-health-contract-009`；断点链见 F-1 / F-4）。
- 风险等级：`high-risk`（新增框架公共契约 + 权限语义 + 跨模块状态 + 新持久化位置），通道 `grill-me-codex`，状态文件 `docs/review-state/AWAKE-DIALOGUE-CHAIN-010-20260911.review.json`，`max_rounds=3`。

## 事实基础（离线核实；含 round 1 / round 2 复核）

| # | 事实 | 证据 |
|---|---|---|
| F-1 | 权限服务在进程内不可用，`host.Permissions` 恒拒 | `framework/MarcusAwakeFramework/src/HostApi.cs:77`、`:1079`、`:1089`；实机 `host.permissions.unavailable` |
| F-2 | 宿主缺 GameData / Prompts / Storage / Rag 真实现 | 同文件 `:1020`、`:1045`、`:1051`、`:1057` |
| F-3 | `CanDegrade` 定义了但全仓无消费者 | `src/PermissionCatalog.cs:45`；`src/PermissionGate.cs` 仅搬运（`:348`），无放行判定 |
| F-4 | 初始化在存储门失败处静默 return，无日志无 UI 状态 | `src/NpcDialogueService.cs:413`（`:481` 更丢弃返回值） |
| F-5 | 绑定失败文案错配为「对话已结束。」 | 同文件 `:472` |
| F-6 | Runtime 侧存储 / RAG 后端已存在，但游戏侧未装配 | `framework/MarcusAwakeStorage/src/SqliteStorageAndRagBackend.cs:64`（仅 `MarcusAwakeRuntimeService/src/RuntimeServiceHost.cs:108` 实例化）；IPC 消息 `MarcusAwakeTransport/src/ProtocolModels.cs:76-84`；客户端 `MarcusAwakeFramework/src/RuntimeServiceClient.cs:110` 未实现 IStorageService/IRagService |
| F-7 | AWAKE 侧对五个服务的调用点共 12 处 | Permissions 2 / GameData 3 / Prompts 3 / Rag 3 / Storage 1 |
| F-8 | **现有世界书包被整包拒绝**（非「内容未产出」） | 游戏目录与工程 `ModuleData/Worldbook/manifest.json` 实测 `"schemaVersion": "awake.worldbook.v1"`；`src/WorldbookRuntime.cs:110` 只接受 `awake.worldbook.registry.v1` 或 `awake.worldbook.v2`，否则 `throw WB2-SCHEMA-UNSUPPORTED:entry` |
| F-9 | 该链路**曾到达 submit，但从未完成过一个回合**（不是「完整跑通」） | `Awake.log` 2026-08-16/17 有 `permission_gate_evaluate ... Granted`、`player_hero_bound`、`npc_dialogue_ready`、`ai_task_submit_accepted`（13 次）；但 `turn_completed` 实测 **0 次**，仅 `npc_dialogue_turn_failed code=ai.cloud_export_denied`（2 次）与 `code=ai.output_schema_invalid`（2 次） |
| F-10 | 公共 API 面有逐字门禁 | `tools/verify_marcus_awake_api_layers.ps1:397` 要求与基线（含方法/属性/字段）完全一致；相位映射在 `:283`；基线文件 `docs/evidence/MARCUS-AWAKE-P3-API-SURFACE-BASELINE-CURRENT-20260829.json` |
| F-11 | 当前玩家数据的出口是 **internal**，AWAKE 无法实现 | `framework/MarcusAwakeFramework/src/ApiCompatibilityExtensions.cs:15` `internal interface ICompatibilityGameDataService`；`:49-59` 以类型转换取用；`src/TestAccess.cs:3` 仅对框架测试开放 IVT。该阻塞已于 2026-09-08 记录：`_houkai_merge/AWAKE.Tests/RedtestR1Behavioral.cs:85` |
| F-12 | `npc_dialogue_ready` 与结算都在存储门之后 | 唯一产出点 `src/NpcDialogueService.cs:434`，位于存储门 `:413` 之后；转录 `src/AwakeTranscriptService.cs:52-61`、关系结算 `src/NpcDialogueService.cs:1380-1387` 同样依赖存储 |
| F-13 | **Runtime 侧存储后端是「已接线」状态**（更正 v2 的 D-9 前提） | `framework/MarcusAwakeRuntimeService/src/RuntimeServiceHost.cs:108` 实例化 `SqliteStorageAndRagBackend`；`:1501` `HandleStorageBusinessAsync` 已把 `storage.kv_*` / `rag.*` 派发到 `:1538` `storageBackend.ExecuteBusinessAsync(...)`。真正未接线的是**框架客户端**（F-6） |
| F-14 | **世界书不可用会直接跳过 `_gateway.SubmitAsync`**，是实机「说不出话」的第一硬门 | `src/NpcDialogueService.cs:939` `IWorldKnowledgeQuery worldbook = WorldbookRuntime.Knowledge;`；v2 包被拒（F-8）→ `:961-970` 置 `Blocked` → `:1021-1024` 返回空 prompt → `:220-223` `CompleteDirectKnowledgeTurn(...)`，**永不调用** `_gateway.SubmitAsync`。判据在 `WorldKnowledgeModels.cs:136-143`（需 `State ∈ {known,partial}` 且 `RetrievedText` 非空） |
| F-15 | 对话转录**成功路径无正向日志**，验收不可观测 | `src/AwakeTranscriptService.cs:92-104` `AppendTurnAsync` 成功时直接 `return`，不写 `AwakeLog`；全仓 `transcript` 相关正向日志只有 `:160` 的 `source=letter` 一条，失败侧才有 `:59 transcript_turn_rejected` 与 `WorldStateStore.cs:1631` |
| F-16 | 世界书加载是**重构回退**，不是「内容未产出」 | `Awake.log`：2026-08-17～08-23 `worldbook_runtime_initialized rules=334 personas=415 warnings=0`（可加载）；2026-09-10 起全部变为 `worldbook_runtime_init_error error=WB2-SCHEMA-UNSUPPORTED:entry`。同一份内容，换加载器后整包被拒 |

## 本批边界

### 做（✅ 8/8 已执行，实测结果见下方「执行结果（2026-09-11）」）

1. 框架**新增**公开服务注入入口（新增重载 / 服务包类型），**不改现有构造函数签名**，且**默认组合路径保持 Unavailable 存根**（硬约束，见 D-2）。
2. 框架新增**公开**的玩家数据提供者契约，由框架内部适配 internal `ICompatibilityGameDataService`（见 D-5）。
3. AWAKE 侧新增三个真实现并注入：`IPermissionService`（本地权限策略）、玩家数据 provider（读 Bannerlord 主角快照）、`IPromptRegistry`（本地登记 + 渲染）。
4. **最小存储装配**：游戏侧文件型 `IStorageService`（见 D-9）。
5. 可观测性修复：初始化 / 绑定失败必须写日志并推 UI 状态；禁止静默 return；修正 F-5 文案错配；**补对话转录成功正向日志**（F-15，否则验收 3 不可观测）。
6. **世界书解封（只走正式格式）**：让 `WorldbookRuntime.Knowledge` 恢复可用，且**只能通过 v2 包**（D-11 / D-12）。做法：D-13 自建出包器已产出最小 v2 试点包（见 D-13 记录），按 D-14 投放（独立源目录 + `sync_module.ps1 -SkipWorldbook`）验收。
7. 公共 API 基线重生成与相位登记（见 D-10）。
8. 红测 + 离线回归 + 实机证据。

### 不做（明确留出）

- Runtime IPC 存储 / RAG 接线（`storage.*`、`rag.*` 能力协商）→ 011 评估；本批**不动** IPC 能力协商，避免扰动 009 刚修好的连接。
- **v1 扁平格式兼容层（原 A 案）→ 明确不做**。理由：见 D-12「喧宾夺主红线」——它会把淘汰的后备版内容推成玩家实际体验的主体，正式 v2 包上线后体验与兼容层同时作废。仓库既有规则「存在但未调用按 P0」同样禁止留一条不接线的兼容分支。
- 世界书**正式内容**的产出与大规模迁移 → 内容线（worldbookstudio）。本批只做 D-11 的**最小 v2 试点包**，不产出正式内容，不改四档文风口径。
- `native_readiness status=Failed code=native_probe_exception` NRE → 既有风险，另案。
- 启动器退出崩溃（`Bannerlord.BLSE.LauncherEx.exe` 0xc0000005）→ 另案。
- 不改存档格式、不改 `SubModule.xml` 版本号（仍 `v0.2.0`）。

## 决策锁定

| # | 决策点 | 结论 |
|---|---|---|
| D-1 | 入口形态 | 框架新增服务注入重载并保留旧签名；AWAKE 在 `src/AwakeHostComposition.cs:68` 注册处传入实现。**12 个调用点一律不改**。 |
| D-2 | 默认路径硬约束 | 服务包参数为 `null` 即「未提供」；框架**不做**任何「上游探测 / 优先级」逻辑。`FrameworkHost.CreateDefaultHost` 与 `FrameworkHostLocator.Register(extension[, runtime])` 的默认装配**必须继续返回 Unavailable 服务**。更正：`framework/MarcusAwakeFramework/tests/Program.cs:401` 只断言默认 host 的 **AI gateway** 存根（`host.ai_gateway.unavailable`），**不覆盖** permissions / prompts / storage / rag 的默认态 → 本批必须**新增**这四项的默认存根断言（否则「默认路径未被污染」无证据）。 |
| D-3 | 权限判定位置 | Soft / Hard 判定发生在**注入的 `IPermissionService` 实现内部**（AWAKE 侧）。`PermissionGate.CanDegrade` 保持现状（仅记录），本批不消费，也不再声称它会参与放行。权限失败码会从 `host.permissions.unavailable` / `Unavailable` 变为 `awake.permission_denied` / `Denied` 或 `Granted`（见下节清单）。 |
| D-4 | 同意默认值 | 云外发（`ai.cloud_export:*`）与路由（`ai.route.invoke:*`）**默认拒绝**：新增一个必须由用户主动开启的显式授权项；既有 `EnableCloudExport` / `AllowCloudExportPlayerState` 作为第二道开关，两者皆开才放行；MCM 不可读时按**拒绝**处理，禁止回落默认 `true`；每次放行写审计日志（route / classification / provider）。该新增项属公共配置面。 |
| D-5 | 玩家数据出口 | 由**框架侧公开发布**：新增 public provider（接口或委托重载），框架内部把它适配成 internal `ICompatibilityGameDataService`；AWAKE 只提供 provider 实例。**不修改 internal 接口可见性、不使用 InternalsVisibleTo 绕过。** 字段映射写死：`PlayerSnapshotDto.Hero.Id = new EntityRef("hero", Hero.MainHero.StringId)`，Clan / Kingdom 取 `Hero.MainHero.Clan`（可能为 null → 留空字段），`SnapshotToken` 取会话内稳定串；正向断言 `AwakeRuntime.CurrentHeroId == Hero.MainHero.StringId`。 |
| D-6 | 提示词实现 | 本地登记 + 本地渲染，渲染结果仍受 `NpcDialoguePromptPipeline.EnsureBudget` 的 32KB 预算约束。 |
| D-7 | 失败可观测 | 所有初始化 / 绑定 / 权限失败路径写 `AwakeLog` 并推 UI 状态；静默 return 视为缺陷。 |
| D-8 | 版本 | `v0.2.0` 不变；BuildId 另立。 |
| D-9 | 最小存储（**两处前提已更正**） | 本批纳入：新增游戏侧文件型 `IStorageService`（每个命名空间一个 JSON 文件，落模块 `PlayerExports\AwakeState\<campaign>\<namespace>.json`），复用既有 `IKeyValueStore` 语义与 `WorldStateStore.cs:375` 这一处唯一入口。**更正 v2 的错误前提①**：Runtime 侧 SQLite/RAG 后端**已接线**（F-13），并非「无调用方」。**更正 v2 的错误前提②（round 3）**：v3/v4 写「在 `RuntimeServiceHost.cs:108` 写 `storage_backend_disabled` 启动日志即标记数据面未启用」——**这是过度包装**：该日志不构成任何门禁（`storageBackend` 仍被赋值、`:1501/:1538` 仍可派发），且运行时经 `Console.Error` 输出后由 `RuntimeServiceClient.cs:1331-1341` **丢弃**，不进 `Awake.log`，**不可观测**。据实降级为：(a) 真实隔离机制 = **客户端能力集**——`RuntimeServiceClient.cs:23-34` 默认能力只含 `health/echo/cancel/diagnostic` + `provider.*`，**不含** `storage.*` / `rag.*`；本批不动该能力集，也不改 IPC 能力协商；(b) 本批只做 **文档声明 + 代码注释**（权威 = 游戏侧文件 KV；Runtime 存储数据面因能力集不含而未启用；011 决策点）。不新增无效日志，不宣称已生效门禁。 |
| D-10 | 公共 API 基线 | 本批必须重新生成 `docs/evidence/MARCUS-AWAKE-P3-API-SURFACE-BASELINE-CURRENT-20260829.json`，并在 `tools/verify_marcus_awake_api_layers.ps1:283` 的相位映射登记新增公共类型 / 成员；`verify_marcus_awake_api_layers.ps1` 与 `verify_marcus_awake_caller_contract.ps1` 必须 PASS。若 `_houkai_merge/MarcusAIFramework_Reference/设计大纲/13_api_contract_catalog.md` 确为权威面，同步回写新增公开契约。 |
| D-11 | **世界书解封方式（已定案：B，v6 更正产出方口径）** | 不做则 F-14 生效，实机仍「说不出话」。背景：v1 扁平格式与 v2 包**内容模型不同**（v1 = `keywords` + `variants[].When{roles,cultures,kingdomIds,...}` + `textMappings`；v2 = `keywords` + `expressions[].grants[{identity_id, conditions{...}}]` + `identities` 注册表），因此旧内容要用就必须翻译一次。**定案：翻译离线做，mod 运行时代码不动**（v2 本就是原生支持路径）。**v6 更正产出方口径**：worldbookstudio 自身仍在更新修复（用户 2026-09-11），故本批**不依赖 studio**，改用**自建最小出包器** `tools/worldbook-pilot-package/` 产出试点包（与 studio 同格式：`runtime.json` + `index.json` + `manifest.json`）；**studio 仍是正式内容的产出方**，修复后由其产物替换试点包，替换时校验口径与验收 4 完全不变（只需重跑 `WorldbookPackageIntegrity.ReadAndVerify` + schema 校验）。**A 案（mod 内运行时翻译）已删除，理由见 D-12。** |
| D-12 | **喧宾夺主红线（可判定版）** | 用户裁定：旧格式兼容「可用归可用，但不能喧宾夺主，否则等于白更新」。三条判据（均可 grep / 实测）：(1) **不得新增接受 v1 的代码分支**——判据：本批 diff 中 `src/` 不出现新的 `awake.worldbook.v1` 判断，不新增 legacy 加载日志，不新增 `WorldbookLoader.LoadDirectory` 调用点（**注意**：`WorldbookLoader.cs:168/:439`、`WorldbookModels.cs:82` 的既有字面量属未接线死代码，本批不删也不复用）；(2) **部署的 manifest 顶层 `schemaVersion` 实测为 `awake.worldbook.v2`**——判据用文件实测而非运行日志，因为 `src/WorldbookRuntime.cs:115` 对 `registry.v1`（`:92`）与 `v2`（`:99`）两条成功分支写的是**同一条**日志，日志不能作为判据；(3) **唯一权威数据源**——判据：全仓 `WorldKnowledgeQueryService` 的构造点只有 `WorldbookRuntime.cs:113` 一处，`_knowledge` 仅由 `WorldbookRuntime` 持有。补充：仓库既有规则「存在但未调用按 P0」同样禁止留一条不接线的兼容实现。 |
| D-13 | **前置验证（✅ 已完成，v6 结案）** | **已完成 2026-09-11**：新增自建出包器 `tools/worldbook-pilot-package/`（`.csproj` 用 `<Compile Include="..\..\src\...">` 直接编译 mod 侧 `WorldbookPackageIntegrity` / `WorldKnowledgeLoader` / `WorldKnowledgeQueryService`，stub 复用 `tools/worldbook-runtime-smoke/SmokeStubs.cs`，保证哈希与加载语义同源），产出 `release/awake-worldbook-pilot/`（`manifest.json` / `runtime.json` / `index.json`，UTF-8 无 BOM）。**实测全绿**：build 0 warning 0 error；`WorldbookPackageIntegrity.ReadAndVerify` 通过；`package-manifest.schema.json` 与 `runtime.schema.json` 均 `True`；`entries=3 identities=6`；4 类角色（村民/士兵/贵族/商人）离线命中并 `AllowsAi=true`；**负路径（无关键词命中）`AllowsAi=false state=not_found`**；重跑哈希一致（`manifestHash=FDFD…0888`）。证据 `docs/evidence/AWAKE-DIALOGUE-CHAIN-010-PILOT-PACKAGE-20260911.json`。**红线**：本 spike **未改动任何 mod 代码**（只新增 `tools/worldbook-pilot-package/` 与 `docs/evidence/`），未新增接受 v1 的分支。原始设计意图（若日后改回 studio 出包仍适用）：离线产出一个最小 v2 试点包（3～5 条，至少覆盖 1 名领主 + 1 名平民/商人角色），写死 keyword + identity/role 匹配条件。 |
| D-14 | **试点包投放与同步口径（round 3 新增）** | 问题：`tools/sync_module.ps1:39-52` 把 `ModuleData\Worldbook\manifest.json` 与 9 个 worldbook 子目录列为**受管文件**，`:222-223` 从工程源复制到 dist / 游戏 / 测试包，`:574-581` 还做 `Assert-TargetVerified` 哈希核对 → **部署试点包后再跑 `-ConfirmGameSync` 会把游戏侧覆盖回工程里的 v1 扁平包，或因哈希不符直接报错**，等于撤销验收 4。定案：(a) 试点包**不进工程源**的 `ModuleData\Worldbook\`（该处仍是后备版扁平内容，本批不动），改放独立源目录 `release/awake-worldbook-pilot/`；(b) 本批给 `sync_module.ps1` 增加显式开关（如 `-SkipWorldbook`），把 `managedWorldbookFiles` / `managedWorldbookDirectories` 从受管集合中排除；(c) 交付说明写明：部署试点包后**不得**再无条件跑 `-ConfirmGameSync`；(d) 回滚 = 删除 `release/awake-worldbook-pilot/` + 还原游戏 `ModuleData\Worldbook\manifest.json`（部署前留副本）。 |

## 会改变的调用点语义（必须声明，不是「12 个调用点不变」的字面含义）

| 调用点 | 现状失败语义 | 本批之后 |
|---|---|---|
| 权限门（`src/PermissionGate.cs:79`、`:215`） | `host.permissions.unavailable` / `Unavailable`，恒失败 | 策略判定：`Granted`，或 `awake.permission_denied` / `Denied` |
| 玩家绑定（`src/AwakeRuntime.cs:1201`） | 权限先失败，绑定不可达 | 权限通过后走 provider；失败码区分 `game_data.unavailable` 与 `player_hero_bind_failed` |
| 提示词登记 / 编译（`src/NpcDialogueService.cs:509`、`:1121`） | 恒失败 → 静默降级 | 本地实现，登记成功、编译走本地渲染 |
| 存储（`src/WorldStateStore.cs:375`） | `host.storage.unavailable` → `storage_not_ready` | 本地文件 KV，正常打开命名空间 |
| 通知 `IsPromptRegistrationUsable`（`src/AiTaskConstants.cs:8`） | 恒 false | 变为真，`EnsureReadyAsync` 不再在该门中止 |

## 契约（入口 → 调用 → 结算 → 可观察结果）

- **入口**：场景中选定 NPC → 打开对话面板 → 输入一句话 → 点「交谈」。
- **调用**：`SendAsync` → `EnsureReadyAsync` → 权限门（本地策略）→ 玩家绑定（provider）→ 提示词登记 / 编译 → `BuildPromptInputAsync` → `AiTaskGateway.SubmitAsync`（路由权限 + 云外发权限）→ Runtime IPC → Provider。
- **结算**：`NpcDialogueOutputValidator` 校验输出 → 会话历史 + 转录落盘 + 关系命令结算。
- **可观察结果**：对 **D-11 试点包覆盖的目标 NPC** 发起对话时，面板出现该 NPC 的回复文本；`Awake.log` 出现 `permission_gate_evaluate ... Granted`、`player_hero_bound`、`npc_dialogue_ready`、`ai_task_submit_accepted`、转录落盘记录；**不再出现任何 `host.*.unavailable` 与 `storage_not_ready`**。**未纳入试点包的 NPC 仍回「我没有可靠的说法。」，这是本批的预期行为，不是缺陷。**

## 验收标准

1. **红测（全部离线可判定）**：① 权限授予 / 拒绝；② 玩家绑定成功；③ 提示词就绪；④ 存储命名空间打开；⑤ **世界书解封**：`WorldbookRuntime.Knowledge != null`、部署的 manifest 顶层 `schemaVersion` 实测为 `awake.worldbook.v2`、且日志不含 `WB2-SCHEMA-UNSUPPORTED`（判据见 D-12 门禁 2）；⑥ **默认路径未被污染**：`CreateDefaultHost`、`Register(extension)` 与 `Register(extension, runtime)` 三条默认装配路径的 permissions / prompts / storage / rag 四项仍为 Unavailable（补 `tests/Program.cs:401` 未覆盖的面）。以上改前必红、改后必绿。负路径：**新授权开关关闭时不得出现 `ai_task_submit_accepted`**。证据归档 `docs/evidence/`。
2. **离线回归全绿**：`tools/build.ps1`、`MarcusAwakeFramework.Tests`、`MarcusAwakeRuntimeService.Tests`、`Awake.SdkSmoke`、`--persona-anchor`、`--redtest-r1-behavioral`、`verify_marcus_awake_api_layers.ps1`（基线更新后 PASS）、`verify_marcus_awake_caller_contract.ps1`、**`verify_marcus_awake_mcm_contract.ps1`**（D-4 新增 MCM 项会触达该门禁）。任一为红即不得宣布本批完成。
3. **实机（用户执行）**：**前置条件必须先满足并在交付说明中写明**——(a) D-11 解封已生效（部署 manifest 顶层 `schemaVersion` 为 `awake.worldbook.v2`、出现 `worldbook_runtime_initialized`、无 `WB2-SCHEMA-UNSUPPORTED`）；(b) MCM 新增显式授权项已由用户打开；(c) Runtime 有健康 Provider/Route。满足后，**对试点包覆盖的目标 NPC**：`permission_gate_evaluate ... Granted`、`player_hero_bound`、`npc_dialogue_ready`、`ai_task_submit_accepted` 四类日志出现，`host.*.unavailable` 与 `storage_not_ready` 归零，**转录落盘出现本批新增的正向日志**（F-15），面板出现该 NPC 的回复文本。任一日志缺失即按失败处理，不得以「看起来能跑」收尾。**非试点 NPC 仍回兜底文案，不计入失败。**
4. **世界书解封证据（B 案）**：交付最小 v2 试点包（**自建出包器** `tools/worldbook-pilot-package/` 产出；studio 修复后由正式产物替换，校验口径不变），`WorldbookPackageIntegrity.ReadAndVerify`（`src/WorldbookPackageIntegrity.cs:26`）通过；`runtime.json` / `index.json` / manifest 三者哈希自洽；给出「目标 NPC 命中该条目」的离线断言。**同时核对 D-12 红线**：未新增任何接受 v1 的分支。正式内容产出不在本批验收范围。
5. **四地哈希一致（代码 + 运行时包）**：build / dist / 游戏 / 测试包，`BUILD_VERIFICATION.txt` 记录。**显式白名单**：`ModuleData/Worldbook/**`（试点包投放位置，见 D-14）与 `PlayerExports/AwakeState/**`（运行期数据）**不参与**代码/运行时包的哈希比对；该白名单及理由必须写入 `BUILD_VERIFICATION.txt`，以免与本条其它受管文件的哈希口径混淆。
6. 不改存档格式、不改 `SubModule.xml` 版本号、不改既有 MCM 项语义。

## 已知取舍与风险

- **世界书是「能不能出话」的硬门**（F-14）：`WorldbookRuntime.Knowledge` 不可用 → `AllowsAi=false` → 每轮走 `CompleteDirectKnowledgeTurn` 回「我没有可靠的说法。」，**根本不调用 `SubmitAsync`**。因此 D-11 不落实的话，010 交付后用户可见结果**没有改善**——这一点必须在交付说明里写明，不得含糊。
- **按 D-12 定案，本批交付后只有试点 NPC 会说话**（其余 NPC 仍回「我没有可靠的说法。」）。这是**刻意的**：宁可窄，也不让淘汰的后备版内容当主体（否则正式 v2 包上线时体验与兼容层同时作废）。交付说明**必须原样写明这一句**，不得写成「对话修好了」。
- **worldbookstudio 出包链仍属未验证项，但已不影响本批**（v6）：`RuntimePackageCompiler` / `Application.Export` 已存在，仍无证据表明今天能直接产出可用最小包。**已用自建出包器 `tools/worldbook-pilot-package/` 绕过**（D-13 已完成并出包通过校验），故本批开工不再被 studio 阻塞。**studio 仍是正式内容的产出方**：其修复后由正式产物替换试点包，替换时验收 4 的口径不变。无论走哪条产出方，**A 案（mod 内运行时翻译）保持禁止**。
- **Runtime Provider / Route 健康是本批的隐性前置**：历史日志显示 `provider-models` 失败会令 `runtime=Stopped`（`docs/AWAKE-CURRENT.md:21,475`）。本批不修 Runtime 健康，但验收 3 依赖它；交付说明必须把它列为**前置条件 + 未验证项**，不能算作本批修复成果。
- 本批引入第二套持久化位置（游戏侧文件 KV）。Runtime 侧存储**已接线**（F-13）；其数据面当前因**客户端能力集不含 `storage.*`/`rag.*`**（`RuntimeServiceClient.cs:23-34`）而不可达，本批**只做文档与注释声明，不新增任何门禁**（见 D-9 的据实降级）。011 必须给出合并或择一的结论，否则届时补齐能力集后会出现双写。
- **试点包会被同步脚本撤销**（D-14）：`sync_module.ps1` 把 `ModuleData\Worldbook\manifest.json` 当受管文件从工程覆盖到游戏。不按 D-14 处理，验收 4 会被验收 5 的同步流程直接推翻。
- 公共 API 变更需重生成基线并登记相位；`MarcusAIFramework_Reference` 文档一致性需确认。
- 新增显式授权项意味着老用户首次使用需在 MCM 主动打开；这是有意的摩擦。
- `native_readiness` 探针失败会污染部分状态判断 → 本批只在日志层标注，不修。
- 实机验证必须由用户运行游戏后提供日志；本批不启动游戏、不改启动器启用状态。

## 执行结果（2026-09-11）

| # | 「做」清单项 | 落点 | 实测 |
|---|---|---|---|
| 1 | 框架新增公开服务注入入口（不改旧签名） | `framework\MarcusAwakeFramework\src\ServiceOverrides.cs`（新增）；`HostApi.cs` 新增 `FrameworkHost(...,IRuntimeServicePort,FrameworkServiceOverrides)` / `CreateDefaultHost(runtime,overrides,bannerlordApi)` / `Register(extension,runtime,overrides)`，旧构造器与旧方法链式转发 | `MarcusAwakeFramework.Tests` `PASS ALL: 12`（含新 `ServiceOverrideComposition`） |
| 2 | 框架公开玩家数据提供者契约 | `ServiceOverrides.cs` 的 `public interface IPlayerSnapshotProvider` + 内部 `ProviderBackedGameDataService` 适配 internal `ICompatibilityGameDataService` | 同上（未提供 overrides 时 gameData 仍报 `host.game_data.unavailable`） |
| 3 | AWAKE 三个真实现并注入 | `src\AwakePermissionService.cs`、`src\AwakePlayerSnapshotProvider.cs`、`src\AwakePromptRegistry.cs`；装配 `src\AwakeHostComposition.cs:62-71`/`:76` | 红测 ①③ 与 ② 失败关闭路径（`PASS dialogue chain player binding redtest code=game_data.campaign_unavailable`） |
| 4 | 最小存储装配 | `src\AwakeFileStorageService.cs`（`PlayerExports\AwakeState\<campaign>\<namespace>.json`，原子写；会话作用域显式 `awake.storage.session_scope_unavailable`） | 红测 ④ 通过 |
| 5 | 可观测性修复 | `src\NpcDialogueService.cs:413`/`:469-472`/`:481`、`src\AwakeTranscriptService.cs:98-104` | 编译通过；正向日志待实机确认 |
| 6 | 世界书解封（只走 v2） | 试点包投放游戏 `ModuleData\Worldbook\`（manifest/runtime/index）；`tools\sync_module.ps1:12`+`:59-63` 加 `-SkipWorldbook` | 部署 manifest `schemaVersion=awake.worldbook.v2`；就地 `DEPLOY-VERIFY-OK`（3 条目 / 6 identity / 4 类角色命中 / 负路径 `not_found`）；`-SkipWorldbook` 干跑 `managed_files=214` 中 Worldbook 条目 **0** |
| 7 | 公共 API 基线重生成与相位登记 | `docs\evidence\MARCUS-AWAKE-P3-API-SURFACE-BASELINE-CURRENT-20260829.json` 重生成；`tools\verify_marcus_awake_api_layers.ps1:290`/`:323-327` | `MARCUS-AWAKE-API-LAYERS-E1 PASS ... current_api=201 unclassified=0 schema_validation=True` |
| 8 | 红测 + 离线回归 + 实机证据 | 红测 `_houkai_merge\AWAKE.Tests\DialogueChainRedtest.cs`（`Program.cs:108-110`）；构建/同步见 `BUILD_VERIFICATION.txt` 末节 | 红测 ①②③④⑤ 全 PASS、⑥ 在框架侧；离线回归 8 项全绿；**实机证据未取得（用户执行）** |

- 版本口径：工程版本 `v0.2.0` 不变；BuildId 另立 `awake-20260911-dialogue-chain-010`（`src\AwakeConstants.cs:11`）；`SubModule.xml` 未改动（`378E9C0B…0F66`）。
- 三地/四地哈希：`Awake.dll` `F1CD21F6…343D`（build=dist=game）；语言文件、`MarcusAwakeFramework.dll`、Runtime 包、`BUILD_VERIFICATION.txt` 亦一致。白名单：`ModuleData/Worldbook/**` 与 `PlayerExports/AwakeState/**` 不参与比对（理由已写入 `BUILD_VERIFICATION.txt`）。
- **未验证项**：本批未启动 Bannerlord，未取得游戏内证据；`AllowAiRouting` 需用户主动打开；Runtime 需有健康 Provider/Route。
- **交付后可见行为（不得软化）**：只有试点包覆盖的目标 NPC 会说话，其余 NPC 仍回「我没有可靠的说法。」（D-12 刻意为之）。

## 待用户签收

- [x] D-11 世界书解封方式 — 已按用户约束定案：**B（出 v2 包；只走正式格式）**，A 案删除（v4）；v6 更正产出方为**自建出包器先出试点包**，**studio 保留为正式内容产出方**。
- [x] 用户确认 **D-12 红线** 与由此产生的代价：**本批只有试点 NPC 会说话**（用户 2026-09-11「可用」）。
- [x] ✅ **D-13 前置验证已完成**（用户 2026-09-11「可以，先做小样吧」）：自建出包器产出最小 v2 试点包，`ReadAndVerify` + 两份 schema + 4 类角色离线命中 + 负路径全部通过，证据已归档。**此项由「待签收」转为「已完成」。**
- [x] 用户确认 D-9（存储并入本批 + Runtime 存储数据面据实降级为文档声明）与 D-4 的显式授权摩擦（用户 2026-09-11「可用」）。
- [ ] 用户知悉 **D-14**（试点包投放与同步口径：独立源目录 + `sync_module.ps1 -SkipWorldbook`；部署后不得无条件跑 `-ConfirmGameSync`）。
- [ ] 用户签收本批次（**已按 v6 执行完毕，等待签收与实机验证**）。**审查预算状态：`max_rounds=3` 已用满（round 1 / 2 / 3 均 `REVISE`）**，第 4 轮会被状态机判死锁；round 3 的六项必修已在 v5 全部落实，v6/v7 只做口径结案与执行落盘（不改 round 3 已审内容）。状态文件 `docs\review-state\AWAKE-DIALOGUE-CHAIN-010-20260911.review.json` 停在 `round=3 / status=revising / user_signoff=false`，签收在本节记录。
