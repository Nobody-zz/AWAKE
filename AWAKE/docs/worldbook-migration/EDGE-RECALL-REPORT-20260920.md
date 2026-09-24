# 互引边表接进召回 · 完工报告（2026-09-20）

> **一句话**：边表按现行判据**重算到全量**（1077 → 1394 边，覆盖 399 → 483 档），**编进了包**（`entries[].extensions.links`，1286 条边落在 445 档上），**接进了召回**（第二趟扩召回）；并用**变异检验**证明「只往候选表末尾追加、不改动任何既有答案」。
>
> 开工令：甲方 `可以，做`（承接我报的「能做、不用改架构、但先重算边表」）。

---

## 〇 为什么不能直接把旧边表接进去

三件事，按重要性排：

1. **旧边表只覆盖 399 档** —— `should-link.v2.json`（09-18 判据，语料 482 档）而**最近三批新增的 76 档里有边的正好是 0**。直接接进去，军事／经济／暗面三批**一档都进不了这条腿**。⇒ 第一件事是**重算**，不是改代码。
2. **部署形态卡死** —— 打包脚本要求包里**恰好** `manifest.json ＋ runtime.json ＋ index.json`（`tools/assemble_worldbook_package.ps1:35,202,204`）⇒ 边表**只能塞进 `runtime.json` 或 `index.json`**，不能新开第四个文件。
3. **召回口子已经留好** —— `FindCandidates` 早就是**两臂经 RRF 合并**（字面腿＝关键词索引＋兜底 term 索引；语义腿＝IPC；`WorldKnowledgeRankFusion.Merge`，k=60）。但边是「条目→条目」、**需要种子**，所以**只能作第二趟扩召回**，不能当平行臂。⇒ **加腿不用新写融合算法。**

---

## 一 边表 v3（重算）

**判据一个字没改**，只是把 v2 的判据**重跑到 558 档语料**上——不重写 K1，是为了不造平行实现。
参数沿用：`DF_MAX=12`／`NAME_MIN,NAME_MAX=2,10`／`CONCEPT_MARK="settlement-types-"`。

| 桶 | 边 | 其中互提 | 进包吗 |
|---|---:|---:|---|
| 专名边 `proper`（DF≤12） | 770 | 368 | ✅ |
| 高频专名边 `hubproper`（DF>12 的真地名） | 516 | 47 | ✅ |
| 类别星形边 `star`（→ 概念词条） | 108 | 1 | ❌ 剔除 |
| **审计件合计** | **1394** | **415** | |
| **进 registry** | **1286** | | |

- 名字池 845（留 841；丢纯泛词 4；保留枢纽名 19）。
- **为什么剔 `star`**：那是「某类别下的所有东西都互指」，是**检索噪声**不是互引（进包里只会让「村庄」召回全部村庄）。剔掉后 registry 只剩 `proper`／`hubproper` 两种。

**覆盖（甲方关心的那条）**：

| | v2（482 档语料） | **v3（558 档语料）** |
|---|---:|---:|
| 边 | 1077 | **1394**（+317） |
| 有边参与的档 | 399 | **483**（+84） |
| 本代新增 76 档里有边的 | **0** | **61** |

⇒ 缺口补上了：三批新档里 61 档现在有边参与，剩 15 档确实无边（多为孤立的货品/牲畜档，正文不点别人的名——**这是正常状态，不是缺陷**）。

## 二 编译输入件

出**两份**，各有各的用途：

| 件 | 路径 | 用途 |
|---|---|---|
| 审计件（带 `evidence` 原文句） | `docs/mappings/worldbook-should-link/20260920/should-link.v3.json` | 人工逐条否边用 |
| **编译输入件**（只机器字段） | `docs/worldbook-studio-plan/link-registry.v1.json` | 编译器读它 |
| 其 schema | `docs/worldbook-studio-plan/link-registry.v1.schema.json` | `additionalProperties:false` |

生成时带三条硬断言：**端点必须在包内**、**无自环**、**同一 `(from,to,viaName)` 不重复**。

**判据跑在编译产物上**（不是重写一份跑在 yaml 上）——否则就是平行实现，将来必然对不上。

## 三 入包（Studio 改四处）

| 文件 | 改什么 | 为什么这么改 |
|---|---|---|
| `Workspace.cs` | `EnumerateSchemaInputFiles` 名单加两个文件名 | 快照有「**声明的输入闭包 == 实际读入清单**」这条不变量（测试 **F73**）⇒ 读了没声明，那条断言就红 |
| `ValidationServices.cs` | 新增 `LinkRegistrySnapshot` ＋ `LinkRegistryService` | **单开服务，不并进 `RegistrySnapshot`** —— 后者有金标基线逐字节比对，加字段必打红 |
| `Application.cs` | 挂进快照 ＋ `source-report.json` 记 `link_edges`／`link_registry_hash` | 缺席时的可见性靠产物给，不靠诊断 |
| `RuntimePackageCompiler.cs` | 有边时才写 `entryExtensions["links"]` | 不写空数组，免得包变大、diff 变吵 |

**两个设计决策**（都写进代码注释了）：

- **文件缺失时一律不报诊断**（只在 `source-report.json` 记 `link_edges: 0`）。报 warning 会把既有诊断金标打红；而「没有边表」本来就是合法状态（等于今天的现役行为）。
- **有边才写** —— 全空数组会让 558 档每档都长一截，`git diff` 也变吵。

### 验收（五项全 OK）

产物 `compiled/geo1-v22-links`（⚠️ **是产物名不是永久地址** —— 编译门会把被判回收的产物挪进 `quarantine/`）：

| 项 | 读数 |
|---|---|
| `source-report.json` | `link_edges=1286`、`link_registry_hash=3B249A4EDD615E4BA590AE0EB8FAC1D9AFAFCD3BE5197C978B93C5C9EBD698FD` |
| 带 `extensions.links` 的条目 | **445** ← 恰好等于 registry 里有出边的档数 |
| 包内 links 边总数 | **1286** |
| 端点不在包内的边 | **0** |
| id 集合 | 与上一包**相同**（新增 0／消失 0） |
| **逐条对拍** | **除 `extensions` 外，558 档每字段逐字节相同（变化 0 条）** ⇒ **只加了边，内容一个字没动** |

- `validation: Valid=True / total=21 / error=0 / warning=21`，21 条**全是 `WB-INDEX-AMBIGUOUS`**（城堡·村庄同名，**本批 0 命中**，是自 v11 起的既有常态）。
- 进包边分布：`bucket` proper 770／hubproper 516；`strength` strong 1119／weak 167；`usableAs` forward 1286／**backward 415**（＝互提边，只有它们能反向用）。

## 四 接进召回（运行时改三处）

| 文件 | 改什么 |
|---|---|
| `WorldKnowledgeModels.cs` | `WorldKnowledgeEntry.Links`；新增 `WorldKnowledgeLink`（`To/ViaName/Strength/Bucket/UsableAs`）；`WorldKnowledgeQueryResult.LinkIds` |
| `WorldKnowledgeLoader.cs` | `LoadLinks` —— 缺失／旧包／自环**一律只是「没有边」，不报错**（旧包要能照常跑） |
| `WorldKnowledgeQueryService.cs` | `FindCandidates` 之后加**第二趟** `ExpandByLinks`；命中扩进来的条目时 `LinkIds` 记一笔；`match_mode` 带 `+link` 后缀 |

**机制**：`LinkExpandPerSeed = 2`（每颗种子最多带 2 条）、`LinkExpandMaxTotal = 3`（一趟最多扩 3 条）。
`LinkIds ⊆ HitIds`（语义上扩进来的一定也在命中里）。

**一条刻意的设计**：扩进来的条目**不新造「低权重」**，而是**直接追加在候选表末尾** ＋ 走既有的 ByteBudget 截断 —— 不引入第二套权重体系。
扩进来的条目**照常过身份闸**（`HasMatchingDeny` ＋ `SelectExpression`），**不在这一层做权限判断**。

## 五 三对照 ＋ 回归（`tools/_link_recall_probe_20260920.py`）

造题 558 条（有出边 445 ＋ 无出边 113），身份固定 `profile.noble`（选高能力身份，免得把「被权限挡了」误读成「没扩到」）。

| 对照 | 要证什么 | 读数 | 判 |
|---|---|---|---|
| **A 阳性** | 有出边的档拿自己标题问 ⇒ 带 `+link` | **439**／445 带 `+link`，其中 **430** 真把扩进来的条目送到嘴边 | ✅ |
| **B 阴性①**（换包） | 同一批问话跑**上一包**（一点边都没有）⇒ `+link` 必须 0 | **0** | ✅ |
| **C 阴性② 变异检验** | 把带边包的边**全清空**、其余一字不动，喂**同一份代码**再跑 | **0** | ✅ |
| ↳ 附带 | 变异包 `hits` 与上一包**逐条相同**（0 条不同） | 0 | ✅ **这条才是「边表是唯一变量」的证据** |
| **D 回归** | 旧包 `hits` 必须是新包 `hits` 的**前缀** | 破坏 **0** 条；末尾多出 **461** 条 | ✅ |

**结论：通过**（阳性 439、阴性① 0、变异检验 0、前缀破坏 0）。

⚠️ **踩过的一个对照设计错（留痕）**：第一版把「问没有出边的档」当阴性②，跑出 31 条 FAIL —— 那是**我的对照设计错，不是代码错**。扩召回的种子是**候选表**，不是被问的那一档，候选里坐着别的有边条目时照样会扩。
⇒ 改成**变异检验**（把边清空、同一份代码再跑），并把「无出边档仍带 `+link`」**降格成机制注记**写进输出（113 条里 31 条带 `+link`，正常）。

**样例**（真的送到嘴边的）：

| 问 | 扩进来 | 送到嘴边的话（尾） |
|---|---|---|
| 《要人》 | `politics.underworld-gangs` | 「这些人多半有两副面孔：在城里是体面商人，出了城就换了装束去当土匪。」 |
| 《农奴》 | `economy.goods-tools` | 「工具 250 一件，铁器里销量最稳的一样，矿区和农田两头都要。」 |
| 《啤酒》 | `economy.goods-wine`, `economy.goods-grape` | 「葡萄 20 一件，十六个葡萄园出产…酒坊就在产区边上，所以鲜葡萄很少走出本区。」 |
| 《奶酪》 | `economy.goods-butter` | 「它跟奶酪总是一起出、一起走，收货的时候按车配，单要一样不划算。」 |

## 六 其余套件（回归）

| 套件 | 读数 |
|---|---|
| 检索烟测 | `RETRIEVAL_GATE PASS`（A=10/13、B=9/10、ALL=19/23；negative／overmatch 均达标） |
| Studio `AuthorityGate` | 5/5 |
| Studio `BatchTests` | 23/23 |
| Studio `Draft.Tests` | 107/107 |
| 生产烟测 | 6 次里 **5 次 31/0**、1 次 30/1（见 §七-②） |

## 七 两条红项（都**不是这次改出来的**，未擅自动）

### ① Studio `A3.3 preview golden` —— 金标指向 OneDrive 旧副本

报 `baseline_commoner input file closure/hash changed`。**根因**：该金标的 `input_files` 记的是 **OneDrive 副本**路径下的 schema 清单
（`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\worldbook-studio-plan\`），而 `FindSchemaRoot` 解析出的正是这份副本 ⇒ 我在本仓 `docs/worldbook-studio-plan/` 新增两个文件后，闭包变了。

**已做因果验证**：把那两个新文件临时移开 ⇒ 该项不再报此错。
⇒ 这是**既有金标的路径陈旧问题**（金标文件本身 `git status` 干净，未被我改）。**未处置**（处置＝改金标指向，属另一件事，须甲方点头）。

### ② 生产烟测 `store-switch-blocks-old-load` 偶发失败

重复跑 6 次 ⇒ 31/0、30/1、31/0、31/0、31/0、31/0，**约 1/6 抖动**。该用例（campaign reset／store 切换）**与互引边无关**。**未深挖未复现定位**。

## 八 契约已补

`docs/worldbook-studio-plan/RUNTIME-MAPPING-CONTRACT.md`（**三边唯一的接口约定**）新增：

- 〈编译输入：互引边表〉整节 —— 三件东西别混（`content-graph` ／ `referrals` ／ 互引边表）、文件与形状、**五条编译规则**、部署形态卡死、隔离验收四对照表。
- 字段映射表加一行（标明**非档内字段**，列它只为「一个地方能查全所有进包的东西」）。
- 编译规则节加一条交叉引用。

## 九 产物与脚本

| 类 | 清单 |
|---|---|
| 边表 | `tools/_link_registry_20260920.py`、`_link_registry_report_20260920.txt` |
| 编译链 | `tools/_link_chain_20260920.py`（`OPBASE=link20260920a`） |
| 探针 | `tools/_link_recall_probe_20260920.py`、`_link_recall_probe_20260920.txt` |
| 读数核对 | `tools/_link_report_numbers_20260920.py`、`.txt` |
| 产物 | `compiled/geo1-v22-links`（**产物名**，非永久地址） |

## 十 未做

- **不上线**（v22 只在 `compiled/`）、**不提交**（按纪律等甲方发话）。

### 关于当初那三条待裁（口径要说清）

甲方 `可以，做` 授权的是**我在可行性核查里报的那套方案**（重算边表 → 塞进 `runtime.json`／`index.json` → 第二趟扩召回）。三条待裁**不是逐条单独裁定**的，我按报过的方案落，具体取值如下——**任一取值都可以改，成本很低**：

| 待裁项 | 本次落法 | 若要改，改哪里 |
|---|---|---|
| ① 扩进来的该不该进正文 | **进**（追加到候选表末尾，由 ByteBudget 截断；不是只作提示） | 想退成「只作提示」＝把这批 id 从命中挪到一个只显示不送正文的字段 |
| ② 扩几跳、每档带几条 | **一跳**；每颗种子最多 **2** 条、一趟最多 **3** 条 | `WorldKnowledgeQueryService.cs` 两个常量 `LinkExpandPerSeed`／`LinkExpandMaxTotal` |
| ③ 要不要动打包脚本 | **没动** | 边表塞进 `runtime.json` 就要求三件套形态不变；这是当初「不能新开第四文件」的直接结论 |
