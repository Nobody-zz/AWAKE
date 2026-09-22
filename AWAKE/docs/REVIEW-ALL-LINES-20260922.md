# 全线状态梳理（09-22）

> ⚠️ **这是读数不是裁决。** 权威状态仍以 `AWAKE/docs/AWAKE-ROADMAP.md`「现状」节为准；
> 本文件只做「各线当前坐标 ＋ 卡在谁手里」，不排期、不改状态。
> **所有数字本次当场实测**（命令 ＋ 数 ＋ 时点写在同句），不引旧读数。
> 09-18／09-19 两版见 `REVIEW-ALL-LINES-20260918.md`、`REVIEW-ALL-LINES-20260919.md`。

---

## 〇、本次实测（时点：2026-09-22 10:2x）

| 取什么 | 命令 | 读数 |
|---|---|---|
| 最后一笔提交 | `git log -1 --pretty='%h %ad %s'` | `74155b7` **09-20 14:40** docs: 世界书更新安全边界 ⇒ **至今约 44 小时零提交** |
| 构建 | `AWAKE/tools/build.ps1 -Configuration Debug`（退出码 0） | `Awake.SdkSmoke.exe` mtime **09-22 10:28**（重编生效） |
| 测试 | `./AWAKE.Tests/bin/Debug/net472/Awake.SdkSmoke.exe` | **`RESULT total=64 passed=64 failed=0`，`EXIT=0`** |
| 世界书（游戏侧） | 同上，用例 `dialogue chain worldbook deployed redtest` | `entries=482 package=awake:worldbook.calradia` |
| 未入库 | `git status --porcelain \| wc -l` | **750 条** |
| 体积 | `du -sh` | `docs` 62 M · `authoring-out` 9.1 M · `src` 2.1 M · `ModuleData` 2.6 M |
| C 档 | `.gitignore:119` | `AWAKE/tools/_reranker_survey/dl/`（**2.2 GB** 下载缓存）**已拉黑** |

### 两条实测更正（旧读数对今天不成立）

1. **「64 例 / 6 条红」已过时**。路线图现状节（09-18 补注）与 09-19 报告都写 6 条红；
   09-20 14:19 `3db0917` 收口后，**今天重编复跑为 64/64 全绿**。
   ⇒ 判红名单以本次实跑为准，**别再照 6 条红那条动**。
2. **未入库不是「几十条」是 750 条**，且**主体是世界书内容**（569/750）。
   ⇒ 别用 `git status` 的观感估量，也**绝不 `git add -A`**。

---

## 一、六条线在哪

| 线 | 现在的坐标（实测） | 卡在哪 |
|---|---|---|
| **主干 · 运行时** | 离线 **64/64 全绿**（09-22 10:28 构建）；最后一笔 src 提交 `3db0917`（09-20 14:19，离线 A 批次三件：存储 owner／对话取消／对话链判据） | **真机**。09-14 那一跑露的四个缺陷，**09-20 那笔改的是离线可验的三件，不等于它们已修**：`AwakeRuntime.cs`／`NpcDialogueVM.cs` 自 09-20 14:19 起零改动 ⇒ **未见针对 `world_fact.root_corrupt`／`native_readiness` 空引用／`npc_dialogue_open_failed` 的修复提交** |
| **世界书 · 内容** | 游戏目录那份 **482 档**（测试直读，见上）；仓库侧 **558 档**（军事 25＋经济 40＋暗面 11＋互引边 1286 条，09-20 新编）**未投送** | **甲方一句话**：558 档投不投（要上须跑 `tools/deploy_worldbook_to_game.ps1 -ConfirmDeploy`） |
| **世界书 · 检索** | 互引边已编进 558 档；**召回接线仍在途未提交**：`src/WorldKnowledge{Loader,Models,QueryService}.cs` **＋94 行**（mtime 09-20 12:00） | **这条线自己的在途改动**，他人不得带走 |
| **角色卡** | 仓库侧 76 张**全 draft**；`persona-workbench/characters/` 有 5 张在改；09-20 产出三份审计（`AUDIT-PERSONA-*-20260920.md`）未入库 | **审批 ＋ 真机**：审批链真身 `tools/persona-awake-joint/` **本机跑不起来**（脚本硬调 `pwsh`，本机只有 PowerShell 5.1） |
| **UI ＋ 美术** | 28 张 `icon_*.png` **未登记**；`ui_awake_frame` 图集 4088/4096 已满；模块内仍 **0 个 `.tpac`**、无 `AssetPackages/` | **人手 ＋ 真机**：`resource.show_resource_browser` → Import **无法脚本化**；官方纸/石色值基准待用现行 `gauntlet_ui.tpac` 复测 |
| **全局 · 工程** | 未入库 **750 条**；2.2 GB 缓存**已** ignore（09-19 拉黑生效）；根目录残留 2 个**中文句子命名的 0 字节文件** ＋ 3 份 `_commit*msg*.txt` | **自己就能做**（见下 E 组） |

---

## 二、未入库 750 条的三档

- **A 该入库（本人/各线认领）**
  - `AWAKE/docs/worldbook-migration/projection/authoring-out/` **569 条**（9.1 M，世界书内容）— 归世界书线
  - `AWAKE/tools/` **107 条**：`_alias_*_20260920.py` 六件、`_cataphract_gear*_20260920.py` 等一次性探针
  - `AWAKE/docs/worldbook-migration/` **27 条**：`ALIAS-POLICY`／`DARK-BATCH-*`／`ECONOMY-*`／`EDGE-*`／`EDGE-TO-RECALL-*` 等 09-20 批产文档
  - `AWAKE/docs/` **9 条**：三份 `AUDIT-PERSONA-*-20260920.md`、`mappings/worldbook-should-link/20260920/` …
  - `persona-workbench/characters/` 5 张 ＋ `ModuleData/Worldbook/persona_definitions/definitions/` 5 条 — 归角色卡线
  - `AWAKE/tools/worldbook-studio/src/Awake.WorldbookStudio.Core/` 4 条（编译器/校验器改动）
- **B 纯垃圾（只建议，不代删）**
  - 根目录 `起因：…`、`前置：…` — **文件名是中文句子、0 字节**
  - `_commit_msg_20260918.txt`、`_commitmsg-20260915-{rag,audit}.txt`、`_verify_out.txt`、`_village_inventory_20260913.json`
- **C 不该入库、该 ignore** — 本期**无新增**：`_reranker_survey/dl/` 2.2 GB 已于 09-19 拉黑（`.gitignore:119`）✅

⚠️ **`src/` 那 3 条是活的在途改动，一律绕开**（谁认领谁交）。

---

## 三、卡在谁手里

**A. 卡在甲方一句话**（批了当天能动）
1. 仓库侧 **558 档投不投**游戏目录（游戏侧现为 482）。
2. 角色卡 76 张 draft 的**审批口径**（审批链本机跑不起来，要不要换路子）。

**B. 卡在要开一次游戏**（离线做不了）
3. 09-14 那四个真机缺陷的**复现与确认**（尤其 `npc_dialogue_open_failed`，它卡着 v0.3）。
4. 世界书包形态的真机验收：日志里 `package=awake:worldbook.calradia`（两段）＋ `entries=` 与所投包一致。

**C. 卡在必须人手**（脚本化不了）
5. Modding Kit 里 Import 出 `.tpac`。

**D. 卡在要人拍板**
6. 美术 4 件已交付资产的**色值复核**（基准来自 2023-12 旧版，须用现行 `gauntlet_ui.tpac` 重测）。

**E. 自己就能做（不做会真丢东西的排最前）**
7. **750 条未入库**——569 条世界书内容压着，是「不做会真丢」的那一条。
8. **删 B 档垃圾**（先经甲方）。
9. 新开 `ui_awake_slot` category（现图集已满，接线前必须先腾位）。

---

## 四、本次动手边界（留痕）

- 只做了**读数**：重编（Debug）＋ 实跑测试 ＋ `git status`／`du`／`git log`。
- **未改任何源码**，未碰 `src/` 在途三件，未提交别人的产出。
- 重编走 `build.ps1`（只编译、不同步游戏目录）⇒ 游戏目录未被触碰。
