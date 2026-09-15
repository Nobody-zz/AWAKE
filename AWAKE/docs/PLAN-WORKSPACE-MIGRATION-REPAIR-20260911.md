# AWAKE 工作区迁移修复计划（2026-09-11）

## 目标与边界

本批次恢复 `D:\AWAKE-Dev` 作为唯一权威工作区后的本地可构建、可测试、可运行工具链状态，并让当前状态文档能准确描述实际候选与证据。

不包含：运行时功能改动、世界书/内容包工作、游戏目录同步、版本号变更、删除既有忽略产物、重写历史证据或历史计划。

## 已确认事实

- `AWAKE\tools\build.ps1` 与 `Awake.SdkSmoke.exe` 已可从新根目录执行，主 Smoke 为 `PASS ALL`。
- 主 Smoke 的旧 `_houkai_merge\AWAKE` 路径已修复；仍有 persona 映射、release-check、G3 验证、Persona Workbench、Worldbook Studio 工具含旧根路径。
- `AWAKE-CURRENT.md` 的历史段落记录候选 `009` 已同步，但当前源码已是 `awake-20260911-dialogue-chain-010`；`AWAKE-VALIDATION.md` 仍以 `004` 为当前候选，二者均不能直接作为迁移后 010 的同步证据。
- 已忽略 `.packages`，且本地包已撤出 Git 索引；文件保留供本机恢复使用。

## 修复批次

### M1：统一可执行工具的根路径解析

**范围：**

- `AWAKE\tools\release_check.ps1`
- `AWAKE\tools\build_persona_*.ps1`
- `AWAKE\tools\persona-awake-joint\verify-g3-*.ps1`
- `AWAKE\tools\persona-workbench\tests\PersonaWorkbench.Core.Tests\Program.cs`
- `AWAKE\tools\worldbook-studio\scripts\real-worker-pravend-smoke.ps1`
- `AWAKE\tools\worldbook-studio\scripts\pravend-expected-facts-check.ps1`

**契约：** 从任意工作目录调用时，工具必须以脚本/项目自身位置推导 `D:\AWAKE-Dev` 根，而不是嵌入旧 OneDrive 或 `_houkai_merge` 绝对路径。可选的外部游戏输入仍保持显式参数；工具不得静默读取旧工作区。

**验收：**

| 用例 | 可观察结果 | 证据 |
| --- | --- | --- |
| 新根定位 | 每个工具能解析到当前 `AWAKE` 或仓库根 | 对应工具的 `-WhatIf`、只读/验证模式或聚焦测试 |
| 旧根隔离 | 源码中无作为默认可执行路径的旧 OneDrive 根 | `rg` 静态扫描；历史报告/fixture 明确排除 |
| 外部输入缺失 | 需要游戏、真实 Worker 或外部资料的工具清晰失败，不回退旧目录 | 参数校验或现有失败码 |
| 主回归 | 现有 `Awake.SdkSmoke.exe` 不退化 | `PASS ALL` |

**非目标：** 不改写历史 JSON/Markdown 报告中的绝对路径；它们是当时证据，不是当前执行入口。

### M2：状态与运行文档收敛

**范围：** `AWAKE-CURRENT.md`、`AWAKE-VALIDATION.md`、根 README、AWAKE 中英文 README、必要的构建/发布说明。

**契约：** 一处只记录一个“当前候选”；候选 BuildId、E1/E2/E3 状态、同步证据和待办必须相互一致。无法从本地 artifact/log 重新确认的 E3/E4/E5 声明应降为“历史记录待核”，不能继承为当前事实。

**验收：**

| 用例 | 可观察结果 | 证据 |
| --- | --- | --- |
| 候选一致 | `CURRENT` 与 `VALIDATION` 指向同一 BuildId 或明确写出无当前候选 | 两文档交叉检查 |
| 证据真实 | 每个 E3+ 主张链接到存在的 sync/evidence 文件和匹配哈希 | 文件存在、哈希/BuildId 比对 |
| 新工作区可复现 | README 命令都从 `D:\AWAKE-Dev` 出发 | 命令静态检查；构建命令实跑 |

**非目标：** 不伪造游戏内 E4/E5；不把历史 004/005/009 证据删除。

### M3：Git 与本地产物卫生

**范围：** `.gitignore`、暂存区清单、迁移期生成物的分类说明。

**契约：** 可再生构建输出、包缓存和本地运行状态不进入索引；源码、契约、计划和必要 fixture 保持可追踪。既有忽略目录只分类，不自动删除。

**验收：**

| 用例 | 可观察结果 | 证据 |
| --- | --- | --- |
| 缓存隔离 | `.packages`、`bin`、`obj`、`_build_out` 不出现在待提交新增文件中 | `git status --ignored` |
| 变更可解释 | 每个暂存文件可归属到迁移或既有功能批次 | `git diff --cached --name-status` 审核 |
| 无误删 | 忽略目录仍存在且未执行删除命令 | Git 状态与操作记录 |

## 执行顺序与停止条件

1. 执行 M1，逐组修复并运行各自最低成本的只读门禁；验证：主 Smoke 保持通过。
2. 执行 M2，先从 BuildId、sync 报告、证据文件建立事实表，再最小化更新两份权威状态文档；验证：候选与证据表无冲突。
3. 执行 M3，审核并分组暂存迁移改动；验证：缓存不再被跟踪、没有无关删除。

停止条件：若某工具的旧路径是测试 fixture 的断言数据、若现有 E3/E4 证据缺失或哈希不匹配、或若修复需要同步游戏目录，则停止该批并记录为阻断项，不替代执行或重新同步。

## 低推理执行器操作卡（Luna）

### M0：每批开始前的固定检查

执行器先记录以下只读基线，**不得** `git add`、`git commit`、`git reset`、`git restore`、`git clean`、`git rm`，也不得运行 `sync_module.ps1`、`package_embedded_runtime.ps1` 或启动 Bannerlord：

```powershell
git status --short
git diff --name-only
git diff --cached --name-only
```

每一个子任务只可改本计划列出的目标文件。改完后重复上述三条命令；若出现计划外文件、暂存区内容变化、删除项或二进制新增项，立刻停止，不自行清理、暂存或覆盖。

### M1-A：脚本默认输出根

**目标文件：** `build_persona_family_mapping.ps1`、`build_persona_game_mapping.ps1`、`build_persona_reference_audit.ps1`、`build_war_sails_reference_mapping.ps1`、`release_check.ps1`。

**唯一允许的改动：** 仅将“本仓库内的默认输入/输出路径”改为从 `$PSScriptRoot` 推导的仓库根；保留参数名、参数类型、外部游戏安装路径和调用方可覆写参数。

**禁止：**

- 不把 `D:\AWAKE-Dev` 写死；脚本必须可随仓库移动。
- 不改 `PersonaDirectory` 这类指向游戏或外部导出资料的输入默认值；它们不是本仓库迁移路径。
- 不执行会生成/覆盖 mappings 或 release 文件的实际任务；只运行帮助、语法或明确的只读检查。

**逐文件验收：** `rg` 证明旧 OneDrive 根不再作为默认仓库路径；PowerShell 解析脚本无语法错误；缺少外部输入时应报参数/文件缺失，不能回退旧目录。

### M1-B：测试根发现

**目标文件：** `PersonaWorkbench.Core.Tests\Program.cs`，以及仍含旧路径且会被测试运行读取的 C# 测试源。

**唯一允许的改动：** 改为从 `AppContext.BaseDirectory` 向上查找同时包含 `AWAKE\` 与测试项目标记的仓库根；测试读取的相对路径必须从该根组合。

**禁止：** 不修改测试预期、golden 内容、生产代码或测试注册顺序来让测试变绿。

**验收：** 先构建目标测试项目；再运行该项目最小的现有 fixture 用例。若没有无副作用的聚焦入口，只记录“可编译、全量执行需另行授权”，不臆造测试命令。

### M1-C：G3 路径契约与历史证据分离

**目标文件：** `persona-awake-joint\verify-g3-plan.ps1`、`verify-g3-s0-scope.ps1`、`verify-g3-s0-focused-evidence.ps1` 及其直接调用的路径解析函数。

这些文件中的 `_houkai_merge/AWAKE/...` 既可能是可执行文件定位，也可能是旧 `write_set` / evidence 的字面量。**禁止机械全局替换。**

执行器必须先逐处标记为以下三类，未能归类即停止：

| 类别 | 处理 |
| --- | --- |
| 运行时文件访问 | 改为从当前仓库根组合路径 |
| 当前批次的 path contract / expected source path | 与新仓库相对路径一并更新，并更新对应的断言 |
| 已生成历史 report / hash / commandLine | 保留原文，不改写 |

验收只允许运行该验证器的只读模式。若它会写 `artifacts/`，须传入系统临时目录的显式 `-ReportPath`；若无此参数或会覆盖仓库证据，停止并申请独立的验证方案。

### M1-D：Worldbook Studio 的真实 Worker 脚本

**目标文件：** `real-worker-pravend-smoke.ps1`、`pravend-expected-facts-check.ps1`。

它们可能读取真实资料或调用真实 Worker，因此默认只做静态路径修复与 PowerShell 解析，**不运行 smoke**。路径应由脚本根推导；任何真实 Worker、网络、外部资料写入或证据生成都属于后续单独授权。

### M2：候选真相表先于文档编辑

在编辑 `AWAKE-CURRENT.md` 或 `AWAKE-VALIDATION.md` 前，创建一个仅本批使用的事实表，逐项验证：

| 字段 | 唯一取值来源 |
| --- | --- |
| 当前源码 BuildId | `AwakeVersion.BuildId` 的源码与本地构建产物；当前为 `awake-20260911-dialogue-chain-010` |
| source DLL SHA-256 | 当前 `_build_out` 中 `Awake.dll` |
| dist/game SHA-256 | 已存在 sync report 与目标文件；不可访问则标记 `unverified` |
| E3 | 同一 BuildId 的 source/dist/game 与 manifest 均匹配 |
| E4/E5 | 同一 BuildId 的用户提供日志/证据文件 |

**决策规则：** 当前 010 的 source、dist、game 或日志不能由本机复核的部分必须标记 `unverified`；009 和 004 只能写为历史候选，不能倒填为 010。不得通过复制旧段落、猜测哈希或更新日期来解决冲突。

### 故障处置与交接格式

Luna 每个子任务仅报告以下字段，之后停止等待上游处理：

```text
task_id:
target_files:
baseline_diff_unchanged: true|false
command:
exit_code:
expected_observation:
actual_observation:
new_or_changed_files:
classification: pass | blocked_external_input | blocked_contract_ambiguity | failed_regression
next_safe_action:
```

`blocked_contract_ambiguity`、计划外 diff、测试失败、需要外部输入、需要写生成证据、需要同步游戏，均不是 Luna 自主重试或“顺手修复”的授权。

## 最终完成标准

- 所有可执行默认路径均指向 `D:\AWAKE-Dev` 或由脚本位置推导。
- 主构建与 `Awake.SdkSmoke.exe` 通过。
- 当前候选与 E0–E5 证据在 `AWAKE-CURRENT.md` / `AWAKE-VALIDATION.md` 中无冲突。
- Git 索引不含可再生缓存或构建产物。
- 不发生游戏目录同步、发布或历史证据删除。

## 执行记录（2026-09-11）

- M1-A：已完成。`release_check.ps1`、四个 persona mapping/audit 脚本已移除旧仓库默认路径，改为从脚本位置推导；外部游戏/资料输入保持原参数语义。
- M1-B：已完成。Persona Workbench Core Tests 的 shared golden fixture 改为从当前仓库结构发现；目标测试通过。
- M1-D：已完成静态修复。两个 Pravend/真实 Worker 脚本已改为从脚本位置推导内部路径；未执行真实 Worker、网络或证据生成。
- M1-C：根定位和兼容解析完成。`persona-awake-joint.ps1` 现在把 `D:\AWAKE-Dev` 解析为 workspace root，并把 v1 scope 中的 `_houkai_merge/AWAKE/...` 与 `_houkai_merge/AWAKE.Tests/...` 仅作为逻辑契约别名映射到当前物理目录；Smoke 证据生成器同步采用相同的物理解析。已通过根路径、别名路径、PowerShell 解析、G3 总计划验证、主 Smoke 和 G3 聚焦证据校验。`verify-g3-plan.ps1` 与 `verify-g3-s0-focused-evidence.ps1` 当前均为 `pass/0`；`verify-g3-s0-scope.ps1` 按预期返回 `blocked/20`（`persona.g3_s0_scope_pending`，原因是历史 lease 已 `released`，而非路径错误）。v1 scope/approval/lease/evidence 原始字节不改，因此不需要伪造重签。
- M2：已完成重基线。当前源码为 BuildId `awake-20260911-dialogue-chain-010`，当前本地证据为 E1/E2；E3/E4/E5 标记为未核验。
- M3：已完成本地索引卫生修复。`.packages` 已忽略，五个本地包已从 Git 索引撤出但未删除。
- G3-S0：已按用户授权建立唯一 active lease 并完成现有 readiness 实现复核；`verify-g3-s0-scope.ps1`、构建、主 Smoke、`verify-g3-s0-focused-evidence.ps1` 均通过。lease `g3-s0-20260911-072513` 已在批次完成后释放。
