# 城堡铺开批（67 座）· 报告

**日期**：2026-09-14　**线**：世界书　**阶段**：P1（城堡建档，全量 67）　**状态**：**已完成**（67 档 / 编译 errors 0 / 联系探针 135·135 / 矩阵 133·133 / 质检硬 0 软 0；另含**检索权重整改**，见第九节）

---

## 一、本批做了什么

承接 PILOT（3 座城堡端到端验通，见 `CASTLE-LINKAGE-PILOT-20260914.md`），本批把**全部 67 座城堡**建为知识词条，并借此把「村庄 ↔ 上级城堡」的联系在**词条层**打通。

- 档型：`doc.geography.castle-<slug>`（新类目；`id` pattern 允许 `castle-*`）。
- 规模：**67 档净新增**（合 PILOT 3 档；`authoring-out` 与 WS `authoring/` **双写**）。
- 来源：独立登记 `source.calradia.game.castles`（快照 `game-castles-desc.txt`，每堡一行）。

## 二、素材面（为什么 quote 是「堡名」）

| 面 | 实况 |
|---|---|
| 官方城堡描述文 | **零**：castle 67 座 `descriptionText` 全空；localization `Settlement.text.castle*` 92 条全是 `castle_village_*`，纯城堡文本 **0** |
| B 级编年史 | **基本无城堡内容**（4 个 chronicle 文件「堡/Castle」仅 1 命中） |
| A 级可用锚 | **官方城堡名**（`Settlements.Settlement.name.castle_*` → 如 `瓦拉戈斯堡`）＋ DB 事实（culture／owner／prosperity／sceneName）＋**下辖村的官方描述文**（内容主体由村描述聚合） |

⇒ `quote` ＝ 官方城堡名，`quote_hash = sha256(quote)`；快照含该名以供 register 定位（`SourceEvidenceMatcher`）。

## 三、联系机制（本批核心）

检索面＝`title + aliases + 锚点` 上的**双向子串**：

- **堡档** `aliases.zh-CN = [堡名, 城堡] + 下辖村名`；`aliases.en = [En名, sid] + 下辖村En名`
- **村档**（P2 待做）`aliases` 增上级聚落名
- `entity_ids = [entity.settlement.<sid小写>]`（自身聚落锚）

⇒ 问村名可召回堡档、问堡名可召回村档，**无需改 C#、无需 content-graph 边**（运行时 `content-graph.json` 无聚落边这一事实不变）。

### 规模分布

| 文化 | 城堡数 | 下辖村合计 |
|---|---|---|
| empire（北/南/西） | 25 | 50 |
| aserai | 9 | 18 |
| khuzait | 9 | 18 |
| battania | 8 | 16 |
| sturgia | 8 | 16 |
| vlandia | 8 | 14 |
| **合计** | **67** | **132** |

- **65 堡辖 2 村；2 堡辖 1 村**（`castle_V1` 于桑克堡、`castle_V4` 奥曼法德堡，档内按「辖…一村」写）。
- 273 村归属：**132 → 城堡（67 座）／141 → 城镇（53 座）**（与 PILOT 实查一致）。

## 四、L2 三层写法（64 条手写 ＋ 复用 PILOT 3 条）

- **assert**（＝`summary`／断言正文）：中立转写，含「堡名＋所属势力＋地理位置＋下辖村＋主要生计」；起句按堡变化（设在／压在／卡在／依着／据…而立／守着／立在／藏在），避免套印。
- **rumor**（10 身份）：堡中视角 ＋ 村民视角的口语对位。
- **detail**（4 身份：headman／merchant／soldier／noble）：扼守地形＋两村产业类型各一＋物流/抽成/驻军等门道。
- **文化口径**（质检词表 hard）：巴旦尼亚用 氏族/族人/长老、**禁**朝廷/敕令；斯特吉亚用 波耶/领主会议；帝国用 元老院/行省；库赛特用 汗帐/汗令；阿塞莱用 长老/埃米尔；瓦兰迪亚用 王命/封臣。

## 五、两个官方数据坑（照官方串写，不"修正"）

1. `castle_A7`（乌格巴堡）下辖村 **`乌格巴` 的官方 CNs 错挂他文**（一段编年史「执政官加里俄斯」文字）⇒ 该堡 L2 **不用**该错挂文，只据 DB 事实与邻村 `本盖兹`（旱谷口橄榄村）落笔。
2. **堡名与首村名 CN 常不一致**（如 `castle_S3`＝**涅维扬斯克堡** vs 村＝**涅夫扬斯克**）⇒ 一律照官方串写。

## 六、验收

### 6.1 结构自检（编译前，`_verify_castles67_20260914.py`）

`检查 67/67 档；问题数: 0`——双写一致、`quote_hash`、快照 hash、`aliases` 收下辖村名、`entity_ids` 自身锚、`min_detail==layer` 全过。

### 6.2 六步编译

```
档数: 448  需注册: 67
[1/5] register 67/67
[2/5] select -> selection.d87ae2cd09e3408da702440b5dfc945e items: 448
[3/5] approve -> approval.9d571b6d66eb43aeb63292348ee3e0c9
[4/5] proof -> compile.3db15ff39d134295ac73a66870e62628
[5/5] compile -> compiled/geo1-castles1  manifest: 02e89fc6742cf413  validation: 21  errors: 0
```

包 `compiled/geo1-castles1`（448 档）；`validation` 21 条**全非 error**。
**register 实测耗时约 15 分钟**（67 档；journal 累积回放导致的近二次方变慢，P4 会更长）。

### 6.3 联系探针（全量 67 堡 × 2 ＋ 1 对照）

`_castle_link_probe_20260914.py run`（135 条 query）→ **PASS 135 / FAIL 0**：
- **67/67**「堡官方名」查询命中该堡档；
- **67/67**「下辖村名」查询**亦命中该堡档**（＝联系双向成立）；
- 1 条对照（`子虚堡`）→ `not_found`。

⇒ 词条层「村庄 ↔ 城堡」互为入口，全量成立。

### 6.4 矩阵探针（CS 段）

`matrix2-spec.json` 扩至 **133 行**（旧 125 ＋ CS1–CS8），探针输 `matrix2-result-full24.json` → 判定 **PASS 133 / FAIL 0**。

| 行 | 查询 | 身份 | 结果 |
|---|---|---|---|
| CS1 | 突比力斯堡 | 商人 detail | known ✅ |
| CS2 | **法纳卜**（下辖村名） | 商人 detail | known ✅（反查到**突比力斯堡**档） |
| CS3 | 瓦拉戈斯堡 | 贵族 detail | known ✅ |
| CS4 | 乌斯托科堡 | 村民 rumor | partial ✅（村民能力上限层） |
| CS5 | 于桑克堡（单村堡） | 商人 detail | known ✅ |
| CS6 | 阿布·科梅尔堡（巴旦尼亚） | 商人 detail | known ✅ |
| CS7 | 哈坤堡（库赛特） | 商人 detail | known ✅ |
| CS8 | 子虚堡 | 商人 detail | not_found ✅（对照） |

**回归观察**：旧村行 IV20–IV31（查村名）现会**同时召回其上级城堡档**（如 IV20「于桑克」首发 `【于桑克堡】`），村档亦在——**联系按设计生效**，且旧行预期片段全部仍在，125 行零回归。

### 6.5 越限自检（`_selfcheck2_20260913.py`）

`docs: 448 / expressions: 993 / grants: 6171 / denies: 3 / passed: true`，违规 **0**；67 堡档**无身份覆盖告警**（每档 rumor 10 身份＋detail 4 身份齐备）。

增量对账（前基线 381 档／859 exprs／5233 grants）：**+67 档 / +134 expr / +938 grant**（＝67×2 expr；67×(10+4) grant）**全部自洽**。

### 6.6 文本质检（`awake-prose-qc`）

`yaml=448 扫到正文=448 硬=0 软=0` → `docs/evidence/awake-prose-qc-20260914-castles/PROSE-QC-CASTLES.md`。

### 6.7 名录（`_gen_name_index_20260913.py`）

`rows=448 exprs=993 grants=6171 cross_dup_hashes=6`——**cross_dup 零新增**（堡档只把村名放进 `aliases`、**不引村档 quote**，设计上已规避）。

## 七、产物清单（未提交）

- **档**：`castle-*.yaml` × 67（`authoring-out/` ＋ WS `authoring/`）
- **生成器**：`_rollout_castles1_gen_20260914.py` ＋ L2 分组素材 `_l2_c_{aserai,battania,empire_n,empire_s,empire_w,khuzait,sturgia,vlandia}.json`
- **取数**：`_castle_fetch_20260914.py`／`_castle_inventory_20260914.json`
- **六步**：`_castles1_sixsteps_20260914.py`（OPBASE `castle1a20260914`）
- **探针**：`_castle_link_probe_20260914.py`；矩阵 `_add_matrix_castle1_20260914.py` ＋ `_verify_matrix2_20260913.py`（CS 段）
- **结构自检**：`_verify_castles67_20260914.py`
- **包**：`compiled/geo1-castles1`
- **质检**：`docs/evidence/awake-prose-qc-20260914-castles/`

## 八、后续（P2–P5，待令）

| 阶段 | 内容 | 规模 |
|---|---|---|
| P2 | 村庄档补 `entity_ids`（自身小写 sid）＋ `aliases` 增上级聚落名 | 273 档改 |
| P3 | 城镇档补 `entity_ids` ＋ `aliases` 增下辖村名 | 53 档改 |
| P4 | 全量重编译（register 因 journal 累积近二次方，最耗时）＋矩阵/自检/名录/质检/报告 | 一次 |
| P5 | 命名规范补 `castle` 类目 | 1 处 |

> 注：本批 register 已在 journal 累积下显著变慢（见 6.2）；P4 约 390 档改＋增，register 时间会进一步拉长，须后台跑、只看文件系统判进度。

---

## 九、附：检索权重整改（同名聚落，09-14 追加）

### 9.1 现象（Max 指出）

**本作「城堡与其下属村同名」是常态**：实查 67 座城堡中 **66 座**有同名下属村（堡名＝村名＋「堡」），唯一例外 `castle_S3`（涅维扬斯克堡 vs 村＝涅夫扬斯克）系译名差异。

⇒ 堡档 `aliases` 收了下辖村名（联系机制所需），于是**问村名必然同时召回堡档**。这是**正常现象，不是数据缺陷**——要动的是**权重**。

### 9.2 根因：此前根本没有权重

`src/WorldKnowledgeQueryService.cs` 的 `FindCandidates()` 原来以

```csharp
.OrderBy(x => x.Id, StringComparer.Ordinal)
```

收尾——**纯 Id 字母序**。于是 `geography.castle-*`（`c`）**恒**排在 `geography.village-*`（`v`）之前，**问村名先答堡档**；`town-*`（`t`）同理被夹在中间。这与相关度无关，纯粹是 id 拼写造成的。

### 9.3 整改：按「谁是被问的那个」排前

`FindCandidates()` 改为按命中质量排序（同质量再比匹配串长度降序，最后按 Id 稳定）：

| 档 | 判据 |
|---|---|
| 0 | **标题即所问**（`Title == query`） |
| 1 | **别名/关键词即所问**（任一 `Keyword == query`） |
| 2 | 标题互为子串（`title ⊂ query` 或 `query ⊂ title`） |
| 3 | 仅关键词子串 |

新增私有静态 `MatchQuality(entry, text)` 承担分档，不改变原有**命中集合**（谁的候选集不变），**只改顺序**。

### 9.4 验收

| 项 | 结果 |
|---|---|
| smoke（`worldbook-runtime-smoke`） | **rc=0 PASS** |
| 权重专项探针（`_weight_probe_20260914.py`） | **7/7**：村名 → 村档首发；堡名 → 堡档首发；镇名 → 镇档首发 |
| 矩阵全量回归 | **133/133**（旧 125 行零回归） |
| 联系探针全量 | **135/135**（联系未断） |
| 旧村行排序 | IV20「于桑克」→ `[…village-usanc, …castle-usanc-castle]`，**村档首发、堡档紧随** |

**代码线正式三段闸口**（`-p:BannerlordApi=1.3.15`）：

| 闸口 | 结果 |
|---|---|
| `dotnet build -c Release -p:BannerlordApi=1.3.15` | **0 警告 / 0 错误** |
| `tools/worldbook-runtime-production-smoke` | **passed=31 failed=0** |
| `dotnet build ../AWAKE.Tests/AWAKE.Tests.csproj -c Release` | **编译通过（0/0）** |

### 9.5 取舍声明（必须说清）

世界书线的标准管线里写着「**红线：不碰 `AWAKE/src/`**」。本次**按 Max 的直接指示**改了 `src/`（检索排序属模组本体代码，不在世界书档内）。
⇒ 这是**跨线动作**：改动只此一处、单文件、不新增文件（故 `AWAKE.Tests`／`tools/*` 的**显式 `<Compile Include>` 清单无需同步**）；已按代码线标准过 **smoke + 模组构建**。若后续希望此类改动归代码线独占，我下次改回「出方案不改码」。

