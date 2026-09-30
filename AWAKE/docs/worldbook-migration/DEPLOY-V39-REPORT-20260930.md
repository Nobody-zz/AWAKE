# v39 部署报告（2B 势力政制件 6 条 → 仓库侧 → 游戏目录）

- **日期**：2026-09-30 22:29–22:31
- **产物**：`tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v39-polity-b/`
- **指令**：甲方「2B 势力政制件（6 条）」
- **一句话**：游戏目录世界书从 **v38（790 条）→ v39（796 条）**，新增 **6 条势力政制件**（六文化各一），其余 790 条逐字未动。

---

## 一、为什么值得部署（v39 是什么）

v39 是 **2B「势力政制件」**（`docs/PLAN-WORLDBOOK-NEXT-20260930.md` §五.7 B 类清单）的编译产物。
B 类件按**双层结构**写：**机制层**（游戏 `policy_*`，谁支持谁反对）＋**风味层**（甲方给定的历史原型）。
**政体不绑死**：制度件不绑任何王国／锚点，只讲"这套制度长什么样、由谁撑着"。

| # | 条目 id | 标题 | 文化 |
|---|---|---|---|
| 1 | `doc.politics.polity-battania` | 林间诸部（巴旦尼亚） | battania |
| 2 | `doc.politics.polity-khuzait` | 草原汗权（库赛特） | khuzait |
| 3 | `doc.politics.polity-empire-three` | 三种皇帝（帝国三分） | empire |
| 4 | `doc.politics.polity-sturgia` | 王公与波耶（斯特吉亚） | sturgia |
| 5 | `doc.politics.polity-aserai` | 血统与水源（阿塞莱） | aserai |
| 6 | `doc.politics.polity-vlandia` | 封建契约（瓦兰迪亚） | vlandia |

与既有 5 件政体件（`feudal`／`senate`／`tribal`／`sacred-kingship`／`pastoral`，v36/v37 上线）合起来，
`polity` 子域共 **11 条**。

**用词纪律**（`REF-CULTURE-ARCHETYPES-20260930.md`）：条目**不用真实史名**（不写凯尔特／蒙古／拜占庭／诺曼／基辅罗斯），
只用游戏语料里出现过的词。本批逐词溯源体检过一遍，自造词（承天受命／各执一端／王命／旧例／采邑 等）全部剔除，
换成语料里确实出现的合规词（召唤／号令／地界／世袭／封地／血亲 等）。

**引文证据层**（6 条 × 3 条，共 18 条引文，全部逐字命中，OK=18 / BAD=0）：

| 条目 | 引文 locator（节选） |
|---|---|
| battania | `rules/rule_巴旦尼亚军事制度__…json#/Variants/2/Content`｜`chronicle-kachar-peninsula.txt#/Variants/5/Content`｜`game-lore-rulers.txt#wv2jPN3Q` |
| khuzait | `game-lore-rulers.txt#l4quU1x3`｜`rules/rule_库赛特军事力量__…json#/Variants/5/Content`｜`game-lore-rulers.txt#RyrdJI5j` |
| empire-three | `game-concepts.txt#X0kKBzsW`｜`game-lore-rulers.txt#uJeoa6u1`｜`game-lore-rulers.txt#uK4IX036`｜`game-lore-rulers.txt#SCogGc7N` |
| sturgia | `game-concepts.txt#Ud1AMybr`｜`game-lore-rulers.txt#nQn87o0F`｜`game-lore-rulers.txt#oUKWHMDq` |
| aserai | `game-concepts.txt#qggtvf8Y`｜`game-lore-rulers.txt#ZIhocjnr`｜`game-lore-rulers.txt#GwvxfXs3` |
| vlandia | `game-lore-rulers.txt#rKKAYQkJ`｜`rules/rule_现代军团__…json#/Variants/7/Content` |

> 注：引文**不进游戏运行时**（只在 authoring 侧作证据），故本批的引文改动不参与三哈希。

---

## 二、部署链（两步，均已完成）

### 阶段一：产物 → 仓库侧
`tools/_deploy_v39_stage1_repo.py`

```
[0] 源三文件齐；schemaVersion=awake.worldbook.v2 packageId=awake:worldbook.calradia
    源 runtime entries=796
    包内 hashes: manifestHash=67D39F44…  contentHash=44C7A92B…  packageHash=8952C7EC…
    vs v38 manifest.json  9972e220… -> 733b8b2f…  (已变)
    vs v38 runtime.json   eb87b44e… -> 13a36639…  (已变)
    vs v38 index.json     b14c9222… -> 7a65a8b3…  (已变)
[1] 已复制 3 文件（字节一致）
[2] 包目录现含: ['index.json', 'manifest.json', 'runtime.json']
[3] registry 已同步（小写口径）
    manifestHash  67d39f44… (未变，结构性)
    contentHash   bd05ec17… -> 44c7a92b…
    packageHash   566b375a… -> 8952c7ec…
[4] 目标 runtime entries=796 polity=11
[5] 6 条新件落包抽查：battania ✓  khuzait ✓  empire-three ✓  sturgia ✓  aserai ✓  vlandia ✓
```

### 阶段二：仓库侧 → 游戏目录
`AWAKE/tools/deploy_worldbook_to_game.ps1 -ConfirmDeploy`

```
DEPLOY_BACKUP manifest.json.before sha=c0dd6227a78d1e10
DEPLOY_COPY manifest.json sha=3641bf74869ca688
DEPLOY_COPY packages\calradia files=index.json,manifest.json,runtime.json
DEPLOY_UNTOUCHED root_files=runtime.json,index.json,migration_report.json
DEPLOY_UNTOUCHED v1_dirs=rules,… files=755 (read by nothing under src/)
DEPLOY_UNTOUCHED persona_definitions definitions=77 (deployed by the persona line)
DEPLOY_ENTRYPOINT D:\SteamLibrary\…\Modules\AWAKE\ModuleData\Worldbook\manifest.json
DEPLOY_BACKUP_ROOT …\artifacts\game-dir-deploy-20260930-222955
DEPLOY_OK
```

**投送前 diff**（游戏侧 = v38，仓库侧 = v39，4 文件全部已变；无 Bannerlord 进程在跑）。

---

## 三、部署后核验（四处逐字节相同）

| 文件 | compiled/v39 | 仓库侧 | 游戏侧 | 一致 |
|---|---|---|---|---|
| `manifest.json` | 733b8b2fee99843f | 733b8b2fee99843f | 733b8b2fee99843f | ✓ |
| `runtime.json` | 13a36639f87bc210 | 13a36639f87bc210 | 13a36639f87bc210 | ✓ |
| `index.json` | 7a65a8b3a5b0351b | 7a65a8b3a5b0351b | 7a65a8b3a5b0351b | ✓ |
| registry `manifest.json` | — | 3641bf74869ca688 | 3641bf74869ca688 | ✓ |

**三哈希口径**（registry 小写 vs 包内大写，逐条相符）：

| 字段 | 值 | 与 v38 比 |
|---|---|---|
| `manifestHash` | `67d39f44…` | **未变**（结构性，不随内容变） |
| `contentHash` | `44c7a92b…` | 变 |
| `packageHash` | `8952c7ec…` | 变 |

> 另注：compile 返回的 `manifest_hash=733b8b2fee99…` 是 `packages/calradia/manifest.json` **文件字节**的 SHA256，
> **≠** 包内 `hashes.manifestHash`（`67d39f44…`）。两者是两个不同口径，勿混。

**游戏侧 runtime 读数**：`entries=796` / `polity=11`；6 条新件标记全部命中（林间诸部／草原汗权／三种皇帝／王公与波耶／血统与水源／封建契约）。

**校验链活性证明**（关键）：
- 部署**前** `-ValidateOnly` ⇒ **FAIL**（`deployed registry differs from source`）
- 部署**后** `-ValidateOnly` ⇒ **PASS**（`DEPLOY_VALIDATE_OK`）

---

## 四、备份与回滚

- **游戏侧备份**：`AWAKE/tools/worldbook-studio/artifacts/game-dir-deploy-20260930-222955/`
  （`manifest.json.before` sha=`c0dd6227a78d1e10` = v38 registry，及旧 runtime/index 等）

回滚 = 把备份文件按原路径拷回，再跑一次 `-ConfirmDeploy`。

---

## 五、版本时点表（更新）

| 时点 | 包内条数 | 说明 |
|---|---|---|
| 09-18 | 482 | v16，长期停在消费端 |
| 09-30 15:22 | 790 | v37（政体件 5 条） |
| 09-30 16:56 | 790 | v38（R1 引文处置，仅 1 条正文修正） |
| **09-30 22:30** | **796** | **v39（2B 势力政制件 6 条），本条记录** |

### 5.1 git 版本库状态（已追平）

v38 报告曾查出「版本库里的世界包滞后两版」。**该问题已由 v38 提交修掉**：

| 检查 | 结果 |
|---|---|
| HEAD 版本库 `runtime.json` 条目数 | **790**（= v38，已追平） |
| 世界包最后一次提交 | `a0d485f deploy(worldbook): v38（R1 引文处置）投送到仓库侧 + 游戏目录` |
| 本次工作区改动 | 4 个世界包文件（v39）+ 5 个 `persona_definitions/*.json`（**属角色卡线，本次不带**） |

⇒ 本次提交只带**世界包 4 文件 + 本报告 + 阶段一部署脚本**，绝不 `git add -A`。

---

## 六、遗留

1. **68 个「2 档复用」** —— 同域相邻主题属可接受，尚未逐条登记理由。
2. **62 个 `clan-*` 家族档三层全无引文** —— 既有缺口，待甲方决定是否批量补料。
3. **真机确认未做** —— 需启动游戏看 `Awake.log` 的 `worldbook_runtime_initialized … entries=` 是否 **796**、
   `package=` 是否两段 `awake:worldbook.calradia`。本条需人工进游戏。
4. **2C 政治主体件（4 条）** —— 下一批候选，尚未派活。
