# AWAKE.Tests 运行期断言救活记录（2026-09-13）

> 代码线。改动文件：`AWAKE.Tests/Program.cs`。**未改任何生产代码**（`AWAKE/src/`、`AWAKE/framework/` 零改动）。

---

## 1. 问题：大半个回归网是死的

`AWAKE.Tests/Program.cs` 的 `RunAsync()` 顺序调用全部用例，断言一律用
`throw new InvalidOperationException(...)`，而 `Main` 只有**一个** `catch` ⇒

```
第一个用例失败 ⇒ 抛到 Main 的 catch ⇒ 打印 FAIL_TYPE/FAIL_MSG ⇒ return 1 ⇒ 后面全部不执行
```

实测：`RunPersonaTemplateSmoke` 排在第 **22** 位（第 94 行），其失败**连坐**了它之后的
**30 个 `RunXxx()` 用例 + 末尾的 `DialogueChainRedtest.Run()`**。

**后果**：截至本轮之前，`AWAKE.Tests` 只有前 21 个用例真正被执行过；
b9 基础设施、transcript 源、信件校验、对话链全套红测等**长期处于"从未运行"状态**。
该工程此前的价值仅等同于一个「可编译」闸口（见 `docs/AUDIT-REDTEST-GATES-20260913.md` §6）。

**这直接决定了本轮优先级**：在一个 2/3 失效的回归网下动 `src`（F5 空 catch / F7 线程亲和性）
等于裸奔，故先修网，再谈修被网覆盖的东西。

---

## 2. 改动：逐个隔离，失败记录后继续

新增三个成员（位于 `RunAsync()` 之后）：

| 成员 | 作用 |
|---|---|
| `RunCase(string, Action)` | 同步用例：执行，捕获异常 → `RecordCaseFailure` |
| `RunCaseAsync(string, Func<Task>)` | 异步用例：同上 |
| `RecordCaseFailure(string, Exception)` | 写入 `CaseFailures`（取 `InnerException ?? ex`），并打印 `CASE_FAIL <name> :: <Type>: <msg>` |

`RunAsync()` 内 **57 处**调用点（同步 48 + 异步 9）由脚本统一改为 `RunCase("RunXxx", () => RunXxx());`；
末尾两条非用例动作也一并纳入：

- `UiDispatcherReset`（`ResetGameThreadForTesting` + `Drain`）
- `DialogueChainRedtest`

收尾汇总：

```
FAILED_CASES <n> / <total>
  - <name> :: <Type>: <msg>
  ...
FAIL Awake.SdkSmoke        ← n > 0  时，return 1
PASS ALL Awake.SdkSmoke    ← n == 0 时，return 0
```

**闸口语义不变**：红了依旧是非 0 退出、依旧打印失败清单；区别只是一个失败不再吃掉其余用例。

---

## 3. 结果：59 个用例全部执行，2 个失败

改动后首次全量运行：

```
FAILED_CASES 2 / 59
  - RunPersonaTemplateSmoke :: InvalidOperationException: approved persona should generate canonical authored DSL
  - RunSharedPersonaGoldenFixtureSmoke :: InvalidOperationException: AWAKE output must match the shared canonical fixture
FAIL Awake.SdkSmoke
EXIT=1
```

**除 persona 两例外，其余 57 个用例全绿** —— 包含此前从未被执行过的：
b9 基础设施、transcript 源 + prompt heroId、contact label、信件请求校验、
messenger history、dialogue session coordinator / queue、onboarding、
content api、事件数据加载、记忆整合、主动动机注册表，以及完整的 dialogue chain 红测
（permission / player binding / prompt / storage / worldbook / worldbook deployed）。

**这是本轮最重要的产出**：一条**真实的、覆盖 src 大部分逻辑的回归基线**已建立。
在此之前，"除 persona 外是否健康"是**无人知道的**。

两个失败用例**均属角色卡线**（persona 模板 / 共享 golden fixture），代码线不越线处理。

---

## 4. 红测：0 失败路径（否则同构盲区）

新增汇总逻辑后，因永远存在那 2 个跨线失败，`PASS ALL / return 0` 分支**根本跑不到** ——
这与 §1 批评的"跑不到的代码"完全同构，必须证伪。

**注入**：临时注释掉那两行 `RunCase(...)` persona 调用。
**结果**：`PASS ALL Awake.SdkSmoke`，`EXIT=0`。✅
**恢复**：两行逐字还原，复跑确认回到 `FAILED_CASES 2 / 59` `EXIT=1`，全仓 `REDTEST-INJECT` 零残留。

---

## 5. 副作用与风险（诚实标注）

1. **continue-on-failure 可能掩盖状态污染**：某些用例共享全局状态，前置失败理论上可致后续连锁误报。
   **当前无证据**：跳过后 57 个用例全绿、无连锁失败。但若将来一次出现成片失败，
   须先甄别是"真坏"还是"被污染"，不能只看数量。
2. **用例总数由 57 变为 59**：多出的 2 个是把原先裸调用（UI reset / dialogue chain）纳入统计，
   不是新增用例。

---

## 6. 遗留：闸口仍被跨线失败绑架（待裁）

`AWAKE.Tests` 现在**能跑、能汇总**，但因那 2 个 persona 失败恒定存在，`FAILED_CASES ≥ 2` 恒成立 ⇒
**无法直接作为自动闸口**（纳进去就是永远红，等于没有闸口）。三条路，需裁决：

- **A. 等角色卡线修绿**（最干净，但代码线的运行期闸口继续空缺）。
- **B. 加 `--skip-persona` 诊断分支**：跳过 persona 系用例，输出明确标注 `SKIPPED_PERSONA=<n>`，
  让代码线能独立拿到"非 persona 全绿"信号。**风险**：多一个绕过开关，须保证输出里显式标注，
  且 `build.ps1` 闸口若采用它必须写明理由。
- **C. 保持现状**：只把 AWAKE.Tests 当**手工诊断**工具，自动闸口仍只到"可编译"
  （即 `build.ps1` 现状）。

本人倾向 **B + 明确标注**，但涉及构建判据（红线由你定），不擅自加"跳过"开关。

---

## 6.1 那 2 个跨线失败的根因已坐实（2026-09-13 晚，只读诊断，未改动其文件）

原先只是**推断**（"golden fixture 未随 DSL 生成变更同步"），现已取得证据链：

| 环节 | 事实 | 证据 |
|---|---|---|
| 生成器当前段序 | `PERSONALITY_PUBLIC` 已被**后置到末尾** | `src/PersonaDslGenerator.cs`：L223 CORE → L228 **PRIVATE** → L234 CONTRADICTION → **L250 `return Section("PERSONALITY_PUBLIC", …)`** |
| fixture 期望段序 | `[PERSONALITY_PUBLIC]` **仍在 `[PERSONALITY_PRIVATE]` 之前**（旧序） | `docs/fixtures/persona-load-v2-golden.json` 的 `expectedDsl` 字段 |
| 变更未同步 fixture | 该提交只动 2 个 src 文件 | `3705618`（"DSL 段序 PUBLIC 后置 + 默认预算 4096 改 6144"）`--stat` = `PersonaDslGenerator.cs`、`WorldbookRuntime.cs` |

⇒ `Program.cs:1268` 的 `expected != result.Dsl` 恒成立，`RunSharedPersonaGoldenFixtureSmoke` 与
`RunPersonaTemplateSmoke`（L1232 同类断言）必然失败。**与代码线改动无关**。

**建议修法**（属角色卡线）：把 fixture 的 `expectedDsl` 按新段序重排——`[PERSONALITY_PUBLIC]` 段移到
`[PERSONALITY_CONTRADICTION]` 之后；若预算常量也参与了输出，同时复核。改完这 2 例即应转绿，
**代码线运行期闸口随之自动恢复**（用户已选"等"而非加跳过开关）。

## 7. 本轮校验

| 判据 | 结果 |
|---|---|
| 主构建 `dotnet build -c Release -p:BannerlordApi=1.3.15` | 0 警告 0 错误 |
| `production smoke` | `passed=31 failed=0` |
| `AWAKE.Tests` 编译 | 0 警告 0 错误 |
| `AWAKE.Tests` 运行 | 59 例全执行，2 失败（跨线），退出码 1 |
| 0 失败路径红测 | `PASS ALL` + `EXIT=0` ✅ |
| 生产代码改动 | 无 |
