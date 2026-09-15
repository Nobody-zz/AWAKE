# 判据红测记录（防伪绿）· 2026-09-13

> **目的**：2026-09-13 为 F1 / F2 / F8 新增了三道判据与回归，但当时只验了"**修好后是绿的**"，**没有验"改坏后会不会变红"**。本记录以「注入缺陷 → 必须变红 → 精确恢复」的方式，证明三道判据对目标缺陷确实灵敏，而非伪绿（红线：能编译 ≠ 有效，绿 ≠ 有覆盖）。
>
> **执行**：模组主体（代码）线。全程只临时改动 3 个文件、每次注入后立即恢复，**无提交**。
>
> **结论**：三道判据**全部红测通过**（注入即红、恢复即绿）；另有 **2 处覆盖盲区**（见 §6），均已诚实标注。

---

## 1. 注入前基线（三件套全绿）

| 判据 | 命令 | 结果 |
|---|---|---|
| 主构建 | `cd AWAKE && dotnet build -c Release -p:BannerlordApi=1.3.15` | 0 警告 / 0 错误 |
| production smoke | `dotnet run --project tools/worldbook-runtime-production-smoke/WorldbookRuntimeProductionSmoke.csproj -c Release -p:BannerlordApi=1.3.15` | `RESULT passed=31 failed=0` |
| AWAKE.Tests 可编译 | `dotnet build ../AWAKE.Tests/AWAKE.Tests.csproj -c Release` | 0 警告 / 0 错误 |

---

## 2. 红测①：AWAKE.Tests「可编译」闸口（回归 F8）

- **注入**：`AWAKE.Tests/AWAKE.Tests.csproj:89` 取消登记 `..\AWAKE\src\AwakeLetterRequestValidation.cs`（复现 F8 原始形态：测试工程引用的源未登记）。
- **预期**：测试工程编不过。
- **实际（逐字）**：

```
D:\AWAKE-Dev\AWAKE.Tests\Program.cs(238,38): error CS0103: 当前上下文中不存在名称"AwakeLetterRequestValidator" [D:\AWAKE-Dev\AWAKE.Tests\AWAKE.Tests.csproj]
D:\AWAKE-Dev\AWAKE.Tests\Program.cs(239,38): error CS0103: 当前上下文中不存在名称"AwakeLetterRequestValidator" [D:\AWAKE-Dev\AWAKE.Tests\AWAKE.Tests.csproj]
D:\AWAKE-Dev\AWAKE.Tests\Program.cs(240,38): error CS0103: 当前上下文中不存在名称"AwakeLetterRequestValidator" [D:\AWAKE-Dev\AWAKE.Tests\AWAKE.Tests.csproj]
    0 个警告
    3 个错误
```

- **判定**：✅ 灵敏（EXIT=1）。注意实际错误码是 **CS0103**（`static class` 被当类型名引用而找不到类型），非最初预想的 CS0246。
- **恢复**：还原 `<Compile>` 行 → 构建回到 0 / 0。

---

## 3. 红测②：production smoke `unknown-state-kind-rejected`（回归 F1，两处）

用例 `ProductionSmokeTests.cs:40` 的两条回归对应 F1 的两处修复，分别注入。

### 3.1 A1 — 缺 `default` 分支（静默成功）

- **注入**：`src/WorldStateStore.cs` `TryApplyCoreAsync` 命令分发 `switch` 整块移除 `default` 分支。
- **实际（逐字）**：

```
FAIL unknown-state-kind-rejected :: InvalidOperationException:an unrouted state kind must be rejected and dropped, never applied; logs=world_state_final_drain_complete
RESULT passed=30 failed=1
```

- **读出**：失败在第一条断言（`DroppedItems==1`）；且日志里**只有 `world_state_final_drain_complete`、没有 `world_state_unknown_kind`** ⇒ 未知 Kind 未留任何痕迹、被判成功 → 正是 F1 描述的「静默丢写、无异常无日志」。✅

### 3.2 A2 — `appliedKeys` 强转（NRE → 可重试噪音）

- **注入**：把 `state["appliedKeys"] as JArray ?? Enumerable.Empty<JToken>()` 恢复为 `(JArray)state["appliedKeys"]`（迭代直接用它）。
- **实际（逐字）**：

```
FAIL unknown-state-kind-rejected :: InvalidOperationException:an unrouted state kind must be rejected and dropped, never applied; logs=world_state_apply_error command=production-smoke.unknown-kind key=unknown-kind-probe error=未将对象引用设置到对象的实例。 || world_state_final_drain_failed pending_writes=1 pending_events=0 dropped=0 code=awake.world_state.final_drain_pending
RESULT passed=30 failed=1
```

- **读出**：`error=未将对象引用设置到对象的实例`（NRE）、`dropped=0 pending_writes=1`、错误码被归为**可重试**的 `awake.world_state.final_drain_pending`、且无 `world_state_unknown_kind` ⇒ **真因（未路由 Kind）被 NRE 掩盖成"暂态故障"**，项滞留队列。与修复注释所述完全一致。✅

---

## 4. 红测③：production smoke `storage-schema-contract`（回归 F2）

- **注入**：`src/AwakeStorageContract.cs:55` 把 `awake.letters.v1` 移出 `IsKnownSchema` 白名单。
- **实际（逐字）**：

```
FAIL storage-schema-contract :: InvalidOperationException:ExpectedSchema must imply IsKnownSchema; kind=Letters schema=awake.letters.v1
RESULT passed=30 failed=1
```

- **读出**：不变式漂移被当场指出，且**报出了漂移的具体 kind 与 schema**（可定位）。✅

---

## 5. 红测④：闸口并入 `build.ps1`（本轮新增）

红测①只证明判据「灵敏」；为消除 §7-2 的盲区，本轮把 AWAKE.Tests 的编译**并入构建入口** `AWAKE/tools/build.ps1`（主构建成功之后追加一步，失败即中止）。

- **放行验证**：连续两次运行 `build.ps1` → 均 `BUILD_OK` + `TESTS_OK configuration=Release`（AWAKE.Tests 0 警告 0 错误）。
- **拦截验证（红测）**：临时取消登记 `AwakeLetterRequestValidation.cs` → `build.ps1` **真的失败**，逐字：

```
BUILD_OK api=1.3.15 configuration=Release output=D:\AWAKE-Dev\AWAKE\_build_out\1.3.15\Release\Awake.dll
D:\AWAKE-Dev\AWAKE.Tests\Program.cs(238,38): error CS0103: 当前上下文中不存在名称"AwakeLetterRequestValidator"
…（238/239/240，计 3 个错误）
CAUGHT: AWAKE.Tests build failed with exit code 1
GATE_PASSED=False
```

- **恢复后**：连续两次运行回到 `BUILD_OK` + `TESTS_OK`。
- ⇒ 闸口**既灵敏、又已接入**：F8 那类「长期编不过而无人发现」不会再无声发生。

### 5.1 过程中踩到的坑（已修，值得记）

`build.ps1` 原本是 **UTF-8 无 BOM + LF** 且**全 ASCII（无中文）**，一直正常。本轮加入**中文注释**后：

**Windows PowerShell 5.1 对无 BOM 的 `.ps1` 按系统 ANSI(GBK) 解码 → 中文注释的末字节吃掉换行 → 紧随其后的那行代码被并进注释。**

后果是**静默跳过**：表现为 `BUILD_OK` 之后**无任何输出、无报错**地中止；另一次则报
`无法将参数绑定到参数"LiteralPath"，因为该参数是空值`——因为被吞掉的正是 `$testsProject = …` 赋值行，随后 `Test-Path -LiteralPath $null` 才报错。

**同一份逻辑两次运行结果不同**（一次通过、一次失败）正是这种字节错位的典型表现。

**修法：给脚本加 UTF-8 BOM**（已加）。**规矩：任何含中文的 `.ps1` 必须带 UTF-8 BOM。**

---

## 6. 恢复核对 + 回归

- 三处注入全部还原后：`git diff --stat` 对 `WorldStateStore.cs` / `AwakeStorageContract.cs` / `AWAKE.Tests.csproj` **为空**；`git status --porcelain` 对三者为空；全仓 `REDTEST-INJECT` 标记 **0 命中**。
- 复跑三件套：主构建 **0/0** · smoke **31/31** · AWAKE.Tests **0/0**。

---

## 7. 覆盖盲区（诚实标注，不当作已完成）

1. **`AWAKE.Tests` 内的 b9 同款遍历未被红测**：该工程**顺序执行、遇错即终止**，而 persona 用例 `RunPersonaTemplateSmoke`（属**角色卡线**）先失败 ⇒ b9 **实际跑不到**。故 `AWAKE.Tests` 目前只是「**可编译**」闸口，其**运行期断言是死的**。→ 须先由角色卡线修绿 persona 用例，才谈得上红测 b9。
2. ~~闸口未接入构建链~~ → **已解决（见 §5）**：原缺口是「判据灵敏、但不在任何构建链上」（＝F8 长期潜伏的根因）。本轮已把 AWAKE.Tests 编译并入 `AWAKE/tools/build.ps1`，并红测过它确实会拦住注入的编译错。

---

## 8. 本轮改动

- 新增本文件（红测记录）。
- 修改 `AWAKE/tools/build.ps1`：主构建后追加 AWAKE.Tests 编译闸口（+10 行），并**给该脚本加 UTF-8 BOM**（见 §5.1）。
- **未改任何其它生产代码**：红测注入均已逐字还原，三文件 `git diff` 为空、全仓 `REDTEST-INJECT` 零残留。
