# 缺陷②＋④ 同批收口报告（原生就绪的可重试语义 ＋ campaign 未绑定时的存储契约）

- 日期：2026-10-01
- 线：主干·运行时 + AI 通路
- 依据计划：`AWAKE/docs/PLAN-REPAIR-WORLDBOOK-HASH-AND-FOUR-DEFECTS-20261001.md` §4.3/§4.4/§4.5/§4.6
- 依据裁决：`AWAKE/docs/HANDOFF-STORAGE-CONSISTENCY-20260922.md:90-99` §1.2 Q11-4（主控直接定）
- 依据设计：`AWAKE/docs/REVIEW-DESIGN-20261001.md:217` §九 行动清单第 3 项
- 本次最高证据等级：**E2**（离线测试 69/69 全绿，含 4 次变异检验）＋ **E1**（主工程 1.4.8 重编 0 错误）。
  **E4 未取**：按用户 2026-10-01 决定，本批不投送 DLL、不启动游戏。

---

## 一 结论

1. **缺陷②「`native_readiness status=Failed code=native_probe_exception`」的根因有两条，都已修**：
   (a) 探针在「战役已建、主角还没绑」时**在守卫里自己抛 NRE**（`Hero.MainHero` 的 getter 链
   `Hero.MainHero → CharacterObject.PlayerCharacter → Game.Current.PlayerTroop` 为 null 时抛），
   被外层 `catch (Exception)` 记成 `native_probe_exception` —— 即"守卫本身会抛"；
   (b) `EnsureNativeReadinessAsync` 把那次早失败的 Task **永久记住**（`if (_nativeReadinessTask != null) return _nativeReadinessTask;`），
   一次早探测黏住整个会话，这解释了真机日志里"同一场既 Failed 又 Ready"。
   修法：给 `NativeReadinessResult.Skipped` 补 `retryable` 语义，把"还没到时候"（`campaign_unavailable` /
   `player_unavailable`）标成可重试，并把 `Hero.MainHero` 单独接住；`EnsureNativeReadinessAsync` 只在
   「已落地的、`Retryable` 且非 `Ready`」结论上丢缓存重探。
2. **缺陷④「状态落 `AwakeState/unbound/`」按 Q11-4 的裁决改成了「拒绝写入并报可重试」**：
   campaign 未绑定时不再往共享的 `unbound\` 桶里塞数据，而是返回
   `awake.storage.campaign_unbound`（`Unavailable` + `retryable: true`）。`ResolveCampaignId()` 未绑定时
   返回 `null` 而不是那个桶名。
3. **两处都有会响的判据**：新增/改写 3 条判据，**先取干净红、再取绿**，并用 **4 次变异检验**逐条证明判据不是摆设。
4. **本批不足以单独救活 v0.2「记得住」** —— 见 §五「行为变更与代价」，这一条必须显式记录，不许含糊。

---

## 二 改了什么（全部在主干线，10 个文件里有 4 个属本批）

| 文件 | 行 | 改动 |
|---|---|---|
| `AWAKE/src/AwakeRuntime.cs` | `:61-68` | `NativeReadinessResult.Skipped(...)` 增可选参 `bool retryable = false`（默认值保持既有调用点语义不变） |
| `AWAKE/src/AwakeRuntime.cs` | `:134` | 新增测试缝 `internal static Func<bool> CampaignBoundProviderForTesting { get; set; }`（生产恒 null） |
| `AWAKE/src/AwakeRuntime.cs` | `:675-712` | `EnsureNativeReadinessAsync`：在飞的 Task 仍单飞；**已落地且可重试的非 Ready 结论丢缓存重探**，并打 `native_readiness_retry generation=<n> code=<code>` |
| `AWAKE/src/AwakeRuntime.cs` | `:797-800` | `campaign_unavailable` → `Skipped(..., retryable: true)` |
| `AWAKE/src/AwakeRuntime.cs` | `:803-819` | `Hero.MainHero` 单独 `try/catch` → `player_unavailable`（可重试），异常打 `native_readiness_player_unbound generation=<n> error=`；不再落进外层 `native_probe_exception` |
| `AWAKE/src/AwakeRuntime.cs` | `:829-835` | 新增 `IsCampaignBound()`（只为离线可测而抽成方法） |
| `AWAKE/src/AwakeFileStorageService.cs` | `:64-83` | `OpenCampaignNamespace`：未绑定即**拒绝**，返回 `awake.storage.campaign_unbound`（`Unavailable`/`retryable: true`），打 `storage_namespace_open_unbound namespace=`；不再拼 `unbound` 路径 |
| `AWAKE/src/AwakeFileStorageService.cs` | `:117-131` | `ResolveCampaignId()` 未绑定时返回 `null`（原来返回 `"unbound"`） |
| `AWAKE.Tests/DialogueChainRedtest.cs` | `:22`、`:155-229` | 存储判据改写（见 §三） |
| `AWAKE.Tests/Program.cs` | `:139-140`、`:570-660` | 两条新用例（见 §三） |

行为不变的证明：把 `AwakeRuntime.cs` 的 `Skipped` 加参、加缝、`IsCampaignBound()` 抽方法、`AwakeFileStorageService` 加缝
这四件先单独落地（**先加缝、不动行为**），重编重跑得 `RESULT total=67 passed=67 failed=0` / `PASS ALL` / exit 0
（证据 `d24-evidence\p1_build.txt` / `p1_smoke.txt`）—— 即"缝本身是惰性的"，后面的红不是缝造成的。

---

## 三 判据：先干净红，再绿

### 3.1 判据改动

1. **`dialogue-chain-redtest` 的存储段改写**（`AWAKE.Tests/DialogueChainRedtest.cs:155-229`）。
   原判据 `:167-168` 断言「campaign namespace must open」并**直接把 `unbound` 当路径**（`:159`）——
   等于**把缺陷④写进了判据**。改为四段：
   - 未绑定必须**被拒**（`:177`）、错误码必须是 `awake.storage.campaign_unbound`（`:179`）、必须**可重试**（`:182`）、
     且**不得在 `unbound\` 留下文件**（`:183`）；
   - 缝给出 `TestCampaignId = "awake-redtest-campaign"`（`:22`、`:187`）后必须**开成功**；
   - set / get / delete / get-after-delete 四条往返（保留原判据）；
   - 必须落在 `<真存档 id>\` 目录（`:207`）且**仍不落 `unbound\`**（`:209`）。
2. **新用例 `native-readiness-retry`**（`AWAKE.Tests/Program.cs:570-616`）：可重试的早结论必须**被重探**
   （第二次必须探到 `Ready`、`probeCalls == 2`）；不可重试的结论必须**仍然只探一次**（`terminalCalls == 1`）。
3. **新用例 `native-readiness-too-early`**（`AWAKE.Tests/Program.cs:618-653`）：用缝把"战役在"打开，
   让**真实的 `Hero.MainHero`** 走到底（离线它的 getter 链同样会抛，与真机同源），必须报
   `Skipped / player_unavailable / retryable=true`；没有战役时必须报
   `Skipped / campaign_unavailable / retryable=true`。

两条新用例插在 `dialogue-chain-redtest` **之前**（`:139-140`），保住"重置 UI 调度线程的用例放最后"这条约定。
代价：`dialogue-chain-redtest` 的序号由 67 变 69，用例总数 67 → **69**（既有报告若按序号引用，需 +2）。

### 3.2 红（实现前）

```
RESULT total=69 passed=66 failed=3
FAILED_CASES native-readiness-retry,native-readiness-too-early,dialogue-chain-redtest
```

三条各自红在对的理由上：

| 用例 | 红时实际值 | 说明 |
|---|---|---|
| `native-readiness-retry` | `a retryable native readiness result must be re-probed, got status=Skipped calls=1` | 永久记忆：第二次调用没重探 |
| `native-readiness-too-early` | `got status=Failed code=native_probe_exception retryable=True` | **在离线复现了真机错误码**（`Hero.MainHero` 确实抛了） |
| `dialogue-chain-redtest` | `campaign namespace must be refused while the campaign is unbound` | 未绑定照写 `unbound` |

证据：`d24-evidence\p2_build_red.txt` / `p2_smoke_red.txt`。

### 3.3 绿（实现后）

```
TESTS_BUILD_EXIT=0   （0 个错误）
SMOKE_EXIT=0
RESULT total=69 passed=69 failed=0
PASS ALL Awake.SdkSmoke
```

另查：测试 bin 下**无 `PlayerExports` 残留**（判据的 `Cleanup` 真的收干净了）；
`MUTATION-TEMP` 在 `AWAKE/src`、`AWAKE.Tests` **零命中**；
四个被改文件 BOM 与行尾与改前一致（`AwakeRuntime.cs` 1412 行纯 LF、`AwakeFileStorageService.cs` 307 行纯 LF、
`DialogueChainRedtest.cs` 325 行纯 LF、`Program.cs` 5190 行纯 CRLF，均无 BOM）。
证据：`d24-evidence\final_tests_build.txt` / `final_smoke.txt`。

### 3.4 主工程 E1

```
MSBuild.exe AWAKE.csproj /restore /t:Rebuild /p:Configuration=Release /p:BannerlordApi=1.4.8 /p:GamePath=<真游戏根>
  → exit 0
  AWAKE -> D:\AWAKE-Dev\AWAKE\_build_out\1.4.8\Release\Awake.dll
  DLL_BYTES=1178624  SHA256=BC896228CED46084687746A940F1814FE90E5C911F9E73A88813869FC554A54D
```

0 错误；唯一告警是 `NU1900`（离线取 NuGet 漏洞数据失败，环境，非代码）。
**这一步不可省**：`AwakeHostComposition.cs` / `SubModule.cs` / `NpcDialogueService.cs` 不在离线套件里，
只有主工程能证明它们与本次改动一起编得过。证据 `d24-evidence\final_main_build.txt`。

---

## 四 变异检验（四条，逐条证明判据会响）

| 变异 | 做法 | 结果 | 证据 |
|---|---|---|---|
| M1 | `EnsureNativeReadinessAsync` 恢复永久记忆（`\|\| _sessionGeneration < int.MaxValue`） | **红**：`FAILED_CASES native-readiness-retry`，`got status=Skipped calls=1`，`passed=68 failed=1` | `mut1_build.txt`/`mut1_smoke.txt` |
| M2 | `campaign_unavailable` 的 `retryable: true` 去掉（回默认 false） | **红**：`FAILED_CASES native-readiness-too-early`，`got status=Skipped code=campaign_unavailable retryable=False` | `mut2_build.txt`/`mut2_smoke.txt` |
| M3 | 去掉 `Hero.MainHero` 的内层 `try/catch`（回到裸守卫） | **红**：`FAILED_CASES native-readiness-too-early`，`got status=Failed code=native_probe_exception`（**真机错误码**） | `mut3_build.txt`/`mut3_smoke.txt` |
| M4 | `ResolveCampaignId()` 恢复 `"unbound"` 回退 | **红**：`FAILED_CASES dialogue-chain-redtest`，`campaign namespace must be refused while the campaign is unbound` | `mut4_build.txt`/`mut4_smoke.txt` |

四次都**只有那一条**变红（`passed=68 failed=1`），随后逐字还原，最终 69/69 全绿。

---

## 五 行为变更与代价（必须显式记录）

### 5.1 ④ 是**行为变更**，且它单独不足以救活 v0.2

Q11-4 的原文就是"行为变更（现行是照写）"。改成拒绝以后：

- **好的那一半**：数据不再进孤儿桶。09-14 真机实证 `unbound\`（onboarding / dialogue.queue / npc.proactive /
  world.events / letters，时间戳 23:15–23:17）与 `1IgZ8yHJynfn\`（contacts / memories / transcripts，09-12）两份并存 ——
  09-11 那场绑上了真存档 id，**09-14 那场绑了空**。真存档 id 一旦出来，`unbound\` 里的账本再也读不回来。
  拒绝写入之后，**惰性调用方**（游戏进行中，届时 campaign 必已绑定）会自然绑对：
  `AwakeEventEngine.cs:295,548`、`AwakeLetterService.cs:447`、`AwakeTerminalBehavior.cs:1267`、
  `NpcDialogueService.cs:481,559,1566`、`NpcLetterInitiator.cs:108`。
  且**开失败不会被记忆**（`AwakeRuntime.cs:928-941`：只打 `world_state_storage_open_failed required=…`、
  退役候选、`return false`；`_worldStateDrainFailed` 只在 drain 路径置位），所以下一次调用会重试。
- **代价（必须说清）**：**唯一的急切调用方**是 `ProbeExtension.cs:239`（跑在 `CampaignSessionReady` 上），
  它成功后的续接动作是 `RestoreCampaignStateAsync`（`ProbeExtension.cs:250` → `:364`）。
  如果真机上 `CampaignSessionReady` 时存档 id 还认不出来（09-14 的时序：存储开启 15:15:03 **早于** 认出存档 id 15:15:11），
  那么**这一次的战役状态回灌会被跳过**——数据不会再放错位置，但也不会在开局时被读回来。

  ⇒ **结论：④ 修好的是"错位"，没有修好"记得住"。** v0.2 要真正通，还需要
  `REVIEW-DESIGN-20261001.md:217` §九 第 3 项那件结构活：**把 `CampaignSessionReady` 拆成
  `CampaignSessionReady` + `CampaignWorldQueryable`**（该文件 §6.1 记两个阶段在
  `AwakeHostComposition.cs:189-190` 是相邻两行、零间隙）。它的验收判据原文就是
  「冷启后 `PlayerExports\AwakeState\` 不出现 `unbound\`；`native_readiness` 不再 Failed；journal 不再孤儿」——
  本批把前两条的地基打好了，第三条（journal 孤儿）要等缺陷①的读侧恢复。

### 5.2 为什么本批**没有**顺手做那次拆分

拆分要动 `AwakeHostComposition.cs`（**不在**离线套件里）＋新增一个生命周期阶段并接到游戏侧的
`SubModule.cs`/`Game.DoLoading` 时序上。触发条件本身**只能靠真机验**，而本批按用户决定不碰游戏 ——
在没有 E4 的前提下改生命周期触发点，正好是"判据用错入口"那一类不可失败的门。所以本批只把
**契约**改对（可离线验证的部分），把**触发结构**留给能取 E4 的时候。

### 5.3 顺带查实的一条事实（供拆分时用）

`AwakeHostComposition.cs:12` 的 `CampaignId` 是**常量字符串** `"bannerlord.campaign"`（`:169` 用它建 `SessionRef`），
**不是**真存档 id。⇒ 会话开始本身**不携带**"存档 id 已就绪"的证据，不能拿它当 ④ 的判据。

### 5.4 姊妹缺陷（本次**不修**，记录在案）

`AWAKE/src/AwakePlayerSnapshotProvider.cs:25-102` `Read()` 与②同源：`:30` 判 `Campaign.Current == null`、
`:36-41` 判 `Hero.MainHero == null`，但 `:97-101` 的 `catch (Exception)` 会把**同一个 getter 抛出的 NRE**
记成 `game_data.player_snapshot_error` 而不是 `player_unavailable`。离线判据只断言"两码之一"
（`AWAKE.Tests/DialogueChainRedtest.cs:36-45`），所以打不到该分支。属同一类"守卫本身会抛"，建议与拆分同批处理。

---

## 六 MCM 评估（项目规则要求每个功能都评估）

**不进 MCM。** 理由：②④都是纯内部时序/ID 语义，玩家侧没有可调行为——
②改的是"早探测的结论算不算数"，④改的是"未绑定时写哪里"。玩家能感知到的只有结果（不再出现
`unbound\` 孤儿、原生就绪能自己恢复），没有可配参数。
缺陷① 的"重置 journal"属开发者调试入口，走既有 dev 菜单，同样不进 MCM（见本批计划 §4.6）。

---

## 七 未完成 / 残留风险

1. **E4 未取**：本批全部证据止于 E2（离线 69/69）＋ E1（主工程 0 错）。**真机上是否真的不再出
   `native_readiness Failed`、`unbound\` 是否真的不再出现，都没有验证过。**
2. **`RestoreCampaignStateAsync` 的跳过**（§5.1 代价）——需要 §九 第 3 项的拆分来收口。
3. **缺陷① 的读侧恢复＋`root_corrupt` 子原因码**未做（09-15 三笔只做了写侧自愈与判据；读路径
   `WorldFactQuery.cs:279-282` 仍只做状态映射，`root_corrupt` 仍会抹掉子原因）。
4. **缺陷③（对话目标 id 归一）** 已在上一批修完并绿，但**同样没取 E4**；`IsEligibleNpcTarget` 第二道门只能真机验。
5. **`AWAKE/tools/worldbook-runtime-production-smoke/ProductionSmokeTestsBoundary.cs:519-612`**（世界书线的生产烟测）
   里有 `Check(readiness.Status == NativeReadinessStatus.Failed, "Probe campaign-ready entry must preserve the failed native boundary")`。
   它的探针是常量 `Failed`，我的改动只会让它**重新探针并再次得到 `Failed`**，所以该断言**仍成立**；
   但**我没有重跑那个工具**（它属世界书线的门），如需确证要请世界书线跑一次。
6. **未提交、未同步游戏目录、未启动游戏**（按用户决定）。游戏侧 `Modules\AWAKE\Awake.dll` 仍是 09-14 的陈旧件。
7. 本会话环境限制（**非本批引入**，不影响仓库）：`dotnet build` 必须加 `-m:1 -nodeReuse:false`
   （否则多进程走命名管道被沙箱挡，会以"生成失败 / 0 个警告 / 0 个错误"约 1.5s 退出 1，**掩盖真因**）；
   不能执行 `.ps1`；不能写工作区外路径。`AWAKE/tools/build.ps1` 本身未因此改动。

---

## 八 回滚

本批 4 个文件可用 `git checkout --` 逐个回退（都是 HEAD 里的干净文件）：
`AWAKE/src/AwakeRuntime.cs`、`AWAKE/src/AwakeFileStorageService.cs`、
`AWAKE.Tests/Program.cs`、`AWAKE.Tests/DialogueChainRedtest.cs`。
改动前的精确副本另存于 `C:\Users\26811\AppData\Local\Temp\dsh-gli9Jz\`（`awake-defect3-20261001-220856\` 为上一批；
本批四处改动可用 `git show HEAD:<path>` 取原文）。回滚后应重编并复跑 `Awake.SdkSmoke.exe`，期望回到
`total=67 passed=67 failed=0`。
