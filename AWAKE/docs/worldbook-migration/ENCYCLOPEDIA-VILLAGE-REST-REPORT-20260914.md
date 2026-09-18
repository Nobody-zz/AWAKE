# 百科化村庄铺开批（余量批）验收报告

**日期**：2026-09-14　**线**：世界书（Worldbook）　**批名**：村庄余量批（瓦兰迪亚/库赛特/巴旦尼亚/斯特吉亚）

**一句话**：把游戏内剩余 135 个村庄一次性铺完 —— 村庄档 **138 → 273**，**273/273 全部入档**；六步编译出 `geo1-full23`（381 档入包，errors 0），矩阵 **125/125**，越限自检违规 0，文本质检硬 0 软 0。

---

## 一、结果总表

| 项 | 值 |
|---|---|
| 本批生成 | **135 档**（瓦兰迪亚 35／库赛特 35／巴旦尼亚 33／斯特吉亚 32） |
| 村庄档总数 | **273**（＝游戏内全部村庄，273/273 收官） |
| 双写一致性 | authoring-out 273 ↔ WS_AUTH 273，逐档 sha256 **零差异** |
| 六步编译 | `geo1-full23`；manifest `fe147bc693e3db46`；validation 21 条 / **errors 0** |
| 入包 | 381 档 / 381 条目（geography 340・economy 22・politics 7・culture 6・war 6） |
| 矩阵探针 | **125/125 PASS**（新增 IV20–IV31） |
| 越限自检 v2 | 381 档；**违规 0**；警告 18 条（全旧挂账，本批 65 档零新增） |
| 名录 | 381 行；cross_dup **6**（旧 2 ＋本批新增 4，已裁定合法，见 §四） |
| 文本质检（awake-prose-qc） | 381 档；**硬 0 / 软 0** |

---

## 二、生成器与来源登记

| 批 | 生成器 | 来源 id | 快照 | hash |
|---|---|---|---|---|
| 瓦兰迪亚 35 | `_rollout_villages4_gen_20260914.py` | `source.calradia.game.villages-desc-vlandia` | `game-villages-desc-vlandia.txt` | 42F8130BB9D7660D |
| 库赛特 35 | `_rollout_villages5_gen_20260914.py` | `source.calradia.game.villages-desc-khuzait` | `game-villages-desc-khuzait.txt` | F706361EC26C22B5 |
| 巴旦尼亚 33 | `_rollout_villages6_gen_20260914.py` | `source.calradia.game.villages-desc-battania` | `game-villages-desc-battania.txt` | 3A9E1641DAFB70CA |
| 斯特吉亚 32 | `_rollout_villages7_gen_20260914.py` | `source.calradia.game.villages-desc-sturgia` | `game-villages-desc-sturgia.txt` | 245A872B3EDE6470 |

- **取数**：`_village_fetch_rest_20260914.py`（DB 只读；逐文化圈取 sid/官方名/描述文；落 `_village_rest_20260914.json`）。
- **档 schema**：`doc.geography.village-<slug>`；`sources[].locator=bannerlord.villages#<sid>`；`quote`＝官方描述文首句（A 级引文锚）＋ `quote_hash`；`aliases.zh-CN=[中文名,"村庄"]`；rumor 层 10 身份／detail 层 4 身份，`grant.min_detail==layer`。
- **L2 文案**：135 条全逐村手写（观感＋传闻＋门道三层），非模板套写。

### 口径纪律（本批显式守）

- **0 条现当代科学视角词**：`沉积/结构/系统/水系/技术/资源/机制/效率/生态/地貌/分析…` 全批零命中（机器证见 §五）。
- **文化身份用词**：巴旦尼亚（部落酋邦）用 部众/族人/氏族、氏族长老/老族长、政令/律令；斯特吉亚（波耶贵族制）用 波耶/领主、领主会议、规矩——**禁用**朝廷/敕令/圣旨一类帝制词（清单 by_culture 硬项）。
- **译名**：正文一律官方译名（乌卡利翁/特朗河/弥戎河/拉科尼斯湖/塔奈西斯湖/比亚里海/车尔特格/伊勒坦山…）。

---

## 三、编译与验收

### 3.1 六步链（`_villages8_sixsteps_20260914.py`，OPBASE=`vill8a20260914`）

```
当前档数: 381   需注册: 135
[1/5] register 135/135 档
[2/5] select -> selection.daa52560cf7349ca92a5d8a416a1082d  items: 381
[3/5] approve -> approval.44f760a3c3574a53a2b0219ec21b5273
[4/5] proof  -> compile.7a28ba90a3e6476ab594acdbbd747331
[5/5] compile -> .../compiled/geo1-full23  manifest: fe147bc693e3db46  validation: 21  errors: 0
DONE
```

- **只注册未入 head 的 135 档**（head 已有 246 档一致，免注册），register 逐档独立 op id＋单个 `--path`（该步不支持逗号批量）。
- compile `--out` 按进程 CWD（仓库根 `D:/AWAKE-Dev/AWAKE`）解析；判定字段＝`manifest_hash`（response 无 `ok` 键）。

### 3.2 结构核验（`_verify_new65_20260914.py`，对 DB 抽验巴旦尼亚＋斯特吉亚 65 档）

- DB 侧 65 村 ⇄ 档内 65 档：**缺档 0**。
- 每档 `sources[].quote` 与 DB 官方描述文首句 **逐字一致**：**不符 0**。
- `aliases` 尾项（zh="村庄" / en=sid）：**不符 0**。

### 3.3 矩阵探针（125 行）

`worldbook-runtime-sim probe` 对 `geo1-full23/manifest.json` 跑 `matrix2-spec.json` → `matrix2-result-full23.json` → 判定：

```
===== 合计 125 行: PASS 125 / FAIL 0 =====
```

新增 12 行（IV20–IV31），四文化圈各抽样并含能力门对照：

| 行 | 查询 | 身份 | 预期 | 实测 |
|---|---|---|---|---|
| IV20 | 于桑克 | merchant·detail | known（头榨走南部商路） | ✅ |
| IV21 | 德拉庞 | merchant·detail | known（干鳕耐存） | ✅ |
| IV22 | 哈坤 | merchant·detail | known（崖间坑道） | ✅ |
| IV23 | 达纳拉 | merchant·detail | known（晒取成盐） | ✅ |
| IV24 | 德鲁伊莫尔 | merchant·detail | known（坑道随石走） | ✅ |
| IV25 | 阿布·科梅尔 | noble·detail | known（坡向朝阳） | ✅ |
| IV26 | 卡格雷夫 | merchant·detail | known（临静湾） | ✅ |
| IV27 | 马拉布罗特 | merchant·detail | known（岩脊露矿） | ✅ |
| IV28 | 于桑克 | villager·rumor | partial（橄榄认地气） | ✅ |
| IV29 | 德鲁伊莫尔 | villager·rumor | partial（石头里藏着银） | ✅ |
| IV30 | 卡格雷夫 | villager·rumor | partial（鱼是水里来的） | ✅ |
| IV31 | 子虚村 | merchant·detail | not_found（不存在对照） | ✅ |

### 3.4 越限自检 v2（`_selfcheck2_20260913.py`）

`{"docs": 381, "expressions": 859, "grants": 5233, "denies": 3, "passed": true}` —— **违规 0**。
警告 18 条全为旧挂账（`desert-nahasa`、`lake-llyn-modris`、`tale-*`、`weapon-crossbow` 等非村庄档的 merchant/soldier 覆盖提示）；**本批 65 档零新增**（村庄档六身份落点齐备）。

---

## 四、名录 cross_dup 变化（2 → 6）裁定

名录生成器 `_gen_name_index_20260913.py` 刷新为 381 行；跨档重复引文（同一 `quote_hash` 出现在 ≥2 档）由 **2 升至 6**。逐条核（`_crossdup_20260914.py`）：

| 组 | 档 | 引文 | 裁定 |
|---|---|---|---|
| 1 | `clan-charas-cortain-secret` × `territory-charas-reign` | 瓦兰迪亚人到来后，沙拉斯落入了无情的戴·科尔坦家族手中 | 旧挂账（charas 家族批） |
| 2 | 同上 | 其无穷的财富助长了后者的野心 | 旧挂账 |
| 3 | `mount-iltan` × `village-vladiv` | 弗拉基夫坐落于伊勒坦山脚下寒冷阴暗的林谷中。 | **合法**（见下） |
| 4 | `mount-iltan` × `village-bukits` | 布基茨坐落于库赛特边境上伊勒坦山的东坡。 | **合法** |
| 5 | `mount-iltan` × `village-glavstrom` | 格拉夫斯特伦坐落于东卡拉迪亚的一座峻峭险峰——伊勒坦山脚下… | **合法** |
| 6 | `mount-iltan` × `village-urikskala` | 乌里克斯卡拉位于伊勒坦运输线上，那是一张由冰川湖构成的网络… | **合法** |

**裁定理由**：新增 4 组全部是「伊勒坦山」档（旧档）与其山脚/沿线村庄档**共用同一段 A 级官方描述文作本档引证**——山档引它证明「这些村落在伊勒坦山」，村档引它作自身界说首句。两档**表达层各不相同**，重复的只是官方原文这一共享引证。防重复纪律针对的是**B 级编年史变体双投**，本条不属。⇒ **判定合法，不改文本、不删引**。

**关键反证**：**村 ↔ 村之间零重复**——135 个新村庄两两之间无任何共享引文，这才是最需要防的一类，已干净。

---

## 五、文本质检（awake-prose-qc）

```
[worldbook] yaml=381 扫到正文=381 硬=0 软=0
```

报告：`docs/evidence/awake-prose-qc-20260914-villages/PROSE-QC-VILLAGES.md`

- 扫描范围：`assertions[].text.zh-CN`（含 rumor/detail 表达层），共 381 档。
- **硬 0**（现当代科学视角词、身份错位词、译名变体、作者旁白混入 全无）；
- **软 0**（推断现代词全无）；
- 观测：作者口癖无、破折号离群档无。

结合 §3.2 的 DB 逐字核验与 §三 的机器门，本批文本质量为**机器下限全过**。

---

## 六、未提交清单（**未提交，待令**）

> 纪律：绝不 `git add -A`；`git commit -F <文件> -- <精确路径>`；提交前查 `git diff --cached --name-only`。

**本批新增（世界书线）**
- 档 135：`docs/worldbook-migration/projection/authoring-out/village-*.yaml`（本批 135 个；连同此前未提交批次，authoring-out 下 `village-*.yaml` 累计 **273 个**未跟踪）。
- 生成器×4：`_rollout_villages4/5/6/7_gen_20260914.py`
- 编排×1：`_villages8_sixsteps_20260914.py`（＋运行日志 `_villages8_run_20260914.log`）
- 工具脚本：`_village_fetch_rest_20260914.py`、`_village_rest_20260914.json`、`_verify_new65_20260914.py`、`_crossdup_20260914.py`、`_cmp_dupe_20260914.py`
- 探针：`tools/worldbook-studio/.../_add_matrix_vill4_20260914.py`、改 `_verify_matrix2_20260913.py`、`matrix2-spec.json`、`matrix2-result-full23.json`
- 报告与证物：本文件、`docs/evidence/awake-prose-qc-20260914-villages/*`
- 刷新：`WORLDBOOK-NAME-INDEX.xlsx`、`SELF-CHECK-20260913-v2.json`

**注意**：仓库中另有他线（UI/美术/模组主体）的在途改动（`docs/UI-ART-*`、`GUI/`、`AssetSources/`、`docs/control-plane/CURRENT.json` 等），**不属本批、不得混提**。

---

## 七、备注

1. **村庄线收官**：游戏内 273 个村庄 100% 入档，百科化「聚落」维度村庄侧完成。
2. **编译包转义坑**（复用提醒）：包内 JSON 是 `\uXXXX` 转义，`grep`/裸读匹配不到中文，判包内文本须 `json.load` 后搜。
3. **提速捷径**：head 一致时只重注册被改档即可直进 select→compile；本轮因有 135 档全新档，仍走完整 register。
