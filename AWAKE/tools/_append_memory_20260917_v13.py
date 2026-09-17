# -*- coding: utf-8 -*-
"""把本轮（v13 概念词条＋B+A）追加到当天记忆日志。**只追加，不覆盖。**"""
import io
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
LOG = r"D:/AWAKE-Dev/.workbuddy/memory/2026-09-17.md"

TEXT = """

---

## 追加（18:00–19:20）：B＋A 落地 —— 概念词条＋泛问词治理，并修掉两处回归

甲方原话：「B+A，另外把村庄、城堡、城镇对应的概念也做成词条。而不是让他成为不明不白的泛用关键词」

**★ 先更正上面那段「待甲方定」**：我推荐过的 **B＝"R1 改成按子串算覆盖度"已被当天实测否掉**，
别再走。三种继承口径都会**连坐大量正当入口**：段继承额外剔 **67**（全是 `X Castle`，`Castle` 段频 67）／
题面继承 **79**／关键词继承 **100**；而「专名＋类别词」（`Mecalovea Castle`／`阿特费尼亚城堡`／`沙拉斯湾`）
是**全库正当形态**，子串继承分不清它和 `德里亚特·村庄`。⇒ **"覆盖度"这条路整个是错方向。**

**落地的 B（新判据）**：`WorldKnowledgeLoader.cs` 新增 `WorldbookKeywordIndex.WrapsGenericCategoryWord`
—— 键的真子串里含**泛问词整词** ⇒ 不进索引；**泛问词自己照旧进索引**（它是概念词条的名字）。
泛问词表 = `{村庄,村子,村落,城堡,城砦,堡垒,城镇,镇子,城市}`，**显式编辑判断**。
- 真语料 **0 条命中**（防复发的空转闸；A 项已把唯一那条 `德里亚特·村庄` 从数据里清了）
  ⇒ **必须变异检验**：验台 `KEYWORDGUARD` 行、直接调真代码（该剔 3/3、该留 7/7）。
  **第一次跑报 4 个反例，全是我造样本的笔误**（把关键词写进了 summary 位）—— 能分清"我的错/规则的错"。
- **四种统计口径全否掉**（`_probe_b_generic_words_20260917.py`）：语料覆盖>40 只捞到 `城堡`（村庄 12、城镇 14）；
  长度 2~3 字 title 捞 **148** 个、把 `沙拉斯`/`吕卡隆`/`毛皮` 算进来会剔 **45 条正当关键词**；
  共享 term 的"罕见度"（df）**反向失效**（`村子` df=4 比 `大瀑` df=6 更罕见 ⇒ 概念词条反升到第 1）。
  ⇒ 「哪些词是类别词、不是名字」**只能是显式表**。

**A**：389 条聚落档各去掉一个裸类别词（村庄 272／城堡 67／城镇 49／德里亚特的复合词 1）；
删除**原值逐字**留痕 `corrections_20260917/REMOVED-BARE-CATEGORY-KEYWORDS-20260917.md`。

**★ 概念词条（3 条新档）踩的两个坑，形状不同、机理不同 —— 这是本轮最值钱的**：
1. **主路命中会"堵死"兜底通道**。`有大瀑布的村子是哪个？` 在 v12 主路零命中 ⇒ 走共享字兜底 ⇒ 4 条含答案；
   概念词条带口语同义词（村子/村落）当别名 ⇒ 别名变 keywords ⇒ 主路命中它 ⇒
   `FindLiteralCandidates` 只在关键词层**一无所获**时才走兜底（`WorldKnowledgeQueryService.cs:363`）⇒
   兜底不跑 ⇒ 只剩 1 条无信息量命中。**修法：中性别名只留主词**（村庄/城堡/城镇），口语交给语义腿与综述 term 兜底。
2. **兜底按 2-gram 共享数排序、打平后按 id 排**，概念词条恰好排前。综述 `…底下的村子。` 切出 `的村`、`村子`
   ⇒ 与查询共享 **2** 个，与正确答案（`大瀑`、`瀑布`）**打平** ⇒ 按 id 比时
   `geography.settlement-types-castle` 排在 `...villages-chornobas` 前 ⇒ 答案掉到第 4。
   **修法：概念词条综述别用"谁都会用"的字**（不出现口语同义词，也不出现「的/在/个＋类别词」的 2-gram）。
   检查器 `_probe_concept_collision_20260917.py`（题集逐句算共享 term，要求 0）**已挂进重编链当门禁**。
   ⚠️ 这是"别用那类字"，**不是**"把题面词补进条目"（后者是改语料凑命中）。

**门禁读数**（v13f 上线包，451 条）：
- `RETRIEVAL_GATE PASS`：`A_hit3=10/13 B_hit3=9/11 ALL_hit3=19/24 negative=0/3 overmatch=2/5`
  —— **与 v12 冻结基线逐用例一致**；`RETRIEVAL_MISS` 清单逐条相同。
- 唯一差异：`西米拉堡是谁的城堡？` 仍是 HIT（答案第 1），第 3 位由 `castles-ab-comer-castle` 换成
  `settlement-types-castle`（经主路关键词命中）。**残留已写进报告**：含类别词的问句里概念词条仍会占靠后位次，
  根子和坑 2 是同一枚硬币（把"定义型条目"和"具体答案"放同一尺度比），**根治要动排序，属识别链路那一线**。
- `KEYWORDGUARD_VARIANT 反例=0 ｜ 真语料命中=0`。
- 上线件：`contentHash fb046945…`／`manifestHash 67d39f44…`（文档集未变）／`packageHash 28f8eac3…`；
  `validation total=21 error=0`；`manifest_hash=9830d34d…`／`result_hash=2C797012…`。

**★ 事故（必记）**：我用**通配** `k1_op_*`/`k1_rv_*` "清理临时文件"，一次移走 **52 个历史 op 记录**
⇒ 重跑 compile 时 studio 的 `QuarantineCompileOrphans()` 按设计把 `compiled/` 下无 op 记录的一级条目清走
（**43 个 `geo1-*` 产物目录 ＋ 4724 个 `reports.*`**）；op 放回后 `validate` 又触发 recovery ⇒
**53 个 op 被标 `state=quarantined` + `WB-AUTHORITY-RECOVERY-409`**。**内容没删，只是换了位置**；
真机与仓库上线件全程不受影响。已清点未批量还原（搬 4767 个目录是又一次同类风险）；
v12 对拍基线已取出冻结 `tools/_baseline/geo1-v12-runtime.json`（sha256 `6f796dfe…`）。
留痕 `corrections_20260917/INCIDENT-OPRECORD-WILDCARD-20260917.md`。清点 `_inventory_quarantine_20260917.py`。
⇒ **两条工具纪律**：① 清理只许动本次那一个 operation，禁通配；② 恢复脚本必须打印"移走了几个"。

**★ 另一条吞错**：`WB-AUTHORITY-MUTATION-UNKNOWN`（503）是**兜底包装码**，真因在 **stderr**；
Python `subprocess.run(..., text=True)` 在 Windows 按 GBK 解码会**静默丢掉 stderr** ⇒ 只看见 503。
真因链：`MUTATION-UNKNOWN → WB-AUTHORITY-COMPILE-422 → WB-SOURCE-001`。
`source_content_hash` 比的是**登记表 `locator_root` 指向文件**的 sha256，不是数据源整库 sha256（本轮错写成 `bannerlord.db`）。

**★ 第三条**：`WB-DOC-003: entity_ids 类型不受支持（仅 hero/clan/settlement）` —— 概念条目不能写 `entity.lore.*`；
**项目文档早已写明**（`LORE-ENTITY-REGISTER-20260912.json` 的 `compiler_behavior`）。我没查就用了 ⇒ 同"知识断层"病。

**★ 第四条**：`ModuleData/Worldbook/` **不在版本管理里**（历史提交 `Remove legacy v1 worldbook…`）
⇒ 上线件不会被提交，提交只带走镜像档与文档。记忆里之前那批提交（v13/`b5…`）里的包本体其实也没进历史。

**提交**：`286a7db feat(worldbook): 聚落概念词条（村庄/城堡/城镇）+ 泛问词治理（B+A）`，**28 个精确路径**
（`git log -- <path>` 已逐条复核；同仓另有别的 agent 在途，未 `-A`）。

**产物**：报告 `docs/worldbook-migration/REPORT-V13-CONCEPT-ENTRIES-20260917.md`；
生成器 `_gen_settlement_types_20260917.py`；重编链 `workspace/full-geo1/_v13b_…py`（OPBASE 已到 v13f）；
上线 `_publish_v13_to_repo_20260917.py`；四个新探针（撞字／泛问词表／兜底排序／关键词索引）。

**工具坏习惯（写下来）：同一文件的多处 Edit 不能并行发** —— 三个 Edit 并行发，两条被后写的那次覆盖掉，
我是在"改完重跑生成器发现别名没变"时才发现的。**同一文件改多处，一律串行。**
"""

with io.open(LOG, "a", encoding="utf-8", newline="\n") as fh:
    fh.write(TEXT)
print("追加 %d 字符 -> %s" % (len(TEXT), os.path.basename(LOG)))
