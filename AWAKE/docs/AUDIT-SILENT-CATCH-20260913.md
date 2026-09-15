# F5 空 catch 审计：62 处疑点，0 处静默数据丢失（2026-09-13）

> 代码线。**结论先行：无需改动任何代码。** 这不是"没查出来"，而是查证后有证据的判定——详见 §3、§4。
> **未改任何生产代码**，故本笔只有文档与工具。

---

## 1. 任务与判据

F5 原始描述：**空 `catch` 补日志**，且只挑**会致静默数据丢失**的那几处。

判据由此定为三条同时成立：
1. 处于**活跃路径**（非死代码、非仅测试）；
2. `catch` 吞掉异常后**无任何痕迹**（无日志、无错误码、无降级记录）；
3. 被保护的操作涉及**持久化写入 / 状态推进 / 数据解析**，吞掉 ⇒ 状态与磁盘不一致或数据静默缺失。

---

## 2. 方法：两次扫描，v1 有结构性缺陷

| 版本 | 判定方式 | 结果 |
|---|---|---|
| v1 | 任何**块体为空**的 `catch` 即报 | 62 处 |
| v2 | 把**连续 `catch` 子句合并成链**，只有**整链皆空**才报 | **58 处** |

**v1 的缺陷**：它看不见**兄弟子句**。本项目存在一个固定写法：

```csharp
try { 危险操作(); }
catch (OperationCanceledException) { }              // 取消不是错误 ⇒ 静默
catch (Exception ex) { AwakeLog.Write("..._error error=" + ex.Message); }   // 其余一律留痕
```

v1 会把第一支报成"空 catch"，**完全忽略紧跟其后的 `catch (Exception)` 已经处理了**。
v2 按链判定后，**恰好排除了 4 处**——而这 4 处正是 v1 结论里"最值得修"的那 4 个。

---

## 3. ⚠️ 必须记住的教训：工具缺陷 ≠ 结论

我（本轮）在 v1 结果基础上曾挑出 5 个"高价值目标"，**全部被后续查证推翻**：

| 原判"静默数据丢失" | 实际写法 | 判定 |
|---|---|---|
| `AwakeEventEngine.cs:126` | `catch (OCE) {}` + `catch (Exception ex){AwakeLog…}` | **已正确**，误报 |
| `AwakeEventBehavior.cs:191`（周报刷新） | 同上 | **已正确**，误报 |
| `AwakeLetterService.cs:363`（信件过期推进） | 同上 | **已正确**，误报 |
| `NpcProactiveService.cs:104`（含 `SaveAsync`） | 同上 | **已正确**，误报 |
| `NpcMemoryService.cs:507`（记忆摘要回调） | 内层 `ParseSummary` 自带 try/catch 自吞；调用方 L526 另有 `Task.WhenAny(…, Delay(30s))` 超时并记 `npc_memory_summary_timeout` | **已正确**，误报 |

**与 F3 同族的错误**：F3 是"复核命令漏了 `AWAKE.Tests` 语料"，本次是"扫描器看不见兄弟 catch"。
**两次都是把工具的盲区当成了代码的缺陷。** ⇒ 判定某处为缺陷前，**必须读完整上下文**，不能只凭扫描输出。

---

## 4. 58 条真·空 catch 链的分类（逐处读过上下文）

| 类别 | 处数 | 说明 | 是否该动 |
|---|---|---|---|
| **A. 资源清理 / Dispose** | 14 | `AiTaskGateway`×5、`PermissionGate`×3、`SceneDialoguePreview`×3、`NpcMemoryService`×2（`_cts.Cancel/Dispose`）、`KnowledgeRuntime`×1。`Dispose`/`Cancel` 抛异常无处可去，吞掉是**标准做法** | ❌ 不动 |
| **B. UI 层** | 14 | `AwakeTerminalBehavior`×6（输入状态探测：`IsAnyInquiryActive`/`IsOnScreenKeyboardActive`/`FocusedLayer`）、各 Overlay `Close()`×7、`AwakeFeedback`×1。探测失败＝当作"否"，属**兼容容忍**；且 Overlay 多属 UI 线 | ❌ 不动 |
| **C. 日志系统自身** | 5 | `AwakeLog`×3、`ProbeExtension`×2。日志写失败**不能再记日志**（递归风险），静默是**唯一正确选择** | ❌ 不动 |
| **D. 原生 API 探测容忍** | 24 | `BannerlordNativeSocialReader`×12（**该类零引用＝死代码**）、`NpcDialogueLauncher`×3、`NpcDialogueStarter`×2、`BannerlordWorldbookIdentityAdapter`×2（**跨线：世界书**）、`SubModule`×1、`AwakeUnnamedProfileService`×1、`NpcDialogueService.AddHeroSkill`×1、`NpcMemoryService`×2（等待后台任务 / 事件回调）。原生 API 在 mod 环境可能抛，取不到就留默认值是**有意设计** | ❌ 不动 |
| **E. 有意降级** | 1 | `AwakeRuntime.CurrentGameHours`：注释已写明"取不到小时粒度时**确定性降级**到 `day*24`" | ❌ 不动 |
| **合计** | **58** | 0 处命中 §1 的三条判据 | **0 处需改** |

**关键交叉验证**：58 处中**没有任何一处**包裹着"持久化写入失败被静默吞掉"。
所有涉及写盘的位置（`File.AppendAllText` 日志、`UpdateLettersAsync`、`SaveAsync`、周报刷新）**要么属 C 类（日志自身），要么有 `catch (Exception ex)` 兄弟分支留痕**。

---

## 5. 结论与建议

- **本轮不修任何代码。** 理由：58 处逐处读过上下文，无一命中"活跃路径 + 无痕 + 涉持久化"三条。
- **不建议"为了统一风格"给它们补日志**：B 类的探测多在**高频路径**（输入判定），补日志会造成刷屏，收益为负；D 类补日志同样有噪音风险，而它们本就以"取不到就跳过"为设计意图。
- **若将来确要提升可诊断性**，优先级最高的一处是 `NpcDialogueService.AddHeroSkill`（`src/NpcDialogueService.cs:823`）：它失败 ⇒ 该技能静默不进 prompt，且调用频率低（每次对话组装一次），补日志**无噪音代价**。但这属**优化**，不是缺陷修复。
- **本审计的最大价值是反面**：避免了按 v1 的错误清单去"修" 4 处**本就正确**的代码。

---

## 6. 项目模式备忘（新加入者必读）

```csharp
try { … }
catch (OperationCanceledException) { }   // ← 空，但这是有意：取消不算错误
catch (Exception ex) { AwakeLog.Write("<domain>_error error=" + ex.Message); }
```

**看到"空 catch"先看它有没有 `catch (Exception)` 兄弟分支，再下判断。**
同时：`Dispose`/`Cancel`、日志自身写盘、UI 事件，这三类的静默吞异常**都是正确的**，不要顺手"补日志"。

---

## 7. 本轮产出

- 本文件。
- 扫描器沉淀：`tools/src-audit/_audit_silent_catch_20260913.py`（按 **catch 链**判定，输出 `_result_silent_catch_20260913.txt`），供以后一键复检。
- **生产代码零改动**；主构建 / smoke / AWAKE.Tests 均未受影响（未触发重编译风险）。
