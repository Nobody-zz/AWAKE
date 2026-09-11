# Plan: AWAKE Worldbook Studio 首批完整功能实现
_基于 MVP 实测缺口；2026-08-22 重新锁定，写码前独立只读复审_

## Goal

把现有 Worldbook Studio MVP 推进到“开发者可以持续维护世界书”的首批完整版本：所有冻结契约都有实际 Core 调用路径、错误码和初步 fixture 测试；CLI/Web 共享同一 application service；输出仍严格隔离于 AWAKE v1 与游戏目录。范围不包含 NPC 运行时动态学习和周报传播，但要完成开发工具侧的来源、正典、审计、身份迁移、权限预览、内容层导出、确认失效、原子发布和报告闭环。

## Gaps to close

- 当前只校验 authoring schema；source/audit/profile/referral/preview/diagnostics/content-graph/current-pointer/id-ledger schema 尚未统一接线。
- 当前没有真实来源登记、source hash/quote hash、registry version/hash、一致性和有效期校验。
- 当前没有 audit event hash chain、approval 事件、revision 连续性、ID ledger tombstone/迁移校验。
- 当前 preview 只按 profile_id 直接匹配；没有继承链、deny 优先、scope、min_detail、fallback referral 与 author diagnostics 双输出。
- 当前 adult/base 只检查单档案 warning；没有从内容图做闭包阻断，也没有 warning confirmation 失效模型。
- 当前 Web 没有 YAML 读取/保存、诊断详情和导出 API；CLI 报告尚未写入固定 compiled/reports 路径。
- 当前 F01–F21 只有一个正例 smoke，需增加固定 fixture 与负向断言。

## Implementation steps

1. 建立 `WorkspaceRootGuard`，在 CLI/Web 启动和每个 command 前拒绝 workspace 根目录位于游戏目录、`Modules\\AWAKE`、v1 `PlayerExports`、AWAKE `ModuleData`、dist、_build_out、冻结/待验候选及这些路径的祖先/子孙；再建立 `ContractCatalog`：加载全部冻结 JSON Schema、profile/referral registry、fixture contract；所有 Schema 按独立 URI 缓存，跨文件 `$ref` 用本地 registry，未知字段和 schema failure 返回稳定错误码。
2. 建立来源登记模型与 `SourceRegistryValidator`：读取 `authoring/sources/*.yaml|json`，校验 source version/hash/quote/locator/license/use/valid_until；正典和 assertion/expression 的 source_ref 必须命中登记版本，源文件 hash、quote hash 和登记 hash 不一致即 `WB-SOURCE-001`。
3. 建立审计与 ID 模型：读取 `authoring/audit/events/*.jsonl` 和 `authoring/identity/id-ledger.jsonl`；验证规范化对象 hash、event hash、previous_event_hash、stream sequence、approval/migration decision、revision 连续性、tombstone 重用、redirect/supersedes 类型和循环。
4. 分离 `RequireAuthoring`、`RequireFixtures`、`RequireCompiled`、`RequireExport`；后者只能写入 `workspace\\export\\WorldbookV2` 及其候选子目录，禁止 authoring/compiled 作为 export 目标。建立 `SemanticValidationPipeline`，固定顺序：Schema → Source → Audit → Ledger/Migration → Registry → Authority/Canon → Timeline → WarningConfirmation → ContentGraph/Tier → Permission。每一阶段输出 code/path/detail，不在 Web 或 CLI 重复实现规则。
5. 建立 profile graph 与权限引擎：解析 inherits，检测未知 profile/环；按硬 deny > explicit grant > inherited profile > scope default 决策；支持 local/regional/national/faction/elite/private、min_detail、min_age、min_steward、culture/kingdom/settlement；输出 `npc-preview.v1` 和 `author-diagnostics.v1` 两种 DTO，NPC DTO 严格去除来源、规则 ID、拒绝正文和内部解释。
6. 建立完整 ContentGraph：节点覆盖 document/assertion/expression/source/referral/alias/redirect/registry/id-ledger/index/cache/report；边覆盖 contains/references_source/fallback/alias/redirect/index_entry/cache_entry/report_reference；从导出根闭包遍历，base 遇 adult_optional/unknown 或不明来源立即生成 `WB-TIER-001` 阻断边。
7. 建立 warning confirmation：token 由 Core 生成并消费，绑定 input revision、source/registry hash、tier、report hash；测试缺失、错误、过期 token。确认记录绑定输入 revision、源/registry hash、规则 ID、tier 和报告 hash；源文件、正典 revision、registry 或规则变化后自动失效，CLI/Web 不能复用旧确认。
8. 让 `WorldbookApplicationService` 提供唯一的 `SaveAuthoring`、`Validate`、`Compile`、`Export`、`Preview` 方法；CLI/Web 只能做参数/HTTP/退出码适配，不得直接 `File.WriteAll*` 或旁路实例化发布器；所有操作使用同一 validated snapshot。完善确定性编译与报告：固定生成 `compiled/manifest.json`、`validation.json`、`mapping.json`、`content-graph.json`、`source-report.json`、`audit-report.json`、`id-report.json`、`preview-*.json`；报告路径与哈希稳定，时间只出现在 pointer/release metadata。
9. 完善原子发布：候选目录不可覆盖或删除，重复候选使用新 build suffix；pointer 字段严格使用 `candidate_dir`、`manifest_hash`、`published_at`、`previous_manifest_hash` 并先过 current-pointer schema；保留 lease/CAS、旧指针 hash、临时目录清理和 `complete.marker`；增加故障注入接口，覆盖 write interruption、disk-full simulation、file lock、lease conflict、pointer replace failure。
10. 完善 CLI：固定支持 `init/doctor/validate/compile/preview/export`；`compile/export` 支持 `--out`、`--content-tier`、`--profile`、`--confirm-adult`、`--json`；退出码固定为 0/2/3/4；报告写入工作区并与 Web 复用同一 service。
11. 完善 Web：增加文档列表、读取 YAML、保存 authoring/fixtures、校验报告、编译报告、预览 envelope、导出候选 API；只允许 loopback，保存路径经 WorkspaceWritePolicy，保存前后 hash 可见。
12. 建立 F01–F21 fixture harness：为每个 fixture 固定测试方法名、执行层级、预期 valid/invalid、错误码和报告字段；F09/F11/F12/F15/F17-D/F18/F20/F21 必须有专门断言，禁止总 smoke 代替；至少覆盖正例、来源缺失、时代冲突、权限冲突、profile 环、迁移环、adult 闭包、suggestion 隔离、confirmation 失效、原子故障、稳定哈希、本地化、v1 边界、canon approval 和全机器 Schema。
13. 运行 offline restore、Release build、fixture harness、CLI smoke、Web HTTP smoke、package/release check；更新 Studio 状态记录，明确已完成与剩余运行时边界。

## Non-goals

- 不启动 Bannerlord，不调用同步脚本，不写入游戏 `Modules`、AWAKE v1 `PlayerExports`、dist、冻结候选。
- 不实现 NPC 在游戏内动态学习、周报传播、AI Provider 或 KnowledgePatch。
- 不把 AI suggestions 写入正典，不自动替开发者裁定参考资料。

## Required integration assertions\n\n- `Validate()` 的调用图必须依次命中 Contract/Source/Audit/Ledger/Registry/Authority/Timeline/Confirmation/Graph/Permission，各阶段至少有一个注入 fixture 断言。\n- `Compile/Export/Preview` 只能消费同一个 validated snapshot，原始 YAML 不得在 adapter 中重新读取。\n- preview envelope 必须校验 fixture/profile、identity snapshot、registry version/hash 与 npc/diagnostics DTO 一致。\n- F12 必须断言旧候选不删除、pointer schema 合法、旧 pointer hash 不变、故障报告字段齐全。\n\n## Completion gates

- 所有上述 validator 都由 `WorldbookApplicationService.Validate()` 唯一 pipeline 调用，旧内联旁路删除。
- CLI 与 Web 对同一 workspace 产生相同 diagnostic code、report hash、manifest hash。
- F01–F21 至少每项有一个自动断言；失败项不能被标记为通过。
- Release package 与 game runtime 路径完全分离；游戏目录未启动、未覆盖。

