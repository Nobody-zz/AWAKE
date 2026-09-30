# v40 部署报告（2C 政治主体件 4 条 → 仓库侧 → 游戏目录）

- **日期**：2026-10-01 00:12–00:26
- **产物**：`tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v40-polity-c/`
- **指令**：甲方批准执行 2C「政治主体件（4 条）」，并裁定**政策名词照用**（有 `bannerlord.db` 政策表原文托底）
- **一句话**：游戏目录世界书从 **v39（796 条）→ v40（800 条）**，新增 **4 条政治主体件**，其余 796 条**逐字未动**。

---

## 一、为什么值得部署（v40 是什么）

v40 是 **2C「政治主体件」**（`docs/PLAN-WORLDBOOK-NEXT-20260930.md` §2C）的编译产物。
C 类件写**政治主体本身**（元老院／保民官／军权／皇权），依据是**政策三方支持度**
（`bannerlord.db · bannerlord_policies · v1.3.15`：`rulerSupport` / `lordsSupport` / `commonsSupport`）。

| # | 条目 id | 标题 | 政策锚 |
|---|---|---|---|
| 1 | `doc.politics.polity-senate-body` | 元老院（帝国议会） | 立法权 · 皇帝选举 |
| 2 | `doc.politics.polity-tribunes` | 保民官（平民之口） | 民众大会 · 否决权 |
| 3 | `doc.politics.polity-marshals` | 元帅与领主（军权之重） | 军权依附 · 士兵效忠 |
| 4 | `doc.politics.polity-royal-privilege` | 皇室特权（君与贵族之争） | 法案否决 · 继承之争 |

与既有 11 件政制件（v36–v39 上线）合起来，`polity` 子域共 **15 条**。

**用词纪律**（09-30 §五十三 纠错后口径）：
- **L1 引文层（硬判据）**：`quote` 必须逐字命中语料 + `quote_hash` 正确。**只有这一层管语料**。
- **L2 设定层（硬判据）**：不自造专名/新概念；不用**真实史名**（凯尔特/拜占庭/蒙古/诺曼/罗马/维京…）。
- **L3 正文层（无判据）**：只要**中文通顺、玩家读得懂**即可；语料命中率**仅作提示**。
- **政策名词照用**（甲方裁定）：`保民官` 等词虽全库 0 命中（只存在于政策表），照用不换。

**引文证据层**（4 条 × 3 条 = 12 条，全部取自 `game-lore-rulers.txt`，逐字命中，OK=12 / BAD=0）：

| 条目 | 引文 locator |
|---|---|
| senate-body | `game-lore-rulers.txt#Cx2FCePF`｜`#bFZrLY8W`｜`#jg6SMQLc` |
| tribunes | `game-lore-rulers.txt#OFOXDkPv`｜`#spQJxKtr`｜`#Cgdg9Q8t` |
| marshals | `game-lore-rulers.txt#uK4IX036`｜`#FSwVVdHV`｜`#5BWAsyeB` |
| royal-privilege | `game-lore-rulers.txt#T1svPL9Z`｜`#kRRnXYOR`｜`#9f11AsTw` |

> 引文**不进游戏运行时**（只作编译期证据/审计层），故不参与三哈希。

---

## 二、编译链（六步，全绿）

```
正典档数（去重 id）= 800
[1] register-batch OK  count=800
[2] select        OK  itemCount=800  selectionId=selection.99df399bfa8146c29af2fdc057ef9b69
[3] approve       OK  approvalId=approval.11330089743e4f45a9c3be9f177a6201
[4] proof         OK  compileProofId=compile.a99406610c7e41b190bcd67973423861
[5] compile       rc=0  manifest_hash=11396b9389f4fba26d843f79495c3bbe177c7fce7237cf8139f68248463d931a
```

**回读产物**：

```
entries = 800         （796 + 4）
polity  = 15          （11 + 4）
新件在册 4/4  ✔
```

**变更集判据**：

| 判据 | 期望 | 实测 | |
|---|---|---|---|
| 新增 | 恰 4 | **4** | ✔ |
| 消失 | 0 | **0** | ✔ |
| 其余逐字未动 | 796/796 | **796/796（有变 0）** | ✔ |

**validation**：仅 `WB-INDEX-AMBIGUOUS` **warning**（城堡/村庄同名歧义，既有数据常态，v39 亦有），**无 error**。

---

## 三、部署链（两步，均已完成）

### 阶段一：产物 → 仓库侧（`tools/_deploy_v40_stage1_repo.py`）

```
[1] 字节复制 3 文件
  manifest.json  11396b9389f4fba2  ✔
  runtime.json   b24f2904a44433ca  ✔
  index.json     9e3fcb5e1d3ce514  ✔
[2] registry 三哈希同步
  manifestHash 67d39f44dc27a763  （未变，结构性）  ✔
  contentHash  44c7a92b… -> e72219c1…           ✔
  packageHash  8952c7ec… -> ecafe12f…           ✔
[3] 回读 entries=800，新件在册 4/4 ✔
```

### 阶段二：仓库侧 → 游戏目录（`deploy_worldbook_to_game.ps1 -ConfirmDeploy`）

```
DEPLOY_PACKAGE awake:worldbook.calradia -> packages\calradia files=manifest.json,runtime.json,index.json
DEPLOY_OK root=…\Modules\AWAKE\ModuleData\Worldbook packages=1
```

### 部署后核验

- `-ValidateOnly` ⇒ **`DEPLOY_VALIDATE_OK`**
- **四处逐字节相同**（仓库侧 vs 游戏侧）：

| 文件 | sha256（前 20） | |
|---|---|---|
| `manifest.json`（registry） | `6979d5d7858766a6a360` | ✔ |
| `packages/calradia/manifest.json` | `11396b9389f4fba26d84` | ✔ |
| `packages/calradia/runtime.json` | `b24f2904a44433ca86a3` | ✔ |
| `packages/calradia/index.json` | `9e3fcb5e1d3ce514a86c` | ✔ |

- 游戏侧 registry 三哈希 = 仓库侧（`67d39f44…` / `e72219c1…` / `ecafe12f…`）；游戏侧 `runtime.json entries=800`。

---

## 四、备份与回滚

| 层 | 备份路径 |
|---|---|
| 仓库侧旧包（v39） | `AWAKE/ModuleData/Worldbook/artifacts/repo-side-backup-v39-20261001-002611/` |
| 游戏侧旧包（v39） | `tools/worldbook-studio/artifacts/game-dir-deploy-20261001-002624/` |

回滚：把上述备份的 3 文件复制回对应位置 → 重跑阶段二 `-ConfirmDeploy`；registry 三哈希须同步回旧值。

---

## 五、版本时点表

| 版本 | 日期 | entries | polity | 内容 |
|---|---|---|---|---|
| … | … | … | … | … |
| v37 | 09-30 | 790 | 5 | 政制通用件 |
| v38-r1 | 09-30 | 790 | 5 | 修订 |
| **v39-polity-b** | 09-30 | **796** | **11** | 2B 势力政制件 6 条 |
| **v40-polity-c** | **10-01** | **800** | **15** | **2C 政治主体件 4 条** ← 现行在挂 |

**三哈希演进（包内，大写）**：

| 版本 | manifestHash | contentHash | packageHash |
|---|---|---|---|
| v39 | `67D39F44…` | `44C7A92B…` | `8952C7EC…` |
| v40 | `67D39F44…`（未变） | `E72219C1…` | `ECAFE12F…` |

> `manifestHash` 不随内容变（结构性：`packageId`/`version`/`kind`/`entrypoints` 未变）。

---

## 六、遗留

1. **真机确认待人工**：需进游戏看 `Modules/AWAKE/Logs/Awake.log` 的
   `worldbook_runtime_initialized … package=… entries=` —— 判两点：① `package=` 是两段 `awake:worldbook.calradia`；
   ② `entries=` 与本包一致（**800**）。
2. **`WB-INDEX-AMBIGUOUS` warning**：城堡/村庄同名（如「阿布·科梅尔堡」vs「阿布·科梅尔」）导致检索可能同时返回两条。
   属既有数据形态，非本批引入；如需消解须补**区分别名**或调整词条边界（另一件独立工作）。
3. **`assertions[].text` 不进上线包** ⇒ 正文改动**不参与三哈希**；玩家听得见的是 `expressions[].text` 与 `summary`。
