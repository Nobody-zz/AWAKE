# Plan: AWAKE Worldbook Studio MVP 实现
_依据 `PLAN-AWAKE-WorldbookStudio-20260821.md` 锁定；按独立只读复审修订_

## Goal

在 `_houkai_merge/AWAKE/tools/worldbook-studio` 建立一个不依赖 Bannerlord、默认离线运行的 Worldbook Studio MVP。工具拥有独立工作区：`authoring/` 保存 YAML 正典和登记文件，`compiled/` 保存可重建中间结果，`export/WorldbookV2/` 保存候选包，`fixtures/` 保存离线样本。第一批完成来源登记、Schema 等价校验、语义/审计/ID/tier 校验、确定性编译、NPC 知识预览和中文本地 Web 入口；任何输出都不能进入 AWAKE v1 运行时或游戏目录。

## Approach

1. 创建 Studio 独立 `.NET 10` Core、CLI、Web、Tests 工程；加入 `global.json` 锁定 SDK 10.0.301、独立 `NuGet.Config`、精确版本 `YamlDotNet 16.3.0` 与 `JsonSchema.Net 9.4.0`，使用工作区 `.packages/` 本地包源、`packages.lock.json` 和包 SHA-256 清单，并提供无缓存时 fail-fast 的离线 preflight。
2. 实现 `SafeYamlLoader`：仅接受固定 YAML Core Schema，拒绝重复键、锚点/别名、标签、对象构造、外部引用、隐式日期、超深结构、未知字段和大小超限；所有 YAML 先转为受控 JSON 节点再进入 DTO。
3. 实现以全部冻结机器 Schema 为唯一结构契约的 JSON Schema 校验：authoring、source registry、audit event、profile/referral registry、preview、diagnostics、content graph、current pointer、id ledger 均必须通过；另用未知字段、`oneOf`、`if/then`、跨文件 `$ref`、registry/profile hash 相等性及 F16/F18/F20/F21 对照测试证明引擎结果与契约一致。
4. 实现工作区服务：目录初始化、来源登记读取、来源内容/quote hash 校验、profile/referral registry hash 校验、路径白名单和 reparse point/junction/symlink 拒绝。
5. 实现语义校验阶段并固定顺序：`AuditEventValidator` → `IdLedgerValidator` → `MigrationValidator` → 来源/时代/引用校验 → profile/referral 绑定 → warning confirmation 失效校验 → content graph/tier 闭包 → 权限规则校验。错误码覆盖 F02–F06、F09–F11、F16–F21。
6. 实现确定性规范化和 compiled 输出：UTF-8、LF、无 BOM、Unicode 字典序对象键、数组保持语义顺序、数字最短表示、缺省字段与显式 null 区分；文件名、显示语言、机器路径、时间和随机值不进入对象/manifest hash。生成索引、content graph、`runtime_mapping_report`、诊断报告和 SHA-256 清单。
7. 实现 `AtomicCandidatePublisher`：只允许写入当前 Studio workspace 的 `export/`；通过统一 `WorkspaceWritePolicy` 只允许 `authoring/`、`fixtures/`、`compiled/`、`export/` 四个根及其受控子目录写入；拒绝 `src/`、`AWAKE.csproj`、`SubModule.xml`、`Modules/`、`dist/Modules/AWAKE`、`ModuleData/`、`_build_out/`、frozen/pending candidate 和游戏目录及其祖先/子孙等路径；Web 保存 API 同样只能写 `authoring/` 或 `fixtures/`；同卷 sibling 临时目录写入，flush 文件和目录，最后写 `complete.marker`，校验全量 hash 后原子替换 `current.json`，保留旧指针和旧候选。通过单写入 lease/CAS 发布锁和可注入故障点验证 F12；成功发布记录 `previous_manifest_hash`，失败时旧指针 hash 不变、旧候选可读、临时目录可清理，并固定报告 `fault_point`、`error_code`、`pointer_before_hash`、`pointer_after_hash`。
8. 实现 content-tier 导出：`compile` 只生成 `compiled/` 中间结果；`export --content-tier base|adult_optional` 从 document 根遍历完整 content graph，未知或不匹配 tier 的节点输出阻断边；默认 `base`，成人层必须提供绑定当前输入 revision、tier 和报告 hash 的 `--confirm-adult <confirmation_token>`，仅传枚举值不算确认。
9. 实现 profile/referral 驱动的 NPC preview 与 author diagnostics 双 DTO：硬拒绝 > 明确允许 > 身份默认 > 地域默认；NPC DTO 只含可见摘要/细节/传闻/未知/推荐中介，不含拒绝正文、规则 ID、来源和解释链；author diagnostics 返回完整解释链。
10. 实现离线 CLI：`init`、`validate`、`compile`、`preview`、`export`、`doctor`；固定退出码 `0/2/3/4`，固定 JSON 报告路径和 `--offline --no-ai` fail-closed。AI suggestions 只能存放在 `authoring/suggestions/`，任何间接正典引用均阻止编译。
11. 实现 `build.ps1`、`test.ps1`、`package.ps1`、`release-check.ps1` 专属入口，并把脚本产物纳入发布清单；再实现 loopback-only 中文 Web：健康检查、工作区状态、文档读取/保存、校验、编译、预览、导出 API；Web 只调用 Core application service，禁止 endpoint 自己实现规则；请求大小、并发写入、路径安全和 CLI/HTTP 结果一致性纳入测试。
12. 实现本地化回退：`zh-CN → zh → en → 内部 ID`；界面标签不暴露英文内部字段，内部 ID 只在作者诊断和机器报告出现。
13. 将 F01–F21 映射到具体 fixture、测试名称、预期错误码和报告字段，运行 Core、CLI、Web、故障注入、网络拦截和发布清单验证。

## Key decisions & tradeoffs

- JSON Schema 是唯一结构契约；运行时 DTO 只是承载类型，不能用“能反序列化”替代 Schema 校验。
- YAML 依赖和 Schema 引擎均固定版本并锁入 lockfile；无法离线恢复时提前失败，不降级为自制的半兼容 YAML 解析器。
- Studio 使用独立目录和项目文件，不把工具代码加入 `AWAKE.csproj`，不修改 `_houkai_merge/AWAKE/src`、运行时世界书或冻结候选。
- `compile` 与 `export` 分离；`export` 是唯一 content-tier 门禁和原子候选发布入口。
- Core application service 是唯一权威业务层，依赖方向固定为 `Core -> CLI/Web adapters`；CLI 和 Web 都只能调用它，二者对相同 fixture 必须比较诊断码、报告 hash、manifest hash 和状态码。
- `base` 是默认导出层；`adult_optional` 必须显式指定且不允许通过 fallback、redirect、cache、index 间接进入纯净包。
- MVP 只做确定性 NPC 可见性计算；不做 NPC 主动学习、周报传播、Provider 调用或运行时 KnowledgePatch。

## Risks / open questions

- 依赖版本已经在计划中冻结为 `YamlDotNet 16.3.0` 和 `JsonSchema.Net 9.4.0`；首次 restore 只负责把固定包放入 `.packages/` 并记录 hash，不允许自动升级。
- Web 端不追求完整 IDE；编辑器先使用保留原文的 YAML 文本编辑区，后续再增加结构化表单。
- 真实磁盘满不可稳定复现，F12 使用故障注入模拟写入失败，不依赖填满系统盘。

## Out of scope

- 不启动 Bannerlord，不执行 `sync_module.ps1`，不写入 `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`。
- 不修改 `_houkai_merge/AWAKE/ModuleData`、`dist`、`_build_out`、当前 v1 `PlayerExports` 或 pending/frozen 候选。
- 不接入 Persona Workbench 业务契约，不复用其 Persona 领域模型。
- 不实现 NPC 动态学习、周报传播、AI 自动正典化或当前 v1 读取器适配。

## F01–F21 test map

| Fixture | Test | 层级 | 关键断言 |
|---|---|---|---|
| F01 | `ValidMinimal_CompilesStable` | Core/CLI | 编译成功、manifest hash 重复运行一致 |
| F02 | `MissingSourceHashes_Blocks` | Schema/Core | `WB-SOURCE-001`、定位字段完整 |
| F03 | `CrossEraReference_Blocks` | Core | `WB-TIME-001`、迁移声明缺失 |
| F04 | `DenyWinsInPreview` | Core/Web | 预览 unknown/降级，拒绝正文不泄漏 |
| F05 | `UnknownOrCyclicProfile_Blocks` | Core | `WB-PROFILE-001` |
| F06 | `RedirectLedgerRules` | Core | 单向通过，循环/类型错/重用阻止 |
| F07 | `ProfilePreviewDiffers` | Core/CLI | 普通身份与贵族身份输出不同、解释链完整 |
| F08 | `PreviewEnvelopeRedaction` | Core/Web | NPC DTO 无 rule/source/reason，diagnostics 有完整链 |
| F09 | `AdultTierClosure_BlocksBase` | Core/CLI | `WB-TIER-001`、输出阻断边 |
| F10 | `SuggestionsNeverCompile` | Core/CLI | 无网络，suggestion 间接引用阻止 |
| F11 | `WarningConfirmationInvalidates` | Core | revision/source/registry 变化后确认失效 |
| F12 | `AtomicPublisherFaultInjection` | Core/CLI | 五故障点、指针前后 hash、旧候选、清理、lease/CAS |
| F13 | `StableHashIgnoresPresentation` | Core | 文件名/数组重排/语言改动不改 ID 和稳定 hash |
| F14 | `LocalizationFallback` | Core/Web | `zh-CN -> zh -> en -> id` |
| F15 | `V1BoundaryGuard` | Core/CLI | 四个边界字段、v1 探测树不被写入 |
| F16 | `AuthoringStructureRules` | Schema/Core | `WB-CANON-001` 或 Schema 错误 |
| F17-A–E | `AuditApprovalMatrix` | Core | approval 存在性、对象/事件 hash、sequence/链、revision、报告字段 |
| F18 | `ValidMinimalSchema` | Schema/Core | 全机器 Schema 通过、registry hash 相等 |
| F19 | `ValidAcceptedVariant` | Schema/Core | accepted_variant 无 canon approval 要求 |
| F20 | `MissingSourceVersionSchema` | Schema | `WB-SOURCE-001` |
| F21 | `UnapprovedCanonBlocks` | Schema/Core | `WB-CANON-001`、审计引用缺失/不匹配 |

## Verification contract

- Studio `restore-offline.ps1` 使用 `<clear />` 的本地 `.packages/` 源和 `--locked-mode --ignore-failed-sources`；无完整缓存、包 hash 不符或源外访问均在 preflight 阶段明确失败。
- `dotnet build`：四个 Studio 工程零错误。
- F01–F21：每项均有 fixture、预期结果、错误码/报告字段和执行层级；未映射项不得宣称覆盖。
- CLI/Web：同一输入比较诊断码、报告 hash、manifest hash；默认无网络请求且只监听 loopback；`current.json` 的指针时间不参与候选 hash。
- 发布：候选含 `complete.marker`、`current.json`、manifest、`SHA256SUMS.txt`；故障注入失败不改变旧指针。


