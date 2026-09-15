# AWAKE Roadmap

> 只写**版本目标**与**开发节奏**；批次执行状态不在这里。
> 维护：全局主控线　更新：2026-09-14
>
> ⚠️ **状态声明**：本文件原先指向的两份"执行状态"载体**都已失效**——
> `AWAKE-CURRENT.md`（83 KB，最后更新 **2026-09-11**）已退役为 `AWAKE-STATE-HISTORY.md`；
> `docs/control-plane/CURRENT.json` 的 `checkpoint_path` 指向 `docs/checkpoints/AWAKE-MOD-RETURN-20260911-checkpoint.md`，
> 而**该文件不存在**，已冻结为 `CURRENT-20260911-frozen.json`。
> ⇒ 在控制面重建之前，**本文件的「现状」一节即当前方向的权威**。

## 终点

**公开上创意工坊（Steam Workshop）。** 在此之前不对外发版。

## 版本阶梯

判据一律以**游戏内**为准。离线全绿不算过版。

| 版本 | 玩家能多做什么 | 过版判据（必须在游戏里） |
|---|---|---|
| **v0.1 点亮** | 装上、进得去、看得见 AWAKE 在跑 | 游戏里出现 AWAKE 的输出；MCM 选项页能打开 |
| **v0.2 记得住** | 它记得住事 | 存盘 → 退出 → 读档，状态还在且一致 |
| **v0.3 活起来** | NPC 会主动找你、会送信、会提起旧事 | 游戏里触发一次 NPC 主动行为；世界书条目被实际引用 |
| **v0.4 有脸** | 界面和图标长出来了 | 新面板在游戏里渲染（不是本地预览）；图标 / 纹章显示正确 |
| **v0.5 撑得住** | 能一直玩下去 | 连续游玩不崩；与常见模组同装不冲突；帧率可接受 |
| **v1.0** | 可以公开了 | 上面全过 ＋ 存档兼容 ＋ 依赖声明 ＋ 上手指引齐备 |

> 旧的 `0.1.x` ～ `1.0` 目标表是**按内容模块**排的（运行时核心 / 内容包 / 世界模拟 / 记忆 /
> 内容系统 / 生态 / 性能 / RC），与实际的**五线并行**不符，已按上表重新按「玩家看得见什么」归位。
> 模块清单没丢，落在各线自己的文档里。

## 两条节奏，别混在一起

| | 单位 | 快慢 |
|---|---|---|
| **对内 · 工程与验证** | 一次说得清的变化 | **小步快跑**：一次只动一个变量，改完就进游戏看一眼 |
| **对外 · 发布** | 一个能公开的完整体 | **攒大的**：还没有玩家之前，频繁发版没有收益，每次都要处理存档兼容、依赖声明、公告 |

⇒ 二者**不冲突，不必二选一**。在 v1.0 之前对外不发版。

> **前提**：小步快跑要成立，得先有一个"一步"能落地的地方。
> 现在五条线**一次都没进过游戏**——所以第一刀不是"小步"，是**先走通一步**：
> 窄到"游戏里出现一行 AWAKE 的字"就算数。

## 现状 · 2026-09-14

### 一句话

**第一刀落地了。** 2026-09-14 23:14–23:19，主干带 AWAKE 进了一次游戏，跑满整场会话、干净退出。
链路是通的，功能真跑起来了；**同时露出四个真缺陷**——现在的问题不再是"没进过游戏"，而是"进去过之后先修哪个"。

### 真机第一次跑（2026-09-14 23:14–23:19）

游戏 `v1.3.15.110062`；本次候选 `build_id=awake-20260912-world-fact-query-004`，`dll_sha256=F23AADA0…`。
证据：`rgl_log_33744.txt`（3553 行，**0 异常**）｜`Modules/AWAKE/Logs/Awake.log`（今晚段 104 行）｜`AwakeProbe.log`｜watchdog 无异常事件。

**跑通了什么**
- 加载 → `register_ok` → 会话生命周期完整：`CampaignSessionReady` → … → `host_campaign_session_drained success=True`；退出 `pending_writes=0 dropped=0`。
- **出图第一次在游戏内成功**：本地端点 → 200，6.6 s，512×512 JPEG 41.6 KB（落盘魔数 `ffd8ffe0`，是真图）。
- **运行时肖像第一次上屏**：`portrait_texture_ready` 212×360 ×3。
- **NPC 对话面板第一次在游戏内开起来**：`hero:lord_1_18`，`Negotiation` ↔ `Chat` 来回切，正常关闭。
- 世界书 v2 包加载 `entries=3 warnings=0`，`manifest_sha256` 与磁盘 `manifest.json` **逐字节相符**。

**露出的四个真缺陷**
1. **`awake.world_fact.root_corrupt` ×4** —— 周报 / WeeklyDynamics **整条链路不可用**（`count=0`，`refresh status=unavailable`）。
   今晚首次出现：该 build 头一回进游戏。
2. **`native_readiness status=Failed code=native_probe_exception`**（空引用）—— **09-10 起反复出现，从未修**；
   同一份日志里也多次 `Ready` ⇒ 是次序 / 竞态，不是恒定坏。
   同一场里 `awake_host_resolution status=runtime_not_ready` → 2.5 分钟后才 `embedded_host_ready`
   ⇒ **会话开头那两分钟的功能走不了**。
3. **`npc_dialogue_open_failed` ×2** —— 主动对话的候选建了、也被接受了，**就是张不开嘴**。v0.3「活起来」卡在这。
4. **状态落盘进 `AwakeState/unbound/`** —— 而 09-11 那场落进 `AwakeState/1IgZ8yHJynfn/`（那名字就是当时的存档 id）。
   `unbound` 的时间戳（15:15:03）**早于**认出存档 id 的时间（15:15:11）⇒ **存储绑在存档之前，绑了个空，之后再没回头**。
   ⚠️ **此条为推断，未证实**——但目录名与两处时间戳都对得上。它卡的是 v0.2「记得住」。

**游戏内的美术缺口（实证）**：`Cannot find texture: ui_awake_button_1 / ui_awake_frame_1 / ui_awake_ornament_1`；
模块内确认 **0 个 `.tpac`**、无 `AssetPackages/`。

### 五条线在哪

| 线 | 离线层（已自证） | 距离"游戏内生效" |
|---|---|---|
| 主干 · 运行时 | 构建 0 错 0 警；production-smoke 31/31；`AWAKE.Tests` 61 例（2 条跨线红） | **✅ 已进游戏**（09-14 23:14，见上「真机第一次跑」）。转进修四个缺陷 |
| 角色卡 | 76/76 五道门；红队 0/17；盲评脱名 77% | 断在**投送**：游戏目录只有 **1 张**卡、**8 条**标签（仓库侧 39）；审批 **76 张全 draft** |
| 世界书 | 448 档；矩阵 133/133；编译包 `Valid: true`；仓库侧包已组出；**已投送到游戏目录并过运行时读取器** | 只差**真机确认**：游戏目录已是 registry ＋ `packages/calradia/`，进游戏看 `Awake.log` 的 `worldbook_runtime_initialized … entries=448` |
| UI 编辑层 | 框架 / Prefab / Brush 落地；Lab 6 面板 0 error | **部分上屏**：运行时肖像纹理已上屏（212×360，09-14）；面板与图标待验 |
| 美术资产（**现役会话＝「本地生图」**） | 产线跑通；官方 UI 贴图已从 `.tpac` 抠出；**09-15 交付 4 件 UI 控件资产**（画位人形／画位框／按钮三态／busy 图标，甲方已验收） | 三件事挡着：**Import 出 `.tpac`**（全链唯一不能脚本化）；**28 张图标做完了没接线**；**`ui_awake_frame` 图集已满**（4088/4096），212×360 画位框装不下 |

> 线名注：美术资产线的现役会话＝**「本地生图」**（工作台 `C:/Users/26811/WorkBuddy/LocalAIPictureGeneration`，接本机 ComfyUI）；
> 原「美术资产」会话已退役（09-15），职能已交接。其交付产物落在 `tools/awake-art-lab/out/`（被 `.gitignore` 排除），
> **只看 git 会误判"美术线没产物"**。

### 「最后一公里」＝ 四个断点

1. **美术 · Import**：Modding Kit → 控制台 `resource.show_resource_browser` → Scan → Import，产出 `AssetPackages/*.tpac`。
   **全链唯一无法脚本化的一步**（该命令只存在于 native `TaleWorlds.Native.dll`，无 CLI 入口）。
   本机 Modding Kit 已装：`bin/Win64_Shipping_wEditor/` 在，含 `TaleWorlds.TwoDimension.SpriteSheetGenerator.exe`。
2. **角色卡 · 投送 ＋ 审批**：76 张定义 ＋ 新 `tag_registry` 投送到 `ModuleData/Worldbook/persona_definitions/`；
   76 张 draft 需过审批门——真身是 `tools/persona-awake-joint/`，其验证链**在本机跑不起来**
   （脚本硬调 `pwsh`，本机只有 Windows PowerShell 5.1）。
3. **世界书 · 仓库侧包形态**：**09-14 已定案并落地**，规范＝`docs/worldbook-studio-plan/RUNTIME-MAPPING-CONTRACT.md`〈仓库侧包形态〉。
   形状：`ModuleData/Worldbook/{manifest.json(registry.v1), packages/calradia/{manifest,runtime,index}.json, persona_definitions/}`；
   身份：`worldId=awake:world:calradia`、`packageId=awake:worldbook.calradia`、`displayName=卡拉迪亚` ——
   改为**编译器常量**（原由 `first["universe"]` 推、`displayName` 取首篇标题 ⇒ 随改名漂）。**448 档与重登记都不动**，只重编译。
   原始病根不是「缺一个文件」，是**同一个 `manifest.json` 被两套相反的要求夹住**（`sync_module.ps1` 要 v1 的 persona 字段 / 运行时只认 registry 或 v2），交集为空 ⇒ 从来没写出来过。
   **落地物**：`tools/assemble_worldbook_package.ps1`（编译产物 → 仓库侧形态，逐字节拷贝 ＋ 回验）；包在 `AWAKE/ModuleData/Worldbook/`（448 entries，三 hash 与 registry 逐条相符）。
   **⚠️ 第四方之前没人提**：`.gitignore` 原把 `AWAKE/ModuleData/Worldbook/*` 整条排除（缘由写的是"v1 已被取代"）——形态定得再好也进不了库。已放行 `manifest.json` ＋ `packages/`，其余仍排除。
   **主干两侧**：`sync_module.ps1` 的受管清单原来**没有 `packages`**（包永远投不出去），且八条 v1 目录是死配置；`Assert-SourceManifest` 已改为断言 canonical 路径 ＋ 校验 registry 里每个包的三件套真的在。
   **投送前已验**：`tools/worldbook-runtime-smoke` 新增 `TestRepositoryPackageForm` 直读真实包（registry 选包 → `ReadAndVerify` 重算三 hash/1.5 MB → 角色卡定位器按形状找到同一根）；**变异检验**改坏一处 summary ⇒ 红 `WB2-HASH-MISMATCH:content`，还原 ⇒ 绿；`sync_module` 测试 17/17（含两条负向用例）。
   **投送（09-14 23:39 已做）**：游戏目录 `Modules\AWAKE\ModuleData\Worldbook\` 换成 registry ＋ `packages/calradia/`（1 覆盖 ＋ 3 新增）；旧试点 `manifest.json` 已备份到 `tools/worldbook-studio/artifacts/game-dir-deploy-20260914-233912/`。**755 个 v1 遗留文件与 `persona_definitions/` 一字未动。**
   **验收**：`tools/worldbook-runtime-smoke` 带 `AWAKE_WORLDBOOK_ROOT=<游戏目录>` 直读**游戏侧那份** —— PASS（并做变异检验证明这个覆盖真的生效，不是回退到仓库侧）。
   **⚠️ 09-15 重投一次（身份纠错后）**：09-14 投的那份 `packageId` 是**三段** `awake:worldbook:calradia`，违反契约的两段 pattern（Studio 测试 `F24` 判红）。契约复原、编译器常量改两段后重编译（`v2f.*` → `compiled/geo1-v5`，448 docs / `0 error / 21 warning`），并用 `tools/deploy_worldbook_to_game.ps1 -ConfirmDeploy` **重投到游戏目录**（旧 `manifest.json` 备份在 `artifacts/game-dir-deploy-20260915-165737/`）。
   现在**仓库侧与游戏侧四件逐字节相同**（`cmp` 实测），`AWAKE_WORLDBOOK_ROOT=<游戏目录>` 的验台 **PASS**。
   ⇒ **真机验证时日志里应出现 `package=awake:worldbook.calradia`（两段）**；若看到三段，说明读的是旧包。
   **还缺**：真机跑一次，看 `Awake.log` 有 `worldbook_runtime_initialized … package=awake:worldbook.calradia … entries=448`（游戏内 dev 菜单有 `worldbook_reload` 可热重载，不必重启）。
4. **美术 · 图标没接线 ＋ 图集容量 ＋ 色值基准**（09-15 从美术工作台档案核实）：
   `GUI/SpriteParts/ui_awake_icon/` 里 **28 张 `icon_*.png` 没有任何登记**（SpritePart / GenericSprite / Brush 全无引用）⇒ 游戏读不到；
   `ui_awake_frame` 图集 `4096×1024` **已用到 4088/4096**，212×360 画位框装不下 ⇒ 接线前须新开 category（建议 `ui_awake_slot`）；
   官方纸/石色值基准是从 **2023-12 旧版**安装抠的（那份已删，本机只剩 v1.3.15），须用 D 盘现行 `gauntlet_ui.tpac` 重测复核——
   色值变了则已交付的 4 件 UI 资产要重校准。

## 下一步

0. **第一刀 · 点亮**（真机恢复后的第一件事）：不等三个断点全清，先走**最窄的一条**——
   主干 `Awake.dll` 已在游戏目录，进一次游戏确认它加载了、能输出一行字。
   这一格走通，v0.1 才算真的开始。**此前所有"最后一公里"的工作都排在它后面。**
1. **入库**（最高优先，且与真机无关，现在就能做）：美术线的**源图与工具**（`AssetSources/`、`GUI/SpriteParts/`、
   `tools/awake-art-lab/`）**仍未入库**——交付产物在 `out/` 是被 `.gitignore` 有意排除，那是另一回事；
   世界书线亦压着未提交产物。**当轮成果当轮提交**（精确 pathspec，勿 `add -A`）。
2. **修 `AWAKE.Tests` 两条跨线红**：根因已坐实——`docs/fixtures/persona-load-v2-golden.json` 的 `expectedDsl`
   未跟随 `PersonaDslGenerator` 的段序改动（`PUBLIC` 已后置）。**不需要游戏**；修好后运行期闸口自动复活。
3. **美术接线 ＋ 色值复核**（详见上第 4 断点）：先新开 `ui_awake_slot` category（`ui_awake_frame` 已满），
   再把 `ui_awake_icon` 那 28 张里**有效的一批**登记进 `AWAKESpriteData.xml`；
   同时用 D 盘现行 `gauntlet_ui.tpac` 复测官方纸/石色值——变了则已交付 4 件资产重校准。
4. **~~定「仓库侧 Worldbook 包形态」~~（已于 09-14 拍板，见上第 3 条）**：形态＝registry 存根 ＋ `packages/<universe>/` ＋ 只入库运行时三件套；身份＝`calradia`。
5. **真机走查改走小步**：**不再攒一次大走查**。每清掉一个断点，就单独进一次游戏看它生没生效。

## 发布门槛（v1.0 前逐条补齐）

- **不崩**：新档 / 读档 / 退出都干净，无红字。
- **存档兼容**：跨版本增删字段有迁移或降级路径。
- **共存**：与常见模组同装不冲突；依赖与加载顺序写清。
- **上手**：装完能自己配起来（MCM 选项 ＋ 一份说明）。
- **文档**：创意工坊页面的说明与截图。

## Version Rules

- Version changes follow playable acceptance, not file count or implementation volume.
- A prior verified build is historical evidence, not evidence for a new candidate.
- A release candidate must have attributable hashes and separated offline/game/save-load evidence.
