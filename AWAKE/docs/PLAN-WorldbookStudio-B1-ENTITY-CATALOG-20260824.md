# Plan: Worldbook Studio B1 实体目录与中文选择基础
_Locked via prior user-approved design; independently reviewed and approved before implementation._

## Goal

在不改变旧世界书 v1、运行时读取器、游戏目录和冻结候选的前提下，把现有人物/家族映射报告整理为稳定、可校验、供 Studio 只读消费的卡拉迪亚人物/家族目录，并把它接入作者 catalog。战帆是同一世界的官方 DLC；本机未安装时只显示“官方 DLC、当前未安装”，不建立独立的战帆世界。B1 不实现王国、文化、聚落的中文选择，避免使用未经核对的本地化 key。

## Approach

1. 固定人物/家族实体目录契约、来源/可用性状态、ID 规则和失败降级行为。
2. 先实现 `tools/build_persona_entity_registry.ps1` 和 entity registry 三份 schema，再从既有映射报告生成实体 registry、manifest 和 diagnostics；生成器不直接读取游戏目录。
3. 在 Core 加入只读实体目录加载与校验，保持旧 profile/referral 路径不变。
4. 将实体摘要接入 `AuthoringEditorModel.BuildCatalog()`，默认隐藏原始代码。
5. 增加人物/家族搜索、实体类型筛选和基础游戏/DLC 可用性提示所需的最小 Web catalog 数据。
6. 添加实体目录 golden、错误输入、重复 ID、来源冲突和旧测试回归覆盖。
7. 修改 `scripts/package.ps1`，确保 `schemas/mappings/persona-entity/` 进入发布包；运行 focused harness、Release build、package/release-check 和代码债务审查，生成 B1 checkpoint；不做游戏内同步。构建和打包是阻断门，债务审查作为旁证记录。

## Key decisions & tradeoffs

- **目录与 profile 分离：**具体实体不污染抽象身份画像，但需要新增一个只读 catalog 来源层。
- **映射报告作为 B1 输入：**复用已核对哈希，避免本批次再次扫描游戏目录；代价是游戏/DLC 数据更新需要重新生成目录。
- **来源与可用性分离：**宁可显示“官方 DLC、当前未安装”，也不把同世界 DLC 实体误称为另一世界或当前已加载实体。
- **B1 明确收窄：**只把有可靠中文名的人物/家族做成作者选项；王国、文化、聚落先保留代码关联，后续补齐权威名称来源再扩展。
- **只接入作者 catalog：**先让编辑器可选、可预览；人物/家族权限条件和运行时消费另立契约，避免 UI 保存出运行时不认识的字段。
- **高级信息折叠：**普通作者看中文，开发者仍可追查原始代码和哈希。
- **发布指针而非平面三文件：**用 generation 目录加 current pointer 保证读者不会看到混合版本；指针更新失败时保留旧 generation。

## Write ownership

- 生成契约、实体 fixture、映射目录：实体数据子任务，写入 `docs/mappings/persona-entity/` 和对应 fixture。
- Core catalog loader/model：Core 子任务，只写 `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/` 指定新文件和 catalog 接口。
- Web catalog view：Web 子任务，只写 `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/` 指定 DTO、HTML/JS 和中文文案。
- Tests：测试子任务，只写 `tools/worldbook-studio/tests/`。
- 主控代理负责跨边界合并、最终验证和 checkpoint；任何子代理不得修改计划、checkpoint、冻结候选或共享文件。

## Risks / open questions

- 现有映射报告对官方 DLC 人物的家族中文名可能不完整；目录必须保留代码、DLC 来源和可用状态，而不是猜名称。
- 需要明确三件套原子发布、mapping 文件哈希、生成器版本/哈希、schema 版本和坏记录隔离策略。
- 需要覆盖 415 人物、82 家族、73 基础家族、9 DLC 家族的 golden 计数，以及同一家族多人物合并和 hero→clan 关联完整性。
- 现有 `entity.*` 到运行时 ID 的转换层尚未覆盖 hero/clan；B1 只能输出作者侧实体摘要，不能宣称已完成运行时权限。
- 工作区若没有目录文件，Studio 应降级而非在启动时隐式扫描游戏目录。
- 若测试发现 `BuildCatalog()` 的公共响应是固定字段 golden，需采用向后兼容的可选字段，不重排既有字段。

## Out of scope

- 不加入 `geography`。
- 不加入知识条目引用图。
- 不实现新版 authoring/runtime schema。
- 不生成王国、文化、聚落的中文作者选择项。
- 不实现人物/家族权限结算。
- 不把 generation pointer 误当作 authoring 文档或运行时存档状态。
- 不修改 `AWAKE/src`、`ModuleData`、`GUI`、游戏 `Modules\\AWAKE`、`PlayerExports`、`dist` 或冻结候选。
- 不启动 Bannerlord，不调用真实云端 Provider 或本机 Worker。
