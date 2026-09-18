# 百科化村庄铺开批 2 验收报告（帝国 49 村）

> 2026-09-13 · geo1-full13（full14 补丁批为终态） · 终态被测包 `tools/worldbook-studio/workspace/full-geo1/compiled/geo1-full14`

## 一、范围与产出

- 村庄全量 273 村（素材 273/273 有中文名＋描述文）按文化圈铺开的第二文化圈：**帝国 97 村拆两批，本批＝排序前 49 村**（`castle_village_EN1_1` … `castle_village_EW8_1`）。
- 49 档 `village-*.yaml`，L1 底座自动（独立快照 `game-villages-desc-emp1.txt`＋来源登记 `source.calradia.game.villages-desc-emp1`，不触碰批 1 阿塞莱快照 hash），L2 逐村手写三层文案（villageType 与 boundSettlement 入文）。
- 双写：`docs/worldbook-migration/projection/authoring-out/` ＋ `tools/worldbook-studio/workspace/full-geo1/authoring/`。

## 二、编译与矩阵（geo1-full13）

| 项 | 结果 |
|---|---|
| 六步链 | register **198/198** → select → approve → proof → compile **0 错误**（21 warning 均既有 WB-INDEX-AMBIGUOUS 类型） |
| 全库规模 | **198 档 / 493 表达 / 2671 grants** |
| 矩阵 | **107/107 PASS**（旧 101 行回归＋新 IV8–IV13） |
| 自检 v2 | 违规 **0**（警告 18 条全旧挂账） |
| 名录 | 198 行；cross_dup **2**（charas 家族批旧挂账，本批零残留） |

### 新增探针 IV8–IV13
- IV8 底俄帕利斯-村民rumor → partial（rumor 文案命中）
- IV9 墨利翁-商人detail → known（detail 文案命中）
- IV10 革耳塞戈斯-贵族detail → known
- IV11 底俄帕利斯-村民detail → partial（runner 钳制到身份能力上限，与批 1 一致）
- IV12 萨戈利那-头人detail → known（判定脚本预期片段曾误写为塞斯塔代姆的文案，当场修正）
- IV13 太虚境-商人 → not_found（不存在对照）

## 三、🆕 交叉引文整改：sethys-river 借引清障

名录新增 3 处 cross_dup：`sethys-river`（塞堤斯河档，早期地理批产物）整句借引了本批三个村庄描述文的**首句**（摩雷尼亚 ES5_1／卡诺普西斯 ES8_1／阿特费尼亚 ES5_2）。

**裁定（按 mount-iltan 先例）**：描述文主引权归村庄档（主消费方），河档改小子串引用——
- `塞堤斯河，位于通往吕卡里亚谷的低矮入口处`
- `密泽亚德高原上的塞堤斯河源头`
- `再向下游河流随即分为三支，形成三河河谷`

三段子串均经 DB 校验为官方描述文**连续片段**（借引合法性）。整改落在 `_fix_sethys_substring_20260913.py`（文字）＋ `_fix_sethys_hash_20260913.py`（hash 同步），双写同步；full14 补丁批重编译收官：register 198/198、compile 0 错误、矩阵 **107/107 PASS**。

## 四、过程教训（两条，均已复盘）

1. **整改脚本 hash 方向错**：首版替换脚本在 `quote:` 行**之后**找 `quote_hash:` 行重算，而本管线 YAML 字段序是 hash 在 quote **上方**——文字改了、hash 未同步，导致名录仍报撞。修正为向上配对重算。
2. **后台编译竞态**：hash 修正发生在 full14 编译启动之后，register 可能读到坏 hash 文件——当场杀掉重跑干净一轮。**规矩：改档前必须确认无六步链在跑。**

## 五、挂账与下一步

1. 帝国余 48 村（`EW8_2` 起）＝批 3，与瓦兰迪亚 36／库赛特 35／巴旦尼亚 33／斯特吉亚 32 续铺。
2. charas 家族批 cross_dup=2 旧挂账未清。
3. 本批改动未提交（49 档 yaml＋生成器＋六步脚本×2＋探针＋整改脚本×2＋报告＋名录）。
