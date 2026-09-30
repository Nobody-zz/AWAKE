# Plan: AWAKE 世界知识平台与玩家可编辑世界书

> **状态：`superseded`（2026-09-30 标记）。原写「待用户签收后进入独立实现计划」—— 该状态已失效。**
> ⚠️ **已被取代**：本方案的目标「把 AWAKE 世界书重做为一套统一知识平台、**替换现有 v1 读取器的设计方向**」**早已完成** ——
> 世界书已推进到 **v38 / 790 档**（`identity 12`、`polity 5`），运行时读取器已换。
> 现行权威：`AWAKE-ROADMAP.md`；当前世界书规范：`WORLDBOOK-AUTHORING-SPEC-v1-20260930.md`。
> 依据：`AUDIT-PLAN-TRIAGE-20260930.md` §3.2。

_Locked via grill — by Claude + user_

## Goal

把 AWAKE 世界书重做为一套以世界观档案为核心、支持世界观包与专属内容拓展、按 NPC 身份分配知识、由代码生成周报、允许玩家在游戏内编辑已有知识并导出复用的统一知识平台。新方案直接替换现有 v1 读取器的设计方向，不承担旧四版世界书迁移，也不修改当前冻结运行时候选。

## Approach

1. **完成现有行为与调用映射**
   - 只读核对 `WorldbookLoader`、`WorldbookService`、`WorldbookRuntime`、`NpcDialogueService`、Studio Core 和现有事件/存档入口。
   - 产出 v1 行为映射表、生产调用图和替换清单；明确每个旧调用者由哪个 `IWorldKnowledgeQuery` 或新服务接管。
   - 映射表只作为证据，不作为新世界书契约。

2. **A0：冻结可机器校验的 Contract v1**
   - 固定契约目录：`_houkai_merge/AWAKE/tools/worldbook-contract/v1/`。
   - 交付并固定 `$id`、顶层 `schemaVersion`、未知字段策略、数组唯一性、ID 正则和引用闭包规则：`registry.schema.json`、`package-manifest.schema.json`、`campaign-activation.schema.json`、`runtime-snapshot.schema.json`、`authoring.schema.json`、`runtime.schema.json`、`overlay.schema.json`、`export.schema.json`、`event-record.schema.json`、`weekly-report.schema.json`、`adapter.schema.json`、`enums.json`、`permission-matrix.json`、`error-codes.json`。
   - 规定：生产输入 `additionalProperties=false`；显示层扩展只能进入 `extensions`；所有跨对象引用必须在同一组合闭包中解析；数组按稳定 ID 去重并拒绝重复；所有 schema 版本必须明确兼容范围。
   - 定义事实、表达、身份配置、转介、包、补丁、运行时更新之间的引用图、必填字段、闭包检查和版本约束。
   - 固定权限结算顺序、`known/partial/referral/not_found/blocked` 判定算法、覆盖层可修改字段和不可修改字段。
   - 分离“知道多少”“信息敏感度”“表达方式”，明确政治、经济、文化、战争四领域。
   - 使用全限定稳定 ID：`<package_namespace>:<object_type>:<local_id>`。

3. **A1：分离包发现、包激活和运行时快照**
   - 保留 `ModuleData/Worldbook/manifest.json` 作为 AWAKE 世界书注册入口，但它只登记受信任模块根目录下的规范化相对包路径和包摘要，不代表当前战役选择。
   - 每个 Universe/Extension 拥有独立 package manifest、命名空间、依赖、目标世界观和 Contract 版本。
   - 固定摘要语义和跨机器规范：所有哈希使用 SHA-256；JSON 使用 UTF-8、对象键按序、无多余空白的 canonical JSON；文本统一 LF、路径统一 `/` 并按大小写折叠规则排序；`manifestHash` 只对规范化 package manifest 计算，`contentHash` 对包内所有允许的 runtime JSON 和索引按规范化路径排序后计算，`packageHash = SHA256(manifestHash_bytes || contentHash_bytes)`；`activation` 保存三者，Snapshot 复核三者和组合 revision。
   - A0 同时交付至少一组固定输入与预期输出的 golden hash fixture，跨 PowerShell、.NET 和 Studio 编译链验证一致。
   - 交付四种不同对象：安装 `registry`、每包 `package manifest`、存档 `campaign activation`、内存 `runtime snapshot`；Snapshot 不直接持久化，只由 activation + overlay 重建。
   - 当前战役选择写入存档：主世界观 ID/version/hash、启用拓展 ID/version/hash、Contract 版本和组合 revision。
   - AWAKE Core 不绑定世界观；卡拉迪亚默认提供但非强制；成人包只能作为目标世界观专属 Extension。
   - 一个战役只激活一个主世界观；多个世界观可以安装但不自动合并。

4. **A2：只读 v2 Runtime Snapshot 与 Loader**
   - 新读取器只消费 v2 编译包，不兼容读取 v1，不静默 fallback；旧冻结候选的受保护产物固定为 `AWAKE\\dist\\Modules\\AWAKE`、游戏目录 `D:\\SteamLibrary\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\AWAKE` 及其 BuildId `awake-20260820-syncpack-001`，新 v2 在 `AWAKE\\artifacts\\worldbook-v2-candidate\\<BuildId>\\Modules\\AWAKE` 和 `AWAKE\\artifacts\\worldbook-v2-candidate\\<BuildId>\\Contract` 中独立生成，未通过门禁前不写入游戏目录。
   - 新候选的模块清单、入口配置和验证命令必须显式绑定 `schemaVersion=awake.worldbook.v2`；旧候选只发现 v1 入口，新候选测试只发现 v2 registry，并加入互相不可发现的 fixture/静态检查。
   - 注册清单只接受 AWAKE/受信任模块根目录下的规范化相对路径；包发现、依赖拓扑排序、循环依赖、冲突和资源配额在加载前完成。
   - Studio 使用 `authoring → normalized contract → runtime package` 编译链；Runtime 只读取编译产物。
   - 启动阶段完成包组合和索引构建，生成不可变 Runtime Snapshot；查询阶段只读快照。
   - Snapshot 至少包含 `snapshotRevision`、组合包摘要、索引版本和 permission matrix hash；缺包、哈希不符、schema 不兼容时返回明确错误并进入知识不可用安全状态。

5. **A3：唯一查询入口与真实调用替换**
   - 交付 `IWorldKnowledgeQuery`、唯一生产 façade 和逐方法替换清单，至少覆盖 `NpcDialogueService` 全部 `Query/BuildPersona` 路径、提示词流水线、Studio preview、周报查询和测试入口。
   - 新候选生产代码不得出现 `WorldbookService.Query(`；加入静态扫描/编译测试，旧冻结候选保留旧入口，新候选只绑定 façade。
   - 用入口→调用→评估→输出 harness 证明 NPC 对话实际消费 v2 结果，而不是只证明新类存在。

6. **阶段 B：身份 Evaluator 与固定权限矩阵**
   - 采用基础身份 + 条件修正，不为每种组合复制正文。
   - 覆盖普通平民、广博平民、公证商人、赎金经纪人、酒馆老板、村镇头人、士兵、贵族亲兵/军官、贵族和特殊专家。
   - 年龄、Steward/相关技能、军阶、职务、家族领袖身份、文化、国家、聚落、亲历事件和已有记忆作为修正条件。
   - Referral Registry 管理转介对象；权限、转介和表达结果都必须有固定 fixture。

7. **阶段 C：Campaign Overlay 与存档契约**
   - 交付 `docs/WORLDBOOK-SAVE-CONTRACT-V1.md` 和 `campaign-activation.schema.json`/`overlay.schema.json` 对应实现；固定 Save key 命名空间 `awake.worldbook.activation.v1`、`awake.worldbook.overlay.v1`、`awake.worldbook.overlay-revision.v1`，并完成 SaveDefiner/注册检查。
   - 明确 `OnGameStart → SyncData(ref ...) → 读档后重建 Snapshot → 战役切换/卸载` 顺序、默认值、旧存档缺失行为和包缺失恢复结果。
   - 将事实补丁、表达补丁、权限补丁、范围补丁、失效补丁和个人笔记拆成不同类型；普通事实补丁不能隐式提高 NPC 权限。
   - 使用不可变 Snapshot + 单调 revision 的 copy-on-write；定义 `baseRevision`、`resultRevision`、`snapshotRevision`；提交必须 compare-and-swap，基准不匹配返回冲突错误；撤销/恢复只生成新 revision，不回退计数器；失败保留旧 Snapshot。
   - 当前战役默认全局生效但仍经身份权限；高级范围可缩小到国家、阵营、地区、聚落、角色或表达版本。

8. **阶段 D：玩家工作台、导入导出和分享**
   - 提供世界观查看、档案馆、玩家知识档案、周报入口、笔记、已有知识编辑、预览、撤销和恢复。
   - 玩家编辑通过草稿→校验→安全提交→增量快照生效，不在对话 Tick 写文件或重建全量索引。
   - 分开“战役快照”和“可复用世界知识包” schema；复用包只保存可重定位补丁，不复制基础世界书。
   - 允许玩家分享和手动导入；导入前进行依赖、哈希、命名空间、权限、冲突和资源配额检查。
   - 固定导入容器为 ZIP 内的 UTF-8 JSON 文件包，拒绝其他容器；限制条目数、压缩后总大小、单文件大小、JSON 深度/字符串/数组长度、最大压缩比、重复文件和大小写/Unicode 规范化碰撞。
   - 导入采用应用专用目录下的 staging + 原子提交；只允许规范化相对路径，失败清理 staging 且不改变当前 overlay；禁止路径越界、脚本/网络执行、压缩包炸弹和任意文件读取。

9. **阶段 E：事件、周报与最小 Adapter Contract**
   - 固定 `IWorldEventRecorder` 为唯一事件写入接口、`IWeeklyReportService` 为唯一周报生成/查询 façade；现有 `WorldEventLedger` 只能作为适配实现，不得另建平行写入路径。
   - 定义不可变事件记录、事件幂等键、周边界、聚合窗口、周报实例 ID、Snapshot revision 和存档保存格式；明确唯一事件写入者、周结算安全触发点、实例缓存和 Save key。
   - `event-record.schema.json` 和 `weekly-report.schema.json` 位于 AWAKE Contract 目录，周报 harness 固定为“事件写入→周结算→按身份/地区查询→存档→读档恢复”。
   - 周报模板属于世界观/拓展包，周报实例属于运行时状态；代码按受众、地区、身份和事件影响范围生成。
   - NPC 不主动阅读完整档案，不为每个 NPC 独立调用 AI 学习。
   - 先交付 `adapter.schema.json`、`IWorldbookGameAdapter` 最小只读接口、能力枚举、稳定实体 ID 映射、版本协商、线程约束、错误码和 fake adapter；缺失能力返回可观察降级结果，再适配一个测试模组。

10. **分层验证与性能门禁**
   - Contract、Compiler、Loader、Evaluator、Runtime integration、Save/load、Import security、UI preview 和 harness E2E 分层测试。
   - 固定基准环境文件 `tools/worldbook-contract/v1/performance-baseline.json`：当前 Windows x64、Release、Bannerlord API v1.3.15、.NET/编译器版本、CPU/内存指纹、数据生成 seed、预热 10,000 次、采样 100,000 次、并发 8、冷启动 10 次、GC/峰值内存采样方式和失败阈值；大包 fixture 至少 10,000 条知识卡片、50,000 个表达、20 个组合包；目标为 warm query p95 ≤ 5ms、快照切换 ≤ 50ms、离线组合加载 ≤ 3s，超限阻止新 BuildId 生成，不得只记录报告。
   - 覆盖身份差异、转介、缺包、损坏包、版本不兼容、循环依赖、扩展冲突、玩家覆盖、导出导入、基础包升级重定位和读档恢复。
   - 通过离线门禁后创建新的 BuildId；当前 `awake-20260820-syncpack-001` 保持冻结不变。
   - 游戏内 E4/E5 绑定新候选 BuildId、包哈希和用户提供的日志/存档证据。

## Key decisions & tradeoffs

- **直接重做读取器**：现有 v1 只作为行为证据，新 v2 不提供静默兼容层。优点是避免继续背负旧 `rules/*.json` 的语义；代价是必须完整完成新 Loader 与调用链接线。
- **入口位置不变、内部结构重做**：保留 `ModuleData/Worldbook/manifest.json`，降低发现和部署改动；`schemaVersion` 与内容目录升级为 v2。
- **单一主世界观**：允许多包安装但每个战役只确认一个 Universe，避免跨世界观事实、实体和时间线混合。
- **默认卡拉迪亚但不强制**：服务 Bannerlord 默认体验，同时保持 AWAKE Core 的世界观无关性。
- **扩展包默认只增不改**：`supplements`/`extends` 可正常组合；`contradicts`/`supersedes` 必须显式声明和审查。正式包冲突默认阻止发布。
- **玩家覆盖层允许明确覆盖**：玩家可以在当前战役修改已有知识，但修改标记为 `player_defined`，不污染基础包；玩家明确确认后可以覆盖正式包结果。
- **玩家编辑默认战役全局**：修改内容对当前战役的档案查询生效，但 NPC 仍受身份权限控制；高级范围可缩小。
- **玩家包允许分享**：导出包可在新存档或其他玩家环境导入，但必须显示依赖、来源和冲突，不能自动成为正式正典。
- **人工选择优先**：开局由玩家手动选择世界观和拓展；代码检查是硬门槛，AI 只能建议。
- **代码生成周报**：运行时不依赖 AI 为 NPC 逐个学习，降低 Token 消耗并提高可复现性。
- **Worker 本机专用**：Worker 不进入发布包、游戏运行时或玩家环境；作者没有 Worker 时系统仍然可用。
- **职责分阶段**：先交付只读 Contract/Compiler/Loader/Evaluator，再交付 Overlay/Save，再交付玩家 UI/分享，最后交付周报和 Adapter，避免多个高风险系统绑定同一候选。
- **注册、激活、快照分离**：静态注册清单、每包 manifest、战役激活状态和内存 Snapshot 不共用一个文件或一个生命周期。
- **Overlay 使用补丁而非替换文件**：玩家修改必须带目标 ID、补丁类型、作用范围、基准 revision、来源和可回滚信息。
- **存档先于玩家 UI**：没有 Save key、重建顺序、CAS revision、崩溃恢复和基础包升级重定位测试，不进入玩家编辑界面阶段。
- **固定导入容器**：分享包只接受受限 ZIP+JSON 格式，先 staging 校验再原子提交，不接受任意目录或可执行内容。
- **新候选隔离**：旧冻结候选继续绑定 v1 Loader/目录；新 v2 只绑定新的候选目录、入口配置和 BuildId，隔离关系写入阶段 A 交付记录。
- **哈希分层**：`manifestHash`、`contentHash`、`packageHash` 和组合 revision 的输入与验证规则固定，不允许以任意单文件 hash 代替包身份。
- **哈希可复现**：SHA-256、canonical JSON、UTF-8、LF、路径规范化和 `packageHash` 拼接规则固定，并由 golden fixture 锁定。
- **事件单一写入路径**：`IWorldEventRecorder` 和 `IWeeklyReportService` 是唯一生产 façade，既有事件 Ledger 只能适配到它们。

## Risks / open questions

- v2 Contract 必须先成为可机器校验的 schema、枚举、优先级矩阵和错误码，任何未定义字段不得进入 Compiler 或 Loader。
- 包注册清单、包 manifest、战役激活状态和 Runtime Snapshot 必须分开，否则多世界观安装与单战役选择会产生版本歧义。
- Overlay、Save/load、导入导出和升级重定位是玩家编辑的前置依赖，不能推迟到玩家 UI 之后。
- 玩家包导入会接触不可信文件，必须有资源配额、路径边界、原子 staging、哈希和引用闭包检查。
- 现有 `NpcDialogueService` 仍直接消费旧 `WorldbookService.Query()`；新 Loader 接入必须覆盖真实调用链，并用测试防止旧生产入口残留。
- 周报需要事件幂等键、时间窗口、实例保存和明确触发入口；不能只定义模板。
- 性能目标是初始门禁而非永久保证；若真实 Bannerlord 运行环境达不到，必须在候选前调整并记录，不得静默放宽。
- 旧的四版世界书、当前 v1 世界书和新世界观内容之间不建立自动迁移链；参考素材必须重新判断、重写、登记来源和审核。

## Out of scope

- 不修改当前冻结候选 `awake-20260820-syncpack-001`。
- 不启动 Bannerlord，不改变启动器启用状态，不覆盖游戏目录。
- 不在本批直接批量重写新世界观正文。
- 不把旧四版世界书作为迁移目标、兼容规范或新正典基础。
- 不让 Runtime 读取 YAML、作者诊断、Worker suggestions 或来源全文。
- 不实现 NPC 自主阅读世界书和逐 NPC AI 学习循环。
- 不把成人拓展做成通用世界观插件。
- 不允许 AI 自动选择世界观、修改正典、审批或发布。
- 不把玩家导出包自动转换为开发者正式拓展包；正式化必须回到 Studio 审核流程。
- 不在阶段 E 之前实现具体大型模组 Adapter；先只冻结最小 Adapter Contract 和测试替身。
- 不在阶段 C 完成前实现玩家编辑 UI；不以未持久化的临时内存修改冒充已完成功能。
- 不把 Runtime Snapshot 直接写入存档；只保存可重建的激活选择、包摘要和 overlay revision。
- 不在未通过新旧候选互相不可发现检查前把 v2 包复制到旧候选或游戏目录。
