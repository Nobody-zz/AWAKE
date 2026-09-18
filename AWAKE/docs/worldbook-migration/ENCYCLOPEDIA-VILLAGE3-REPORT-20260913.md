# 百科化村庄铺开批 3 验收报告（帝国余 48 村）

> 2026-09-13 · geo1-full15（full16 补丁批为终态） · 终态被测包 `tools/worldbook-studio/workspace/full-geo1/compiled/geo1-full16`

## 一、范围与产出

- 村庄全量 273 村按文化圈铺开的第三文化圈余量：**帝国 97 村拆两批，本批＝排序后 48 村**（`castle_village_EW8_2` … `village_EW6_4`）。至此**帝国 97 村全数入档**。
- 48 档 `village-*.yaml`，L1 底座自动（独立快照 `game-villages-desc-emp2.txt`＋来源登记 `source.calradia.game.villages-desc-emp2`，不触碰批 1/2 快照 hash），L2 逐村手写三层文案（villageType 与 boundSettlement 入文）。
- 双写：`docs/worldbook-migration/projection/authoring-out/` ＋ `tools/worldbook-studio/workspace/full-geo1/authoring/`。

## 二、编译与矩阵（geo1-full15）

| 项 | 结果 |
|---|---|
| 六步链 | register **246/246** → select → approve → proof → compile **0 错误**（warning 均既有 WB-INDEX-AMBIGUOUS 类型） |
| 全库规模 | **246 档 / 589 表达 / 3343 grants** |
| 矩阵 | **113/113 PASS**（旧 107 行回归＋新 IV14–IV19） |
| 自检 v2 | 违规 **0**（警告 18 条全旧挂账） |
| 名录 | 246 行；cross_dup **2**（charas 家族批旧挂账，本批新增 1 处已清） |

### 新增探针 IV14–IV19
- IV14 瓦忒亚-村民rumor → partial（rumor 文案命中）
- IV15 伽伦戈利亚-商人detail → known（detail 文案命中）
- IV16 玻瑞阿戈拉-贵族detail → known
- IV17 瓦忒亚-村民detail → partial（runner 钳制到身份能力上限，与批 1/2 一致）
- IV18 拉耳图绪斯-头人detail → known
- IV19 镜花谷-商人 → not_found（不存在对照）

## 三、🆕 交叉引文整改：sethys-river 借引再清障

名录新增 1 处 cross_dup：`sethys-river`（塞堤斯河档）整句借引了本批村庄戈耳科律斯（ES2_2）描述文的**首句**——`戈耳科律斯坐落于歌里亚河畔，那是塞堤斯河流经三河河谷最南端的支流。`

**裁定（同 mount-iltan／批 2 先例）**：描述文主引权归村庄档（主消费方），河档改小子串引用 `塞堤斯河流经三河河谷最南端的支流`（经 DB 校验为官方描述文**连续片段**）。整改落在 `_fix_sethys_gorcorys_20260913.py`（文字＋hash 同上方向重算，双写同步）；full16 补丁批重编译收官。

## 四、过程说明

- 本批生成器与六步脚本均从批 2 派生（独立 OPBASE、独立输出包、独立快照源），管线零改动。
- hash 同步沿用批 2 修正后的**向上配对**写法（quote_hash 在 quote 上方），未再复发方向错。

## 五、挂账与下一步

1. 余下文化圈续铺：瓦兰迪亚 36／库赛特 35／巴旦尼亚 33／斯特吉亚 32（合计 136 村）。
2. charas 家族批 cross_dup=2 旧挂账未清。
3. PoC3 CraftedItem 聚合通道仍待排期。
4. 本批改动未提交（48 档 yaml＋生成器＋六步脚本×2＋探针＋整改脚本＋报告＋名录）。
