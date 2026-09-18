# 百科化村庄铺开批 1 验收报告（阿塞莱 40 档，含乌格巴补档）

> 2026-09-13 · 最终被测包 `tools/worldbook-studio/workspace/full-geo1/compiled/geo1-full12`
> 初版 39 档（geo1-full11）→ 乌格巴补档后定稿 40 档（geo1-full12，见 §六）

## 一、批次范围

- **村庄全量素材实测**：273 村，中文名与中文描述文 **273/273 全覆盖**（此前旧口径已作废）。
- 本批按文化圈切分：**阿塞莱 40 村 → 39 档**（跳过乌格巴，见 §三）。
- 剩余待铺开：帝国 97／瓦兰迪亚 36／库赛特 35／巴旦尼亚 33／斯特吉亚 32（共 233 村，约六批）。

## 二、流水线（沿用 PoC 通道，零改动）

- 生成器 `_rollout_villages1_gen_20260913.py`：DB 取数 → 快照 `game-villages-desc.txt`（独立来源，避免触碰城镇批快照 hash）→ 来源登记 `source.calradia.game.villages-desc` → 39 档 yaml，双写 authoring-out ＋ workspace authoring。
- L1 底座自动：官方中文名／别名（中文名＋"村庄"＋英文名＋settlementId）／A 级引文（描述文首句）／hash。
- L2 观感人工：39 村逐村手写断言／rumor／detail 三层文案（物产×地理×行情视角，villageType 与 boundSettlement 入文）。
- 六身份显式落点、grant.min_detail==layer、JSON 往返断引用＋ignore_aliases，红线全守。
- 六步链 `_villages1_sixsteps_20260913.py`（OPBASE=vill1a20260913）：register **148/148** → select 148 项 → approve → proof → compile → **geo1-full11，诊断 21 条全为 warning（WB-INDEX-AMBIGUOUS 英文别名跨档撞，既有类型），0 错误**。

## 三、新发现：官方 CNs 本地化错挂（挂账）

- `castle_village_A7_1`（乌格巴）的描述文 token `Settlements.Settlement.text.castle_village_A7_1` 在官方 **CNs 语言下错挂**为别处「执政官加里俄斯谋划流放名单」叙事文；**繁中/英/日等其余 11 语言均正常**（繁中：「烏格巴坐落在普萊錫克海沿岸…」）。
- 裁定：按素材纪律不自造简中 A 级文本 → **乌格巴本批跳过、39 档**；待官方文本修复或授权以多语言源转写后补档。快照与档均未收录该村。
- 另做全量「描述文↔村名」一致性扫描：273 村中仅此 1 处真错挂；另 11 处为开头句式不同（内容确属该村，正常）。
- 描述文 token 与 id 不必同名的坑（如 `castle_village_A1_2` 的描述 token 为 `village_A1_3`）沿用城镇批做法：一律从字段本身解析 token。

## 四、验收结果

| 项 | 结果 |
|---|---|
| 编译 | geo1-full11，manifest `633A7584E9712BE7…`，0 错误 |
| 全库规模 | **148 档 / 393 表达 / 1971 grants**（自增 39/78/546） |
| 矩阵 | **100/100 PASS**（旧 94 行回归＋村庄新 6 行 IV1–IV6） |
| 自检 v2 | 违规 **0**；警告 18 条＝既有旧挂账，村庄批零新增 |
| 名录 | 148 行，cross_dup=2（家族批旧挂账，零新增） |

矩阵新探针（IV＝村庄）：
- IV1 突比力斯·村民 rumor → partial（rumor 文本命中）✅
- IV2 拉迈萨·村民求 detail → partial（**预期修正**：runner 将请求层钳到身份能力上限，回落 rumor 层，与 IT/IS 批 villager 行为一致，非 blocked）
- IV3 代尔·哈瓦·商人 detail → known ✅
- IV4 本盖兹·贵族 detail → known ✅
- IV5 纳赫兰·头人 detail → known ✅
- IV6 云梦泽（不存在对照）→ not_found ✅

## 五、挂账与下一步

1. 剩余 233 村按文化圈铺开：帝国（97，需拆两批）→ 瓦兰迪亚 36 → 库赛特 35 → 巴旦尼亚 33 → 斯特吉亚 32。
2. PoC3 CraftedItem 聚合通道、家族批开行（清 cross_dup=2）仍待排期。
3. 本批改动未提交（40 档 yaml＋生成器＋六步脚本＋矩阵扩展＋报告＋名录）。

## 六、乌格巴补档（用户令，同日追加）

- **口径**：官方 CNs 该条描述文错挂不可用，但繁中/英/日等 11 语言正常。补档不"自造简中官方文"——**快照行与引文用官方 EN 原文（A 级逐字）**（`Uqba sits alongside the Perassic Sea.`），简中断言/rumor/detail 为依官方多语言源（EN/CNt/JP 交叉核对）的**转写**，注记写入快照行与本文档。
- **实现**：生成器取消跳过、改特例通道（`UQBA_SID`；快照行带 `注：官方CNs该条描述文错挂…`）；快照重生成 → 新 hash `8922DA67E165970B`，40 村档同批刷新 source_content_hash。
- **geo1-full12 六步全绿**：register **149/149** → compile 0 错误（21 条 warning 同前，既有类型）。全库规模 **149 档 / 395 表达 / 1985 grants**。
- **矩阵 101/101 PASS**：新增 IV7-乌格巴-村民rumor → partial（转写 rumor 文本命中），not_found 对照不变。
- **自检 v2**：违规 0；名录 149 行，cross_dup=2 零新增。
- 若日后官方修复该条 CNs，可回切：改生成器特例通道为常规 CNs 路径重跑即可（引文将回到官方简中原句）。
