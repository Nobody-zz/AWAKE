# Worldbook Studio 批 1（热修 14 条）执行结果（2026-09-10）

> 批次定义见 `WORLDBOOKSTUDIO-REDTEAM-REMEDIATION-PLAN-20260910.md` 第 3 节「批 1」；发现问题证据见 `WORLDBOOKSTUDIO-REDTEAM-UX-QUALITY-20260910.md`。
> 前置：批 0 六条口径已定案（见 `WORLDBOOKSTUDIO-BATCH0-DECISIONS-20260910.md`）。
> 硬边界（全程遵守）：未启动游戏、未同步游戏目录、未访问云端 Provider / 真实 API Key / Worker / 网络服务。

## 1. 逐条结果

| 编号 | 问题 | 结论 | 主要改动位置 | 证据 |
|---|---|---|---|---|
| G1 | 生成失败原因被三层丢弃 | **已修** | `Program.cs`（新增 `AiFailureProjection`：响应增加 `detail`、PASS-* 状态映射、`SafeMessage` 分支）；`index.html`、`studio-authoring-ux.js`（优先展示 `detail`） | `Detail()` 取冒号后原因并截断 400 字符；新增 2 条 Draft.Tests 用例 |
| G5 | 输出预算默认 3000 tokens / 本地超时变量名误导 | **已修** | `AssistanceProviderContracts.cs`（默认 8000、新增 `WorkerTimeoutSeconds` 读 `WORLD_BOOK_WORKER_TIMEOUT_SECONDS`，旧变量回退）；`AuthoringDraftProviders.cs` | 逐行核对默认值；显式环境变量仍生效 |
| U1 | 首屏被 `/api/editor-catalog` 阻塞 | **已修 + 已实测** | `EntityCatalogService.cs`（进程内缓存，键 = 8 个文件的路径+时间戳+长度，仅成功才缓存）；`index.html`（移出首屏 `Promise.all`） | 打包版实测见第 2 节 |
| U2 | 右侧目录无防抖、全量重建 | **已修** | `studio-entity-catalog.js`（200 ms 防抖、每页 40、「继续加载（还有 N 个）」、选中项强制保留） | 前端 7 个 node 用例全绿 |
| U3 | 同一入口三处文案不一致 | **已修** | `studio-draft.js`（状态行/空态/第二份副本统一为「开始生成草稿」） | 逐处核对该按钮实际文案 |
| U5 | 80,000 字符上限不可见（本批并入） | **已修** | `studio-draft.js`（统计行 `已用 / 80,000 字符`、≥90% 提示、超限文案与阻断不变） | 前端用例 + 探针 |
| U6 | 0 候选时显示「1 份」 | **已修** | `studio-draft.js`（0 候选如实说明「只给出顶层内容，没有形成候选草稿」） | 前后端一致 |
| U7 | 建档按钮禁用不指出缺项 | **已修** | `studio-draft.js`（新增 `draftCreateBlockers()`，按钮旁显示「还差：…」） | 与旧 9 条件逐条等价比对 |
| U8 | 可选面板失败拖垮首屏 | **已修** | `index.html`（目录失败在 `loadEditorCatalog()` 内部消化，只报 warning 诊断，文档列表照常渲染） | 代码路径核对 |
| U9 | 错误码覆盖不足 / 端口占用提示无用 | **已修** | `Program.cs`（补齐 `WB-AI-DRAFT-*`/`WB-AI-WORKER-HANDSHAKE-*` 等实际会抛的码）；`LauncherForm.cs`（指向 `AWAKE_WB_PORT`，`WB-WEB-EXIT-001` 补明确文案） | `rg` 枚举抛码点逐个核对 |
| N4 | 本地草稿自动保存失败被静默忽略 | **已修** | `studio-local-drafts.js`（新增 `onResult` 回调）、`studio-draft.js`（`too-large`/`unavailable` 首次失败即提示，同一原因只提示一次） | 前端用例 + 探针 |
| N6 | 启动期批量恢复全量同步扫描 | **已修（改时序 + 门闩）** | `Program.cs`（恢复改为后台任务；`/api/ai/authoring/batch`、`/api/ai/batch` 前缀先 `await` 恢复再放行；失败写启动日志） | 对外行为不变；`BatchRecoveryService.cs` 未改 |
| B6 | `#toast`/`#batchNotice` 未进读屏通道 | **已修** | `index.html`（`#toast` 加 `role="status" aria-live="polite"`；`#batchNotice` 补 `aria-live`） | DOM id 未变 |
| K2 | 首次使用缺 Worker 引导（FTUE） | **已修（文档 + 界面提示）** | `README_使用说明.txt`、`新手指引_世界书内容编辑者.md`、`tools/customer-delivery/docs/本地Worker配置.md`、`studio-draft.js`、`studio-batch.js` | 见第 3 节「如实口径」 |
| D6 | 交付脚本写死开发机游戏路径 | **已修 + 已正测** | `tools/ui-workstation/UiWorkstation.Adapter.ps1`（新增 `-GameRoot` 参数 + `AWAKE_GAME_ROOT` + Steam 注册表/`libraryfolders.vdf` 自动探测 + 目录特征兜底校验 + 探测不到时明确 warning） | 见第 2 节 |

### 1.1 额外改动（同批顺手修的可验证缺陷）

- `tools/ui-workstation/test-adapter.ps1`：`$error` 是 PowerShell 只读自动变量，导致该冒烟脚本**从来没有跑通过** → 改为 `$errorPath`。修后冒烟通过（见第 2 节）。

## 2. 实测证据

### 2.1 U1 目录端点（打包版 `artifacts/current-test/WorldbookStudio`，回环 dev 模式，只读）

```text
call 1  200   422 ms  bytes=220125
call 2  200    12 ms  bytes=220125
call 3  200    10 ms  bytes=220125
call 4  200    10 ms  bytes=220125
call 5  200     9 ms  bytes=220125
documents 200   5 ms
```

对照红队原测量（同一端点、修前）：`598 / 218 / 211 / 196 / 198 ms，median = 211 ms`。
结论：**首次 422 ms（冷缓存仍要做一次读+校验+哈希）→ 后续 ~10 ms，约 20 倍**；且该请求已移出首屏关键路径。
**未降低的**：响应载荷仍是 220 KB（属另一类优化，不在本批）。

### 2.2 D6 护栏正测（临时目录，未碰游戏目录）

- 构造 `…\Modules\Native\SubModule.xml` 作为"游戏目录特征"，把 `RuntimeRoot` 指到该树内 →
  `exit=1`、`WB-WORKSTATION-GAME-403`、`runtimeDirCreated=False`（拒绝且未创建任何文件）。
- `UiWorkstation.Adapter.ps1` 冒烟：`UI ADAPTER SMOKE PASS port=58302 instance=ui-02731a4a…`。
- 路径探测：`D:\SteamLibrary\…\Mount & Blade II Bannerlord` 经 `libraryfolders.vdf` 命中（说明客户机上该检查不再是死代码）。

### 2.3 测试与打包

```text
docs / 前端（Node，7 个用例）              PASS（含 editor-session 12/12、draft-dom-state 5/5、editor-safety 4/4）
tests\Awake.WorldbookStudio.Draft.Tests    83/83 PASS（含新增 2 条 Pass 失败原因用例）
scripts\build.ps1                          0 警告 0 错误
scripts\test.ps1                           全流程跑完（node → build → 5 个 dotnet 套件 → 5 个冒烟脚本）
scripts\package.ps1                        EXIT=0，TEST: PASS，CONTRACT: PASS，PASS: release check
```

## 3. K2 的如实口径（重要）

本批核实到一条**比红队描述更严重**的事实：本机 Worker **只能通过环境变量配置**，界面**没有任何填写入口**。

- `WORLD_BOOK_LOCAL_WORKER_URL`（无默认值，必须由启动方设置）
- `WORLD_BOOK_LOCAL_WORKER_SECRET_ENV`（指向存放密钥的环境变量名）
- 依据：`AssistanceProviderContracts.cs:28,42`；`/api/ai/providers` 只处理云端 baseUrl/model/apiKey。

因此本批的文案**没有**写"在设置里填写 Worker 地址"这类不存在的操作，而是如实写明：Worker 是随包之外的外部依赖、必须由提供方在启动前配置、界面没有填写入口，并给出四条出路（确认已启动 / 确认环境变量 / 改用云端 / 不使用 AI）。

**由此新增一条待办**（建议进批 4）：本机选项在普通客户机上目前不可达——要么在启动器补一个 Worker 地址/密钥配置入口，要么把该选项显式标注为"需预先配置"并调整默认选项。

## 4. 同批新发现（进 backlog，不在本批修）

| 编号 | 发现 | 建议去处 |
|---|---|---|
| X1 | 本机 Worker 无 UI 配置入口，客户机上不可达（见第 3 节） | 批 4 |
| X2 | `WB-PORT-409-OTHER` / `WB-PORT-409-NON_STUDIO` 在 `src` 内无任何产生点（死分支）；端口占用的真实路径是 `WB-WEB-EXIT-001` | 批 5/6（清理类） |
| X3 | `AssistanceProviders.cs:398` 建议/分析链路的本地 Worker 仍复用 `CloudTimeoutSeconds` | 批 2/5 |
| X4 | `studio-batch.js` 的错误映射仍只用自己的码表，不消费服务端 `detail` | 批 5 |
| X5 | `tests/frontend` 未覆盖"目录加载中/失败"的新分阶段首屏时序 | 批 2（补用例） |

## 5. 未验证项与剩余风险

- **U1 首次调用仍是 422 ms**：缓存只解决重复调用；首次成本与载荷（220 KB）未变。
- **首屏分阶段时序仅做静态 + DOM 桩验证**，未起真实服务观察；目录 0.2–0.6 s 窗口内表单分类下拉先空后补。
- **读屏播报未实测**：`#toast` 仍靠 `display:none` 切换，部分读屏对这种切换播报不稳（要彻底稳需 CSS 常驻可见，属批 5）。
- **N6 门闩只覆盖两个 batch 路由前缀**；今后若有新端点读 `batches/`，必须同步加入门闩。
- **`SafeMessage` 文案无单测**（`Program.cs` 局部函数），为逐条读源码核对。
- **`WB-PORT-409-*` 文案改善在真机不可见**（该码不产生），实际生效的是 `WB-WEB-EXIT-001` 文案。
- 所有改动均为源码层；**未同步到客户交付包**（需重新走 `package-customer.ps1`），也**未在游戏内验证**（本批不涉及游戏侧）。

## 6. 边界声明

未启动 Bannerlord；未同步游戏目录；未访问真实云端 Provider / API Key / Worker / 网络服务；未修改真实源目录与五个世界书迁移候选；未生成新的世界书 rewrite candidate；未把任何候选标记为 approved / canon / compiled / published / runtime-ready；未修改 AWAKE 模组本体、ModuleData、dist 或冻结构建产物。临时探针脚本全部删除，未在仓库留下一次性文件。
