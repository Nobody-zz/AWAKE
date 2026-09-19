# AWAKE 全线进度梳理 · 2026-09-19

- 缘起：甲方 Max 21:57「好久不见。现在梳理一下当前状态」
- 出具：全局主控线
- **性质：这是读数，不是裁决。** 权威状态仍以 `docs/AWAKE-ROADMAP.md`「现状」节为准；本文件的作用是**把各线并到一页**，并标出"谁卡着"。
- ⚠ **口径**：以下读数**全部于 09-19 21:58–22:0x 按工作区实跑/实测**，不是记忆里的旧读数。
- 上一份：`docs/REVIEW-ALL-LINES-20260918.md`（09-18 13:0x）。本文件只写**之后发生的变化**，未变的不重复。

---

## 一、一句话

**代码侧从 09-18 13:35 起零提交，整整 33 小时没人动过 —— 今天只有 UI 线在干活，而它干的那件事，正好把美术那格的方向改了一半。**

---

## 二、这 33 小时变了什么（09-18 13:35 → 09-19 21:57）

| 项 | 变化 |
|---|---|
| **提交** | HEAD 停在 `65e0ec3`（**09-18 13:35**）。09-19 **零提交**（虽然 UI 线全天真在干活） |
| **未入库** | 从 **567 ＋ 91** 份降到 **27 条**（09-18 13:26–13:35 那批 6 笔提交把积压收口了）。⚠ 但这 27 条里埋着一颗 **2.2 GB 的地雷**，见 §3.2 |
| **UI 线** | 复检了「图集放错目录」这个判定，**结论被否掉**：`GUI/SpriteSheets/` 是死路，**必须走 `AssetPackages/*.tpac`**。AWAKE 现在**没有这个目录** |
| **测试** | **不变**：重编后仍是 **64 例 / 6 条红**（编号与名单与 09-18 完全一致） |
| **四个真机缺陷** | **不变**：仍只清掉①，且 `AwakeFileStorageService.cs` 最后提交是 **09-11**（`0630a6d`），**8 天没碰** |

---

## 三、🚨 本次实测的三条

### 3.1 测试 6 条红 —— 重编后仍然 6 条，"构建边界"这个疑点排除了

09-18 留了个尾巴：那个 exe 构建于 10:04，而最后一笔动 `src` 的提交 `0bff7b9` 在 10:05
⇒ 严格说那读数**不含 0bff7b9**。今天**重编了一次**（`build.ps1 -Configuration Debug`，0 错 4 警，18 秒），
用新 exe（**09-19 21:58**）复跑：

```
RESULT total=64 passed=58 failed=6
FAILED_CASES g3-s0-focused-readiness,persona-template,shared-persona-golden-fixture,
             persona-persistence,persona-anchor,dialogue-chain-redtest
```

⇒ **一模一样。** 这 6 条红是**真实的**，不是构建差一分钟造成的。可以定案、不必再试。
⇒ 其中 `dialogue-chain-redtest` 的老问题照旧：它要 `awake.worldbook.v2`，包给的是 `registry.v1`
（`DialogueChainRedtest.cs:274` ← `:221`）——**判据没跟着 09-14 的决定走**。

### 3.2 🚨 未入库只剩 27 条，但其中一条是 **2.2 GB**

```
AWAKE/tools/_reranker_survey/dl/        2.2 GB   ← 下载缓存
AWAKE/_backup_dist_runtime_..._1125/     74 MB   ← 备份
AWAKE/tools/_probe_onnx_{,_mem,_threads}_20260916/native/   11 MB × 3  ← 原生库副本(x3)
```

⇒ **"绝不 `git add -A`"这条纪律，现在关乎 2.3 GB 的误入库。** 这不是洁癖问题，是**仓库会不会被撑爆**的问题。
⇒ 处置见 §3.3 的三档。

### 3.3 剩下的 27 条，分三档

**A. 该入库（约 8 条）**
- `AWAKE/docs/UI-SPRITE-ATLAS-LOOKUP-20260919.md`（新）＋ `UI-DEPLOY-GAP-20260916.md`（今天追加 §5）
- `AWAKE/tools/awake-ui-lab/_probe_sprite_sheet_path_20260919.py` ＋ `_probe_sprite_tpac_20260919.py`
- **`AWAKE/AssetSources/GauntletUI/`**（2.8 MB，三张图集源图）—— roadmap「下一步 1」明写要入，**压了 6 天**
- ⚠ `AWAKE/tools/worldbook-pilot-package/WorldbookPilotPackage.csproj`（**已改未提交**）：只多一行
  `<Compile Include="..\..\src\WorldKnowledgeSemantic.cs" />` —— 这是 **09-17 接语义通道时的配套改动**，
  忘了提交。**就是"改共享工程文件要同步显式编译清单"那条**，若别处按 HEAD 编译会**静默漏掉语义代码**。

**B. 纯垃圾，该删（约 12 条）**
- **两个 0 字节的怪文件**（09-17 23:03 创建，文件名是句子）：
  `前置：（那份结论，本文复核它）。` ／ `起因：甲方「按你说的增长点，你打算怎么做？」`
  ⇒ 某次命令行把句子当成了文件名。**空文件，删掉零风险。**
- 根目录与 `AWAKE/` 下一批 `_` 前缀临时物：`_verify_out.txt`／`_commit_msg_20260918.txt`／`_commitmsg-20260915-*.txt`／
  `_village_inventory_20260913.json`／`_wtest_ps/`／`AWAKE/_wb_validate*.txt`／`AWAKE/_orig_tax_tmp.json`／`AWAKE/docs/_af_vers.txt`

**C. 不该入库，该进 `.gitignore`（4 条，见 §3.2）**
- 2.2 GB 的 `_reranker_survey/dl/`、74 MB 的 `_backup_dist_runtime_*`、三份 `_probe_onnx_*/native/`、`tools/out/`

---

## 四、UI 线今天干的事（唯一动的线）

**它把自己昨天/上午的结论否掉了。** 有人报「图集放错目录，跟 Modding Kit 无关」，UI 线复检后：

- **原版根本没有 `GUI/SpriteSheets/` 目录**（`Modules/Native/GUI/` 只有 Brushes/Fonts/Prefabs/SpriteData）；
  `Native/AssetPackages/gauntlet_ui.tpac`（323 MB）里**搜得到** `ui_conversation_1` 等名字。
- 全机 108 个模块：29 个有 `AssetPackages/`，**只有 2 个有 `GUI/SpriteSheets/`，而这 2 个没有任何成功记录**。
- 唯一"四件套齐全"的样本 `SimpleBank` 走的**也是 tpac**；`AssetSources/` 是**源**、不是运行时落点。

⇒ **AWAKE 侧现状**：`Modules/AWAKE/GUI/SpriteSheets/` **存在但是空的**（别往里放东西）；
三张图集已在 `AssetSources/GauntletUI/`（尺寸与声明逐一对上）；**缺的是 `Modules/AWAKE/AssetPackages/*.tpac`**。

⇒ **对 roadmap 的修正**：「Import 出 tpac 是全链唯一不能脚本化的一步」这句要**收窄**——
"必须要有 tpac"这个方向**加强了**；但"**只能**手点"未定（`_tex.tpac` 只有 479 字节，是纯元数据壳，**未验**）。

---

## 五、四个真机缺陷：仍只清了一个

| # | 缺陷 | 现状（实查） |
|---|---|---|
| ① | `awake.world_fact.root_corrupt`（周报链整条不可用） | ✅ **离线已修（09-15 三笔）**：`bae3841`(19:54) 分"还没有/坏了" → `25de029`(19:59) 补写读往返判据 → `437bedd`(20:04) 坏账本自愈。**至今没在游戏里确认过** |
| ② | `native_readiness` 空引用（09-10 起反复，是竞态） | ❌ 未见修复 |
| ③ | `npc_dialogue_open_failed`（v0.3 卡这） | ❌ 未见修复 |
| ④ | 状态落 `AwakeState/unbound/`（v0.2 卡这） | ❌ **`AwakeFileStorageService.cs` 最后提交＝09-11 `0630a6d`**，8 天零改动 |

依据：`git log --since='2026-09-18 10:05' --name-only -- AWAKE/src` 只列出 **3 个 identity 文件**
（即 `0bff7b9` 那一笔），此后 `src` 无任何提交，工作区亦无未提交改动。

---

## 六、卡点按"卡在谁手里"（这才是能动的清单）

**A. 卡在"甲方一句话"（2 件，批了就能动）**
1. 世界书包**要不要投到游戏目录**（仓库 482 vs 游戏目录 448，差 34 条）。
2. 联系边表 v2（978 条）**怎么接**——两条接法二选一。

**B. 卡在"要开一次游戏"（3 件 ＋ 1 次确认）**
- `native_readiness` 竞态、`npc_dialogue_open_failed`、存档绑 `unbound`；
- 外加：**周报修复的真机确认**（09-15 离线三笔已落地，至今没在游戏里看过一次）。

**C. 卡在"必须人手 / 需先验"（1 件）**
- 美术 `AssetPackages/*.tpac`：方向已确认为 tpac，但**479 字节壳式 tpac 能不能被运行时接受，未验**——
  先验这个，能过就把这步脚本化。

**D. 卡在"要人拍板"（1 件）**
- 角色卡 76 张 draft 的审批。

**E. 卡在"自己就能做"（2 件，最该先做）**
1. **给 4 条不该入库的路径加 `.gitignore` 条目**（防 2.3 GB 误入库）——**五分钟的事，防的是不可逆事故**。
2. **入库 A 档那 8 条 ＋ 提交那行 `WorldbookPilotPackage.csproj`**；顺手删掉 B 档两个 0 字节怪文件。

---

## 七、下一步（只说先做哪个，不排期）

1. **先做 §六 E**：`.gitignore` 兜底 ＋ 入库。精确 pathspec，**勿 `add -A`**。
2. **要甲方一句话的两件**（§六 A）——批了，世界书这一侧当天就能再往前一步。
3. **`dialogue-chain-redtest` 先定"哪个包形态才是对的"**，再决定改判据还是改包（其余 5 条红归属角色卡线）。
4. 三个真机缺陷 + 周报确认，等方便开游戏。

⚠ **第三阶段及以后一律不排期**（甲方 09-15 明示；`docs/DECISION-20260915-PHASE3-BOUNDARY.md`）。

---

## 附 · 本文件的证据出处

- 提交与 HEAD：`git log -25`（HEAD＝`65e0ec3`，09-18 13:35）；`git log -1 -- AWAKE/src`（＝`0bff7b9`，09-18 10:05）
- 测试读数：重编 `build.ps1 -Configuration Debug`（09-19 21:58，0 错 4 警）后实跑
  `AWAKE.Tests/bin/Debug/net472/Awake.SdkSmoke.exe`（退出码 1）
- 存储文件零改动：`git log -1 -- AWAKE/src/AwakeFileStorageService.cs` ＝ `0630a6d`（09-11 11:55）
- 周报三笔：`bae3841`／`25de029`／`437bedd`（均在 09-15 19:5x–20:0x）
- 未入库清单与体积：`git status --porcelain`（27 条）＋ `du -sh` 各目录
- UI 线判定：`docs/UI-SPRITE-ATLAS-LOOKUP-20260919.md`；流水见 `.workbuddy/memory/2026-09-19.md`
