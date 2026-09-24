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
| **v0.4 有脸** | 界面、图标、**肖像**都长出来了 | 新面板在游戏里渲染（不是本地预览）；图标 / 纹章显示正确；**NPC 肖像由 AWAKE 在游戏内生图并上屏**（甲方 09-15 原话「我建议做回去」；生图是 **port 回框架**、不是重建，片 1 已落地 `8a947ce`；依据 `docs/RULING-CODE-LINE-HANDOVER-20260915.md`） |
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

> ⏳ **读本节前先看这一格（09-17 晚补）：本节是 09-14 的现场快照，09-15 有追加，但 09-17 一整天没收录。**
> - **09-17 识别线变了一整天**（本地语义通道接上生产路径 ＋ 三根门禁中的两根挂了新判据 ＋ 世界书内容侧做了 v13f），
>   本节**一处都没写**（"语义臂／两条通道／判据 E" 在本节里零命中）。
>   要今天的口径看：`docs/DONE-20260917-语义臂接上生产路径.md`、`docs/CHAIN-20260917-现在的检索链路.md`、
>   `docs/DECISION-20260917-两条通道怎么合.md`、`docs/SPEC-20260917-该空手时空手判据.md`。
> - **世界书档数已不是 448**：本节 5 处写 `448`（`:100`／`:126`／`:128`／`:134`／`:137`），那是 09-14／09-15 那几版包的真实数；
>   包换过两回 —— 09-17 早 448、**09-17 18:39 v13f 之后是 451**。
>   ⇒ 其中 `:100` 与 `:137` 是**验收指令**（"真机看 `entries=448` 才算对"），**这两处已改正为 451**；
>   `:126`／`:128`／`:134` 是当时形态工作的记录，**保留原数**（改的是它们的时点含义，不是数字）。
> - **09-18 凌晨档数再变两回**：`461`（v14 把 09-16 做好的**头盔批 10 档**并回上线包 —— 该批此前被
>   `_v2j_kwclean_20260916.py:9-11` 有意排除，此后每轮重编都静默消失）→ **`482`**（v15 **护甲形制批 21 张卡**
>   上线；v16／v17 只原地修内容，档数不变）。⇒ 本节所有 `entries=` **验收指令现在都应是 482**，
>   "451"已成过时读数。依据：`docs/FEED-20260918-护甲形制批上线与两处修复.md`。
> - **角色卡那格的"五道门"是 09-14 的口径**：该线后来按规范 v4 扩到**八道门禁**（见技能 `awake-persona-card-gates`），
>   "76/76 五道门"这个读数**不是**八道门下的读数 ⇒ 别拿它当今天的状态。
> - **09-18 13:1x 补：测试读数已变，本节"61 例（2 条跨线红）"过时** —— 实跑（`AWAKE.Tests/bin/Debug/net472/Awake.SdkSmoke.exe`，退出码 **1**）
>   得 **total=64 / passed=58 / failed=6**，红：`g3-s0-focused-readiness`／`persona-template`／`shared-persona-golden-fixture`／
>   `persona-persistence`／`persona-anchor`／`dialogue-chain-redtest`。
>   其中 `dialogue-chain-redtest` 要的包形态是 `awake.worldbook.v2`，与 09-14 拍板落地的 `registry.v1` **不是同一个**
>   ⇒ **判据没跟着决定走**；改它之前先定"哪个形态是对的"。
>   ⚠ 该读数出自 **10:04 的构建**，而最后一笔动 `src` 的提交 `0bff7b9` 在 **10:05** ⇒ 要准数须重编再跑。
>   全过程与其余实测见 `docs/REVIEW-ALL-LINES-20260918.md`。
> - **✅ 09-19 22:0x 补：上面那个"要准数须重编"的疑点已排除** —— 09-19 21:58 重编（0 错 4 警）后复跑，
>   仍是 **total=64 / passed=58 / failed=6**，红名单**逐条一致** ⇒ **6 条红是真实的，不是构建差一分钟造成的，可定案。**
> - **⚠ 本节补注尚未收录 09-19 的 UI 线判定（它改了「美术」那一格的方向）**：`GUI/SpriteSheets/` 是**死路**，
>   图集**必须走 `AssetPackages/*.tpac`**；AWAKE 现在**没有** `AssetPackages/`。原句「Import 出 tpac 是**唯一**不能脚本化的
>   一步」中的"**只能**手点"要**收窄**（479 字节的纯元数据壳 tpac 是否被运行时接受＝**未验**）。依据
>   `docs/UI-SPRITE-ATLAS-LOOKUP-20260919.md`。09-19 的完整实测与卡点见 `docs/REVIEW-ALL-LINES-20260919.md`。
> - **✅ 09-22 10:2x 补：「6 条红」又过时了一次 —— 现在全绿。**
>   重编（`tools/build.ps1 -Configuration Debug`，smoke exe mtime **09-22 10:28**）后实跑：
>   **`RESULT total=64 passed=64 failed=0`，`EXIT=0`**。
>   ⇒ 上面 :63-68 那整段（6 条红的名单 ＋ 「要准数须重编」那个疑点）**只对到 09-20 14:19 之前成立**；
>   `3db0917`（09-20 14:19，离线 A 批次三件）收口后**判红名单为空**，**别再照它动手**。
>   同一格的世界书读数一并更正：游戏目录那份 **`entries=482`**（用例 `dialogue chain worldbook deployed redtest` 直读，
>   `package=awake:worldbook.calradia`）；仓库侧 **558 档已编好、尚未投送**。
>   ⚠️ **全绿 ≠ 真机缺陷已修**：那四个缺陷是游戏内发生的，离线测不到 —— 见下「下一步」第 0 条。
>   09-22 的完整实测、未入库三档与卡点五组见 `docs/REVIEW-ALL-LINES-20260922.md`。
> - **✅ 09-24 补：条件字段多了 `clan_ids`，知识条目现在可以绑家族。**
>   甲方 09-24 裁定「我就是要知识条目的某一个说法绑定一个 clan」⇒ 当日实现运行时全链
>   （schema → 条件模型 → 编译器 → 加载器 → 判定器 → 查询侧 ＋ 编辑器白名单，共 7 处），
>   提交 `8edbff9`。零替身探针两轮全链验通：主轮 `[1,0,0]` → 变异轮 `[0,1,0]`（真翻转），
>   `CLAN_GATE PASS` 九条全绿。档：`docs/DECISION-20260924-CLAN-BINDING.md`。
>   ⚠️ **这一步越过了 `docs/superpowers/specs/2026-08-24-worldbook-entity-catalog-design.md:31`
>   那句「B1 不实现 `hero_ids`/`clan_ids` 运行时权限条件」** —— 视为被甲方 09-24 裁定覆盖，
>   **设计稿待回写**；`hero_ids` 未跟进（保持原状）。现有 558 档**没有一档**用到 `clan_ids`。

### 一句话

**第一刀落地了。** 2026-09-14 23:14–23:19，主干带 AWAKE 进了一次游戏，跑满整场会话、干净退出。
链路是通的，功能真跑起来了；**同时露出四个真缺陷**——现在的问题不再是"没进过游戏"，而是"进去过之后先修哪个"。

> **09-15 代码线移交 → 全局主控线已裁决**（`docs/RULING-CODE-LINE-HANDOVER-20260915.md`）。
> 三条排序：**① 先收口上面那一跑露出的四个缺陷 → ② 再走「工具候选」（唯一纯接线，v0.3 的素材）→ ③ 事件落盘 / 资产与生图排后**（后两者要动框架，风险隔离）。
> 「模型／媒体／资产／日志」四面：**照现有 5 个可替换槽同构加注入口**（不造"5 可替换 ＋ 4 不可替换"两套并存的路）。
> 状态列"从证据自动生成"：**方向对但现在不做**，先停掉手工维护的假状态。
>
> ⚠️ 本线移交书的前提「本线至今零游戏内验证」**不成立**（09-14 已跑过）；但也不能算"验过了"——
> 那是一次 ad-hoc 跑，产出的是**缺陷清单**，不是通过记录。正确表述：**游戏内已经开过头，但没有任何一条断言在游戏里被判过。**

> 📈 **功能前景评估（09-15，原版框架迁移到手之后）**：`docs/OUTLOOK-MARCUS-FRAMEWORK-20260915.md`。
> 一句话：**底座从"要造"变成"要搬 ＋ 接线"，但"玩家看得见什么"一点没变**——
> 原版是**厚骨架 ＋ 零玩法**，AWAKE 是**有肉没骨架**，两块今天才拼上。
> ⇒ **不再为"框架功能完整"排优先级**，按版本阶梯需要哪件搬哪件；人力全转「游戏内 ＋ 内容侧」。
> ⚠️ 两个新出现的硬约束：**发布合规**（AWAKE 缺 `LICENSE`/`NOTICE` 署名）与**原版「成人内容」章节不能跟着搬**。

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
| 主干 · 运行时 | 构建 0 错 0 警；production-smoke 31/31；`AWAKE.Tests` **64 例全绿**（09-22 10:28 重编后实跑 `failed=0` ⇒ ⚠️ **本节旧的「61 例 2 条跨线红」已过时**，见上方 09-22 补注） | **✅ 已进游戏**（09-14 23:14，见上「真机第一次跑」）。**09-15 已移交状态**；本轮＝① 收口四个缺陷 → ② 工具候选（A）→ ③ 四面加注入口（同构）；见上「09-15 代码线移交」 |
| 角色卡 | 76/76 五道门；红队 0/17；盲评脱名 77% | 断在**投送**：游戏目录只有 **1 张**卡、**8 条**标签（仓库侧 39）；审批 **76 张全 draft** |
| 世界书 | 448 档（**⚠️ 09-18 凌晨已为 482，见上方时点格**）；矩阵 133/133；编译包 `Valid: true`；仓库侧包已组出 | 只差**真机确认**：游戏目录已是 registry ＋ `packages/calradia/`，进游戏看 `Awake.log` 的 `worldbook_runtime_initialized … entries=`**（读今天的包应是 482；判法见下方「四个断点」第 3 条末尾的注）**。<br>✅ **09-20 14:4x 实纠正：游戏目录那份已是 `482`** —— 红测直读（`DialogueChainRedtest` 走 registry 选包 ＋ 重算三 hash）：`entries=482 package=awake:worldbook.calradia`。⇒ 上面那句"09-18 实测仍是 448"**已过时**。真机确认时 `entries=` 应是 **482**；而 09-20 新编的 **558 档**（军事 25 ＋ 经济 40 ＋ 暗面 11 ＋ 互引边 1286 条）**尚未投送**，要上须跑 `tools/deploy_worldbook_to_game.ps1 -ConfirmDeploy` |
| UI 编辑层 | 框架 / Prefab / Brush 落地；Lab 6 面板 0 error | **部分上屏**：运行时肖像纹理已上屏（212×360，09-14）；面板与图标待验 |
| 美术资产（**现役会话＝「本地生图」**） | 产线跑通；官方 UI 贴图已从 `.tpac` 抠出；**09-15 交付 4 件 UI 控件资产**（画位人形／画位框／按钮三态／busy 图标，甲方已验收） | 三件事挡着：**Import 出 `.tpac`**（全链唯一不能脚本化）；**28 张图标做完了没接线**；**`ui_awake_frame` 图集已满**（4088/4096），212×360 画位框装不下 |

> 线名注：美术资产线的现役会话＝**「本地生图」**（工作台 `C:/Users/26811/WorkBuddy/LocalAIPictureGeneration`，接本机 ComfyUI）；
> 原「美术资产」会话已退役（09-15），职能已交接。其交付产物落在 `tools/awake-art-lab/out/`（被 `.gitignore` 排除），
> **只看 git 会误判"美术线没产物"**。

> 📣 **设计裁定（09-17，**不改上面任何状态、不排期**）**：甲方重申设计初衷「**我要你用 AI 模拟真实知识传播路径和对话**」
> ⇒ **检索命中率不是成败标准**，它只是"取数段的健康度"。已拍板四件：
> ① 传播**混着来**（有名字的按人／无名群众按地批量）；② **事件为主、时间为底线**；
> ③ 分级＝**两个轴**（事件侧 S/A/B/C ＋ 知识侧"公开程度"开关）；④ 领主/贵族推动 **新增一条"公文"通道**（**不复用**私人信件线）。
> 依据：`docs/DECISION-20260917-设计初衷-知识传播与对话.md`（在册出处／我偏在哪／三段底座盘点／三个已核缺口／速度表）。
> ⚠️ 这是**记录**，不是开工单 —— **第三阶段及以后一律不排期**。

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
   **还缺**：真机跑一次，看 `Awake.log` 有 `worldbook_runtime_initialized … package=awake:worldbook.calradia … entries=`。
   ⚠️ **这里原写 `entries=448` —— 别照那个数判**：448 是 09-14／09-15 那两版投送的包；09-17 18:39 v13f 之后是 451；**09-18 凌晨 v15／v17 之后是 482**。
   真正要判的是**两个**：① `package=` 是**两段** `awake:worldbook.calradia`（三段＝读的是旧包）；
   ② `entries=` 与**你投的那份包**的 `runtime.json` 条数一致（今天的包 = 482）——**不是与"448"／"451"一致**。
   （游戏内 dev 菜单有 `worldbook_reload` 可热重载，不必重启。）
4. **美术 · 图标没接线 ＋ 图集容量 ＋ 色值基准**（09-15 从美术工作台档案核实）：
   `GUI/SpriteParts/ui_awake_icon/` 里 **28 张 `icon_*.png` 没有任何登记**（SpritePart / GenericSprite / Brush 全无引用）⇒ 游戏读不到；
   `ui_awake_frame` 图集 `4096×1024` **已用到 4088/4096**，212×360 画位框装不下 ⇒ 接线前须新开 category（建议 `ui_awake_slot`）；
   官方纸/石色值基准是从 **2023-12 旧版**安装抠的（那份已删，本机只剩 v1.3.15），须用 D 盘现行 `gauntlet_ui.tpac` 重测复核——
   色值变了则已交付的 4 件 UI 资产要重校准。

## 下一步

> ✅ **「第一刀 · 点亮」09-14 已走通**（`Awake.dll` 进游戏、注册成功、会话跑满、干净退出）——见上「真机第一次跑」。
> 当时定的口径：不等三个断点全清，先走**最窄的一条**；这一格走通，v0.1 才算真的开始。

0. **收口那一跑露出的缺陷**（09-15 裁决后的第一件，代码线）：
   `world_fact.root_corrupt`（周报 / WeeklyDynamics 整链不可用）、`native_readiness` 空引用（**09-10 起反复、从未修**，同场里也多次 `Ready` ⇒ 是竞态不是恒定坏）、
   会话头两分钟 `awake_host_resolution runtime_not_ready`、`npc_dialogue_open_failed`（**v0.3 卡在这**）。
   四条都**已经在游戏里真实发生过** ⇒ **复现即可**，比再造场景划算，**不必攒一次大走查**。
   附带一条（**v0.2 卡在这，推断未证实**）：状态落 `AwakeState/unbound/`——存储绑在认出存档 id 之前，绑了个空。
0-bis. **工具候选（A）**：纯接线、成本最低，且是 v0.3「活起来」的素材（工具接上了，NPC 才谈得上"做事"）。
0-ter. **先补全离线闭环（09-15 甲方：暂时无法开机实测）**：**先做能离线补的**，真机那一步留着一次点亮。
   清单与交付提示词见 **`docs/PLAN-OFFLINE-CLOSURE-20260915.md`**（含三条已复核的时效更正：对话链 P1 只剩 P1-3 未修；`world_fact.root_corrupt` 可离线复现且可离线验证；`npc_dialogue_open_failed` 离线复现不了）。
   ⚠ 离线全绿**不等于**闭环成立，**不得**标记"实机完成"。
1. **入库**（最高优先，且与真机无关，现在就能做）：美术线的**源图与工具**（`AssetSources/`、`GUI/SpriteParts/`、
   `tools/awake-art-lab/`）**仍未入库**——交付产物在 `out/` 是被 `.gitignore` 有意排除，那是另一回事；
   世界书线亦压着未提交产物。**当轮成果当轮提交**（精确 pathspec，勿 `add -A`）。
2. ✅ **~~修 `AWAKE.Tests` 两条跨线红~~（09-20 14:19 `3db0917` 已收口，09-22 实跑 64/64 全绿）**。
   原根因记录：`docs/fixtures/persona-load-v2-golden.json` 的 `expectedDsl` 未跟随 `PersonaDslGenerator` 的段序改动
   （`PUBLIC` 已后置）。**不需要游戏**；修好后运行期闸口自动复活。⇒ 本条**已完成，不再排期**。
4. **美术接线 ＋ 色值复核**（详见上第 4 断点）：先新开 `ui_awake_slot` category（`ui_awake_frame` 已满），
   再把 `ui_awake_icon` 那 28 张里**有效的一批**登记进 `AWAKESpriteData.xml`；
   同时用 D 盘现行 `gauntlet_ui.tpac` 复测官方纸/石色值——变了则已交付 4 件资产重校准。
4. **~~定「仓库侧 Worldbook 包形态」~~（已于 09-14 拍板，见上第 3 条）**：形态＝registry 存根 ＋ `packages/<universe>/` ＋ 只入库运行时三件套；身份＝`calradia`。
5. **真机走查改走小步**：**不再攒一次大走查**。每清掉一个断点，就单独进一次游戏看它生没生效。

## 发布门槛（v1.0 前逐条补齐）

- **不崩**：新档 / 读档 / 退出都干净，无红字。
- **存档兼容**：跨版本增删字段有迁移或降级路径。
- **共存**：与常见模组同装不冲突；依赖与加载顺序写清。
- **上手**：装完能自己配起来（MCM 选项 ＋ 一份说明）。
- **分级**：**本体（工坊主包）不含成人向内容**——要做走独立扩展包，与本体代码／内容解耦（甲方 09-15 定，见 `docs/DECISION-20260915-PHASE3-BOUNDARY.md`）。
- **文档**：创意工坊页面的说明与截图。

## Version Rules

- Version changes follow playable acceptance, not file count or implementation volume.
- A prior verified build is historical evidence, not evidence for a new candidate.
- A release candidate must have attributable hashes and separated offline/game/save-load evidence.
