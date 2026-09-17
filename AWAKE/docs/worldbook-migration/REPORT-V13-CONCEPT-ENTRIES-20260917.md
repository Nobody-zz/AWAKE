# v13：聚落「概念词条」＋ 泛问词治理（B+A）

**日期**：2026-09-17
**甲方原话**：「B+A，另外把村庄、城堡、城镇对应的概念也做成词条。而不是让他成为不明不白的泛用关键词」
**产物**：`ModuleData/Worldbook/packages/calradia/`（v13f，451 条）
**门禁**：`RETRIEVAL_GATE PASS`（读数与 v12 基线逐用例一致）

---

## 一、这一轮改了什么

甲方点的三件事，加一件必须做的事（改了内容就得重编上线、过验台门禁）：

| # | 事 | 落点 | 结果 |
|---:|---|---|---|
| A | 清数据：去掉 389 条聚落上的裸类别词 | 三条概念档 + 389 条聚落档（编译器输入） | 村庄 272 / 城堡 67 / 城镇 49 / 德里亚特的手写复合词 1 = **389** |
| B | 索引侧治本：拦住"把类别词裹进去"的键 | `src/WorldKnowledgeLoader.cs` | 规则已落；**当前空转**（真语料 0 条命中），变异检验全对 |
| ★ | 把「村庄／城堡／城镇」升格成**概念词条** | `authoring/settlement-types-*.yaml`（3 条新档） | 进包、可检索、`validation error=0` |
| — | 重编 → 上线 → 过门禁 | `_v13*` 链 + 发布脚本 | 三哈希同步，包 451 条 |

**A 的删除原值逐字留痕**：`corrections_20260917/REMOVED-BARE-CATEGORY-KEYWORDS-20260917.md`（389 行表格，
逐条给出旧/新 keywords）。

**B 的规则**：`WorldbookKeywordIndex.WrapsGenericCategoryWord` —— 一条关键词若把某个泛问词
**整个裹进去**（如 `德里亚特·村庄`），就不进检索索引。
为什么必须拦：检索是**双向子串**，问「村庄」时 `kw.IndexOf(text) >= 0` 也命中，
于是"村庄"这句短问句会被那条手写复合键整个劫走（v12 的实测形态）。
边界：只打"裹住整个泛问词"这一种形状；泛问词**自己照旧进索引**——它是概念词条的名字，
正是「村庄是什么」该落的地方。

## 二、上线件读数

| 项 | 值 |
|---|---|
| 条目数 | 451（v12 基线 448 ＋ 3 条概念词条）|
| 编译 | `validation total=21 error=0`；`manifest_hash=9830d34d…`；`result_hash=2C797012…` |
| 与 v12 的 DIFF | keywords 变 **389**（期望 389）；新增 **3**（恰是三条概念词条）；summary 变 0、expressions 变 0 |
| 包 `contentHash` | `fb046945a8ab829b…` |
| 包 `manifestHash` | `67d39f44dc27a763…`（文档集未变 ⇒ 与上一版一致）|
| 包 `packageHash` | `28f8eac371ab7f0d…` |
| 注册表 | 三哈希已同步（`ModuleData/Worldbook/manifest.json`）|
| 备份 | `tools/worldbook-studio/artifacts/repo-package-before-v13f-20260917-183325/` |

## 三、门禁读数

**三根门禁都跑了**（改的是产品代码 `src/WorldKnowledgeLoader.cs`，按纪律必须同批复跑）：

```
RETRIEVAL_GATE PASS need A_hit3>=10 B_hit3>=9 ALL_hit3>=19 negative<=3 overmatch<=5
RETRIEVAL_SUMMARY A_hit3=10/13  B_hit3=9/11  ALL_hit3=19/24   （＝v12 基线，一条不差）
MERGE_GATE     PASS need literal A>=10 B>=9 ALL>=19 merged A>=10 losses@3=0 …
IDENTITY_GATE  PASS need leaks=0 deniedFail=0 且非空转
               扫描：被排除行 78（泄漏 0）；该知道行 210（真拿到 205）；点名题失败 0
KEYWORDGUARD_VARIANT 变异检验反例=0 ｜ 真语料命中=0
```

- 逐用例对照 v12 基线：**除 1 条外 top3 完全一致**，`RETRIEVAL_MISS` 清单与 v12 **逐条相同**。
- 那 1 条是 `西米拉堡是谁的城堡？`：答案仍在第 1 位（HIT 不变），只是第 3 位从
  `castles-ab-comer-castle` 换成了 `settlement-types-castle`（见 §五 残留）。
- 变异检验（`KEYWORDGUARD`，跑在验台里、直接调真代码，不另写判据）：
  该剔的 `德里亚特·村庄`／`厄尔凡尼亚·村庄`／`某某城市` 全剔 ✔；
  该留的 `拉文尼亚`／`塔奈西斯湖`／`帝国之湖`／`Mecalovea Castle`／`村庄`／`城堡`／`城镇` 全留 ✔。
  **第一次跑它报了 4 个反例 —— 全是我造样本时的笔误（把关键词写成了 summary 位）。
  它能把"我的错"和"规则的错"分开，说明这个检验不是摆设。**
  真语料 0 条命中 ⇒ 这道闸是**防复发**用的，今天不挡任何东西。

## 四、两处回归与机理（这一轮最值钱的部分）

概念词条第一次上线时**门禁红了**，红的形状换了两次，两次的机理不同：

**① 主路命中会"堵死"兜底通道。**
`有大瀑布的村子是哪个？` 在 v12 里主路零命中 ⇒ 走共享字兜底 ⇒ 返回 4 条，含正确答案。
v13 给概念词条塞了口语同义词（村子/村落）当关键词 ⇒ 主路命中概念词条 ⇒
`FindLiteralCandidates` 只在关键词层**一无所获**时才走兜底（`WorldKnowledgeQueryService.cs:363`）
⇒ 兜底不跑 ⇒ 只剩 1 条无信息量的命中。
修法：概念词条**中文别名只留主词**（村庄/城堡/城镇），口语说法交给语义腿与综述 term 兜底。

**② 兜底排序里，2-gram 打平后按 id 排，概念词条恰好排在前。**
别名收窄后兜底恢复了（hits 由 1 回到 4），但正确答案掉到第 4 位。
机理：兜底按「与查询共享几个 term」排序，term 切的是 **2-gram**。
概念词条综述里 `…底下的村子。` 切出 `的村`、`村子`，与查询共享 **2** 个 ⇒ 与正确答案**打平**；
再按「最长共享 term → id」比，`geography.settlement-types-castle` 排在 `geography.villages-chornobas` 之前。
修法：概念词条**综述措辞改掉** —— 不出现口语同义词，也不出现「的/在/个＋类别词」这类
谁都能撞上的 2-gram。改完与门禁题集**共享 term = 0**。
检查器：`tools/_probe_concept_collision_20260917.py`（对题集逐句算共享 term，要求 0），
**已挂进重编链当门禁**（`COLLISION` 行）。

> 曾试过的两条歧路（都量过、都否掉，记在这里免得下次重走）：
> · **"共享的字越罕见越靠前"**（用 df 当罕见度）—— 实测**更糟**：`村子` df=4 比 `大瀑` df=6 更罕见，
>   概念词条反而升到第 1。**统计罕见度判不出"这是类别词"**。
> · **统计口径推泛问词表**（语料覆盖 >40 / 长度 2~3 字的 title）—— 前者只捞到 `城堡`（村庄覆盖 12、
>   城镇 14）；后者捞到 148 个、把 `沙拉斯`/`吕卡隆`/`毛皮` 这些真名也算进去，会剔掉 45 条正当关键词。
>   ⇒ 「哪些词是类别词、不是名字」是**编辑判断**，只能是一张显式的表（现落在 `WorldKnowledgeLoader.cs`）。

## 五、残留与下一步

1. **含类别词的问句里，概念词条仍会占到一个靠后的位次**（例：`西米拉堡是谁的城堡？` 第 3 位，
   经主路关键词命中）。只要不进前 3 就不影响门禁，但它和 §四② 是同一枚硬币：
   **兜底/主路把"定义型条目"和"具体答案"放在同一个尺度上比**。
   根治要动排序（属识别链路那一线），本轮**不动**——三根门禁的读数现在与 v12 逐条一致，值这个价。
2. 概念词条的 `revision=1`、`status: needs_review`；`entity_ids` 留空（概念条目无游戏锚定实体，
   且项目文档 `LORE-ENTITY-REGISTER-20260912.json` 的 `compiler_behavior` 已写明 `entity.lore.*` 不可编译）。
3. 事故留痕：`corrections_20260917/INCIDENT-OPRECORD-WILDCARD-20260917.md`
   （我用通配移走 52 个 op 记录触发了 studio 孤儿回收；43 个产物目录 + 4724 个报告目录被隔离，
   **内容没删**；已清点、未批量还原；v12 对拍基线已取出冻结）。

## 六、过程脚本与证据清单

| 文件 | 干什么 |
|---|---|
| `tools/_gen_settlement_types_20260917.py` | 三条概念词条的生成器（内容 + 两处措辞纪律的注释） |
| `tools/_fix_settlement_type_source_20260917.py` | 导出源文件 + 建来源登记表（`WB-SOURCE-001` 的修法） |
| `tools/worldbook-studio/workspace/full-geo1/_v13b_settlement_types_source_20260917.py` | 六步重编链（register→select→approve→proof→compile→验收；含撞字门禁） |
| `tools/_publish_v13_to_repo_20260917.py` | 上线到仓库侧包目录 + 同步注册表三哈希 + 回读 |
| `tools/_probe_concept_collision_20260917.py` | 概念词条正文「撞字」检查器（题集逐句算共享 term） |
| `tools/_probe_b_generic_words_20260917.py` | B 项四种统计口径并排量（否掉统计路线的依据） |
| `tools/_probe_fallback_order_20260917.py` | 复算兜底排序，定位 §四② 的机理 |
| `tools/_inventory_quarantine_20260917.py` | 事故面清点（只读） |
| `tools/_make_corrections_removed_keywords_20260917.py` | 生成 A 项删除原值留痕 |
| `tools/_smoke_v13f_20260917.txt` | 最终门禁读数（`RETRIEVAL_GATE`） |
| `tools/_merge_gate_v13f_20260917.txt`／`_identity_gate_v13f_20260917.txt` | 另两根门禁读数 |
| `tools/_baseline/geo1-v12-runtime.json` | v12 冻结对拍基线（sha256 `6f796dfe…`）|

> 链日志 `_v13b_chain_log.txt` 里 **18:38 那一段的抬头仍写着 v13e**（脚本抬头字符串忘了跟着
> `OPBASE` 改，已修）；该段实为 **v13f** 那一轮，按时间戳读。
