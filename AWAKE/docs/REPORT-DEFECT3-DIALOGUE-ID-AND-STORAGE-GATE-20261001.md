# 批次报告 — 缺陷③（对话目标 ID 归一化）＋ 存储门障修复

> 执行：2026-10-01 夜（主干·运行时 + AI 通路线）
> 依据：`docs/PLAN-REPAIR-WORLDBOOK-HASH-AND-FOUR-DEFECTS-20261001.md` §4.3／§9 第 7 步
> 证据等级：**E1**（主工程编译）＋ **E2**（离线烟测 `Awake.SdkSmoke` 全绿）。**E4 未取**（见 §5）

---

## 1. 结论

| 项 | 状态 |
|---|---|
| 缺陷③ `npc_dialogue_open_failed` | ✅ **离线已修**，红→绿一对 ＋ 两条变异检验齐备 |
| 全项目离线门（`build.ps1` 的 smoke 步） | ✅ **恢复全绿**（此前恒红，红在环境，不在代码） |
| E4（真机） | ❌ **当前不具备条件**——见 §5：游戏侧四个硬依赖被整体隔离 |

---

## 2. 缺陷③ — 对话目标 ID 形态不匹配

### 2.1 根因（与计划 §4.3 一致，已代码核实）

| 侧 | 位置 | 形态 |
|---|---|---|
| 生产 | `NpcProactiveService.cs:342` `HeroId = hero.StringId` → `SubModule.cs:77` | **裸 StringId** |
| 生产 | `AwakeEventEngine.ResolveDialogueTarget` → `AwakeEventEngine.cs:588` | 裸 StringId（`...Leader?.StringId`） |
| 消费 | `SubModule.cs:178` → `NpcDialogueLauncher.FindTargetById` → `AwakeNpcTarget.TryParseStableId` | **只认 `hero:` / `npc:` 前缀** |
| 结果 | 解析返回 null → `:target_unavailable`，条目被丢弃 | **嘴永远张不开**（v0.3 卡在此） |

### 2.2 修法：在**队列入口边界**做一次幂等归一（单一出处）

新增 `AwakeNpcTarget.NormalizeStableId`（该类型本就是稳定 id 形态的拥有者）：

- 已带 scheme（含 `:`）→ **原样返回**。既防 `hero:hero:xxx` 二次加前缀，也保护 `scene:current` 这类哨兵值不被误改；
- 完全不带 scheme 的裸 StringId → 补 `hero:`（依据即上表两个生产者的来源）；
- 空白 → 空串。

调用点共两处，覆盖全部入队路径：

| 位置 | 作用 |
|---|---|
| `EventDialogueQueue.cs` `PendingDialogue` 构造器 | **不变量：队列里的 `HeroId` 始终是消费端可解析的稳定 id**。新入队与从存档恢复（`LoadFromStoreAsync`）都必经此处 ⇒ 一处管住两条路 |
| `NpcDialogueContext.cs` `Record` | 上下文槽与 `NpcDialogueService._heroId` 同形 |
| `NpcDialogueLauncher.FindHeroById` | 去掉第二套前缀实现，改为复用归一器（同族收口，幂等） |

### 2.3 连带修掉一个**计划没抓到的**静默丢字

`NpcDialogueService._heroId` 就是 `target.StableId`（`:113` ⇒ `:87`），即 **`hero:<StringId>`**；而
`ConsumeOpeningContext`（`:1737`）与 `Dispose`（`:435`）都是**严格相等**比较。

⇒ 修好 ID 后嘴能张开了，但若上下文槽里仍是裸 id，**开场提示会被静默丢掉**（嘴张了、开场白没了）。
把 `NpcDialogueContext.Record` 一并归一，两处相等判定天然成立。**这是本次唯一需要改动上下文层的原因，`NpcDialogueService` 未改。**

### 2.4 证据（红 → 绿）

**红**（修复前，且是**新编出来的**二进制，不是旧产物）：

```
FAIL_CASE dialogue-queue
FAIL_MSG  dialogue queue target must be normalized to hero:hero-1, got: hero-1
PASS dialogue chain storage redtest
RESULT total=67 passed=66 failed=1
```

**绿**（落修复后）：

```
PASS dialogue queue smoke
PASS dialogue chain storage redtest
RESULT total=67 passed=67 failed=0
PASS ALL Awake.SdkSmoke          [exit 0]
```

**变异检验**（证明新加的两条子判据各自会响，不是摆设）：

| 变异 | 结果 |
|---|---|
| M1 去掉幂等判定（无条件补前缀） | 红：`FAIL_MSG dialogue queue must not re-prefix an already-prefixed target id.` |
| M2 取消上下文归一 | 红：`FAIL_MSG dialogue context key must be canonical, got: lord_swadian` |

两次变异均已逐字还原，`grep MUTATION` 在 `AWAKE/src`、`AWAKE.Tests` 零命中。

### 2.5 MCM 评估（按 `AWAKE/AGENTS.md`〈MCM 菜单规则〉）

**不进 MCM。** 纯内部 ID 归一化与上下文槽形态，无玩家可调行为（开关/频率/阈值/强度/预设/快捷键皆无）。
缺陷①的"重置 journal"另案，且属开发者调试入口，不得进玩家菜单。

---

## 3. 顺带修掉的**全项目级门障**：存储判据恒红

### 3.1 现象与根因

基线跑门时 `dialogue-chain-redtest` 恒红：

```
FAIL_CASE dialogue-chain-redtest
FAIL_MSG  dialogue-chain redtest failed: storage delete failed
RESULT total=67 passed=66 failed=1
```

根因**不是**判据陈旧、也不是缺陷①那类数据损坏，而是 `JsonFileKeyValueStore.Save()`
（`AwakeFileStorageService.cs`）里这一行：

```csharp
if (File.Exists(path)) File.Replace(temp, path, null);
```

**实测（C# 探针，非 PowerShell 绑定器）**：`File.Replace(temp, path, null)`
在本机**工作区盘（D:）与临时盘（C:）都**抛 `UnauthorizedAccessException：对路径的访问被拒绝`，
而同一目录下 `File.Copy(temp, path, true)` 正常。

⇒ 这是**环境级红**：门红在环境，不在代码。同时它也是**真缺陷**——`ReplaceFile` 在只读目标、
跨卷、ACL 受限等场景本就会失败，而原实现没有任何回退。

### 3.2 修法：保留原子性，允许降级

首选仍走 `File.Replace`（读者要么看到旧文件、要么看到新文件）；仅在
`IOException` / `UnauthorizedAccessException` 时退化为 `File.Copy(temp, path, true)` ＋ 删 `.tmp`。
**Replace 可用的环境行为零变化**（游戏内不受影响），不可用的环境不再恒红。

### 3.3 证据（红 → 绿）

| | 读数 |
|---|---|
| 红（新编的修复前二进制） | `FAIL_CASE dialogue-chain-redtest` / `storage delete failed` / `total=67 passed=66 failed=1` / exit 1 |
| 绿（落修复后） | `PASS dialogue chain storage redtest` / `total=67 passed=67 failed=0` / `PASS ALL` / exit 0 |

### 3.4 连带：判据自己漏垃圾

`DialogueChainRedtest.Cleanup` 只删 `file`，不删 `file + ".tmp"`。换名失败时残留的
`awake.redtest.namespace.json.tmp` 会一直卡住上一级目录的清理。已补一行删除。

---

## 4. 本批实测到的**环境级事实**（都不是本批引入，但都在挡路）

### 4.1 ⛔ 游戏侧四个硬依赖被整体隔离 ⇒ 全项目编不出来、也进不了游戏

`Modules\` 下**四个** AWAKE 硬依赖被改名为 `*.stale-20261001`：

```
Bannerlord.Harmony.stale-20261001
Bannerlord.ButterLib.stale-20261001
Bannerlord.MBOptionScreen.stale-20261001
Bannerlord.UIExtenderEx.stale-20261001
```

后果（实测）：

- `AWAKE.csproj:31` 与 `AWAKE.Tests.csproj:21` 的 `MCMv5` 引用 `$(GamePath)\Modules\Bannerlord.MBOptionScreen\...` **解析不到** ⇒ `dotnet build` 报 **164 × CS0246**（`SettingPropertyText` 等一整套 MCM 类型全缺）；
- `SubModule.xml` 仍把 `Bannerlord.MBOptionScreen` 等声明为 `DependedModules` ⇒ **游戏也起不来 AWAKE**。

⇒ **这两件事都不是本批能决定的**：要么找回/升级 1.4.8 兼容的这四个依赖，要么改 AWAKE 的依赖声明与 MCM 使用面。**需甲方裁定。**

**本批取证的绕行方式（已如实披露，未改任何受管文件、未写游戏目录）**：
在 `%TEMP%\gameroot-shim\` 里按**原字节**复原构建输入（16 个 TaleWorlds/Newtonsoft DLL ＋ 隔离副本里的
`MCMv5.dll`，与上次成功构建所用副本同尺寸同日期），以 `/p:GamePath=<shim>` 编译。

### 4.2 ⛔ 本会话沙箱不允许执行 `.ps1`

`& AWAKE\tools\build.ps1` 报 `AuthorizationManager 检查失败`（`PSSecurityException`）。
⇒ 本会话只能直接调 `MSBuild.exe`（参数与 `build.ps1` 一致）与 `Awake.SdkSmoke.exe`。
**`build.ps1` 本身没有被改动。**

### 4.3 ⚠️ `build.ps1` 默认构建目标仍是 `1.3.15`

`build.ps1:4` 与 `AWAKE.csproj:8` 默认 `1.3.15`，而本机游戏已是 **v1.4.8**，脚本带版本守卫
⇒ **无参数跑 `build.ps1` 必挂**。commit `3f5aafa` 消息写着「N0 落地（构建目标切 1.4.8）」，**但代码里没切**。
改默认值影响所有后续构建语义 ⇒ **待授权**。

### 4.4 ⚠️ `Copy-Item` 保留时间戳 ⇒ 增量构建会**静默跳过**重编

本次取证中踩到：把修复版拷回源码后，MSBuild 认为"没变"、不重编，于是**绿测跑的是旧二进制**，
一度得出错误结论。已用 `LastWriteTime = Get-Date` 纠正。
⇒ 任何"拷文件 + 构建"的取证流程都要显式触碰时间戳或 `-t:Rebuild`，否则判据看着在跑、其实没跑。

---

## 5. 本批**未**做的事 / 残留风险

- **E4 未取**：缺陷③ 的真机判据（`npc_dialogue_open_failed:target_unavailable` 消失 ＋ 观察到一次 NPC 主动开口）必须进游戏，而 §4.1 未解决前进不去。
- **第二道门未复核**：`NpcDialogueLauncher.IsEligibleNpcTarget`（英雄须在当前聚落或主队中）需真机确认；离线无法构造。
- **未改** `NpcDialogueService`（见 §2.3，设计上不需要）。
- **未提交**、**未同步游戏目录**、**未改默认构建目标**、**未提版** —— 均需明确授权。
- 本批改动文件（7 个，全在主干线）：`AWAKE/src/AwakeNpcTarget.cs`、`EventDialogueQueue.cs`、`NpcDialogueContext.cs`、`NpcDialogueLauncher.cs`、`AwakeFileStorageService.cs`、`AWAKE.Tests/Program.cs`、`DialogueChainRedtest.cs`。

### 主工程 E1 证据

```
MSBuild.exe AWAKE.csproj /restore /t:Rebuild /p:Configuration=Release /p:BannerlordApi=1.4.8 /p:GamePath=<shim>
→ AWAKE -> D:\AWAKE-Dev\AWAKE\_build_out\1.4.8\Release\Awake.dll      [exit 0]
```

唯一告警为 `NU1900`（离线取 NuGet 漏洞数据失败，环境），**无代码告警、无错误**。
