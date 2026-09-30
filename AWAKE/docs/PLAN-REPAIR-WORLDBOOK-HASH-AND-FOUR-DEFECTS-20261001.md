# PLAN — 世界书哈希契约 + 四个真机缺陷 修复批次

> 立项：2026-10-01　作者：巡检会话（只读盘点）
> 状态：**待用户授权开工**（本文件不预设任何已完成的实现）
> 依据：2026-10-01 凌晨对本工作区的独立巡检。**所有"实测"结论均为本次亲自复现**，不是引用旧文档。
>
> 证据分级沿用 `AWAKE/AGENTS.md`：`E0` 计划/静态存在；`E1` 编译与解析；`E2` 离线测试/Smoke；`E3` 同步与哈希一致；`E4` 匹配 BuildId 的真机入口闭环；`E5` 存读档/长时回归。

---

## 0. 为什么开这个批次

巡检发现：**项目所有自动化门都是绿的，但世界书这条线在游戏里实际上是全灭的。**

| 项 | 声称 | 实测（2026-10-01 01:5x） |
|---|---|---|
| 主工程 Release 构建 | 0 warning / 0 error | ✅ 属实（`AWAKE.Tests` 另有 4× CS4014） |
| `Awake.SdkSmoke.exe` | `PASS ALL` | ❌ **`total=67 passed=66 failed=1`，exit 1** |
| 世界书投送 | 「✅ 已投送，entries=790」 | ⚠️ 字节确实投送了，但**运行时拒绝加载该包** |
| `maf-lint` | 0 blocking | ⚠️ 561 warning，且脚本恒定 `exit 0`（见 §6） |

⇒ 本批次的第一目标不是"加功能"，而是**让判据重新能失败**，然后修掉它指向的东西。

---

## 1. 范围

**做：**
- P0-1 世界书 `contentHash` 跨组件契约不一致（游戏内 800 档世界书加载失败）
- P0-2 `SdkSmoke` 未挂进构建链（P0-1 之所以漏掉的元凶）
- P0-3 路线图 §4.3 的四个真机缺陷
- P1-1 两个"假绿"校验器
- P1-2 角色卡 `tag_registry` 缺口（提交阻塞项，不是本批实现，但必须在提交前解决）
- P2 卫生/文档/合规（可与上面解耦）

**不做（明确排除）：**
- 不碰美术资产线、不碰 UI 接线、不碰 NavalDLC 接入（路线图 §七 的 3/4 与 §六 另开批次）
- 不提版、不发版、不同步游戏目录（**均需用户单独授权**，见 §8）
- 不动任何内容包正文

---

## 2. P0-1 — 世界书 `contentHash` 契约不一致 【最高优先，一行修复】

### 2.1 现象（E2 实测）

```
Awake.SdkSmoke.exe
[67/67] FAIL dialogue-chain-redtest
FAIL_MSG  WB2-HASH-MISMATCH:content
FAIL_TRACE  Awake.WorldbookPackageIntegrity.ReadAndVerify
          → Awake.WorldbookPackageRegistry.Select   (WorldbookPackageRegistry.cs:88)
          → Awake.SdkSmoke.DialogueChainRedtest.RunWorldbookPackageCheck  (DialogueChainRedtest.cs:231)
RESULT total=67 passed=66 failed=1
```

`DialogueChainRedtest.ResolveDeployedManifest()` 指向**游戏目录**：
`D:\SteamLibrary\...\Modules\AWAKE\ModuleData\Worldbook\manifest.json`。

### 2.2 已排除的可能（都实测过，写下来免得后人重走）

| 假设 | 实测 | 结论 |
|---|---|---|
| 仓库侧与游戏目录不一致 | 三件套 SHA-256 全同 | ✗ 排除 |
| 投送脚本改了字节 | 包与编译产物 `geo1-v40-polity-c` 逐字节一致 | ✗ 排除 |
| 三哈希之间不自洽 | `packageHash == SHA256(manifestHash‖contentHash)` ✅ | ✗ 排除 |
| 浮点格式分歧 | `runtime.json`/`index.json` 浮点 token = 0 | ✗ 排除 |
| 布尔序列化分歧 | Newtonsoft `JValue.ToString(Formatting.None)` 输出 `true`/`false` 正确 | ✗ 排除 |
| 转义字符集分歧 | `+` `` ` `` `'` `<` `>` `&` 在包内出现 0 次 | ✗ 排除 |
| 代理对/非 BMP | 0 个 | ✗ 排除 |

### 2.3 根因（E2 实测，精确到一行）

把**两套规范化算法都实现出来对拍**（运行时 Newtonsoft 版 vs Studio System.Text.Json 版）：

```
contentHash 声明值            : E72219C17E0876F006DCA482F0F12DE87527C79295F6DCD41295A94C4C55A39A
contentHash 运行时算 (R)      : 8757D53E30F7132F7C9236E25FC7A4C856CD5A1F45C2918F119054786450BABB   ← 不符
contentHash Studio 算 (S)     : E72219C17E0876F006DCA482F0F12DE87527C79295F6DCD41295A94C4C55A39A   ← 相符

规范化串首个差异 @ char 2460262
  R: …王位被讲成\"神赐的权利\"而非…
  S: …王位被讲成\u0022神赐的权利\u0022而非…
```

| 侧 | 位置 | 行为 |
|---|---|---|
| 运行时 | [`AWAKE/src/WorldbookPackageIntegrity.cs:236`](../src/WorldbookPackageIntegrity.cs) | `case '"': builder.Append("\\\"");` → 输出 `\"` |
| Studio | `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/Contracts.cs:40` | System.Text.Json 默认编码器 → 输出 `\u0022` |

**完备性证明**：两串长度差 **恰好 8 = 2 处 × 4 字符**（`\"`=2 字符，`\u0022`=6 字符），而 `runtime.json` 里 `\u0022` 恰好出现 **2 次**、`\"` **0 次** ⇒ 两套规范化**只在这一处不同**。

**为什么只有 calradia 挂、pilot 不挂**：pilot 包内嵌双引号字符数 = 0。判据没问题，是内容第一次踩到它。

### 2.4 游戏内后果（E1 静态确认）

`WorldbookRuntime.TryLoadAndPublish()` 吞掉异常：

```
WorldbookRuntime.cs:204-208   catch (Exception ex) { AwakeLog.Write("worldbook_runtime_init_error error=" + ex.Message); return false; }
WorldbookRuntime.cs:230-233   _knowledge == null  →  BuildStatusText() = "世界书未加载"
```

⇒ **不崩、不报错弹窗，静默全灭**：800 档世界书一条都进不去，NPC 无从引用任何条目。
这很可能是路线图 §四「v0.3 卡在世界书条目被实际引用」的**上游原因之一**。

### 2.5 修复

**方案 A（推荐，已实测可行）**：把运行时的转义口径对齐 System.Text.Json。

```csharp
// AWAKE/src/WorldbookPackageIntegrity.cs:236
-  case '"': builder.Append("\\\""); break;
+  case '"': builder.Append("\\u0022"); break;
```

**验证已做**：用改后的算法重算 contentHash = `E72219C1…`，**与声明值完全吻合**。

**方案 B（不推荐）**：改 Studio 侧输出 `\"`。需重编译并重投所有包、重算全部哈希，且会改变磁盘上包文件的字节 ⇒ 爆炸半径大得多。

**为什么 A 是对的**：磁盘上的包文件本来就是 System.Text.Json 写的（`\u0022`）。**规范化口径的权威在"写文件的那一侧"** —— 运行时是读取方，必须复现写入方的口径。这也解释了为什么运行时其余转义规则（`<` `>` `&` `'` `+` `` ` ``、大写 `\uXXXX`）与 `JavaScriptEncoder.Default` 逐条吻合：当初就是照着它写的，**只漏了 `"` 这一条**。

**不需要重新生成任何包、不需要重新投送** —— 只改运行时验证器，现有包立刻可用。

### 2.6 连带必须做的两件事（否则同一个坑会再来）

1. **把口径写进契约**：`docs/worldbook-studio-plan/RUNTIME-MAPPING-CONTRACT.md` 增加一节「规范化 JSON 口径」，逐条列出：对象键按 `StringComparer.Ordinal` 升序、字符串转义规则（含 `"` → `\u0022`）、`\uXXXX` 大写、非 ASCII 一律转义、数字按原文字面量。
2. **加穷举一致性测试**（防下一处分歧）：新增离线用例，把 `0x00–0x7F` 全码位 + 代表性非 BMP 字符，分别喂给运行时规范化器与 Studio 规范化器，**断言字节流逐字节相等**。
   - 注意：本次只证到"这 2 处"，`\b` `\f` `\\` `\n` `\r` `\t` 在 calradia 包内**出现 0 次**，所以它们**尚未被实测覆盖**。这个测试正是为了补上这个空白。

### 2.7 验收判据

| 级别 | 判据 |
|---|---|
| E1 | 主工程 `0 warning / 0 error` |
| **E2** | `Awake.SdkSmoke.exe` → **`total=67 passed=67 failed=0`，exit 0**（当前 66/67） |
| E2 | 新增的穷举转义一致性测试通过 |
| E3 | `_build_out` / `dist` / 游戏目录 DLL SHA-256 三方一致 |
| E4 | **需用户授权**：进游戏后 `Awake.log` 出现 `worldbook_runtime_initialized … entries=800`，且**不再**出现 `worldbook_runtime_init_error` |

### 2.8 风险与回滚

- 风险：极低。单行、单分支，且已用真实包对拍验证。
- 回滚：`git checkout -- AWAKE/src/WorldbookPackageIntegrity.cs`（**注意**：该文件当前无未提交改动，回滚干净）。
- 边界：只改 `WriteString` 对 `"` 的处理，**不得**顺手改动其他转义分支（否则 manifestHash 可能跟着变，而它现在是好的）。

---

## 3. P0-2 — `SdkSmoke` 没挂进构建链 【元凶】

### 3.1 现状

[`AWAKE/tools/build.ps1:45`](../tools/build.ps1) 只做 `dotnet build`，**从不运行** `Awake.SdkSmoke.exe`。

⇒ 构建恒绿，测试恒红，没人会知道。**P0-1 就是这样漏掉 8 天的。**

这一条项目自己在 [`AWAKE/docs/HANDOFF-STORAGE-CONSISTENCY-20260922.md:139-140`](HANDOFF-STORAGE-CONSISTENCY-20260922.md) 里已经写明了（「`build.ps1` 只编译不跑测试」「25 个常驻工具脚本，零个跑测试」），**只是没人接**。

### 3.2 修复

`build.ps1` 在 `TESTS_OK` 之后追加一步：运行 `AWAKE.Tests\bin\$Configuration\net472\Awake.SdkSmoke.exe`，**非 0 即抛错**（复用现有 `throw` 风格，保持 `BUILD_OK` 语义不被污染，新增 `SMOKE_OK` 行）。

注意：不能把 smoke 结果并进 `TESTS_OK` —— 那是"编译通过"，语义不同。

### 3.3 验收判据

- E2：在**当前未修复**的树上跑 `build.ps1 -BannerlordApi 1.4.8`，必须**失败**（红测）。
- E2：打完 P0-1 后同一条命令**成功**（绿测）。
- 这一对红→绿，才是这道门真的生效的证据。

### 3.4 附带修正

`build.ps1:4` 与 `AWAKE.csproj:8` 的默认 `BannerlordApi` 仍是 `1.3.15`，而本机游戏已是 `v1.4.8`，且脚本带版本守卫 ⇒ **`.\AWAKE\tools\build.ps1` 无参数必挂**。
commit `3f5aafa` 的消息写着「N0 落地（构建目标切 1.4.8）」，**但代码里没切**。默认值应改为 `1.4.8`。

---

## 4. P0-3 — 路线图 §4.3 的四个真机缺陷

> 本节根因来自一次代码级审计（逐文件阅读 + 游戏程序集 IL 分析），**证据等级 E0/E1，尚未在真机复现**。
> 与路线图原假设的对照已注明。

### 4.1 缺陷 2 与 4 是**同一个族**：`OnGameStart` 里干得太早

两条都是"在 `MBSubModuleBase.OnGameStart` 期间就去读只有 `Game.DoLoading` 之后才存在的东西"。

| | 缺陷 2 `native_readiness Failed` | 缺陷 4 状态落 `AwakeState/unbound/` |
|---|---|---|
| 触发点 | `ProbeExtension.cs:261-263`（由 `CampaignSessionReady` 触发） | `WorldStateStore.cs:411-414` 开 namespace |
| 谁太早 | `AwakeRuntime.cs:764-771` 的 `Hero.MainHero == null` 守卫 | `AwakeFileStorageService.cs:67-72` 的 `ResolveCampaignId()` |
| 真正缺失的 | `Game.Current.PlayerTroop`（IL：`Hero.MainHero` → `CharacterObject.PlayerCharacter` → `Game.Current.PlayerTroop`，**为 null 时 getter 抛 NRE** ⇒ 那句守卫本身就会炸） | `Campaign.UniqueGameId`（IL：只在 `Campaign.OnNewGameCreatedInternal` / `OnLoad` 赋值） |
| 谁赋的值 | `Game.set_PlayerTroop` ← `Campaign.OnNewCampaignStart` / `InitializeGamePlayReferences` ← `Game.DoLoading` | 同上 |
| 时序 | `CampaignSessionReady` 由 `SubModule.cs:53` → `AwakeHostComposition.cs:189-190` 在 `OnGameStart` 里抛出，**早于** `Game.DoLoading` | 同上 |
| 为什么是间歇的 | 启动被 defer 时（`AwakeHostComposition.cs:129-152`，经 `AwakeUiDispatcher.Enqueue` 于 `:324-331` 重跑）跑得晚 ⇒ 返回 Ready。这解释了日志里同场既 Failed 又 Ready | — |
| 额外放大 | `EnsureNativeReadinessAsync` 把 Task **永久记忆**（`AwakeRuntime.cs:671-679`，仅在 `:989`/`:1050` 清），一次早失败黏住整个会话 | `Stores` 静态字典**按路径缓存**（`AwakeFileStorageService.cs:74-82`），`unbound` 路径被冻结 |

**修复方向**
- 探测点后移到 `CampaignEvents.OnSessionLaunchedEvent` / `OnGameLoadedEvent`；
- 守卫改成**不调用 getter** 的写法：`Game.Current?.PlayerTroop == null` → `Skipped`，不要写 `Hero.MainHero == null`；
- 非 `Ready` 的结果**不要 memoize**；
- namespace 开启同样后移，或让 `ResolveCampaignId` 在 `UniqueGameId` 由空转非空时**重新解析并迁移** `unbound` 目录；静态缓存不得只按路径做键。

**连带**：缺陷 4 是缺陷 1 的上游 —— 09-14 状态路径从 `1IgZ8yHJynfn` 漂到 `unbound`，把已写的 journal 变成了孤儿，新路径读到空。

### 4.2 缺陷 1 `awake.world_fact.root_corrupt` —— 路线图假设**不对**

**不是数据损坏，是自伤**：
- `WorldStateStore.cs:907-908` 把**任何** `ReadRoot` 失败都塌缩成 `Corrupt` + `root_corrupt`；
- 而真实后端对"键不存在"返回的是**成功 + 空串**（`AwakeFileStorageService.cs:141-143`），`IsStorageKeyNotFound`（`WorldStateStore.cs:279-284`）在真实后端下是**死代码**；
- 于是"空 ⇒ Corrupt"叠加写入侧的"corrupt ⇒ 放弃"构成**永久死锁**；
- `docs/REPORT-WEEKLY-ROOT-RECOVERY-20260915.md:22-40` 证实磁盘上**从来没有过** journal 文件。

**已经半修了**：`WorldFactJournal.cs:135` 现在把空/空白映射为 `Missing`（注释日期 2026-09-15）；`WorldStateStore.cs:3671-3679` 会隔离损坏 root 并重建。

**残留（今天仍然真）**：
- 读路径**没有恢复能力**：`WorldFactQuery.cs:279-282` 只做状态/错误映射 ⇒ 真损坏时 `awake_weekly_report_refresh status=unavailable` 会一直挂着，**要等某条新世界事实恰好被写入**才自愈；
- `root_corrupt` 抹掉了所有子原因（`chunk_missing` / `chunk_key_mismatch` / `world_fact.fact_invalid`）。

**修复方向**：透出 `ReadRoot` 的真实原因码；补读侧恢复（或"重置 journal"动作）；先修缺陷 4 以免继续制造孤儿。

### 4.3 缺陷 3 `npc_dialogue_open_failed` —— 路线图假设**不对**

**不是 overlay 的 `no_top_screen`**，是**ID 形状不匹配**，而且**完全确定性**：

| 侧 | 位置 | 形态 |
|---|---|---|
| 生产 | `SubModule.cs:77`（由 `NpcProactiveService.cs:525` 在 `npc_proactive_accepted` 后喂） | **裸 StringId**（如 `CharacterObject_1741`） |
| 生产 | `AwakeEventEngine.cs:588` | 裸 id |
| 消费 | `SubModule.cs:178-182` → `NpcDialogueLauncher.FindTargetById` → `AwakeNpcTarget.TryParseStableId`（`AwakeNpcTarget.cs:126-132`） | **只认 `hero:` / `npc:` 前缀** |
| 结果 | 解析失败 → null → `:target_unavailable`，条目被丢弃（`EventDialogueQueue.cs:47-66`） | 嘴永远张不开 |

真机记录形态：`CharacterObject_1741:target_unavailable`（`docs/WALKTHROUGH-20260925-…:54-61`）。

**修复方向**：在**队列边界**归一化（`SubModule.cs:77` 改 `EventDialogueQueue.Enqueue("hero:" + heroId, hint)`），或让 `FindTargetById` 对无前缀 id 回退 `FindHeroById(characterId)`。
⚠️ 必须**幂等**：已有生产者可能已传前缀，直接拼接会出 `hero:hero:xxx`。建议在 `EventDialogueQueue.Enqueue` 内做一次"已带前缀则不再加"的归一化，而不是在各调用点各加一次。
修完还要复核第二道门 `IsEligibleNpcTarget`（`NpcDialogueLauncher.cs:473-486`：英雄须在当前聚落或主队中）。

### 4.4 建议的批次顺序（依赖关系）

```
缺陷 4（时序 + 路径）  ──┬──► 缺陷 1（读侧恢复；4 不修则继续造孤儿）
缺陷 2（时序 + 守卫）  ──┘
缺陷 3（ID 归一化）      ← 独立，可与上面并行
```

理由：2 与 4 是同源时序问题，应**同批**修（同一个 `OnGameStart` 后移动作），否则改一半会让日志更难读；1 的恢复逻辑依赖 4 不再漂路径；3 完全独立。

### 4.5 验收判据

| 缺陷 | 判据 | 级别 |
|---|---|---|
| 2 | 冷启会话头两分钟 `Awake.log` **不再出现** `native_readiness … Failed`；`Game.Current?.PlayerTroop` 为空时记 `Skipped` 而非 Failed | E4 |
| 4 | 新建战役后 `PlayerExports\AwakeState\` 下**不出现** `unbound\`；路径为真实 `UniqueGameId`；存→退→读档状态一致 | E4/E5 |
| 1 | 无事实写入 + 人为损坏 root 的会话里，周报路径能自愈（`status=available`）或至少给出**具体**子原因码 | E2/E4 |
| 3 | `npc_dialogue_open_failed` 的 `…:target_unavailable` 记录**消失**（或变成 `hero:*`），且真机观察到一次 NPC 主动开口 | E4 |

> ⚠️ 全部四条都需要**用户进游戏**才能判 E4。离线只能证到 E1/E2。

### 4.6 MCM 评估（按 `AWAKE/AGENTS.md`〈MCM 菜单规则〉必须写）

- 缺陷 2/3/4：**纯内部时序与 ID 归一化，无玩家可调行为 ⇒ 不进 MCM**。
- 缺陷 1 的"重置 journal"：属于**开发者调试入口**，不是玩家选项 ⇒ 走既有 dev 菜单（`worldbook_reload` 一类），**不得**新增玩家可见 MCM 项；且需与玩家菜单分组分离。

---

## 5. P1-2 — 角色卡 `tag_registry` 缺口 【提交阻塞项】

巡检的独立盘点结论（**不是本批实现，但提交前必须解决**）：

- `AWAKE/tools/persona-workbench/characters/` 下 **279 张新卡未跟踪**（+ 279 个 `.origins.json` 边车，配对完整）；
- 其中 **276 张**使用了 `tag_registry.json` 里**不存在的 14 个标签**（`trait.ruthless`、`expression.cold`、`behavior.calculating`、`trigger.civilian_safety` …）；
- 未提交的 `PersonaWorkbench.Verify/Program.cs` 改动**恰好**把"未注册标签"从通过改为报错 ⇒ 作者知道这个坑；
- 但 `tag_registry.json` **本身是干净的、没跟着改**。

⇒ **若就这么提交：276 张卡会被运行时降级成 `IDENTITY_ONLY`（人设全丢），而所有静态门都是绿的。**

**处置**：扩 `tag_registry.json` 补齐 14 个 id（或给 276 张卡改标签），**再**提交这一整批（279 卡 + 279 边车 + 5 个 `definition.json` + 4 个工具文件 + 指南 v4.3 + 31 份新文档）。
⚠️ 提交纪律：精确 pathspec，**禁 `git add -A` / `git add .`**。

---

## 6. P1-1 — 两个"假绿"校验器

P0-1 之所以能藏 8 天，除了 §3，还有这两处**结构上测不出该缺陷**的校验：

| 位置 | 现状 | 问题 |
|---|---|---|
| [`AWAKE/tools/deploy_worldbook_to_game.ps1:82`](../tools/deploy_worldbook_to_game.ps1) | 只比对 registry 与包 manifest 的 `packageId/version/kind/relativePath/packageHash` **字符串** | 从不重算 contentHash ⇒ 恒报 `DEPLOY_VALIDATE_OK` |
| [`AWAKE/tools/assemble_worldbook_package.ps1:125-143`](../tools/assemble_worldbook_package.ps1) | `-ValidateOnly` 只比对哈希字符串 + 字节拷贝一致性 | 同上 ⇒ 恒报 `ASSEMBLE_VALIDATE_OK` |

**修复**：两个脚本的校验步骤都补一次**真实重算**（按 §2.6 的规范化口径重算 `manifestHash`/`contentHash`/`packageHash` 并与声明值比对）。PowerShell 侧实现时**必须**与运行时口径一致 —— 建议直接复用 §2.6 的穷举一致性测试来锁住三方（运行时 / Studio / PS）口径。

**验收**：E2 —— 在**未修 P0-1** 的树上跑这两个脚本，必须**报红**；修完转绿。

### 6.1 顺带：`maf-lint` 这道门无法失败

`MarcusAIFramework_Reference/SDK_20260815/analyzers/maf-lint.ps1:48` 恒定 `exit 0`，只 `Write-Warning`。实测 `AWAKE/src` 有 **561 条 warning**（MAF004×373 / MAF005×132 / MAF003×45 / MAF002×11）。

⇒ 质量门写的「0 blocking」**在实现上不可能被违反**。要么给 lint 定义 blocking 子集并让它能 `exit 1`，要么把这条门从质量门里去掉、改成"趋势指标"。**不要**继续把它当门。

---

## 7. P2 — 卫生 / 文档 / 合规（可与 P0 解耦）

### 7.1 一次性脚本入库（违反根 `AGENTS.md`〈临时产物〉）

- `AWAKE/tools/` 顶层 667 个 `_*.py/_*.txt/_*.json` 中 **641 个是 tracked**（09-16～09-30 的探针、日志、commit-message 草稿）
- 仓库根：28 个 `_*.txt`；根新建 `tools/` 下 95 个文件（77 个未跟踪 `_*.py`）
- `.gitignore` 对 `_*`、`*.bak`、`_archive-*` **零规则**

**动作**：加 ignore 规则 → 清理。⚠️ 清理属"整理用户未要求的遗留目录"，**需用户明确授权**，不要自作主张删。

### 7.2 文档权威失准

| 问题 | 位置 |
|---|---|
| 权威自己落后于自己的最新提交（790 vs 实际 **800**） | `AWAKE-ROADMAP.md:53`、`README_CN.md:33`、`README_EN.txt` |
| `BUILD_VERIFICATION.txt` 是**前一个项目**的（302 KB / 2434 行，表头 `2026-08-12`、`SlaaneshsEmbrace.dll`、`SubModule v0.2.0`，停更 09-11） | `AWAKE/BUILD_VERIFICATION.txt` |
| 路线图有一处**事实错误**：称 `AWAKE-MOD-RETURN-20260911-checkpoint.md`「该文件不存在」，**它存在** | `AWAKE-ROADMAP.md:162`、`docs/control-plane/README.md:19` |
| 无任何退役标记却自称"当前权威"、仍写 `v0.2.0` 与已退役的串行阶梯 | `docs/AWAKE-Task-Queue-20260816.md` |
| `SubModule.xml:5` 写 `v0.2.0`，路线图却把 v0.2 标 🔴 | `AWAKE/SubModule.xml` |

### 7.3 合规

- **`LICENSE` 与 `NOTICE` 确认缺失**（0 个 tracked 文件）—— 路线图 §八 自己列为 v1.0 门槛。
- 根 `README.md:10` 写 `ModuleData/ # localization only (no world book)`，而该目录实际有 6.4 MB 世界书 ⇒ 与 `README_CN.md:106` 自相矛盾。

### 7.4 架构硬规则的疑似违反（**未在本批修，仅登记**）

| 位置 | 违反的规则 |
|---|---|
| `AwakeImageClient.cs:35,92-103`（+ `AwakeImageProbe.cs:61-90`、`NpcDialogueVM.cs:444-485`） | 〈架构硬规则〉「所有 AI 调用只走逻辑 Route + Output Schema，**不在游戏侧直接 HTTP/保存 Key**」—— 游戏侧自建 `HttpClient`、经 `AwakeImageSecretStore.TryRead` 取 key、发 `Authorization: Bearer` |
| `SceneDialoguePreview.cs:12-13` | 「不长期持有 TaleWorlds 实时对象」—— 静态 `List<GameEntity>` + `Mission` 跨帧持有（在 `SubModule.cs:144` 重置，短命但有风险） |
| `AwakeWorldKnowledgeSemanticIndex.cs:125-132,147-154` | 「UI/campaign tick 中不得阻塞」—— `Task.Wait(timeout)` / `.Result`（有界且已注释，但确实阻塞调用方） |

⇒ 建议单独立项评估，**不要塞进本批**（否则批次无法收敛）。

---

## 8. 需要用户授权的动作清单

以下动作**一律不自行执行**（依 `AWAKE/AGENTS.md`〈工作流程〉）：

1. **同步游戏目录**（`sync_module.ps1` / `deploy_worldbook_to_game.ps1 -ConfirmDeploy`）—— 且须游戏已退出
2. **启动游戏**做 E4 验证
3. **提交**（含 §5 的角色卡批次）
4. **删除/整理** 641+95 个一次性脚本与遗留目录
5. **改默认构建目标** `1.3.15` → `1.4.8`（影响所有后续构建的语义）
6. **提版 / 发版**

---

## 9. 建议的执行顺序

| # | 动作 | 依赖 | 证据级别 | 工作量 |
|---|---|---|---|---|
| 1 | P0-1 一行修复 + 穷举转义一致性测试 | — | E2 | 小 |
| 2 | P0-2 `build.ps1` 挂 smoke（含红测→绿测） | 1 | E2 | 小 |
| 3 | P1-1 两个校验器补真实重算 | 1 | E2 | 小 |
| 4 | 授权后：同步 + 进游戏看 `entries=800` | 1,2 | E3/E4 | 小 |
| 5 | P0-3 缺陷 2+4（同批，时序后移） | — | E1→E4 | 中 |
| 6 | P0-3 缺陷 1（读侧恢复） | 5 | E2/E4 | 中 |
| 7 | P0-3 缺陷 3（ID 归一化，幂等） | — | E1→E4 | 小 |
| 8 | P1-2 角色卡 tag_registry 补齐 + 提交 | — | E2 | 中 |
| 9 | P2 文档/合规/卫生 | — | E0 | 中 |

**第 1–3 步是一组**：它们共同把"绿得很假"变回"绿就是绿"。**建议先做这 3 步并单独验证一次**，再谈后面。

---

## 10. 本计划未做的事

- 未修改任何代码、配置、游戏目录
- 未提交任何东西
- 未清理任何遗留文件
- 四个缺陷的根因**尚未在真机复现**（E0/E1），§4 的验收判据必须由用户进游戏后才能判 E4/E5
- 本文件是**新增未跟踪文件**；是否入库由用户决定
