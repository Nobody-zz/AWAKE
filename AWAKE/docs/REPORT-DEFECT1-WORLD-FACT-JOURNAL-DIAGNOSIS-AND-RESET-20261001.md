# 缺陷① 世界事实账本：读侧诊断码 + 显式「重置账本」入口（2026-10-01）

线：主干 · 运行时（AI 通路 / 存储 / 世界事实）
依据：`docs/PLAN-REPAIR-WORLDBOOK-HASH-AND-FOUR-DEFECTS-20261001.md` §4.2（`:205-219`）、§4.5（`:254`）、§4.6（`:261-262`）
状态：离线 E2 全绿（70/70，含 4 条新判据）；主工程 E1 0 错（含 dev 菜单接线）；**E4 未取**；未提交、未同步游戏目录。

---

## 一 结论

`awake.world_fact.root_corrupt` 这条链，本批修的是计划点名的两个残留：

1. **残留②（子原因被抹）已修**：账本 root 坏掉时，日志与返回值不再只有一个写死的 `awake.world_fact.root_corrupt`，
   而是给出 codec 判断出的**具体子原因**（`root_json_invalid` / `root_schema_mismatch` / `root_phase_invalid` /
   `root_bounds_invalid` / `root_chunk_keys_invalid`），并落一行 `world_fact_journal_root_corrupt code=…`。
   与 chunk 侧早就存在的子原因码（`chunk_missing` / `chunk_key_mismatch` / `fact_invalid` …）**口径对齐** ——
   修之前 root 与 chunk 的诊断能力是不对称的。

2. **残留①（读侧没有恢复入口）按计划的替代方案补齐**：新增显式动作
   `WorldStateStore.ResetWorldFactJournalAsync()`（隔离坏 root → 重建空账本 revision 1），挂在**既有开发者测试菜单**
   （`world_fact_journal_reset`）。重置成功后账本读作 `Empty`，周报链因此能回到 `status=available`。

**明确没做的事（有意，不是漏）**：**没有**做"读路径发现坏账本时自动改写存储"。
两条理由都是仓库硬规则/结构事实，不是偏好：

- 写侧 `AppendWorldFactJournalAsync`（`AWAKE/src/WorldStateStore.cs:3660`）是在**持 `WorldFactJournalWriterGate` 的时候**
  去读账本的（`AWAKE/src/WorldStateStore.cs:3670`）。读路径若也写 root：取同一把门会**死锁**；
  不取则可能撞坏写侧的写后读回校验（`:3737-3748` 的 `root_commit_unknown` / `root_replace_retryable` / `root_conflict`）。
- 仓库硬规则「效果走 Command + Preflight + 权限 + 幂等」。读侧隐式写存储，绕过了
  `AwakeConstants.PermissionStorageWrite` 的预检。显式开发者动作则与既有 `worldbook_reload` / `worldbook_export_overlay` 同类。

⇒ **代价（必须说清）**：周报链**不会自动**恢复，需要开发者点一下那个菜单项。见 §七。

---

## 二 改了什么

### 源码（4 个文件，全在主干线）

| 文件 | 改动 |
|---|---|
| `AWAKE/src/WorldFactJournal.cs` | `ReadRoot` 新增三参重载 `(json, out root, out errorCode)`；两参旧签名**原样保留**并转发（离线判据 6 处调用不动）。五个子原因码 + 检查次序契约 |
| `AWAKE/src/WorldStateStore.cs` | 读路径透传诊断码 + 落日志（`:901-919`）；新增 `ResetWorldFactJournalAsync`（`:975-1051`） |
| `AWAKE/src/AwakeDeveloperTestActions.cs` | 新增 `ResetWorldFactJournal()`（`:112`），异步走 `AwakeBackgroundTask.Run`，反馈回 UI 线程 |
| `AWAKE/src/AwakeTerminalBehavior.cs` | 开发者菜单新增元素 `world_fact_journal_reset`（`:1152-1157`）与分派分支（`:1242-1245`） |

关键点：

- **检查次序是契约的一部分**（`AWAKE/src/WorldFactJournal.cs:152-165`）：`schema → phase → bounds → chunkKeys`。
  理由：`{"schema":"other"}` 什么都不带，若先查 bounds 就会报 `root_bounds_invalid`，把排查引向错的方向。
  这条次序被判据钉住（见 §四 变异 M2）。
- **旧两参签名不动**：`AWAKE.Tests/Program.cs:2479-2498` 有 6 处 `ReadRoot(json, out scratch)` 调用，
  改成三参会连带改判据；加转发重载既保住调用面又拿到诊断码。
- **重置不删数据**：先 `QuarantineCorruptJournalAsync` 把坏值复制到 `…journal.root.quarantine`（沿用 09-15 既有实现），
  再写空账本；隔离失败 ⇒ `awake.world_fact.journal_quarantine_failed`（`retryable: true`）且**不动原件**。
- **健康账本不动手**：非 `Corrupt` ⇒ 直接 `Succeeded("awake.world_fact.journal_reset_not_needed")`。
- **存储不可用不吞**：`Unavailable` 原样报出（`retryable: true`）—— 隔离/重建解决不了环境问题。
- **写空账本用 `BuildRoot(1, 7, 1, 空)`**：窗口 1..7 只为满足 root 的边界不变量；空账本没有事实，
  查询窗口取自请求（`WorldFactQuery.ResolveWindow`），与 root 的 bounds 无关。

### 判据（`AWAKE.Tests/Program.cs`）

- `RunWorldFactJournalSmoke`（`:2477-2541`）：新增 2b 段（`:2495-2521`），用三参重载钉住五种子原因 + 检查次序。
  其中**乱序 chunkKeys 必须手写 JSON**（`BuildRoot` 自己会排序，造不出乱序）。
- `RunWorldFactJournalRecoverySmokeAsync`（`:2619`、`:2658`）：坏 root 必须透出 `root_json_invalid`；
  末尾再加**第二种坏法**（`{"schema":"other"}` ⇒ `root_schema_mismatch`），证明子原因是跟着坏法变的，
  不是一个写死串换另一个写死串。
- 新增 `RunWorldFactJournalResetSmokeAsync`（`:2676`）六步：① 坏账本读作 `Corrupt` + 子原因；
  ② 重置成功且返回值＝诊断出的子原因；③ 旁路 key 存在；④ 重置后 `Empty` 且 `Revision >= 1`；
  ⑤ 再调一次 ⇒ `journal_reset_not_needed`；⑥ **重置后再写一条世界事实必须成功读回 1 条**
  （证明重置真的解开了写侧那道「corrupt ⇒ 放弃」的死锁）。
- 用例注册 `AWAKE.Tests/Program.cs:142`，插在 `native-readiness-too-early` 之后、
  `dialogue-chain-redtest` 之前（后者会重置 UI 调度线程绑定，必须保持最后）。**用例数 69 → 70**。

---

## 三 判据红绿

先写判据、**取红**，再实现、取绿（红必须红在对的理由上）。

### 红（改动前 / 只加判据）

```
FAIL_MSG malformed journal root must report awake.world_fact.root_json_invalid, got=awake.world_fact.root_corrupt
FAIL_MSG a malformed journal root must surface the codec sub-cause, got=awake.world_fact.root_corrupt
FAIL_MSG a malformed journal root must read as corrupt with its own sub-cause, got=Corrupt code=awake.world_fact.root_corrupt
RESULT total=70 passed=67 failed=3
FAILED_CASES world-fact-journal,world-fact-journal-recovery,world-fact-journal-reset
```

三条失败全部指向同一件事：**子原因被塌缩成 `root_corrupt`**。`world-fact-journal-roundtrip` 仍 PASS ⇒ 无回归。
证据：`%TEMP%\dsh-gli9Jz\d1-evidence\p2_build_red.txt`、`p2_smoke_red.txt`。

### 绿（实现后）

```
PASS world fact journal smoke
PASS world fact journal roundtrip smoke
PASS world fact journal recovery smoke
PASS world fact journal reset smoke
RESULT total=70 passed=70 failed=0
PASS ALL Awake.SdkSmoke
```

- 离线：`dotnet build AWAKE.Tests\AWAKE.Tests.csproj -c Release -m:1 -nodeReuse:false` ⇒ 0 错误 / 6 警告
  （4 条既有 `CS4014` + 2 条 NU1900 离线取漏洞数据失败）；`Awake.SdkSmoke.exe` ⇒ exit 0。
  证据：`d1-evidence\p3_build_green.txt`、`p3_smoke_green.txt`、`final_build.txt`、`final_smoke.txt`。
- 主工程 **E1**：`MSBuild.exe AWAKE.csproj /restore /t:Rebuild /p:Configuration=Release /p:BannerlordApi=1.4.8 …`
  ⇒ **exit 0 / 0 错误**，`AWAKE -> _build_out\1.4.8\Release\Awake.dll`（1184256 B）。
  这一步**不可省**：`AwakeDeveloperTestActions.cs` 与 `AwakeTerminalBehavior.cs` **不在** `AWAKE.Tests` 的显式编译清单里，
  dev 菜单那 12 行只有主工程能证明编得过。证据：`d1-evidence\e1_main_build.txt`、`final_main_build.txt`。

---

## 四 变异检验（证明新判据不是摆设）

每条都故意破坏新判据保护的东西，取红后**逐字还原**；`grep MUTATION-TEMP` 在 `AWAKE/src` + `AWAKE.Tests` 最终零命中。

| 变异 | 破坏点 | 结果 |
|---|---|---|
| M1 | `ReadRoot` catch 退回粗码 `root_corrupt` | `passed=67 failed=3`，三条 FAIL_MSG 即 §三 那三条 ⇒ 子原因判据会响 |
| M2 | 检查次序打乱（bounds 提到 schema 前） | `passed=68 failed=2`：`…must report awake.world_fact.root_schema_mismatch, got=awake.world_fact.root_bounds_invalid` ⇒ **次序契约真的被钉住** |
| M3 | 去掉「健康账本不动手」的早退 | `passed=69 failed=1`：`a healthy journal must not be reset, got ok=True value=awake.world_fact.root_corrupt` |
| M4 | 跳过隔离，直接覆盖 root | `passed=69 failed=1`：`reset must quarantine the corrupt root before replacing it.` ⇒ **"重置不许变成删数据"真的被钉住** |

证据：`d1-evidence\mut1.txt` … `mut4.txt`。

---

## 五 行为变更与代价

- **写侧零变化**：`AppendWorldFactJournalAsync` 与 09-15 的隔离自愈逻辑一个字没改。
- **读路径**：只有**真坏**时多一行日志（`world_fact_journal_root_corrupt code=…`），健康路径零新增。
- **返回值**：坏账本的 `ErrorCode` 由粗码变具体码 —— 这是**有意的行为变更**，也是本批的目的；
  既有消费方（`WorldFactQuery.MapStatus`、`WeeklyReportService` 的状态门）都按 `Status` 分支，不看码值，故不受影响。
- **新入口**：开发者菜单多一项。`AWAKE/src/AwakeConfig.cs:222` `EnableDeveloperMenu` 默认 false，玩家侧无变化。

## 六 MCM 评估：**不进 MCM**

按计划 §4.6（`:261-262`）：缺陷① 的「重置 journal」是**开发者调试入口**，不是玩家选项 ⇒ 走既有 dev 菜单
（与 `worldbook_reload` 同类），**不新增玩家可见 MCM 项**，也不与玩家菜单混排。
新键 `awake.dev_tools.world_fact_journal_reset{,_hint}` 沿用既有 `awake.dev_tools.*` 惯例（仓库 json/xml/csv 里查不到这些键，
`AwakeLocalization.Resolve` 的 fallback 就是实际文案），**不动语言表**。

---

## 七 未完成 / 残留风险

1. **E4 未取**：没有投送 DLL、没有启动游戏。`ResetWorldFactJournalAsync` 的真机行为（真后端
   `AwakeFileStorageService` 依赖 `AwakeUiDispatcher` 与 `AwakeModulePaths`，离线套件用不了真件）
   靠"读真件代码 + 判据注明出处"保证同源，**不是跑出来的**。
2. **周报链不会自动恢复**：本批给的是**显式**入口，不是自愈。计划 §4.5 的验收是
   「能自愈（`status=available`）**或**至少给出具体子原因码」——本批**同时满足后半句，并用重置动作补上前半句的手动路径**。
   若要把"自愈"做成自动的，需要**另开一条权限预检过的 Command 路径**（见 §一 两条理由），
   并在写侧那道门上做排队而不是直接抢 —— 属独立批次。
3. **`root_corrupt` 仍在**：codec 给出空诊断码时它仍是兜底值。真机若再看到它，说明坏法没被 codec 分类覆盖。
4. **dev 菜单接线只有 E1**：`AwakeDeveloperTestActions.cs` / `AwakeTerminalBehavior.cs` 不在离线清单里，
   那 12 行的运行时正确性（尤其 `AwakeBackgroundTask` + `AwakeUiDispatcher.Enqueue` 的线程跳转）只能等 E4。
5. **姊妹发现（未修，建议另案）**：`AWAKE/src/AwakePlayerSnapshotProvider.cs:97-101` 把 `Hero.MainHero` getter
   自身抛的 NRE 记成 `game_data.player_snapshot_error`，而不是 `player_unavailable` —— 与缺陷② 同源，
   离线判据 `AWAKE.Tests/DialogueChainRedtest.cs:36-45` 只断言"两码之一"，所以打不到。
6. **文档同步待办**：`docs/AWAKE-ROADMAP.md` §4.3 的四个真缺陷清单归全局主控线维护，本批**未改**；
   本报告与 `docs/PLAN-REPAIR-WORLDBOOK-HASH-AND-FOUR-DEFECTS-20261001.md` §4.2 是现状依据。
7. **产物指纹**：`Awake.dll` 1184256 B、sha256 `F1F281558B9C0E078A1628FECDDB3E0BACFE9A7EA2FFC47BB36B8DBE528EC489`。
   项目 `<Deterministic>true</Deterministic>`（`AWAKE/AWAKE.csproj:14`），但 `/t:Rebuild` 会重新嵌入 framework 运行时，
   **跨次构建的 DLL 哈希可能不同** —— 稳定的身份是源码指纹：
   `WorldFactJournal.cs` 8ABEE78A…、`WorldStateStore.cs` 93D7311D…、`AwakeDeveloperTestActions.cs` DC10B406…、
   `AwakeTerminalBehavior.cs` 617CC9F1…、`Program.cs` 95C37283…。

## 八 回滚

改动是**加法 + 一个码值**，回滚＝还原这 5 个文件：

```
git checkout -- AWAKE/src/WorldFactJournal.cs AWAKE/src/WorldStateStore.cs \
                AWAKE/src/AwakeDeveloperTestActions.cs AWAKE/src/AwakeTerminalBehavior.cs
```

判据侧回滚需连带去掉 `AWAKE.Tests/Program.cs` 的 `world-fact-journal-reset` 用例与两处扩写
（否则 70/70 会变红）。注意 `AWAKE/src/WorldStateStore.cs` 同时承载缺陷②④ 与本批改动，逐文件回滚会一并丢掉前面两批。

---

## 环境备忘（本会话恒定，非仓库缺陷）

- 沙箱**不能执行 `.ps1`**（`AuthorizationManager 检查失败` / `PSSecurityException`）⇒ 直接调 `MSBuild.exe`。
- 沙箱**不能写 `D:\AWAKE-Dev` 之外路径**（需一次性 `danger-full-access`）。
- `dotnet build` **必须加 `-m:1 -nodeReuse:false`**，否则以「生成失败 / 0 个警告 / 0 个错误」约 1.5 s 退出 1
  （MSBuild 多进程走命名管道被沙箱挡），且会**掩盖真因**。
- 引用行号一律以 `Select-String` / `[System.IO.File]::ReadAllLines()` 为准
  （`Get-Content | Select-Object -Skip N` 实测偏差约 10 行）。
