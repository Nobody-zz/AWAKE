# 离线验台全链读数（修秤后首份完整结果）

- **产出线**：模组主体（代码线）
- **时间**：2026-09-15 19:30 前后
- **读给谁**：总控（分派）／角色卡线（第 2~5 条）／世界书线（第 6 条）
- **依据**：提交 `081f1a5`（修秤）之后的实跑日志 `.workbuddy/tmp/sdk-baseline.log`（181 行，可复现）

---

## 一、秤修好了

**旧行为**：`RunAsync()` 顺序直调 55 条判据，任一判据抛异常即终止全链。
09-15 因 G3-S0 证据路径指向已不存在的旧工作区（`_houkai_merge`），全链断在第 19 条 ——
**后面 36 条判据的现状被藏了四天**，谁都不知道它们是什么状态。

**新行为**（`AWAKE.Tests/Program.cs`，提交 `081f1a5`，+158/−73）：

- 判据清单化：`BuildCaseList()` 以 `(名字, Func<Task>)` 声明式列出全部 55 条。
- 逐条独立执行：每条单独 try/catch，失败不再中断后续。
- **默认 continue-on-failure**；需要旧的"首败即停"语义时显式传 `--stop-on-first-failure`。
- 输出增量（已核实无脚本按此输出解析，纯增量）：
  - 逐条 `[序号/总数] ok|FAIL <名字>`
  - 失败时保留 `FAIL_TYPE` / `FAIL_MSG` / `INNER_MSG`，新增 `FAIL_CASE`（哪条判据）与
    `FAIL_TRACE`（逐行栈，可直接 grep）
  - 末尾 `RESULT total=N passed=M failed=K` ＋ 失败清单 `FAILED_CASES`
  - 全绿仍打 `PASS ALL Awake.SdkSmoke`
- 新增 `--list-cases` 打印判据全表。

**四项实跑验证**（全部做过，不是推断）：

| 项 | 做法 | 结果 |
|---|---|---|
| 不中断 | 基线实跑 | 第 19 条失败后第 20~55 条**照跑**，`total=55 passed=49 failed=6` |
| 绿→红逃不过 | 改坏 `build-identity` 期望 hash（变异检验） | 第 1 条立刻变红、后 54 条照跑、`FAILED_CASES` 首位点名 |
| 全绿路径真实存在 | 清单裁到 2 条 | `RESULT total=2 passed=2 failed=0` + `PASS ALL` + `EXIT=0` |
| 回退开关可用 | `--stop-on-first-failure` | 精确停在 `[19/55]`，`passed=18`，`stopped_at=g3-s0-focused-readiness` |

恢复源文件后重跑，输出与基线**逐行 diff 完全一致**（无残留）。

---

## 二、全链现状

```
[19/55] FAIL g3-s0-focused-readiness
[24/55] FAIL persona-template
[25/55] FAIL shared-persona-golden-fixture
[26/55] FAIL persona-persistence
[27/55] FAIL persona-anchor
[55/55] FAIL dialogue-chain-redtest
RESULT total=55 passed=49 failed=6
```

**其中第 24/25/26/27/55 这五条，是四天来第一次被执行。**

---

## 三、逐条取证

### 1. `g3-s0-focused-readiness`（第 19 条）— 已知，属被测实现

- 断言原文：`Missing required namespace must not reuse the stale owner.`
- 断言位置：`AWAKE.Tests/Program.cs:3463`
- 真因（此前已定位）：`AWAKE/src/AwakeRuntime.cs:872-880` 打开失败分支只回收候选、
  **不动旧持有者**；只有替换成功时才在 `:894-898` 清空。
- 判据权威（`AWAKE/tools/persona-awake-joint/verify-g3-s0-focused-evidence.ps1:138,157-158`
  要求 `existing_owner_required_missing_no_use` 且 `publishedOwnerCount=0`、`ownerUsed=false`）。
- 归属：`docs/persona-awake-joint-g3-s0-scope.v1.json` 的 `writeSet` 明列
  `AwakeRuntime.cs` / `AwakeTestFakes.cs` / `Program.cs` ⇒ **cross-line 协作件，待裁决**。

### 2. `persona-template`（第 24 条）— 角色卡线

- 断言原文：`approved persona should generate canonical authored DSL`
- 断言位置：`AWAKE.Tests/Program.cs:1231-1234`
- **诊断粒度不足**：这是一条超长 `||` 链（约 22 个条件），失败只告诉你"整条不成立"，
  **不告诉你是哪一项**。要定位必须先打印 `first.Dsl` 再逐项比对。
- 相关实现在 `AWAKE/src/PersonaDslGenerator.cs`，最近改动 `de2b9a0`（09-15 收口入账，
  上一次是 09-10）⇒ **该改动从未被这条判据验过**（此前全链断在第 19 条）。

### 3. `shared-persona-golden-fixture`（第 25 条）— 角色卡线

- 断言原文：`AWAKE output must match the shared canonical fixture`
- 断言位置：`AWAKE.Tests/Program.cs:1270`
- 判据是 `expectedDsl` 与生成结果的**全串相等**。已知背景：`PUBLIC` 段已后置，
  生成器段序改动后金标样本 `expectedDsl` 未同步。

### 4. `persona-persistence`（第 26 条）— 角色卡线

- 断言原文：`persona storage key should include branch`
- 断言位置：`AWAKE.Tests/Program.cs:1318`
- 相关实现 `AWAKE/src/PersonaPersistenceService.cs`，同样在 `de2b9a0` 收口入账。

### 5. `persona-anchor`（第 27 条）— 角色卡线

- 断言原文：`persona state namespace must not be in the default storage open list.`
- 断言位置：`AWAKE.Tests/Program.cs:1466`
- 注意：该用例内部 15 条子检查（`persona.anchor.*`）**全部 PASS**，只有最后这条额外断言失败。
- 与第 4 条同属"存储键 / 命名空间"类，**疑似同源**（待验证）。

### 6. `dialogue-chain-redtest`（第 55 条）— 世界书线，**判据对象错配**

- 断言原文：`deployed manifest schemaVersion must be awake.worldbook.v2, got=awake.worldbook.registry.v1`
- 断言位置：`AWAKE.Tests/DialogueChainRedtest.cs:221-222`
- 该用例内部 5 条子检查中前 4 条 PASS；**第 5 条（worldbook）的 pilot 段也 PASS**
  （`PASS dialogue chain worldbook redtest entries=3 identities=6`），只有**部署段**失败。

**实测两份 manifest 是两种形态，不是版本号写错**：

| 文件 | schemaVersion | 形态 | mtime |
|---|---|---|---|
| `<game>\Modules\AWAKE\ModuleData\Worldbook\manifest.json` | `awake.worldbook.registry.v1` | **注册表**（`registryId` + `packages[]`） | 09-15 16:44 |
| `AWAKE/ModuleData/Worldbook/manifest.json`（工程内部署源） | `awake.worldbook.registry.v1` | 同上 | — |
| `AWAKE/release/awake-worldbook-pilot/manifest.json` | `awake.worldbook.v2` | **单包**（`packageId` + `entrypoints` + `hashes`） | 09-11 12:11 |

⇒ 部署件与工程内的部署源**一致**（都是 registry.v1）；判据要求的 v2 是**另一个形态**（pilot 验收小样）。
`docs/worldbook-studio-plan/RUNTIME-MAPPING-CONTRACT.md` 的〈仓库侧包形态〉记的也是
`manifest.json(registry.v1)`。

**推断（待世界书线确认）**：该判据把"pilot 单包 v2"的期望套在了"registry.v1 部署件"上，
属于**判据对象错配**，而非部署件陈旧。若确认，修法是改判据（按 registry.v1 校验部署件），
不是改部署件。

---

## 四、判读注意

1. **可能有级联失败**：用例之间仍有共享可变状态（世界状态 store / UI 调度线程 / 静态注册表）。
   continue-on-failure 模式下，某条失败可能把后续带坏。**判读时先看序号最小的那条。**
2. **长 `||` 链断言不可诊断**：第 2 条就是例子。建议各线把这类断言拆成逐项、报出具体项。
3. **"从未执行"≠"最近才坏"**：第 24~27 条从 09-11 起就在链上，但全链断在第 19 条，
   所以它们**从未被跑过**，坏多久无法从日志判断。可查 `PersonaDslGenerator.cs` /
   `PersonaPersistenceService.cs` 的改动史（均在 `de2b9a0` 入账）。

---

## 五、请分派

| 条 | 归属 | 请谁定 |
|---|---|---|
| 1 `g3-s0-focused-readiness` | 跨线（writeSet 含 `AwakeRuntime.cs`） | 总控裁决归谁 |
| 2~5 persona 四条 | 角色卡线 | 角色卡线认领 |
| 6 `dialogue-chain-redtest` | 世界书线 | 世界书线确认"判据错配"的推断，并定改判据还是改部署件 |

代码线（本线）已完成的只有"修秤"本身，未触碰上述任何一条的实现。
