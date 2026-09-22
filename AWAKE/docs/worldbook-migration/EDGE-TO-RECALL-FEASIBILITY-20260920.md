# 互引边表接进召回 —— 可行性核查（2026-09-20）

甲方问：**边表接进召回能不能做？**
答：**能做，而且不用改引擎架构——但有三件事得先办。**以下是当场核过的读数与卡点，不是估计。

---

## 一、先分清三个东西（现在容易混）

| 名字 | 在哪 | 是什么 | 谁在读 |
|---|---|---|---|
| `content-graph.json` | 编译产物目录（**不进部署**） | **结构图**：4841 条边，`edge_type` 只有三种 —— `index_entry` 558 ／ `references_source` 2145 ／ `contains` 2138。节点 2733 ＝ 文档 558 ＋ 断言 721 ＋ 表达 1414 ＋ 来源 37 ＋ 索引 1 ＋ 两个登记表各 1 | **没人**。`src/` 里 `content-graph` 零命中 |
| `should-link.v2.json` | `docs/mappings/worldbook-should-link/20260918/` | **互引边表**：1077 条边 ／ 813 个不同对 ／ 382 条双向 ／ 覆盖 **399 档**。字段 `from/to/viaName/strength/df/bucket/mutual/direction/usableAs/evidence` | **没人**。全仓只有我 09-18 自己写的建表与审计脚本读它，`src/` 里 `should-link` 零命中 |
| `runtime.json` 顶层 `referrals` | 部署包里 | **指路**：只有 **4 条**（公证商人／赎金经纪人／酒馆老板／头人），语义是「**我不知道，你可以去问 X**」（`WorldKnowledgeQueryService.cs:111-116`，`state=referral`），由 grant 的 `referral_ids` 产出 | 运行时真的读（`WorldKnowledgeLoader.cs:103`） |

⇒ **别把互引边当 `referrals`**：「指路」是换人问，「互引」是这条知识还牵涉谁。语义不同，出口也不同。

## 二、卡点与口子（逐条实测）

1. **部署形态卡死**：`tools/assemble_worldbook_package.ps1:35` 写死 `$requiredEntryFiles = @('runtime.json','index.json')`，`:202-204` 要求包里**恰好**是 `manifest.json ＋ runtime.json ＋ index.json`，多一个就 throw。
   ⇒ **边表要进包，只能塞进 `runtime.json` 或 `index.json`，不能新开第四个文件**（除非同步改这个脚本）。
2. **校验不拦新增字段**：`src/WorldbookPackageIntegrity.cs:124-135` 的 `ValidateIndex` **只比三样** —— `runtime.entries[].id` ↔ `index.entryIds`、`runtime.indexes.keywordToEntryIds` ↔ `index.keywordToEntryIds`、`runtime.indexes.domainToEntryIds` ↔ `index.domainToEntryIds`。
   ⇒ 往 `runtime` 里**加一个 `links` 块不会破校验**。代价是三哈希全变（contentHash 只覆盖 runtime ＋ index），每改一次边表就要重编重签。
3. **召回口子已经留好**：`FindCandidates`（`WorldKnowledgeQueryService.cs:328-351`）**已经是两臂经 RRF 合并**——字面腿（关键词索引 ＋ 兜底 term 索引，`:353-395`）＋ 语义腿（IPC，`:65`），合并用现成的对称 RRF（`WorldKnowledgeRankFusion.Merge`，k=60）。
   ⇒ **加一条腿不用新写融合算法。**
4. **但边的语义与前三腿不同**：那三腿都是「问句 → 条目」；边是「**条目 → 条目**」，**它需要种子**。
   ⇒ 它不能当平行的第三个 RRF 臂，只能作**第二趟扩召回**：先跑原有两腿拿种子，再沿边扩一跳，把结果交给同一个 RRF —— 或更稳，**只作加权提示，不直接进正文**。

## 三、★ 最关键的一条：现有边表根本没覆盖新内容

- 边表覆盖 **399 档**；当前包 **558 档**。
- **新增的 76 档（军事 25 ＋ 经济 40 ＋ 暗面 11）里，有边的 = 0。**
⇒ 直接接进召回，**最近做的三批一档都进不了这条腿**。所以第一件事不是改代码，是**重算边表**（跑现成的 `tools/_build_should_link_v2_20260918.py`，或按新语料加参数重跑）。

## 四、做法（四步，按代价从小到大）

1. **重算边表**，覆盖全 558 档。判据不变（「A 条正文点名了 B 条的名字」），只是语料变大。
2. **编进 `runtime.json`**：建议放 `extensions.links` 或顶层 `links`；**只带机器字段** `{from,to,strength,bucket,mutual,direction,usableAs,viaName}` —— **丢掉 `evidence`**（那是论证长句，只给审计看，进包就是白涨体积加白改三哈希）。`index.json` 可以不带（`ValidateIndex` 不比它）。
3. **加载侧**：`WorldKnowledgeLoader.BuildSnapshot` 加一句 `LoadLinks(...)`，照 `LoadReferrals` 的写法，约 10 行。
4. **召回侧**：`FindCandidates` 拿完两腿候选后，以它们为种子沿边扩一跳 —— 约 30 行 ＋ 3 个常量（扩几跳、每档带几条、总量上限）。

## 五、三条待裁（都属口径，不是技术）

1. **扩进来的东西该不该直接进正文？** 边表判据是「**提到**」，不是「**该一起答**」。所以扩进来的应当是**低权重或只作提示**；平起平坐的话，问「村子」会被沿边扩出一大片。
2. **扩几跳、每档带几条？** 一跳 ＋ 每档 2~3 条是保守起点，但要定数。
3. **要不要动打包脚本？** 只往 `runtime.json` 里塞就不必动；代价是**每次改边表都要重编重签**。

## 附：本次核查用到的读数

- 包 558 档 ／ 现役 482 档 ／ 差 +76；`era`·`lifecycle`·`status`·`certainty` 在包文本里**各 0 次出现**；`secret` 层表达全库 **2 条**（都在 `politics.clans-charas-cortain-secret` 一档）；**444/558 = 80% 的档只有 2 条表达**。
- `content-graph.json`：`graph_version awake.worldbook.content-graph.v1`，节点 2733、边 4841。
- `runtime.json` 顶层键：`entries / extensions / identities / indexes / packageId / referrals / revision / schemaVersion / version / worldId`；`indexes` 只有 `domainToEntryIds` ＋ `keywordToEntryIds`。
- 身份清单 **12 条**，无 `awake:identity:bandit`。
