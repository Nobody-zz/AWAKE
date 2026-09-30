# AWAKE Roadmap

> 只写**版本目标**与**当前坐标**；批次执行流水不在这里（进 `docs/`；执行期流水另见**执行 agent 的本地工作记忆**，不入库，但**撤线后文件仍在、可据以续做**）。
> 维护：全局主控线　更新：**2026-09-30**
>
> **本文件即当前方向的权威。** `docs/` 下文档太多（652 份），找东西看 **`AWAKE-DOC-INDEX-20260930.md`**（文档导航入口）。

---

## 一、终点

**公开上创意工坊（Steam Workshop）。** 在此之前不对外发版。

---

## 二、版本阶梯

判据一律以**游戏内**为准。离线全绿不算过版。

| 版本 | 玩家能多做什么 | 过版判据（必须在游戏里） | 状态 |
|---|---|---|---|
| **v0.1 点亮** | 装上、进得去、看得见 AWAKE 在跑 | 游戏里出现 AWAKE 的输出；MCM 选项页能打开 | ✅ **已走通**（09-14 23:14 真机跑满一场，干净退出） |
| **v0.2 记得住** | 它记得住事 | 存盘 → 退出 → 读档，状态还在且一致 | 🔴 卡在「状态落 `AwakeState/unbound/`」（推断未证实） |
| **v0.3 活起来** | NPC 会主动找你、会送信、会提起旧事 | 游戏内触发一次 NPC 主动行为；世界书条目被实际引用 | 🔴 卡在 `npc_dialogue_open_failed`（候选建了、被接受了，就是张不开嘴） |
| **v0.4 有脸** | 界面、图标、**肖像**都长出来了 | 新面板在游戏里渲染（不是本地预览）；图标/纹章显示正确；**NPC 肖像由 AWAKE 在游戏内生图并上屏** | 🟡 部分：肖像已上屏（212×360）；面板与图标待验 |
| **v0.5 撑得住** | 能一直玩下去 | 连续游玩不崩；与常见模组同装不冲突；帧率可接受 | ⬜ 未开始 |
| **v1.0** | 可以公开了 | 上面全过 ＋ 存档兼容 ＋ 依赖声明 ＋ 上手指引齐备 | ⬜ 未开始 |

> 旧的 `0.1.x`～`1.0` 目标表是**按内容模块**排的（运行时核心／内容包／世界模拟／记忆／内容系统／生态／性能／RC），
> 与实际的**五线并行**不符，已按上表重新按「玩家看得见什么」归位。模块清单没丢，落在各线自己的文档里。

---

## 三、两条节奏，别混在一起

| | 单位 | 快慢 |
|---|---|---|
| **对内 · 工程与验证** | 一次说得清的变化 | **小步快跑**：一次只动一个变量，改完就进游戏看一眼 |
| **对外 · 发布** | 一个能公开的完整体 | **攒大的**：还没有玩家之前，频繁发版没有收益，每次都要处理存档兼容、依赖声明、公告 |

⇒ 二者**不冲突，不必二选一**。在 v1.0 之前对外不发版。

---

## 四、当前坐标（2026-09-30）

### 4.1 五条线在哪

| 线 | 离线层（已自证） | 距离「游戏内生效」 |
|---|---|---|
| **主干 · 运行时** | 构建 0 错；`AWAKE.Tests` **64 例全绿**（09-22 重编后实跑 `failed=0`） | ✅ **已进游戏**（09-14 23:14）。09-15 已移交；本轮＝① 收口四个缺陷 → ② 工具候选（纯接线）→ ③ 四面加注入口（同构） |
| **角色卡** | 76/76 八道门（规范 v4）；红队 0/17；盲评脱名 77% | 🔴 断在**投送**：游戏目录只有 **1 张**卡、**8 条**标签（仓库侧 39）；审批 **76 张全 draft** |
| **世界书** | **790 档**（v37；矩阵 133/133；编译包 `Valid: true`） | ✅ **已投送**（09-30 15:22）：仓库侧与游戏目录**逐字节一致**，`entries=790`（geography 416／economy 151／politics 132／war 71／culture 20）、identity 12、polity 5。真机看 `Awake.log` 的 `worldbook_runtime_initialized … entries=790` |
| **UI 编辑层** | 框架/Prefab/Brush 落地；Lab 6 面板 0 error | 🟡 **部分上屏**：运行时肖像纹理已上屏（212×360）；面板与图标待验 |
| **美术资产**（现役会话＝「本地生图」） | 产线跑通；官方 UI 贴图已从 `.tpac` 抠出；**09-15 交付 4 件 UI 控件资产**（甲方已验收） | 🔴 三件事挡着：**Import 出 `.tpac`**（全链唯一不能脚本化）；**28 张图标做完了没接线**；**`ui_awake_frame` 图集已满**（4088/4096） |

> 线名注：美术资产线的现役会话＝**「本地生图」**（工作台 `C:/Users/26811/WorkBuddy/LocalAIPictureGeneration`，接本机 ComfyUI）；
> 原「美术资产」会话已退役（09-15）。其交付产物落在 `tools/awake-art-lab/out/`（被 `.gitignore` 排除），**只看 git 会误判"美术线没产物"**。

### 4.2 真机基线（2026-09-14 23:14–23:19）

游戏 `v1.3.15.110062`（**现已升级到 v1.4.8，见 §六**）；候选 `build_id=awake-20260912-world-fact-query-004`，`dll_sha256=F23AADA0…`。
证据：`rgl_log_33744.txt`（3553 行，**0 异常**）｜`Modules/AWAKE/Logs/Awake.log`（今晚段 104 行）｜`AwakeProbe.log`｜watchdog 无异常事件。

**跑通了什么**
- 加载 → `register_ok` → 会话生命周期完整：`CampaignSessionReady` → … → `host_campaign_session_drained success=True`；退出 `pending_writes=0 dropped=0`。
- **出图第一次在游戏内成功**：本地端点 → 200，6.6 s，512×512 JPEG 41.6 KB（落盘魔数 `ffd8ffe0`，是真图）。
- **运行时肖像第一次上屏**：`portrait_texture_ready` 212×360 ×3。
- **NPC 对话面板第一次在游戏内开起来**：`hero:lord_1_18`，`Negotiation` ↔ `Chat` 来回切，正常关闭。
- 世界书包加载 `warnings=0`，`manifest_sha256` 与磁盘 `manifest.json` **逐字节相符**。

### 4.3 露出的四个真缺陷（未修）

| # | 缺陷 | 卡住哪个版本 |
|---|---|---|
| 1 | `awake.world_fact.root_corrupt` ×4 —— 周报 / WeeklyDynamics **整链不可用**（`count=0`，`refresh status=unavailable`） | 功能缺口 |
| 2 | `native_readiness status=Failed code=native_probe_exception`（空引用）—— **09-10 起反复出现，从未修**；同场里也多次 `Ready` ⇒ 是**次序/竞态**，不是恒定坏 | 会话头两分钟 |
| 3 | `npc_dialogue_open_failed` ×2 —— 主动对话的候选建了、也被接受了，**就是张不开嘴** | **v0.3「活起来」** |
| 4 | 状态落盘进 `AwakeState/unbound/`（**推断，未证实**）—— 存储绑在认出存档 id 之前，绑了个空 | **v0.2「记得住」** |

---

## 五、「最后一公里」＝ 四个断点

1. **美术 · Import**：Modding Kit → 控制台 `resource.show_resource_browser` → Scan → Import，产出 `AssetPackages/*.tpac`。
   **全链唯一无法脚本化的一步**（该命令只存在于 native `TaleWorlds.Native.dll`，无 CLI 入口）。
   本机 Modding Kit 已装：`bin/Win64_Shipping_wEditor/` 在，含 `TaleWorlds.TwoDimension.SpriteSheetGenerator.exe`。
2. **角色卡 · 投送 ＋ 审批**：76 张定义 ＋ 新 `tag_registry` 投送到 `ModuleData/Worldbook/persona_definitions/`；
   76 张 draft 需过审批门——真身是 `tools/persona-awake-joint/`，其验证链**在本机跑不起来**（脚本硬调 `pwsh`，本机只有 Windows PowerShell 5.1）。
3. **世界书 · 仓库侧包形态**：**09-14 已定案并落地**，规范＝`docs/worldbook-studio-plan/RUNTIME-MAPPING-CONTRACT.md`〈仓库侧包形态〉。
   > ✅ **09-14→09-30 的 10 版欠账已一次清掉**（这是本线此前最大的坑：编译链从 09-18 一路跑到 09-30，但仓库侧与游戏目录都一直停在 482）。
   > **根因**：`deploy_worldbook_to_game.ps1` **只搬「仓库侧→游戏侧」**，不生成内容 —— 完整链是**两步**，只跑第二步 = 把旧内容又搬一次。
   > **两步链**：① 产物 →（`tools/_deploy_v37_stage1_repo.py` 一类脚本）→ 仓库侧；② 仓库侧 →（`deploy_worldbook_to_game.ps1 -ConfirmDeploy`）→ 游戏目录。
   > **真机判两个**：① `package=` 是**两段** `awake:worldbook.calradia`（三段＝读的是旧包）；② `entries=` 与**你投的那份包**一致（现为 **790**）。
   > 游戏内 dev 菜单有 `worldbook_reload` 可热重载，不必重启。
4. **美术 · 图标没接线 ＋ 图集容量 ＋ 色值基准**（09-15 从美术工作台档案核实）：
   `GUI/SpriteParts/ui_awake_icon/` 里 **28 张 `icon_*.png` 没有任何登记**（SpritePart / GenericSprite / Brush 全无引用）⇒ 游戏读不到；
   `ui_awake_frame` 图集 `4096×1024` **已用到 4088/4096**，212×360 画位框装不下 ⇒ 接线前须新开 category（建议 `ui_awake_slot`）；
   官方纸/石色值基准是从 **2023-12 旧版**安装抠的（那份已删，本机只剩现行版），须用 D 盘现行 `gauntlet_ui.tpac` 重测复核——色值变了则已交付的 4 件 UI 资产要重校准。

---

## 六、⭐ 战帆 DLC（NavalDLC）—— 软依赖，但内容必须做

**口径（甲方 09-30 定）：DLC 不是硬依赖，玩家可以选择不加；但本模组关于 DLC 的内容一定要做。**

- 官方侧 `NavalDLC` 是 `OfficialOptional`，但 `DefaultModule=true`（默认加载）⇒ 绝大多数玩家**实际会带**。
- **基础游戏已全线升到 v1.4.8**；AWAKE 默认构建目标仍是 1.3.15。
- **当前状态分三层**（09-30 修正，**不是"完全没接"**）：
  - ✅ **工具/参考层已建**（08-24）：`docs/mappings/war-sails-reference/`（528 条）＋ 实体注册表带 DLC 状态（`official_dlc_not_installed` 53 hero／9 clan）；
  - 🟡 **内容层部分**：4 份正典档写 Nord（`clan-clan_nord_1/2/3`、`military-nord`），**无 DLC 条件**；
  - ❌ **运行时层未接**：`src/*.cs` 零命中；**世界书 schema 无 DLC 字段**（想写条件也写不出来）。
- ⭐ **口径 08-24 已定**（`war-sails-reference/README.md`）：「战帆是同一世界的官方 DLC……**本机未安装时，不能把"本机缺少数据"当成对象冲突**」⇒ 与本轮甲方口径一致，**不是新决定**。
- **硬纪律**：不得把 NavalDLC 写进 `DependedModules`（那是硬依赖）；一切走运行时探测 + 静默降级。

完整评估见 **`docs/DLC-COMPAT-NAVAL-20260930.md`**（含 §1.3 三层修正、待办 N0–N2、待裁定问题）。

---

## 七、下一步

> ✅ **「第一刀 · 点亮」09-14 已走通** —— 不等三个断点全清，先走**最窄的一条**。这一格走通，v0.1 才算真的开始。

0. **收口那一跑露出的四个缺陷**（09-15 裁决后的第一件）：
   `world_fact.root_corrupt`、`native_readiness` 空引用、会话头两分钟 `runtime_not_ready`、`npc_dialogue_open_failed`。
   四条都**已经在游戏里真实发生过** ⇒ **复现即可**，比再造场景划算，**不必攒一次大走查**。
   附带一条（**v0.2 卡在这，推断未证实**）：状态落 `AwakeState/unbound/`。
1. **战帆 DLC 接入（N0→N1→N2）**：切构建目标到 1.4.8 → 加运行时 DLC 探测（软）→ 内容侧给 nord 档补 DLC 条件。见 §六。
2. **入库**（最高优先，且与真机无关，现在就能做）：美术线的**源图与工具**（`AssetSources/`、`GUI/SpriteParts/`、`tools/awake-art-lab/`）**仍未入库**；
   世界书线亦压着未提交产物。**当轮成果当轮提交**（精确 pathspec，勿 `add -A`）。
3. **美术接线 ＋ 色值复核**（详见 §五 第 4 断点）：先新开 `ui_awake_slot` category（`ui_awake_frame` 已满），
   再把 `ui_awake_icon` 那 28 张里**有效的一批**登记进 `AWAKESpriteData.xml`；同时用 D 盘现行 `gauntlet_ui.tpac` 复测官方纸/石色值。
4. **真机走查改走小步**：**不再攒一次大走查**。每清掉一个断点，就单独进一次游戏看它生没生效。

---

## 八、发布门槛（v1.0 前逐条补齐）

- **不崩**：新档 / 读档 / 退出都干净，无红字。
- **存档兼容**：跨版本增删字段有迁移或降级路径。
- **共存**：与常见模组同装不冲突；依赖与加载顺序写清。**含「不带 NavalDLC 也能跑」这条**。
- **上手**：装完能自己配起来（MCM 选项 ＋ 一份说明）。
- **分级**：**本体（工坊主包）不含成人向内容**——要做走独立扩展包，与本体代码／内容解耦（甲方 09-15 定，见 `docs/DECISION-20260915-PHASE3-BOUNDARY.md`）。
- **合规**：⚠️ **当前缺 `LICENSE` 与 `NOTICE` 署名文件**（09-15 提出，至今未补）。
- **文档**：创意工坊页面的说明与截图。

---

## 九、Version Rules

- Version changes follow playable acceptance, not file count or implementation volume.
- A prior verified build is historical evidence, not evidence for a new candidate.
- A release candidate must have attributable hashes and separated offline/game/save-load evidence.

---

## 附：失效载体（保留供追溯）

本文件原先指向的两份"执行状态"载体**都已失效**：

- `AWAKE-CURRENT.md`（83 KB，最后更新 **2026-09-11**）已退役为 `AWAKE-STATE-HISTORY.md`；
- `docs/control-plane/CURRENT.json` 的 `checkpoint_path` 指向 `docs/checkpoints/AWAKE-MOD-RETURN-20260911-checkpoint.md`，
  而**该文件不存在**，已冻结为 `CURRENT-20260911-frozen.json`。

⇒ 在控制面重建之前，**本文件的「当前坐标」一节即当前方向的权威**。

> 另：本文件 09-14 至 09-30 期间曾以「层层补注」形式累积历史批注（十几层 `补：` 折叠）。
> 09-30 已把有效结论**折进正文**（§四/§五/§六），历史批注不再单独保留 —— 要读时点演变，看
> `docs/REVIEW-ALL-LINES-2026091*.md` 与 `docs/worldbook-migration/DEPLOY-V*-REPORT-*.md`。
