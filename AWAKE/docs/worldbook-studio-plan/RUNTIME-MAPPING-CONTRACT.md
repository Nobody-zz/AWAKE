# Worldbook Studio MVP 运行时映射契约

## 目标

> ⚠️ **本节第 5 行的两条禁令（「不把候选包放入当前 AWAKE `ModuleData`」「手工复制或显式传入 v2 manifest 属于 unsupported」）已于 2026-09-14 由 Max 拍板**翻**掉** —— 见下方〈仓库侧包形态〉。原文保留以留痕，别再据以判断。

MVP 只生成 `awake.worldbook.v2` 候选包和映射报告，不把候选包放入当前 AWAKE `ModuleData`。Studio 只保证输出路径硬阻断和正常自动探测不扫描 export；手工复制或显式传入 v2 manifest 属于 unsupported，不宣称旧 loader 会拒绝消费。marker 仅用于诊断。

## 仓库侧包形态（2026-09-14 定 · 世界书 ＋ 角色卡 ＋ 主干三方对齐）

**这是三边唯一的接口约定。改它=改三边，不是改一条线。**

### 先看主干写死的三条（不可协商）

| # | 事实 | 出处 |
|---|---|---|
| 1 | 入口路径＝`<祖先>/ModuleData/Worldbook/manifest.json`（或 `<祖先>/Worldbook/manifest.json`），自 assembly 目录**向上最多 6 层** | `src/WorldbookRuntime.cs` `LocateManifest()` |
| 2 | 该文件必须是 `awake.worldbook.registry.v1` **或** `awake.worldbook.v2`；否则 `WB2-SCHEMA-UNSUPPORTED:entry` | `WorldbookRuntime.TryLoadAndPublish():152-170` |
| 3 | 角色卡根＝**含 `persona_definitions/` 的那一层**；`ModuleData/Worldbook/` 已是 canonical，且定位**刻意不读 manifest** | `src/PersonaRootLocator.cs` |
| 4 | 包**只认 3 个文件**：`manifest.json` ＋ `entrypoints.runtime` ＋ `entrypoints.index`。`ReadAndVerify` **不扫目录** ⇒ 其余报告文件可留在 tools 侧不入库 | `src/WorldbookPackageIntegrity.cs:48-61` |

### 定下来的形状

```
AWAKE/ModuleData/Worldbook/          ← 仓库侧唯一根；＝部署根
├─ manifest.json                     awake.worldbook.registry.v1   ← 唯一入口
├─ packages/
│   └─ <universe>/                   ← 一个 universe 包 ＝ Studio compile 产物
│       ├─ manifest.json             awake.worldbook.v2
│       ├─ runtime.json              448 entries
│       └─ index.json                awake.worldbook.index.v1
└─ persona_definitions/              ← 角色卡，位置不动
    ├─ definitions/*.json
    ├─ tag_registry.json
    └─ pilot-allowlist.json
```

- **为什么不把 v2 manifest 平铺当根**：根还要放 `persona_definitions/`，将来还要放第二个包；平铺会把「内容」和「身份」混在一层，且加不了 extension／分卷包。
- **为什么留 registry 这一层**：`packages[]` 天然多包，`enabledByDefault` ＋ `awake.worldbook.campaign-activation.v1` 是运行时**已经实现**的选包能力，且该路径有离线冒烟（默认选包／显式选包／路径逃逸／hash 不符，见 `tools/worldbook-runtime-smoke/Program.cs:1203+`）。
- **旧 v1 目录**（`rules/` `personality_background/` `unnamed_persona/` `voice_mapping/` `event_data/` `debt/` `dialogue_history/` `compressed_memory/`）**不进这个形态**：除 v1 loader 自己的默认值外，`src/` 里没有任何读写方（`dialogue_history` 那处是 prompt 变量名撞车），游戏目录里也一直是空的（AF 迁移遗留）。

### 身份定名（2026-09-14 拍）

**世界身份改成从「包」取，不再从文档取。**

原实现有两个隐患，都是「**universe 级身份取自任意一篇文档**」：

1. `packageId` / `worldId` 由 `first["universe"]` 推出 ⇒ 把 **schema 的「来源宇宙」枚举**（`awake_current` / `bannerlord_1084` / `warband_future` / `ck3_mod` / `unknown`，见 `awake.worldbook.authoring.v1.schema.json`）当成了**运行时世界身份**。两者不是一个概念：`universe` 是溯源分类，`Application.cs:800` 还拿它判「未来档案引用当前宇宙来源」。
2. `displayName` 取 `first["title"]` ⇒ **随改名漂**（v2 是 `Charas Bay: Origin Tales and Sailor Lore`，v3 变成 `沙拉斯湾·三种来历`）。

⇒ 修法＝在 `RuntimePackageCompiler` 里把三者写成**常量**（AWAKE 只装一个世界，没有第二个 world 的需求）：

| 字段 | 值 |
|---|---|
| `worldId` | `awake:world:calradia` |
| `packageId` | `awake:worldbook.calradia` |
| `displayName` | `{zh-CN: 卡拉迪亚, en: Calradia}` |

⚠️ **拼法受契约约束（2026-09-15 纠）**：`packageId` 走 `common.schema.json#/$defs/package_id`，
pattern 是**两段** `^[a-z][a-z0-9_-]*:[a-z][a-z0-9_.-]*$` —— **只允许一个冒号**。
`worldId` 走 `$defs/stable_id`，是**三段** pattern。**两者规则本来就不同，不要"统一"**。
（09-14 初版把 `packageId` 写成三段 `awake:worldbook:calradia`，Studio 测试 `F24` 判红；
按**先行的** `awake:pilot.worldbook` 惯例改为两段 `awake:worldbook.calradia`，ns=`awake`、name=`worldbook.calradia`。）

**⇒ 448 档一个字都不用改**（`universe: awake_current` 本身是对的，含义是「AWAKE 当前宇宙」），**不重登记**，只重编译。
`AuthoringTemplateFactory` 的 `universe = "awake_current"` 默认值也**保持不动**（它填的是溯源字段）。
将来若真出现第二个世界，再把常量换成 workspace 级身份文件；现在不做。

### 三边各改什么（09-14 均已落地）

| 边 | 改什么 |
|---|---|
| **世界书** | ① `RuntimePackageCompiler` 身份改常量（3 处，见上）。② 新增组装脚本 `tools/assemble_worldbook_package.ps1`：`compile --out` 产物 → 逐字节取三件套 → 建 registry → 落 `ModuleData/Worldbook/`（`-ValidateOnly` 回验）。③ 本节即翻掉的禁令出处。 |
| **角色卡** | 位置不动，**一行没改**。`tools/sync_module.ps1` 的 `Assert-SourceManifest` 原先要求源 manifest 带 **v1 的** `personaDefinitionDirectory`／`personaTagRegistryFile` —— 那两个字段在 registry/v2 manifest 里**不存在**，而运行时又不认 v1 manifest ⇒ 该断言已改为**直接断言 canonical 路径存在**（schema 无关，且与 `PersonaRootLocator` 同口径）。roster 是否写进包（丙-b）另择时机。 |
| **主干** | ① `tools/sync_module.ps1`：受管清单加 `ModuleData\Worldbook\packages`（**原来根本没有它 ⇒ 包永远投不出去**）；删掉八条已在磁盘上不存在的 v1 目录（死配置）；`Assert-SourceManifest` 重写（见上）。② **`.gitignore`**：原文 `AWAKE/ModuleData/Worldbook/*` 整条排除，只会排除出"包进不了库"。已放行 `manifest.json` ＋ `packages/`，v1 内容仍排除，`persona_definitions/` 照旧放行。③ 运行时**代码零改动**——registry 与 v2 两条路都已实现。 |

> ⚠️ **第四方**：上一轮的"三方对齐"漏了 `.gitignore`。它不是"谁能读"的问题，而是**这份形态到底在不在版本控制里**的问题——形态定完仍可能 `git status` 干净得像什么都没发生。任何再改这里形状的人，都要同时看 `.gitignore`。
> ⚠️ **`tools/sync_module.ps1` 是混编文件**：UI 线在同一文件里加了 `GUI\Prefabs\AwakePortraitProbe.xml`（在途未提交）。提交时不能整文件一把抓，须 `git add -p` 或按行拆分，否则会把别人的在途改动带走。

### 落地物与验证（09-14）

| 项 | 位置 / 结果 |
|---|---|
| 组装脚本 | `tools/assemble_worldbook_package.ps1`（逐字节拷贝；**刻意不删任何东西**——多余文件/兄弟包目录一律报错，不静默清理） |
| 仓库侧包 | `AWAKE/ModuleData/Worldbook/manifest.json` ＋ `packages/calradia/{manifest,runtime,index}.json`（448 entries） |
| 包 hash | `manifestHash 5EA7E980…`／`contentHash 53A2E10F…`／`packageHash BD95072C…`，registry 三值逐条相符，三件套与编译产物 SHA256 完全相同 |
| 编译 | `manifest_hash 8a0d56f1…`、`result_hash 99F03F8E…`、448 docs、`0 error / 21 warning`（warning 条数与上一版**逐条相同** ⇒ 无新问题） |
| 离线验收（真实包） | `tools/worldbook-runtime-smoke` 的 `TestRepositoryPackageForm`：registry 选包 → `ReadAndVerify` 重算三 hash ＋ index 一致性 → `PersonaRootLocator` 从包内向上按形状找到**同一个根**并断言 76 张卡在 |
| **变异检验** | 语义改坏 `runtime.json` 一处 `summary.zh-CN` ⇒ **红**（`WB2-HASH-MISMATCH:content`，栈指向新判据 `Program.cs:1242`）；按字节还原 ⇒ **绿**。全绿本身不算证据，这一步才算 |
| `sync_module` 测试 | `tools/tests/sync_module.Tests.ps1` **17/17**（夹具已从 v1 形态改为 registry ＋ `packages/`）。其中**两条是新断言的反例**：注册表列出了包但包不在 → 拒；包缺 `entrypoints.index` → 拒。**没有反例的断言＝没测过。** |

**仍未做**：① 投送到游戏目录（要 `-ConfirmGameSync`，且会覆盖游戏里那个 v2 试点包）；② 真机 `LocateManifest` 命中；③ roster 写进包（丙-b）。

## 字段映射

| Authoring JSON Pointer | 编译候选 v2 | 现有 v1 映射 | 状态 |
|---|---|---|---|
| `/revision` | `revision` | 无字段 | unsupported_for_v1 |
| `/id` | `id` | `WorldbookRule.Id` | mapped；缺失/重复硬失败 |
| `/title` | `display.title` | 无稳定字段 | report_only |
| `/summary` | `summary` | 无字段 | unsupported_for_v1 |
| `/domain` | `domain` | 无字段 | unsupported_for_v1 |
| `/universe` / `/era` | `timeline` | 无字段 | unsupported_for_v1 |
| `/status` | `canon_status` | 无字段 | unsupported_for_v1 |
| `/content_tier` | `content_tier` | 无字段 | unsupported_for_v1 |
| `/registry_bindings` | `registry_bindings` | 无字段 | unsupported_for_v1 |
| `/authority` | `authority` | 无字段 | unsupported_for_v1 |
| `/sources` | `provenance.sources` | 无字段 | unsupported_for_v1 |
| `/assertions[*]/id` / `/revision` | `assertions[].id` / `revision` | 无稳定字段 | unsupported_for_v1 |
| `/assertions[*]/text` | `assertions[].text` | `WorldbookRule.Content` 或变体正文 | lossy；必须保留报告 |
| `/assertions[*]/expressions[*]/id` / `/revision` | `expressions[].id` / `revision` | 无稳定字段 | unsupported_for_v1 |
| `/assertions[*]/expressions[*]/text` | `expressions[].text` | `WorldbookVariant.Content` | lossy；正文可映射 |
| `/assertions[*]/expressions[*]/layer` | `expressions[].layer` | 无可靠字段 | report_only |
| `/assertions[*]/expressions[*]/grants` | `access.grants` | `WorldbookWhen.Roles/Cultures/KingdomIds/SettlementIds/SkillMin` | lossy |
| `/assertions[*]/expressions[*]/denies` | `access.denies` | 无可靠否定字段 | unsupported_for_v1；不得静默丢弃 |
| `/assertions[*]/expressions[*]/fallback_referral_ids` | `expressions[].grants[].referral_ids` | `WorldKnowledgeRule.ReferralIds` | only public-askable registry targets; deny/disabled/unknown paths emit none |
| `/lifecycle` / `/aliases` / `/redirects` | `identity.*` | 无稳定字段 | unsupported_for_v1 |
| `/author_created` / audit event | `provenance.review` | 无字段 | unsupported_for_v1 |

## 编译规则

- 任一必填字段为 `unsupported_for_v1` 时，只能生成 v2 候选包和报告，不得生成 v1 兼容文件。
- `mapped`、`lossy`、`unsupported_for_v1` 是机器可校验枚举。
- `lossy` 必须列出丢失风险和源字段；`unsupported_for_v1` 必须列出阻断原因。
- 候选包写入独立目录并带 `awake.worldbook.v2.incompatible_with_v1=true` 标记，marker 只作为诊断信息。
- 文件名、数组下标和显示名称不能参与 ID 生成。

## 隔离验收

F15 必须覆盖：正常自动探测不发现 export；`--out` 位于 v1 探测树或其规范化等价路径时在写入前拒绝。手工复制或显式传入 manifest 属于 unsupported，不纳入“不消费”保证；若未来需要 fail-closed，必须另立运行时适配计划。
