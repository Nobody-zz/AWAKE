# 世界书存量对账表（40 档全审）· 2026-09-13

> 依据：《世界书条目章法》v0.3 §七 + §5.2。Max 令"回去审一遍完成了的知识条目"。
> 工具：审查脚本 `_audit40_20260913.py`（命名/素材/引文/交叉覆盖）+ 自检 v2 `SELF-CHECK-20260913-v2.json`（权限/规模/身份覆盖）+ 红测一归类 `_redtest1_20260913.py`。
> 结论标尺：✓ 合规 ／ ⚠ 整改（不迁移）／ ✗ 迁移。**本次无 ✗。**

## 一、总评

40 档全部通过硬门可查部分：编译 0 诊断、矩阵 61/61、不变式 min_detail==layer 零违规、grant 上限零违规、deny 3 处全部合规（只在秘密档且定向）、**引文五元组 40 档零缺失、aliases 双语 40 档零缺失**、档型归类 40 档全部落位无待裁。

但 §5.2 打分维度暴露 **三类批量整改项**，其中两项比 §七 原预挂账严重：

| # | 整改项 | 规模 | 与预挂账对比 |
|---|---|---|---|
| 1 | **en title 空缺**（违 §3.4"en 不得空"） | **20 档** | 预挂账只记了 3 档，漏了老 12 档与地理批全部 |
| 2 | **纯 B 源、无 A 内核**（违 §3.1"内核唯一母本=A"） | **14 档** | 预挂账完全没有此项；含 war 批 2 档 |
| 3 | 交叉覆盖警（quote_hash 跨档） | 3 组 | 预挂账 2 组，**A89432B9 为本次新发现** |

## 二、整改项明细

### 2.1 en title 空缺 ×20（批量小改项）

`charas-town` `charas-cortain-secret` `charas-reign` `varcheg-town` `varcheg-swap` `husn-fulq-town` `husn-fulq-tales` `lycaron-town` `lycaron-mines` `lycaron-rock-tales` `der-vill` `der-furs` `kach-own` `kach-land` `kach-tales` `lac-lake` `lac-tales` `dawn-mtn` `dawn-stew` `dawn-taboo`

处置：一次性批量补齐（en=官方英文名，先查后写纪律适用于新增的译名），建议随家族批或单开半批执行，补完重编译＋名录重刷。

### 2.2 纯 B 无 A 内核 ×14（分两路处置）

| 路 | 档 | 处置 |
|---|---|---|
| **A 缺因待核**（多为地理/文化，游戏 DB 本就可能无描述文） | charas-bay / charas-origin-tales / dawn-mtn / dawn-stew / dawn-taboo / der-furs / der-vill / kach-land / kach-own / kach-tales / lac-lake / lac-tales | 逐档回查 DB 两处（localization + descriptionText，章法 §1.1 坑 1）：查无 → 在档内留"DB 无文"豁免注记，纯 B 合法；查有 → 补 A 引文 |
| **war 批带病** | **royal-guard / sturgia-military** | war 批素材 json（`_war1_material_20260913.json`）里有 A 级引文但未入档——回查为何没挂，该补则补 |

### 2.3 交叉覆盖 ×3 组

| quote_hash | 跨档 | 处置 |
|---|---|---|
| A6320996 / 3C991D66 | charas-reign × charas-cortain-secret | 已挂账：随家族批归位 |
| **A89432B9** | nahasa-desert × perassic-sea | **新发现**：同引文双投，按"同变体不双投"裁定去留 |

### 2.4 规模与身份覆盖（警告级，沿自 v0.2）

- 规模超限：`der-furs`(6 断言) `lac-lake`(6) `kach-land`(5) `charas-bay`(4) `kach-tales`(4) `paravenos`(断言3 带 5 表达)——随各自批次顺带收敛。
- 身份覆盖缺口 20 档：tales/地理类为预期缺口（章法 §3.6 口径）；军制 3 档（`sturgia-military` 缺 merchant / `royal-guard` 缺 merchant / `mamluk` 缺 soldier）在下批矩阵加行时裁定。

## 三、40 档明细

| 档 | 域 | 归类 | 素材 | en title | 警项 | 结论 |
|---|---|---|---|---|---|---|
| charas-town | politics | 实体本档 | A+B | ✗空 | — | ⚠ |
| charas-bay | geography | 侧档(挂锚点) | B | ✓ | 纯B核因·规模4 | ⚠ |
| charas-origin-tales | culture | 场景/专题 | B | ✓ | 纯B核因·覆盖 | ⚠ |
| charas-reign | politics | 事件档 | A+B | ✗空 | 交叉×2 | ⚠ |
| charas-cortain-secret | politics | 秘密档 | A+B | ✗空 | 交叉×2 | ⚠ |
| varcheg-town | politics | 实体本档 | A+B | ✗空 | — | ⚠ |
| varcheg-swap | politics | 事件档 | A+B | ✗空 | — | ⚠ |
| husn-fulq-town | geography | 实体本档 | A | ✗空 | — | ⚠ |
| husn-fulq-tales | culture | 场景/专题 | A | ✗空 | — | ⚠ |
| lycaron-town | geography | 实体本档 | A+B | ✗空 | — | ⚠ |
| lycaron-mines | economy | 侧档(挂锚点) | A | ✗空 | — | ⚠ |
| lycaron-rock-tales | culture | 场景/专题 | A+B | ✗空 | 覆盖 | ⚠ |
| der-vill | geography | 实体本档 | B | ✗空 | 纯B核因 | ⚠ |
| der-furs | economy | 侧档(挂锚点) | B | ✗空 | 纯B核因·规模6 | ⚠ |
| kach-own | politics | 事件档 | B | ✗空 | 纯B核因 | ⚠ |
| kach-land | geography | 侧档(挂锚点) | B | ✗空 | 纯B核因·规模5 | ⚠ |
| kach-tales | culture | 侧档(挂锚点) | B | ✗空 | 纯B核因·规模4·覆盖 | ⚠ |
| lac-lake | geography | 场景/专题 | B | ✗空 | 纯B核因·规模6 | ⚠ |
| lac-tales | culture | 场景/专题 | B | ✓ | 纯B核因·覆盖 | ⚠ |
| dawn-mtn | geography | 场景/专题 | B | ✗空 | 纯B核因 | ⚠ |
| dawn-stew | politics | 场景/专题 | B | ✗空 | 纯B核因 | ⚠ |
| dawn-taboo | culture | 场景/专题 | B | ✗空 | 纯B核因·覆盖 | ⚠ |
| sethys-river | geography | 场景/专题 | A+B | ✓ | — | ✓ |
| miron-river | geography | 场景/专题 | A+B | ✓ | 覆盖 | ⚠ |
| mount-iltan | geography | 场景/专题 | A+B | ✓ | — | ✓ |
| mount-erithrys | geography | 场景/专题 | A+B | ✓ | — | ✓ |
| llyn-modris | geography | 场景/专题 | A+B | ✓ | 覆盖 | ⚠ |
| saneopa | geography | 场景/专题 | A+B | ✓ | — | ✓ |
| tanaesis-lake | geography | 场景/专题 | A+B | ✓ | 覆盖 | ⚠ |
| perassic-sea | geography | 场景/专题 | A+B | ✓ | 交叉 | ⚠ |
| dryatic-mountains | geography | 场景/专题 | A+B | ✓ | — | ✓ |
| devseg-plateau | geography | 场景/专题 | A+B | ✓ | 覆盖 | ⚠ |
| nahasa-desert | geography | 场景/专题 | A+B | ✓ | 交叉 | ⚠ |
| paravenos | politics | 场景/专题 | A+B | ✓ | 表达5·覆盖 | ⚠ |
| sturgia-military | war | 专题档 | **B** | ✓ | 纯B·覆盖(merchant) | ⚠ |
| vlandia-military | war | 专题档 | A+B | ✓ | — | ✓ |
| khuzait-military | war | 专题档 | A+B | ✓ | — | ✓ |
| royal-guard | war | 专题档 | **B** | ✓ | 纯B·覆盖(merchant) | ⚠ |
| mamluk | war | 专题档 | A+B | ✓ | 覆盖(soldier) | ⚠ |
| crossbow | war | 专题档 | A+B | ✓ | 覆盖(merchant) | ⚠ |

**计**：✓ 8 档 ／ ⚠ 32 档 ／ ✗ 0 档。⚠ 档无一需要迁移——全部是"补 en title / 补 A 内核 / 消交叉 / 收规模"级整改。

## 四、整改执行记录（09-13 14:3x，Max 令"修"）

| 项 | 处置 | 结果 |
|---|---|---|
| 2.2 纯 B 核因 | DB 两处逐档回查（`_fix2_dbprobe`）：**12 档查有官方文并补 A 级引文**——charas-bay(EW1_1 加隆托海角)、charas-origin-tales(town_V7 卡拉德登陆首句)、dawn-mtn(K6_2 柯希·罗希尼词源)、dawn-stew(K6_1 山麓马匹)、der-furs/der-vill(V6_2 河狸水貂海豹)、kach-land(S1_1)、kach-own(town_S7 全文)、kach-tales(town_S7 阿基娜公主传说段)、lac-lake(EN2_1)、royal-guard(local DHbF9JvO 皇家侍卫)、sturgia-military(local k1Xr4rKn 亲卫骑兵)；**2 档豁免**（dawn-taboo 山灵禁忌、lac-tales 血染湖水——官方 DB 两处查无对应描述文，§1.1 口径下纯 B 合法） | 纯 B 14 → 2（豁免） |
| 2.3 交叉裁定 | A89432B9：泽翁尼卡城文全文归 perassic-sea，nahasa-desert 改引"泽翁娜之风"主题句（corrections_20260913/nahasa-desert.yaml.md 原值留痕）；新补引文用子串策略避免撞 hash（EN7_1/EN9_1 两轮撞车后定 EN2_1） | 交叉警 3 → 2（余 2 条为家族批挂账） |
| 2.1 en title | 20 档补齐（Charas/Varcheg/Husn Fulq/Lycaron/Deriat/Kachyar Peninsula/Lakonis Lake/Dawn Mountains；Dawn Mountains 经 DB 多语言文实证为官方英译） | en 空缺 20 → 0 |
| 重编译 | 六步编译 **geo1-full5**（快照扩容 geography2 +8 行 / war1 +2 行，source_content_hash 三处同步；validate 0 诊断） | manifest 5b0b4ff9… |
| 回归 | 矩阵 61/61 全绿；自检 v2 40 档 0 违规；名录重刷（rows=40 / exprs=186 / grants=546 / cross_dup=2） | 全绿 |

工具：`_fix2_dbprobe_20260913.py` / `_fix_batch_20260913.py` / `_fix_tail_20260913.py` / `_fix3_crosstalk_20260913.py` / `_fix4a_sources_20260913.py` / `_fix4b_sixsteps_20260913.py`。血泪两笔入册：①CLI `--workspace` 漏传导致根目录错位；②authority 六步的 operation id **每档必须独立**（共用 id 会吃到幂等缓存产生 CAS-409 假冲突）。

## 五、处置顺序建议（已完成 1–4，第 5 项随下批）

1. ~~纯 B 核因~~ ✅ 12 补 2 豁；
2. ~~war 批 A 引文回补~~ ✅（royal-guard/sturgia-military 补官方名行）；
3. ~~A89432B9 交叉裁定~~ ✅（naha 子串化，corrections 留痕）；
4. ~~en title 批量补齐~~ ✅（20 档）；
5. 规模收敛与军制身份裁定——随下批顺带。

1–4 已完成并重编译（geo1-full5）＋矩阵回归 61/61＋自检 0 违规＋名录重刷。对账表原 ⚠ 32 档中：纯 B/en title/交叉三色已清；规模与覆盖警告（§2.4）保持挂账随批收敛。
