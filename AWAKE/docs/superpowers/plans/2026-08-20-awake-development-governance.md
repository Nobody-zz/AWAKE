# AWAKE Development Governance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` for inline execution with checkpoints. Do not use subagents unless the user separately authorizes delegation for this implementation.

**Goal:** 将 AWAKE 的开发流程重构为单一调度入口、批次内自动接续、精简状态恢复、可归因候选构建和受控 local_worker 路由。

**Architecture:** 升级现有 `bannerlord-mod-development-orchestrator` 作为唯一工作流调度层；将 `awake-task-continuity` 限定为状态恢复，将 `long-horizon-short-task-execution` 限定为已批准批次内执行，将 `local-ollama-batch-worker` 限定为显式批量初筛。AWAKE 项目新增 CURRENT/ROADMAP/VALIDATION 三类状态文件，运行时在启动日志打印 BuildId、DLL hash 短码和世界书 manifest hash；候选进入 `pending_game` 后冻结，直到新日志产生。

**Tech Stack:** Markdown Skills、PowerShell、C# net472、现有 AWAKE build/test/release scripts、SHA-256、Bannerlord 1.3.15/1.4.8。

---

## Task 1: 固化计划与迁移边界

**Files:**
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-Task-Queue-20260816.md`
- Reference: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-Development-Governance-Design-20260820.md`
- Create: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\superpowers\plans\2026-08-20-awake-development-governance.md`

- [ ] **Step 1: 将治理任务状态从 `decision_needed` 改为 `approved`**
  - 在任务队列表格中将 `GOV-20260820-1` 标为 `approved`。
  - 把下一动作改为“执行 Task 2–Task 4；每个 Task 完成后更新 checkpoint”。
  - 保留 `VAL-20260817-1` 的 `blocked_sync`，不把治理任务误写成 0.2.1 功能完成。

- [ ] **Step 2: 建立执行检查点格式**
  - 为治理任务增加独立检查点段，字段固定为：`task_id`、`batch_id`、`status`、`files_changed`、`verification`、`known_limitations`、`next_action`、`last_error`。
  - 检查点只记录治理改动，不复制旧队列全文。

- [ ] **Step 3: 验证状态修改**
  - Run: `rg -n "GOV-20260820-1|status:|next_action|VAL-20260817-1|blocked_sync" "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-Task-Queue-20260816.md"`
  - Expected: `GOV-20260820-1` 为 `approved`，`VAL-20260817-1` 仍为 `blocked_sync`，无第二个 `in_progress`。

## Task 2: 升级唯一调度 Skill

**Files:**
- Modify: `C:\Users\26811\.codex\skills\bannerlord-mod-development-orchestrator\SKILL.md`
- Reference: `C:\Users\26811\.codex\skills\long-horizon-short-task-execution\SKILL.md`
- Reference: `C:\Users\26811\.codex\skills\awake-task-continuity\SKILL.md`
- Test: deterministic text audit using PowerShell `rg` and frontmatter checks.

- [ ] **Step 1: 定义调度权威和执行租约**
  - 在 `Purpose`/`Start Once` 后加入唯一调度规则：该 Skill 决定任务分类、专项 Skill、执行租约、停止条件和最终证据分层。
  - 明确用户批准一个批次后，租约只覆盖该批准批次内的可逆、非高风险短任务。
  - 明确不得自动跨越版本、批次、真机门禁或不可逆操作。

- [ ] **Step 2: 定义用户交互门槛**
  - 将常规进度通知与需要用户决策分开。
  - 明确同一执行租约内不因短任务完成而要求用户回复“继续”。
  - 只有产品取舍、不可逆修改、API Key、游戏启动、游戏目录同步、外部状态不确定、429/取消等情况才停止等待用户。

- [ ] **Step 3: 定义路由顺序**
  - 明确确定性脚本优先于 AI。
  - 明确少量语义审查由云端直接完成。
  - 明确 local_worker 只用于显式批量机械初筛。
  - 明确本地结果不得直接修改、发布、同步或批准。

- [ ] **Step 4: 保留现有 Bannerlord 硬门禁**
  - 不删除现有的 AGENTS、Gauntlet、构建、同步、游戏内验证和安全规则。
  - 只删除与连续性、工具路由、重复确认冲突的通用段落，改为引用本调度 Skill。

- [ ] **Step 5: 验证 Skill 文本**
  - Run: `Get-Content -LiteralPath "C:\Users\26811\.codex\skills\bannerlord-mod-development-orchestrator\SKILL.md" -Raw`
  - Run: `rg -n "唯一|Execution Lease|执行租约|不需要用户回应|local_worker|429|turn_aborted|游戏目录|BuildId" "C:\Users\26811\.codex\skills\bannerlord-mod-development-orchestrator\SKILL.md"`
  - Expected: 调度、租约、Worker 路由、停止条件和证据分层均有唯一表述；没有把 Worker 赋予审批权。

## Task 3: 精简连续性与短任务 Skill

**Files:**
- Modify: `C:\Users\26811\.codex\skills\awake-task-continuity\SKILL.md`
- Modify: `C:\Users\26811\.codex\skills\long-horizon-short-task-execution\SKILL.md`
- Modify: `C:\Users\26811\.codex\skills\local-ollama-batch-worker\SKILL.md`
- Reference: `C:\Users\26811\.codex\skills\abort-aware-execution\SKILL.md`

- [ ] **Step 1: 收窄 `awake-task-continuity`**
  - 保留：读取精简 CURRENT、恢复活动批次、唯一 next_action、状态迁移和历史归档。
  - 删除或改写：工具路由、逐短任务等待、构建发布细节和 Worker 规则。
  - 保留“无活动租约且有多个候选时等待选择”的规则。

- [ ] **Step 2: 修改短任务停止规则**
  - 将“短任务结束后停止”改为“短任务结束后写检查点；执行租约仍有效则自动进入同批次下一项”。
  - 仍禁止跨批次、跨版本和跨真机门禁自动推进。
  - 429、客户端取消、外部不确定和不可逆操作仍立即结束租约。

- [ ] **Step 3: 收紧 Worker 路由和熔断**
  - 增加门槛：确定性脚本可完成的工作不得调用 Worker；少于 8 条且需要上下文判断的治理/架构审查由云端直接处理。
  - 保留 `low`/`medium`/`high` 约束、单批串行、稳定 ID/hash、云端校验和“不直接修改”。
  - 明确 Worker timeout/network error 为 `in_doubt`，不自动 replay、不升级 high；需要重试时使用新 `request_id` 并保留原记录。
  - 记录本次 6 条 medium 规则审查 180 秒超时作为路由例证，但不把机器性能写成永久保证。

- [ ] **Step 4: 验证三份 Skill 的职责边界**
  - Run: `rg -n "唯一|仅负责|不再负责|执行租约|自动进入|等待用户|Worker|in_doubt|不自动重放|不能直接修改" "C:\Users\26811\.codex\skills\awake-task-continuity\SKILL.md" "C:\Users\26811\.codex\skills\long-horizon-short-task-execution\SKILL.md" "C:\Users\26811\.codex\skills\local-ollama-batch-worker\SKILL.md"`
  - Expected: continuity 不再拥有工具路由，short-task 不再逐项停顿，Worker 不拥有决策权。

## Task 4: 精简 AWAKE 项目规则

**Files:**
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\AGENTS.md`
- Reference: `C:\Users\26811\.codex\skills\bannerlord-mod-development-orchestrator\SKILL.md`
- Reference: `C:\Users\26811\.codex\skills\awake-task-continuity\SKILL.md`

- [ ] **Step 1: 保留项目硬规则**
  - 保留 AWAKE/Marcus 边界、Bannerlord 生命周期、MCM、Gauntlet、存储、AI Route、权限、内容红线、构建同步、真机验证和临时文件规则。

- [ ] **Step 2: 移除重复工作流规则**
  - 将“每轮先读全部任务队列”“继续/做只能延续旧任务”“每个功能必须单独确认”等重复描述改为：工作流由 orchestrator 调度，状态恢复由 continuity skill 处理。
  - 不删除项目要求的 grill-me、PLAN、独立审查和用户签收门禁。

- [ ] **Step 3: 增加批次冻结和证据状态原则**
  - 在项目规则中写明：一个活动实施批次、一个冻结候选；候选 pending_game 后不得继续改同一运行时 DLL。
  - 写明 E0–E5 证据等级只作为治理状态，不降低已有项目验证门槛。

- [ ] **Step 4: 验证项目规则没有丢硬门禁**
  - Run: `rg -n "grill-me|APPROVED|Bannerlord|Campaign\.Current|Gauntlet|Save|Storage|Route|Permission|SdkSmoke|SHA-256|游戏内验证|游戏正在运行|成人|18\+" "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\AGENTS.md"`
  - Expected: 项目硬规则仍存在；通用调度规则不再大段重复。

## Task 5: 创建精简状态文件

**Files:**
- Create: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-CURRENT.md`
- Create: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-ROADMAP.md`
- Create: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-VALIDATION.md`
- Create: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\checkpoints\GOV-20260820-1-checkpoint.md`
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-Task-Queue-20260816.md`

- [ ] **Step 1: 写 CURRENT 文件**
  - 只记录当前治理批次、0.2.1 blocked_sync、源码/dist/game DLL 哈希、当前版本、唯一 next_action、最近错误和游戏进程安全状态。
  - 目标长度 100–200 行。

- [ ] **Step 2: 写 ROADMAP 文件**
  - 从当前路线图提取 `0.2.1` 到 `1.0.0` 的版本目标、依赖和升级门槛。
  - 不复制逐轮日志和 Persona Workbench 历史。

- [ ] **Step 3: 写 VALIDATION 文件**
  - 建立 E0–E5 矩阵，至少覆盖：启动/读档、地图对话、transcript、记忆、关系、信件、世界书、MCM、主动对话、事件/世界效果、give_gold、承诺和发布哈希。
  - 每项记录当前候选、历史旧构建证据和缺口。

- [ ] **Step 4: 写治理 checkpoint**
  - 使用固定字段：`task_id`、`batch_id`、`status`、`files_changed`、`verification`、`known_limitations`、`next_action`、`last_error`。

- [ ] **Step 5: 验证状态文件**
  - Run: `Get-ChildItem -LiteralPath "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs" -File | Where-Object Name -in @('AWAKE-CURRENT.md','AWAKE-ROADMAP.md','AWAKE-VALIDATION.md')`
  - Run: `rg -n "task_id|batch_id|status|files_changed|verification|known_limitations|next_action|last_error|E0|E1|E2|E3|E4|E5" "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-CURRENT.md" "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-ROADMAP.md" "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-VALIDATION.md" "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\checkpoints\GOV-20260820-1-checkpoint.md"`
  - Expected: 四个文件存在，CURRENT 不包含长历史，VALIDATION 具备 E0–E5 表格。

## Task 6: 增加 BuildId 与运行时构建指纹

**Files:**
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\AwakeConstants.cs`
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\SubModule.cs`
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\src\WorldbookRuntime.cs`
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE.Tests\AWAKE.Tests.csproj`
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE.Tests\Program.cs`
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\release_check.ps1`
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\BUILD_VERIFICATION.txt`

- [ ] **Step 1: 添加构建常量**
  - 在 `AwakeConstants.cs` 增加 `BuildId` 和 `BuildFingerprint` 常量；初始值由本批次固定为 `awake-20260820-governance-001`，不要按运行时随机生成。
  - 保留 `Version`、`InformationalVersion` 和旧日志格式兼容。

- [ ] **Step 2: 添加世界书 manifest hash 读取**
  - 在 `WorldbookRuntime` 中只读 `ModuleData/Worldbook/manifest.json` 字节并计算 SHA-256；找不到或计算失败时输出短错误状态，不阻断已有世界书加载。
  - 日志只输出 hash 短码，不输出文件正文。

- [ ] **Step 3: 扩展启动日志**
  - `SubModule.OnSubModuleLoad` 的 `module_load` 和战役启动日志增加 `build_id`、`dll_hash` 短码、`worldbook_hash` 短码、`api`。
  - DLL hash 使用当前程序集路径读取；计算失败时记录 `unknown`，不得让日志失败阻断游戏。

- [ ] **Step 4: 增加纯逻辑测试**
  - 在 `AWAKE.Tests/Program.cs` 增加断言：BuildId 非空、版本/信息版本非空、hash 短码函数对固定字节输入确定、未知路径返回安全值。
  - 测试不得启动 Bannerlord、读 API Key 或写游戏目录。

- [ ] **Step 5: 扩展 release_check**
  - 检查 BuildId 常量存在且非空。
  - 检查 root/dist/game DLL 哈希一致时才报告候选同步通过；游戏目录缺失或不一致必须报告 `BLOCKED_SYNC` 而不是泛化失败。
  - 输出候选 BuildId、DLL SHA-256 和世界书 manifest hash。

- [ ] **Step 6: 窄验证**
  - Run: `dotnet run --project "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE.Tests\AWAKE.Tests.csproj" -c Release`
  - Expected: `PASS ALL Awake.SdkSmoke`。
  - Run: `dotnet build "C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\AWAKE.csproj" -c Release -p:BannerlordApi=1.3.15`
  - Expected: 0 warnings, 0 errors。

## Task 7: 构建、静态检查与候选冻结

**Files:**
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-CURRENT.md`
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-VALIDATION.md`
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\checkpoints\GOV-20260820-1-checkpoint.md`
- Reference: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\release_check.ps1`

- [ ] **Step 1: 构建 1.3.15 和 1.4.8**
  - Run the existing project build entry for each API version; do not change `SubModule.xml` version.
  - Expected: 0 warnings, 0 errors for both.

- [ ] **Step 2: Run offline gates**
  - Run `Awake.SdkSmoke`.
  - Run localization, asset boundary, worldbook placeholder and release checks.
  - Record each result separately as E1/E2; do not mark game verification.

- [ ] **Step 3: Freeze candidate without game sync**
  - Record BuildId, root/dist DLL hashes, worldbook file/ID counts and manifest hash.
  - Because the project rules require explicit authorization before overwriting the game directory, do not sync `D:\SteamLibrary\...\Modules\AWAKE` in this task unless the user separately authorizes it.
  - Set governance batch to `offline_verified`; set 0.2.1 to `blocked_sync` or `pending_game` according to actual game hash state.

- [ ] **Step 4: Verify handoff**
  - The checkpoint must state: no game launch, no game-directory overwrite, offline evidence, exact game-sync blocker, and one next action.
  - Expected: a future user-authorized sync can be performed without rereading the historical queue.

## Task 8: Final governance validation and handoff

**Files:**
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-CURRENT.md`
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-VALIDATION.md`
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\checkpoints\GOV-20260820-1-checkpoint.md`
- Modify: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\AWAKE-Task-Queue-20260816.md`

- [ ] **Step 1: Audit the rewritten Skills**
  - Confirm each Skill has valid YAML frontmatter with only supported required fields and a description that matches its actual routing.
  - Confirm no Skill tells Codex to call the local Worker for filesystem discovery or to ask the user for routine continuation.

- [ ] **Step 2: Audit state and evidence separation**
  - Confirm CURRENT is short, ROADMAP has version goals only, VALIDATION has evidence matrix, and checkpoint has one next action.
  - Confirm historical queue remains available but is no longer the sole recovery source.

- [ ] **Step 3: Update final status**
  - Mark `GOV-20260820-1` `offline_verified` only if all local files, Skill audits, tests and build checks pass.
  - Do not mark `0.2.1` `game_verified` or `done` without a new runtime log from the matching BuildId.

- [ ] **Step 4: Final report**
  - Report: files changed, skill validation, build results, Smoke results, static checks, candidate hashes, game sync status, game verification status, and exact next action.
  - Do not claim that BuildId proves gameplay; it only makes runtime evidence attributable.

## Rollback and Safety Rules

- Do not use Git rollback; this workspace is not the authoritative Git checkout and is inside OneDrive.
- Before modifying an existing Skill or project rule file, write one exact-file backup to a temporary directory with a timestamp; do not copy the entire project.
- Do not overwrite the game module while Bannerlord is running.
- Do not start Bannerlord.
- Do not store API Keys, Worker input secrets, or full prompt bodies in the governance files.
- If a tool call returns 429, timeout, `turn_aborted`, or an ambiguous external result, stop the current execution lease and write the checkpoint before any retry.