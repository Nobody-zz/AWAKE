# 角色卡 T3 补改：突剌格 / 蒙楚格（2026-09-14）

> 线：角色卡 ｜ 性质：**整改**（改卡＋物化定义）｜ 依据：`docs/PERSONA-POLISH-BACKLOG-20260913.md`（T3）、`docs/O5-V2-CROSS-CARD-GATE-20260913.md`
> 证据：`docs/evidence/persona-t3fix-20260914/`（`before/`／`after/`／`patch_t3.py`）

## 0. 一句话

全库 76 张中，**只有这两张的 `core` 与 `selfClaimExamples` 一个「我」都没有**（纯第三人称），违反项目已文档化的「`core` 是第一人称独白」；本轮按 T3 规范补改为第一人称，**机械侧全绿、预算无回归**。

## 1. 怎么发现的

做高危点测（`PERSONA-SPOTCHECK-11-20260914.md`）时，顺带扫全库 `core` 人称（`scan_person.py`）——按密度排序，头两名是 **0.0%**：突剌格、蒙楚格。再扫 `examples` 人称（`scan_examples_person.py`）：同样只这两张 **0.0%**，其余 74 张 ≥ **2.6%**。

## 2. 为什么算缺陷，不算风格选择

| 依据 | 原文要点 |
|---|---|
| `docs/PERSONA-POLISH-BACKLOG-20260913.md` | T3＝「第三人称百科式描述…core 几乎无第一人称」⇒ **重写：换第一人称口吻** |
| `docs/O5-V2-CROSS-CARD-GATE-20260913.md` | 「**`core` 是第一人称独白**、`examples` 是台词样本——作者腔只会沉淀在这两处」 |
| 库内分布 | 74/76 皆第一人称 ⇒ 这两张是离群，不是另一条正当分支 |

**怎么漏的**：批 1 报告把蒙楚格记作「已达标（v4 试点）」而跳过；突剌格虽在批 1「10 张待改」名单内，但批 1 只动 `selfClaimExamples`（**core 重写自批 2 才开始**）⇒ 两张的 `core` 都没换人称。

## 3. 改了什么

| 卡 | 字段 | 改法 |
|---|---|---|
| 突剌格_tulag_arkit_khuzait | `core` ＋ 4 条 examples | 第三人称陈述 → 第一人称；信息量不删 |
| 蒙楚格_monchug_urkhunait_khuzait | `core` ＋ 5 条 examples | 同上；**另修一处事实自相矛盾** |

**事实修正（蒙楚格样例 1）**：原文「会盟散后，**兀儿浑乃特的族长**把一卷染了血的军功册摊到他面前……」——蒙楚格本人就是兀儿浑乃特族长，句子自指矛盾。改为「**库吉特的族长**」，与其 `identityFacts`「库吉特领袖墨速宜等对其心存不满，认为牺牲未获应有回报」一致。

**改法**：字节级定位替换，**不重排 JSON**（两张文件行数 94→94、105→105 不变）。脚本 `patch_t3.py` 四道自检：①旧串在原文唯一命中；②改后仅 `core`＋`selfClaimExamples` 变（逐字段比对）；③样例条数不变；④正文不再残留本名。

## 4. 改前 / 改后 · 同装置对比（通则①）

| 装置 | 改前 | 改后 | 结论 |
|---|---|---|---|
| 1 schema / 2 affiliations / 3 text / 4 enhancement / 5 compile | 全 PASS | 全 PASS | **同** |
| 4′ 可搬运 `scan_recite_risk.py` | 76 张 risk=0 | 76 张 risk=0 | **同** |
| 5′ 预算 `budget_audit.py 6144,16000` | 74/76 零损失，截断 2 | 74/76 零损失，截断 2 | **同（无回归）** |
| 物化 `materialize-definitions.ps1` | — | **恰 2 个 definition 变动** | 确定性成立 |
| 物化审计 `audit-definitions.ps1` | `FILES=77 VALID=1` | `FILES=77 VALID=1` | **同** |
| 文本质检 `prose_qc.py` | 硬 0 软 0 | **硬 0 软 0** | **同** |
| `core` 第一人称密度 | 突剌格 0.0% · 蒙楚格 0.0%（全库最低两名） | 突剌格 3.6%（337 字/12 我）· 蒙楚格 2.9%（452 字/13 我）；**全库最低为 2.3%**，无一张 ≤2.0% | **已修** |
| `examples` 第一人称密度 | 突剌格 0.0% · 蒙楚格 0.0% | 突剌格 3.8% · 蒙楚格 2.7%；**全库最低 2.6%，无一张 0** | **已修** |

两卡表达质量门明细：`突剌格 4 样例 E1/E2/E2b/E4 全 Y、E5 无 FAIL、PASS`；`蒙楚格 5 样例 同上、PASS`。
全量报告 `docs/AUDIT-CHARACTER-ENHANCEMENT-20260913.md` 重跑后与改前**逐字一致**（0 行 diff）。

## 5. 一处如实说明（不得省）

蒙楚格的 DSL 在 6144 档仍 **`trimmed=True`**（底座自身超长，是既存问题，与本次无关）；被丢的是**尾部 `PERSONALITY_PUBLIC`（examples）外壳段**（标准 §3.4 已登记）。
⇒ **该卡 examples 的人称改动，在当前预算下不送达运行时提示词**；送达的是 `core`——恰是本次修正的核心。卡面一致性与物化定义层已同步修正。

## 6. 改动文件

- `AWAKE/tools/persona-workbench/characters/突剌格_tulag_arkit_khuzait.persona.json`（+5/−5 行）
- `AWAKE/tools/persona-workbench/characters/蒙楚格_monchug_urkhunait_khuzait.persona.json`（+6/−6 行）
- `AWAKE/ModuleData/Worldbook/persona_definitions/definitions/突剌格_tulag_arkit_khuzait.definition.json`
- `AWAKE/ModuleData/Worldbook/persona_definitions/definitions/蒙楚格_monchug_urkhunait_khuzait.definition.json`

## 7. 一句话收尾

这不是"风格之争"——项目自己写了「core 是第一人称独白」，这两张是全库仅有的例外，且都能追到"批 1 不动 core"这一步。已按 T3 补齐，装置前后无回归。
