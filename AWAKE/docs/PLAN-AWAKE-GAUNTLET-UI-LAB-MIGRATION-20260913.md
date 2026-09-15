# AWAKE Gauntlet UI Lab 资产迁移与实现计划（修订版 R2）

- 批次：`AWAKE-UI-LAB-ASSET-MIGRATION-20260913`
- 状态：`plan_revised_r2`（R1 为 `asset_import_in_progress`）
- 修订触发：独立复审结论 `REVISE`（2026-09-13），要求补齐宿主契约、E2 生命周期测试、导入校验器
- 证据上限：本地 `E2`；游戏内仍为 `E4`，必须由用户实机验收
- 门槛：本修订版通过只读复审并由用户签收后，才允许进入游戏内 F10 壳实现

## 目的

将旧工作区已经验证过的、与具体模组无关的 Gauntlet UI Lab 资产，纳入 AWAKE 的工具链。最终用途是为 AWAKE 的对话确认、长文本、空状态和列表/详情交互提供隔离的 UI 夹具，减少把每一次界面修改都直接放进战役实测的次数。

本批次只做资产登记、导入，以及**本地（离线）夹具骨架与生命周期契约**；不改变 `Awake.dll`、游戏目录、启动器、存档、MCM 或现有 NPC 对话入口。

## 复审整改对照

| 复审要求 | 落点 | 交付物 |
| --- | --- | --- |
| ① 宿主 movie/owner ID、重复打开合并、关闭/重开、界面切换、部分加载失败、卸载清理契约 | §1 | 计划契约 + `src/Awake.UiLab.Core/UiLabHostSession.cs` |
| ② 新写 E2 生命周期测试：重复打开、关闭前打开、界面切换、部分加载失败、关闭重开，验证 movie/layer/焦点/输入限制全释放 | §2 | `tests/Awake.UiLab.Tests/`（E2） |
| ③ `verify-import.ps1`：递归白名单、拒绝源码/二进制/包/旧命名、记录历史来源哈希 | §3 | `verify-import.ps1` + `import-manifest.v1.json` |

## 已确认事实

- 旧宿主为独立的 `GauntletUILabHost`，只依赖 Bannerlord 原生程序集，目标游戏版本为 `v1.3.15.110062`。
- 旧宿主源码 `GauntletUiLabHostAdapter.cs` 声明的 `MovieId = "GauntletUILab"`、`OwnerId = "GauntletUILabHost"`、`CandidateId = "GauntletUILabHost-runtime-v1"`。
- 已部署游戏目录的宿主 DLL 与旧工作区宿主 DLL 的 SHA-256 一致：`9E54A9A9ACAB1FC15AC2C9A9A4E1F249BB890FCA1EB174670155E24D2C30EF4C`。
- 旧工具已获得离线 E2 生命周期夹具证据（`state/validation/ui-lab-owner-lifecycle-20260831.json`，`status=passed`）；旧的 Host/生产插件共同入口 E4 修复批次仍为 `revising`，不视为可迁移完成结论。
- 当前 AWAKE 已有自己的 `NpcDialogueOverlay`、`NpcDialogueVM` 与 `NpcDialogue.xml`；它们不被本批次修改。
- AWAKE 生产入口的实际口径（读 `src/NpcDialogueOverlay.cs`）：`new GauntletLayer("NpcDialogue", 541, false)` → `_layer.LoadMovie("NpcDialogue", vm)` → `Screen.AddLayer` → `InputRestrictions.SetInputRestrictions(true, InputUsageMask.All)` → `IsFocusLayer = true` → `ScreenManager.TrySetFocus`；关闭为逆向五步。本计划的 Lab 契约与该口径逐条对齐，但**不得复用 `NpcDialogue` 这一 movie/layer 名**。

## 资产范围

导入：

1. 独立 Lab 的通用 Prefab 基线，用于结构、长文本、空列表、列表/详情兄弟层级和返回按钮位置的离线检查。
2. 固定夹具状态的语义清单与历史验证证据的来源哈希。
3. 宿主构建所需的 Bannerlord 原生程序集清单，作为后续 AWAKE 自有宿主的参考。

明确排除：

- 旧模组的生产适配器、MCM 入口、Profile、内容、配置、发布包、游戏包、截图与任何业务数据。
- 旧 C# 实现代码。后续宿主必须以 AWAKE 自有命名空间重新实现，并只吸收可验证的生命周期约束。
- 未获得用户实机证据的 E4/E5 结论。

## 目标边界与最小闭环

后续实现切片的闭环为：开发者跑 AWAKE 本地 UI Lab → 选择稳定 fixture（首先为“对话指令二次确认”）→ 载入只读假数据 → 可观察到确认/取消/长文本/空状态 → 关闭后释放 movie、layer、焦点和输入限制。

夹具不得调用 `NpcDialogueService`、AI、存储、世界书、命令执行器或战役状态。它只能使用专用 fixture ViewModel 和静态 DTO；因此“确认”与“取消”只更新夹具显示状态，不写入任何游戏数据。

**本地端的证据上限（必须明确）**：本地 Lab 是离线逻辑夹具，做的是 fixture 状态机、宿主生命周期契约与 Prefab 绑定一致性；它**不渲染 Gauntlet**，也不等同于 Bannerlord 真机渲染结果。渲染、输入、焦点与截图的最终判定一律为 `E4`。

## §1 AWAKE 自有宿主契约（复审第 ① 项）

### 1.1 唯一 movie / owner ID

| 名称 | 取值 | 说明 |
| --- | --- | --- |
| `MovieId` | `AwakeUiLab` | 独占；不得与 `NpcDialogue` 等任何生产 movie 重名 |
| `LayerName` | `AwakeUiLab` | 与 movie 同名，便于日志定位 |
| `OwnerId` | `AwakeUiLabHost` | 进程内唯一 owner 标识 |
| `CandidateId` | `AwakeUiLabHost-runtime-v1` | 版本化候选标识，参与 generation 栅栏 |
| `LocalOrder` | `560`（待验证：需在 E4 前与 `NpcDialogue`(541) 及其它 AWAKE layer 的排序确认） | 仅影响真机层序 |

约束：

- Lab 一律使用上表自有 ID；**禁止**借用 `NpcDialogue` 的 movie/layer 名，避免与真实对话入口抢同一 movie 缓存与焦点。
- 同一进程内同一 `MovieId` 同时只允许一个会话持有 owner。第二个会话的打开请求一律被拒（`owner_conflict`），且**不创建任何 layer 或 movie**（对应旧证据 `dual_owner_conflict` / `rejected_owner_no_ui_creation`）。
- 与旧宿主的**有意差异**：旧宿主用命名 mutex + sidecar 状态文件与旧模组的生产插件跨进程竞争 owner。AWAKE 的 Lab 没有共同入口的竞争方（生产入口仍是 `NpcDialogueOverlay`，本批次不触碰），因此不引入跨进程 lease，只保留**进程内 owner + generation 栅栏**。若后续 F10 壳需要与其它入口共用 owner，必须重新评估并补跨进程仲裁，不得默认沿用本决定。

### 1.2 会话状态机

状态：`Closed` → `Opening` → `Open` → `Closing` → `Closed`，任一步失败可进入 `Faulted`。

接口：

- `RequestOpen(source)`：请求打开。
- `RequestClose(source)`：请求关闭（用户主动/脚本）。
- `Tick()`：驱动清理、重试、屏幕切换检测与合并重开。真机由安全 tick / finalize 驱动，本地由测试驱动。
- `Unload()`：只写 close intent，不阻塞、不返回清理完成。
- `Snapshot()` / `Ownership`：返回可断言的持有台账。

请求结果码（`UiLabRequestOutcome.Code`）：
`opened` / `already_open` / `coalesced` / `closed` / `owner_conflict` / `screen_unavailable` / `movie_load_failed` / `open_exception` / `cleanup_queued` / `cleanup_incomplete` / `cleanup_retry_exhausted` / `stale_ignored`。

### 1.3 逐条契约与后置条件

| 场景 | 契约 | 结果码 | 后置条件 |
| --- | --- | --- | --- |
| 重复打开 | 已 `Open` 再请求 → 合并，不创建第二 layer/movie | `already_open` | 持有计数不变 |
| 打开途中再请求 | `Opening`/`Closing` 期间请求 → 记为**一次**重开意图，由 `Tick` 执行 | `coalesced` → `opened` | 只新增一次 layer |
| 关闭/重开 | 关闭完成后再开必须拿到**新 generation**，generation 严格递增 | `closed` → `opened` | 创建数 == 释放数 |
| 界面切换 | `Open` 期间 `TopScreen` 变化 → 自动关闭并释放全部持有 | （自动）`screen_changed` | 所有权全清 |
| 部分加载失败 | 见 1.4，任一阶段失败 → 全量回滚 | `movie_load_failed` / `open_exception` | 所有权全清、层已移除 |
| 卸载清理 | `Unload()` 只写 close intent；`Tick` 驱动清理，次序固定：失焦 → 复位输入限制 → 释放 movie → 移除 layer → finalize | `cleanup_queued` → `closed` | **全部**确认释放后才释放 owner |
| 清理中途失败 | 保留 runtime 与 owner，置 pending，`Tick` 重试至多 `MaxCleanupAttempts=3`；耗尽后 `Faulted` 且所有权保留（可见，不静默） | `cleanup_incomplete` / `cleanup_retry_exhausted` | 不静默吞错 |
| 迟到调用 | 旧 generation 的 tick / 关闭结果一律 no-op 并记账（`stale_ignored`） | `stale_ignored` | 不误释放新会话 |
| 无顶层界面 | `TopScreen` 不可用时打开 → 不创建任何持有 | `screen_unavailable` | 创建数 == 0 |

### 1.4 “部分加载失败”的判定边界

获取（acquisition）台账次序固定：

1. 取得 owner → 2. 构造 layer → 3. 把 layer 加入 screen → 4. 加载 movie → 5. 设置输入限制 → 6. 取得焦点。

失败发生在第 `k` 步，则按 `k-1 … 1` 逆序回滚已获得的持有。若任一回滚步骤自身失败，则转入 §1.3 的“清理中途失败”路径（保留 + 重试），**不得**留下半清理状态却报告成功。

## §2 E2 生命周期测试（复审第 ② 项）

- 工程：`tools/awake-ui-lab/tests/Awake.UiLab.Tests/`，`net10.0`，**无游戏依赖、无 NuGet 依赖**，`dotnet run` 即可执行。
- 夹具：`FakeUiLabPlatform` 记录创建/释放计数、持有标志与释放次序，并支持故障注入（movie 加载失败、指定清理步骤连续失败 N 次、`TopScreen` 切换）。
- 被测：`src/Awake.UiLab.Core/UiLabHostSession.cs`（本地与后续 F10 壳共用同一个会话类型与同一套 fixture DTO / 状态 ID）。

用例清单（每条都必须断言：movie / layer / 焦点 / 输入限制四项释放、创建计数 == 释放计数、generation 单调）：

| 用例 | 断言要点 |
| --- | --- |
| `duplicate_open_coalesced` | 二次打开返回 `already_open`，layer/movie 创建数仍为 1 |
| `open_while_closing_reopens_after_cleanup` | 关闭途中打开返回 `coalesced`，`Tick` 后重开成功且只新增一次 layer |
| `screen_switch_releases_all` | `Tick` 检测到屏幕变化 → 自动释放，四项持有全清 |
| `partial_load_failure_rolls_back` | movie 加载失败或输入/焦点阶段抛错 → 全量回滚，四项持有全清 |
| `close_reopen_no_leak` | 关闭再打开，创建数 == 释放数，generation 递增 |
| `unload_close_intent_only_then_tick_cleanup` | `Unload()` 不即时清理；`Tick` 后全清且 owner 在**最后**一步释放 |
| `cleanup_retry_then_exhausted` | 首次清理失败保留持有；重试成功则全清；耗尽则 `Faulted` 且持有保留 |
| `stale_generation_ignored` | 旧 generation 调用被忽略，不释放新会话持有 |
| `dual_owner_conflict` | 第二 owner 请求被拒且不创建 UI |
| `fixture_state_ids_match_json` | 代码内状态 ID 集合 == `fixtures/fixture-state-ids.v1.json` |
| `prefab_binding_parity` | 基线 Prefab 声明的绑定/命令都能被 fixture DTO 提供（无悬空绑定） |

## §3 `verify-import.ps1` 完善（复审第 ③ 项）

- **递归白名单**：从 Lab 根目录递归遍历全部文件，逐个比对白名单相对路径；出现白名单外文件即失败（`unexpected_file`）。
- **拒绝类别**：
  - 可执行/二进制：`.dll` `.pdb` `.exe` `.so` `.dylib`；
  - 归档/交付包：`.zip` `.7z` `.nupkg` `.tpac` `.rar`；
  - Bannerlord 模块入口：任意 `SubModule.xml`；
  - 旧模组命名：`GauntletUILabHost`、`AnimusForge`、`LoveHate`、`Houkai`、`MarcusAINpc`。
- **明确例外**（避免误杀）：
  - `assets/GauntletUILab.baseline.xml` 允许出现标题文本 “Gauntlet UI Lab”（它是导入基线本身）；
  - `import-manifest.v1.json` 的 provenance 字段允许出现旧绝对路径与旧宿主标识（仅作为来源记录）。
- **来源哈希**：清单中登记历史 `shared/UiLabRuntime.cs` 与 `state/validation/ui-lab-owner-lifecycle-20260831.json` 的 SHA-256，`status=reference_only`、不入库；校验器在旧根可达时重算并比对，不可达时记 `skipped_unreachable`（**不得**静默通过）。

## 设计决定

| 决定 | 采用方案 | 放弃的方案 |
| --- | --- | --- |
| 宿主身份 | `tools/awake-ui-lab` 下的独立 AWAKE 工具宿主，自有 movie/owner ID | 复用旧模组 DLL 或将 Lab 混入 `Awake.dll` |
| 复用方式 | 导入通用 Prefab/fixture 约束，以 AWAKE 命名空间重新实现 | 复制旧生产适配或旧业务代码 |
| 可测性 | 把平台（screen/layer/movie/焦点/输入）抽成接口，本地注入假实现 | 直接依赖 `GauntletLayer`/`ScreenBase`，导致本地不可跑 |
| owner 仲裁 | 进程内唯一 owner + generation 栅栏 | 跨进程 mutex/sidecar（无竞争方，不需要） |
| 首个夹具 | NPC 指令提案的确认/取消展示，不接真实结算 | 直接接通真实对话、命令和存档 |
| 证据 | 本地 XML/绑定/生命周期 E1-E2；用户实机 E4 | 将编译、截图粗评当作实机成功 |

## 验收

| 场景 | 预期 | 证据 |
| --- | --- | --- |
| 来源完整性 | 每个导入资产都指向旧绝对路径和 SHA-256 | 导入清单与 hash 检查（E0） |
| 隔离 | 导入目录不含旧模组命名、业务代码、二进制或发布包 | 递归白名单 + 静态扫描（E1） |
| 基线结构 | Prefab 保持详情与列表的兄弟层级，返回按钮不在详情滚动区 | XML 检查（E1） |
| 绑定一致 | 基线 Prefab 的绑定/命令都能被 fixture DTO 提供 | `prefab_binding_parity`（E2） |
| 后续夹具 | 确认、取消、空状态和长文本均有稳定 fixture ID，确认/拒绝只改内部状态 | `fixture_state_ids_match_json` + 状态机断言（E2） |
| 生命周期 | 打开、关闭、重复打开、界面切换、部分加载失败都不留下 layer/movie/focus/input ownership | §2 十一条用例全绿（E2）；实机仍为 E4 |

## 非目标与未覆盖（E4 前置项）

- 不修复现有 AWAKE `NpcDialogueOverlay` 的生命周期，也不替换其实现。
- 不实现真实 Gauntlet 渲染；本地 E2 **不等于** Bannerlord 真机渲染结果。
- 基线 Prefab 目前**没有确认/拒绝控件**；二次确认界面的 Prefab 需另立 AWAKE 自有 prefab（E4 前置项，本批次不产出）。
- `LocalOrder = 560` 为待验证取值。
- 不同步或覆盖游戏目录；不迁移旧 UI Lab 的资产索引、OneDrive 草稿事务、旧模组资料或历史审查状态。
- 不引入跨进程 owner 仲裁（理由见 §1.1）。

## 执行顺序

1. 导入并登记通用资产、状态清单与来源证据。（R1 完成）
2. 补齐宿主契约、E2 用例与导入校验器。（本 R2）
3. 修订版通过只读复审并由用户签收。
4. 用户明确授权后才准备测试包并进行 E4（含 F10 壳与确认/拒绝 Prefab）。

## 本地全量 fixture 驱动器（增量）

实机 E4 暂缓期间，本地端优先补全"离线可验收"能力。新增 `UiLabFixtureRunner`（Core，可复用；F10 壳将共用同一份逻辑，不复制业务逻辑）：

- `RunAll()`：遍历 `UiLabFixtureStateIds.All`（17 个登记状态），逐个造夹具并判定非空壳，返回报告（total/built/failed/逐条 entries）。
- `SimulateDecisionLifecycle(stateId)`：构造 → 确认（Confirmed）→ 拒绝（Rejected），断言全程只改夹具内部 `Decision`、不触发 Close、不碰游戏数据。
- `LongTextExceeds(stateId, minChars)`：长文本夹具详情长度边界检查。

命令入口（`tests/Awake.UiLab.Tests`）：

```
dotnet run --project tools/awake-ui-lab/tests/Awake.UiLab.Tests            # E2 生命周期（16 用例）
dotnet run --project tools/awake-ui-lab/tests/Awake.UiLab.Tests -- --drive-fixtures
  # 本地全量 fixture 验收（17 状态全绿），加 -- --evidence <path> 落 JSON
```

验收现状（E2 16/16，fixture-drive 17/17）：覆盖假数据、状态切换、确认/拒绝内部状态机、长文本边界；不含真渲染/截图（E4）。
