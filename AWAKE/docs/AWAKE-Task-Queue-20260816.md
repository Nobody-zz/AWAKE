# AWAKE 任务队列

> 最后整理：2026-08-20
> 当前权威：本文件只保留有效状态和推进方向；旧的逐轮记录已归档到 `docs/archive/AWAKE-Task-Queue-History-20260816-to-20260817.md`。
> 连续性规则：每轮开始重读 `awake-task-continuity/SKILL.md` 与本文件；同一时间最多一个 `in_progress`；“继续/做”只延续当前锁定任务，不自动选择新功能。
> 验证纪律：编译、SdkSmoke 和发布检查不等于游戏内通过；真机结论必须来自用户运行当前 DLL 后产生的新日志。

## 1. 当前总览

- 开发阶段：P1-P2 已批准的离线批次 C-I 已全部实现，当前没有未完成的已批准离线开发项。
- 当前锁定任务：`PREP-20260820-1` 新一轮只读预检；状态为 `decision_needed`，禁止直接同步当前冻结候选。
- 当前状态：治理批次保持 `offline_verified`；预检发现 dist 的 Persona Definition 路径多嵌套一层，发布门禁尚未覆盖该缺陷。候选 BuildId=`awake-20260820-governance-001`，源码/dist DLL=`9A9284EC...`，游戏 DLL=`FF98881E...`，因此 `0.2.1` 处于 `decision_needed` / `blocked_sync`。
- 内测门槛：Level 0 尚未放行；必须先完成本轮真机验收并处理其中的阻断反馈。
- 当前版本：AWAKE `v0.2.0`，未正式提版。
- 当前 1.3.15 DLL SHA-256：源码构建与 dist 为 `9A9284ECBF96102BC681E8DBF937C523E7A397B0CE85F1BF52531E361121FA65`；游戏目录为 `FF98881E58102E992CFFE2B959222B406165762BEA2E2E3819E1647AF8F61021`，当前不一致。
- 部署状态：`_build_out/1.3.15` 与 dist DLL 一致，游戏 `Modules/AWAKE` 落后；同时 dist 的 Persona Definition 目录布局错误，游戏中存在正确与错误嵌套两套副本。旧日志不能证明当前候选，当前不得直接同步。
- Git 状态：`AWAKE-Repo` 已同步批次 I 与路线图文档但尚未提交；未经用户明确要求不提交、不推送。
- 路线图决策：采用 AWAKE 核心 + 官方内容包双轨；1.0 以“完整稳定可玩的 AI 世界模组”为第一成功标准；按可玩闭环分版、按测试阶段设门槛。
## 1.1 当前待进行计划（按版本目标）

| 计划/任务 | 版本目标 | 状态 | 下一动作 |
|---|---|---|---|
| `PREP-20260820-1`：新一轮同步/打包预检 | `0.2.1` 准备 | `decision_needed` | 先审查并批准 Persona dist 布局、release gate、安全同步方法和候选 re-freeze 方案 |
| `GOV-20260820-1`：开发工作治理重构 | 项目治理 | `offline_verified` | 保留为已完成证据，不再直接触发同步 |
| `VAL-20260817-1`：批次 I、ContactHubHistory 与既有反馈真机验收 | `0.2.1` | `blocked_sync` | 先修复 dist Persona 布局并重新冻结候选，再统一 DLL/内容哈希，最后复验地图历史、记忆落盘/读档和世界书加载数 |
| `PLAN-Awake-AF-Batch1`、`Batch2to5` | `0.2.1` | `pending_game` | 与 Level 0 一起验收，不重复离线开发 |
| `PLAN-DevTestTools`、`PLAN-EventInboxUI`、`PLAN-WeeklyReportBrowser` | `0.2.1` | `pending_game` | UI 和诊断真机验收 |
| `PLAN-WorldEventPersistence`、`PLAN-MarcusMcmConfig`、`PLAN-MessengerPersistence` | `0.2.1` | `pending_game` | 存读档、MCM、历史持久化验收 |
| `PLAN-ContactHubHistory`、`PLAN-Interactions`、`PLAN-UnifiedDialogueSession` | `0.2.1 → 0.3.0` | `pending_game` | 先完成当前验收，再作为 0.3 社交基座扩展 |
| `PWB-20260818-1`：Persona Workbench Free Preview | 独立试用 / `0.8.0` 作者工具预研 | `stage_complete` | `r33` 已打包交付；暂时暂停，后续按用户试用反馈或 `0.8.0` 作者工具计划再恢复 |
| `PLAN-SceneVisualSelection`、`PLAN-ProactiveLogic` | `0.3.0` | `pending_game` | 场景入口、主动对话和解释性触发真机复验 |
| B1 双模式对话（原版窗口 AI 模式） | `0.3.0` | `decision_needed` | 按路线图范围单独锁定实施 PLAN，不自动开工 |
| 记忆分级、关系阶段、承诺深化、流言基础 | `0.4.0` | `queued` | 以现有记忆/账本为基线重新写 PLAN |
| 事件内容批次、世界效果、周报后果链 | `0.5.0` | `queued` | 先选 20–40 条可验收内容，再走 PLAN/审查 |
| Messenger 来信送达、群体议事、社会传播 | `0.6.0` | `queued` | 新建通信与传播 PLAN；媒体/TTS 非硬门槛 |
| 国家态度、称号、借贷、阴谋和刺杀 | `0.7.0` | `queued` | 命令与风险结算稳定后分批立项 |
| 世界书管理、作者工具、公开 API、内容包 manifest | `0.8.0` | `draft` | 合并 API/世界书/内容工具计划后审查 |
| 性能预算、迁移矩阵、Beta/RC、正式发布 | `0.8 → 1.0` | `queued` | 在玩法闭环稳定后进入发布计划 |

当前优先顺序不是“立即做最高版本号”，而是：`0.2.1` 真机证据 → 阻断问题修复 → `0.3.0` 双模式对话与社交闭环 → `0.4.0` 记忆关系 → `0.5.0` 事件世界反应。

## 2. 最近完成的独立任务（已暂停）

### PWB-20260818-1：Persona Workbench Free Preview

- 优先级：P1（阶段性交付完成）；状态：`stage_complete`，自 2026-08-20 起暂停，当前不再占用 AWAKE 开发锁。
- 权威计划：`PLAN-PersonaWorkbench-FreePreview-20260818.md`，已通过四轮独立只读审查，详见对应 REVIEW-LOG。
- 已完成短任务：`PWB-core-contract-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Core/PersonaCore.cs`、Core/Tests `.csproj`、`tests/PersonaWorkbench.Core.Tests/Program.cs`。
  - 结果：建立 `persona-workbench.character.v1`、五类通用标签、默认注册表、schema/ID/core/未注册标签校验与固定分类顺序 DSL 生成。
  - 验证：先运行测试，因 `PersonaWorkbench.Core` 尚不存在而按预期失败；实现后执行 `dotnet run --project tools/persona-workbench/tests/PersonaWorkbench.Core.Tests/PersonaWorkbench.Core.Tests.csproj -c Release`，通过 `PASS PersonaWorkbench.Core.Tests`。
  - 限制：尚未实现工作区锁/历史、网页服务、AI Provider、Key 保存、AWAKE overrides、游戏目录同步或 Bannerlord 启动。
- 已完成短任务：`PWB-local-document-storage-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Core/PersonaDocumentStorage.cs`、`tests/PersonaWorkbench.Core.Tests/Program.cs`。
  - 结果：增加最大 64 KiB UTF-8 文档上限、严格 root 字段白名单与重复字段拒绝、JSON 往返、读取时 Persona/标签校验，以及同目录临时文件 + flush + 替换的基础原子写入。
  - 验证：先运行新增测试，因 `PersonaDocumentCodec` / `PersonaDocumentStore` 缺失而按预期失败；实现后执行 `dotnet run --project tools/persona-workbench/tests/PersonaWorkbench.Core.Tests/PersonaWorkbench.Core.Tests.csproj -c Release`，6 项通过并输出 `PASS PersonaWorkbench.Core.Tests`。
  - 限制：尚未实现网页服务、AI Provider、Key 保存、AWAKE overrides、游戏目录同步或 Bannerlord 启动。
- 已完成短任务：`PWB-workspace-recovery-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Core/PersonaWorkspace.cs`、`tests/PersonaWorkbench.Core.Tests/Program.cs`。
  - 结果：实现单工作区写锁、根目录/相对路径围栏与 reparse-point 拒绝、SHA-256 写前条件、外部改写 conflict draft、`.history` 单文件备份、`.pending` 临时文件和 `persona-workbench.commit.v1` prepared journal 恢复。
  - 验证：先运行新增测试，因 Workspace 类型缺失而按预期失败；实现后执行 `dotnet run --project tools/persona-workbench/tests/PersonaWorkbench.Core.Tests/PersonaWorkbench.Core.Tests.csproj -c Release`，9 项通过并输出 `PASS PersonaWorkbench.Core.Tests`。
  - 限制：尚未实现 AI Provider、Key 保存、AWAKE overrides、游戏目录同步或 Bannerlord 启动。
- 已完成短任务：`PWB-local-web-shell-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Web/` 下的项目、loopback 策略、预览服务、启动入口与 `wwwroot` 静态页面；`tests/PersonaWorkbench.Web.Tests/Program.cs`。
  - 结果：Kestrel 只监听 `127.0.0.1:51337`，全响应带 restrictive CSP/no-referrer/nosniff/COOP/CORP，页面只调用无持久化的 `/api/preview` 生成 DSL；无 Provider、无 Key、无写入端点。
  - 验证：Web 测试先因入口/策略/服务缺失失败；实现后 `dotnet run --project tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/PersonaWorkbench.Web.Tests.csproj -c Release` 通过 3 项。受控烟测实际启动服务，`GET /` 返回 200 与 restrictive CSP，`POST /api/preview` 返回有效 DSL（134 UTF-8 bytes），随后已发送 Ctrl+C 停止服务。
  - 限制：尚未实现 AI Provider、Key 保存、AWAKE overrides、游戏目录同步或 Bannerlord 启动。
- 已完成短任务：`PWB-local-document-ui-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Web/WorkbenchSessionManager.cs`、`WorkspaceDocumentService.cs`、`Program.cs`、`wwwroot/index.html`、`wwwroot/app.js`、`wwwroot/site.css`、`tests/PersonaWorkbench.Web.Tests/Program.cs`。
  - 结果：启动 URL fragment 的一次性 bootstrap token 换取 30 分钟 instance-bound session/CSRF 值；保存/打开接口必须携带两个自定义 header。自由模式只接受当前工作区根下的单层 `.persona.json` 文件名；保存和打开复用 Core 的锁、哈希冲突、历史与 journal 保护。
  - 验证：先运行测试，因 Session/Document 服务缺失而按预期失败；实现后执行 `dotnet run --project tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/PersonaWorkbench.Web.Tests.csproj -c Release`，7 项通过。浏览器启动延后至 Kestrel `ApplicationStarted`，避免首次 session bootstrap 抢跑。
  - 限制：未进行真人浏览器视觉验收；尚未实现 AI Provider、Key 保存、AWAKE overrides、游戏目录同步或 Bannerlord 启动。
- 已完成短任务：`PWB-provider-contract-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftContract.cs`、`tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs`。
  - 结果：建立 OpenAI-compatible 草稿请求/响应契约；端点只接受 HTTPS 云端或严格 loopback HTTP，拒绝非 loopback HTTP、未知 scheme 与 URL userinfo；任何 3xx 响应直接拒绝且不跟随。客户端单请求串行，429 只记录 `Retry-After` 冷却，不自动重试；响应只接受单 choice 的 JSON 文本，候选字段严格白名单、重复/未知字段、控制字符、超深/超大/未注册标签均拒绝。成功只返回可编辑 `draft`，尚未接入网页动作或 Provider 配置。
  - 验证：按测试先行，Provider 类型缺失时 `PersonaWorkbench.Web.Tests` 编译失败；实现后 `dotnet run --project tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/PersonaWorkbench.Web.Tests.csproj -c Release` 通过 11 项（新增 endpoint、redirect、429 busy/cooldown、structured draft）；`dotnet run --project tools/persona-workbench/tests/PersonaWorkbench.Core.Tests/PersonaWorkbench.Core.Tests.csproj -c Release` 通过 9 项。全部使用注入的假 `HttpMessageHandler`，未读取/保存真实 API Key、未向外部 Provider 发请求。
  - 限制：尚未实现云端首次确认 UI、Provider 网页动作、超时/取消/格式修复的完整 UI 提示、AWAKE overrides、游戏目录同步或 Bannerlord 启动。
- 已完成短任务：`PWB-provider-secure-config-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Web/ProviderSecretConfiguration.cs`、`tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs`。
  - 结果：持久 Provider 配置只保存 endpoint、model 与 DPAPI 密文；DPAPI 不可用时明确返回 `provider.secret_protection_unavailable`，没有明文回退。临时手动 Key 使用可清除的内存 `char[]` 容器，不写入配置；诊断文本可按已知 secret 与 Bearer Authorization 模式脱敏。Windows DPAPI 已用测试字符串验证加/解密往返；本轮未读取用户真实 Key，未写出配置文件，也没有外发请求。
  - 验证：按测试先行，安全配置接口缺失时 `PersonaWorkbench.Web.Tests` 编译失败；实现后 Web 测试通过 16 项（新增 DPAPI 密文、不可用 fail-closed、脱敏、内存 Key 与 Windows DPAPI 往返），Core 测试通过 9 项。
  - 限制：尚未将配置接入网页会话或提供云端首次确认 UI；尚未实现超时/取消/格式修复的完整 UI 提示、AWAKE overrides、游戏目录同步或 Bannerlord 启动。
- 已完成短任务：`PWB-provider-web-actions-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftActionService.cs`、`ProviderDraftContract.cs`、`Program.cs`、`tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs`。
  - 结果：新增受现有 session/CSRF gate 保护的 `/api/provider/confirm-cloud` 与 `/api/provider/generate-draft`。仅 loopback Provider 可直接生成；所有非 loopback HTTPS 端点必须先被当前服务实例明确确认。服务仅从后端内存 vault 取 Key，未设 Key 时不外发；Provider 真实调用禁用代理和自动重定向。成功只返回未批准的 Persona `draft`；失败仅进入内存隔离记录（状态/错误码/时间），不污染可编辑文档。无前端输入控件、无自动确认、无自动批准。
  - 验证：按测试先行，动作接口缺失时 `PersonaWorkbench.Web.Tests` 编译失败；实现后 Web 测试通过 20 项（云端确认、本机豁免、缺失 Key 不调用、失败隔离及临时 Key 仅传入后端 Provider 请求）。本轮只用假 Provider，没有读取真实 Key 或执行外网调用。
  - 当时限制：该接口层尚无会话 Key 设置、云端确认 UI 或 DNS 前后复核；这些缺口已由后续 `PWB-provider-settings-ui-20260818` 接续处理。超时/取消/格式修复 UI 与可视化失败草稿仍未实现。
- 已完成短任务：`PWB-provider-settings-ui-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftContract.cs`、`ProviderDraftActionService.cs`、`Program.cs`、`wwwroot/index.html`、`wwwroot/app.js`、`wwwroot/site.css`、`tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs`。
  - 结果：新增 Provider 设置区：端点、模型、一次性密码型 Key 输入、自由描述、显式云端确认、生成 draft 与清除 Key。Key 只经 session/CSRF 保护的 loopback POST 送入后端内存 vault；JS 的 `finally` 立即清空输入，脚本不使用 `localStorage`/`sessionStorage`，Key 不会写入 Persona、历史或导出。loopback Provider 可无 Key 生成；云端仍需确认 + Key。Provider 在发起请求前与收到成功响应后均重新解析端点，非 loopback 地址拒绝 loopback/private/link-local/multicast/unspecified DNS 结果；代理与重定向继续禁用。
  - 验证：按测试先行，新增 session Key 清除、私网 DNS 解析拒绝及浏览器临时设置静态门禁；实现后 Web 测试通过 23 项，Core 测试通过 9 项。所有 Provider 测试使用 fake handler/resolver；未读取用户真实 Key，未执行外部或本机模型请求。
  - 当时限制：没有 DPAPI 持久配置文件/“记住 Key”界面，当前 Key 仅当前进程有效；DNS 解析到 HTTP 实际连接之间无法由 `HttpClient` 精确取得已连接 IP，仍以请求前与响应后双检作为防重绑定防线。429/超时/取消 UI 已由后续 `PWB-provider-resilience-ui-20260818` 接续处理；真人浏览器视觉验收、格式修复、可视化失败草稿、AWAKE adapter 与游戏目录同步仍未实现。
- 已完成短任务：`PWB-provider-resilience-ui-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftContract.cs`、`ProviderDraftActionService.cs`、`Program.cs`、`wwwroot/index.html`、`wwwroot/app.js`、`wwwroot/site.css`、`tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs`。
  - 结果：Provider 客户端默认使用 45 秒 deadline，可注入测试 deadline；用户取消保持 `provider.cancelled`，内部 deadline 返回 `provider.timeout`，两者都不重试。429 的 `CooldownUntilUtc` 透传至动作响应，网页按倒计时锁定生成按钮直到冷却结束；生成中按钮锁与取消按钮使用 `AbortController`。新增 `/api/provider/failures`，只返回已隔离的状态/错误码/时间，不返回 Key、Prompt 或原始 Provider 文本；网页只用 `textContent` 渲染失败记录。
  - 验证：按测试先行，新增 429 metadata、超时、用户取消、取消隔离与 resilience UI 静态门禁；实现后 Web 测试通过 28 项，Core 测试通过 9 项。所有 HTTP/解析测试使用 fake handler/resolver，未读取真实 Key，未向任何 Provider 发请求。
  - 限制：未做真人浏览器视觉验收；Provider 失败隔离目前只在进程内，不跨重启持久化；未做“记住 Key”的 DPAPI 配置文件、格式修复动作、页面国际化、Windows 自包含打包、AWAKE adapter 或游戏目录同步。
- 已完成短任务：`PWB-free-preview-launch-20260818`。
  - 文件：`tools/persona-workbench/start-free-preview.ps1`、`stop-free-preview.ps1`、`package-free-preview.ps1`、`README-FreePreview.md`、`tests/PersonaWorkbench.Web.Tests/Program.cs`。
  - 结果：提供无需 Node 的本机启动/停止入口；启动器仅运行 Persona Workbench 可执行文件、等待 `http://127.0.0.1:51337/` 返回 200，并保持服务器窗口隐藏。停止器读取本任务自己的 PID 文件，核对 `MainModule.FileName` 与目标 exe 完全一致后才结束进程，避免误伤其他进程。发布脚本使用 framework-dependent `dotnet publish --self-contained false`，生成 SHA-256 manifest，并拒绝 `.env`、日志、`.map`、`.pdb`、源码、测试、历史、临时状态与疑似 Key 文件。
  - 交付包：`tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview`，20 个文件、19 条 manifest 清单，含 `PersonaWorkbench.Web.exe`、静态页面、启动/停止脚本和 `README-FreePreview.md`；不含 Node、不含 Key、不含日志/历史/测试 fixture/源码映射。
  - 验证：先写启动/停止/发布门禁失败测试，修复了通配符复制和 PowerShell `TrimStart` 实际问题；Web 测试 29 项通过，Core 测试 9 项通过，三个 PowerShell 脚本解析通过。源码目录与发布根目录均完成启动 → loopback 200 → 精确停止烟测；未启动 Bannerlord、未调用本机或外部 Provider。
  - 限制：framework-dependent 包要求目标 Windows 已安装对应 .NET 运行时；未做真人浏览器视觉验收，未做自包含单文件打包，未实现 DPAPI “记住 Key”、AWAKE adapter、游戏目录同步或游戏内验收。
- 已进行短任务：`PWB-free-preview-browser-smoke-20260818`。
  - 文件：`tools/persona-workbench/start-free-preview.ps1`、`stop-free-preview.ps1`。
  - 发现与修复：从源码根目录启动时，启动器未选择 `artifacts/PersonaWorkbench-FreePreview`，误用开发输出目录导致静态资源回退到 `/index.html` 后 404；停止器也无法识别 artifacts 下的实际 exe。两个脚本现在都按“根目录发布包 → artifacts 发布包 → 开发输出”顺序选择目标 exe，并保持精确路径核验。
  - 验证：修复后从源码根目录执行启动器，`GET http://127.0.0.1:51337/` 返回 200；随后停止器按 PID 与绝对 exe 路径核验并成功停止 PID 7144。发布包自身启动/停止也通过。未启动 Bannerlord、未调用 Provider、未使用真实 Key。
  - 浏览器状态：受控本机页面成功加载，标题为 `Persona Workbench · Free Preview`，表单字段可编辑；一次预览点击使用了不存在的 `#preview-button` 选择器，属于烟测操作错误，尚未据此判断应用失败。保存/读取尚未验收，因为本次受控标签页没有启动器生成的一次性 fragment 令牌。
  - 限制：未完成表单提交后的 DSL 预览、带 bootstrap session 的保存/读取和浏览器视觉验收；不能把本轮标为 done。
  - 本轮续验：根目录启动/停止脚本修复后再次启动并成功返回 200；原受控标签页已失效，尝试使用旧绑定得到 `Unknown tab: 1`，按止损规则未反复重连、未调用 Provider，已停止本机工作台。
  - 限制：本轮仍未完成表单提交后的 DSL 预览、带 bootstrap session 的保存/读取和浏览器视觉验收；不能把本轮标为 done。
- 下一动作：用户发送“继续”后，从现有 `iab` 浏览器绑定新建一个标签页，使用 `button[type=submit]` 完成 DSL 预览断言；不要复用旧 tab ID。随后单独处理如何在受控浏览器中安全取得本次启动的一次性 fragment 令牌，再验收保存/读取。不要自动调用 Provider，不要把浏览器烟测误报为游戏内验收。
- 已进行短任务：`PWB-guided-tag-ui-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/index.html`、`site.css`、`tests/PersonaWorkbench.Web.Tests/Program.cs`，并更新 `artifacts/PersonaWorkbench-FreePreview` 发布包。
  - 结果：六个稳定标签不改 ID，改为新手可理解的五步分组顺序：整体印象 → 说话方式 → 做事方式 → 受刺激时 → 底线与承诺；增加“不懂标签也没关系”说明；触发项明确标注后续将拆为触发事件、反应方式和强度；页面仍保留自由实验模式。
  - 边界：本批没有把尚未注册的 18 个预设或浪漫/XP 组合包伪装成正式标签；正式 taxonomy 仍由 `PWB-tag-registry-design-20260818` 负责。
  - 验证：Web 测试 30 项通过，Core 测试 9 项通过，PowerShell 三脚本解析通过；临时发布包和正式发布包均启动、`GET /` 200、页面包含 `tag-group-impression`，并按精确路径停止。未启动 Bannerlord、未调用 Provider、未使用真实 Key。
  - 发布备注：为安全切换暂留 `artifacts/PersonaWorkbench-FreePreview.previous-20260818` 旧包备份；删除操作被 Windows 安全门禁阻止，未强行绕过。
- 下一动作：用户发送“继续”后，从现有 `iab` 浏览器绑定新建一个标签页，验证新分组的实际视觉顺序和 `button[type=submit]` DSL 预览；随后再处理一次性 fragment 令牌下的保存/读取。不要自动调用 Provider，不要把静态门禁当作浏览器或游戏内验收。
- 新反馈：`PWB-free-input-first-20260818`（`queued`，关联 `PWB-guided-tag-ui-20260818`）。用户指出实际最高频入口应是“输入少量描述 → 扩展/补全人物形象”，不能把标签选择和 AI 初稿放在自由描述之前。后续界面应改为：自由描述主入口 → 生成/扩展可编辑人物草稿 → 可选标签与关系细化 → DSL 预览；Provider 仍必须显式配置与主动触发。
- 已进行短任务：`PWB-free-input-first-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/index.html`、`site.css`、`tests/PersonaWorkbench.Web.Tests/Program.cs`，并更新 `artifacts/PersonaWorkbench-FreePreview` 发布包。
  - 结果：页面顺序改为“先描述人物（推荐入口） → 扩展人物形象 → 确认/修改角色 → 标签与关系细化 → DSL 预览”；自由描述和主操作位于首屏；端点、模型、Key、云端确认收进折叠的 `AI 设置（高级）`；无 AI 时仍可直接手动编辑和预览。
  - 验证：Web 测试 31 项通过，Core 测试 9 项通过；临时包与正式包均通过自由描述位于编辑器之前、折叠高级设置、启动 `GET /` 200 和精确停止门禁。未启动 Bannerlord、未调用 Provider、未使用真实 Key、未宣称浏览器视觉验收通过。
  - 发布备注：安全切换暂留 `artifacts/PersonaWorkbench-FreePreview.previous-20260818-guided-tags` 旧包备份；此前的 `artifacts/PersonaWorkbench-FreePreview.previous-20260818` 也仍在，删除操作未绕过 Windows 安全门禁。
- 下一动作：用户发送“继续”后，从现有 `iab` 浏览器绑定新建标签，验收首屏视觉顺序、自由描述输入、AI 高级设置折叠和 DSL 预览；随后再处理一次性 fragment 令牌下的保存/读取。Provider 仍不自动调用。
- 已进行短任务：`PWB-starter-personality-behavior-tags-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Core/PersonaCore.cs`、`tests/PersonaWorkbench.Core.Tests/Program.cs`、`tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/index.html`，并更新 `artifacts/PersonaWorkbench-FreePreview` 发布包。
  - 新增正式基础标签：性格 `trait.proud`、`trait.pragmatic`、`trait.guardian`、`trait.traditional`；表达 `expression.direct`、`expression.formal`、`expression.teasing`、`expression.warm`；行为 `behavior.observes_before_acting`、`behavior.tests_loyalty`、`behavior.keeps_leverage`、`behavior.protects_inner_circle`、`behavior.takes_command`。
  - 结果：标签全部进入默认注册表和新手页面，保留稳定英文 ID；页面提供中文说明和悬停解释；旧六个标签继续兼容，未注册标签仍被拒绝。
  - 边界：本批只扩充可组合基础标签，没有把傲娇、病娇、姐姐系、妈妈系或浪漫/XP 组合包伪装成单一基础标签；这些继续由组合包/多轴任务处理。
  - 验证：Core 测试 10 项通过，Web 测试 31 项通过；临时包和正式包均包含新增标签，启动 `GET /` 200，精确停止通过；未启动 Bannerlord、未调用 Provider、未使用真实 Key。
  - 发布备注：安全切换暂留 `artifacts/PersonaWorkbench-FreePreview.previous-20260818-free-input` 旧包备份；此前两份旧包备份也仍在，删除操作未绕过 Windows 安全门禁。
- 下一动作：用户发送“继续”后，做浏览器视觉验收并确认新标签分组；随后进入标签冲突/互斥、强度数值和浪漫/XP 组合包设计，不自动调用 Provider。
- 已完成（离线）：`PWB-tag-intensity-model-20260818`（关联 `PWB-starter-personality-behavior-tags-20260818`）。原先的二元复选框原型已替换为 trait/expression/behavior 的 `unset | slight | moderate | strong | defining` 强度；trigger/boundary 仍为条件规则；互相牵制的倾向、多轴关系、浪漫与情绪状态继续单独建模。
- 已完成短任务：`PWB-tag-intensity-model-20260818`。
  - 文件：`tools/persona-workbench/src/PersonaWorkbench.Core/PersonaCore.cs`、`PersonaDocumentStorage.cs`、`tools/persona-workbench/src/PersonaWorkbench.Web/PersonaPreviewService.cs`、`wwwroot/index.html`、`wwwroot/app.js`、`wwwroot/site.css`、Core/Web 测试与本队列。
  - 结果：trait/expression/behavior 从二元复选框改为 `未设定 / 轻微 / 中等 / 强烈 / 核心特征` 五档；缺省表示未设定，不表示否定；显式强度优先于旧 `Tags`，旧标签仍可读取并按兼容规则映射为中等。trigger/boundary 暂保持条件式二元规则，不允许写入 facet strength。
  - 契约门禁：未注册 facet、0 或 5 等越界强度、trigger/boundary 数值化都会被拒绝；DSL 按稳定类别顺序生成强度文本，重复输入保持确定性。
  - 验证：Core 实际测试 12 项通过，Web 实际测试 34 项通过；Release 构建 0 警告/0 错误；预览包 20 个文件、19 条 SHA-256 manifest，`PersonaWorkbench.Web.exe` SHA-256=`3F3736A1EA9A60D2EA106EBF8CC306FE43DB1166C8ED609228313FCF0124369D`；正式包启动、首页/app.js 标记检查、`GET /` 200、精确停止全部通过。
  - 限制：未做真人浏览器视觉验收、未启动 Bannerlord、未调用 Provider、未使用真实 Key；trigger/boundary 的条件参数、多轴关系/浪漫情绪轴、正式标签注册表审查仍是后续任务。
- 新建议：`PWB-tag-registry-design-20260818`（`queued`，关联强度模型）。当前注册表仍是可用的最小 starter set，不宣称已经覆盖正式角色库。下一步按已批准 schema 做独立 taxonomy 审查：稳定英文 ID、中文名、语义说明、互斥/依赖、强度/优先级、适用角色范围、bundle 展开和版本迁移；优先补关系/浪漫/情绪多轴与可解释组合包，再扩充标签，禁止一次性堆积同义词。
- 设计纠偏：`PWB-reaction-boundary-rule-model-20260818`（`queued`，需先完成计划与审查）。第 4 组“受刺激时”和第 5 组“底线与承诺”不能通过不断增加“背叛、抛弃、善意、誓言、家族”等具体复选框来补齐；这类情境近乎无限，枚举后会碎片化、重叠且难以解释。已撤回 2026-08-18 的枚举式补丁并恢复上一版正式预览包。
  - 第 4 组方向：改为“压力与冲突反应”。基础层只描述跨情境反应轴，例如迎击↔回避、外露↔压抑、即时↔延迟、缓和↔记恨、独自控制↔寻求依靠；具体敏感事件使用可编辑条件规则或自由文本，不注册为无限基础标签。
  - 第 5 组方向：改为“价值排序与承诺机制”。基础层描述承诺谨慎度、守诺强度、利益可交换度与身份/关系优先级；具体底线使用可编辑规则，至少表达保护对象、约束强度、适用范围、例外代价和被突破后的反应，而不是简单勾选“有/没有某条底线”。
  - UI 原则：新手层先给少量通用轴与自然语言输入；高级层再展开条件、范围、强度和例外。预设只能生成可编辑草稿，不能把具体事件伪装成穷尽式正式标签。
  - 已完成短任务：`PWB-reaction-commitment-core-20260818`。
    - 文件：`tools/persona-workbench/src/PersonaWorkbench.Core/PersonaCore.cs`、`PersonaDocumentStorage.cs`、`tests/PersonaWorkbench.Core.Tests/Program.cs`。
    - 结果：新增可选 `reactionProfile` 与 `commitmentProfile`；反应使用五条 `-2..2` 双极轴和两个条件文本字段，承诺使用三条 `-2..2` 双极轴及优先顺序、保护对象、范围、破例代价和突破后反应字段。`null` 表示未设定，`0` 表示明确平衡。
    - DSL：固定输出顺序为常态人格 → `[persona.reaction]` → `[persona.commitment]` → 旧 `[persona.trigger]` / `[persona.boundary]`；未配置的新模型不产生空段落。
    - 验证：Core 实际测试 14 项通过，覆盖轴值范围、`null`/`0` 区分、JSON 往返、确定性 DSL 顺序和旧标签兼容。
    - 限制：Web 请求映射、编辑控件、打开/保存 UI 和发布包尚未更新；正式预览包仍是上一版强度模型。
    - next_action：短任务 `PWB-reaction-commitment-web-20260818`，只接 PersonaPreviewRequest、app.js 与第 4/5 组编辑控件，然后运行 Core/Web 测试。
  - 已完成短任务：`PWB-reaction-commitment-web-20260818`。
    - 文件：`tools/persona-workbench/src/PersonaWorkbench.Web/PersonaPreviewService.cs`、`wwwroot/index.html`、`wwwroot/app.js`、`wwwroot/site.css`、`tests/PersonaWorkbench.Web.Tests/Program.cs`。
    - 结果：第 4 组改为“压力与冲突反应”的五条双极轴，第 5 组改为“价值排序与承诺机制”的三条双极轴；具体条件、反应、价值顺序、范围、破例代价和突破后反应进入折叠的结构化文本区。旧 trigger/boundary 复选框只保留在兼容区域，不再作为主要编辑方式。
    - 数据恢复：预览、打开、保存和 Provider draft 映射均携带 `reactionProfile` / `commitmentProfile`；加载使用显式 null/undefined 判断，轴值 `0` 不会被误当成未设定。
    - 验证：Core 实际测试 14 项通过，Web 实际测试 35 项通过；覆盖 Web DTO 映射、控件存在、旧标签兼容入口和显式零值恢复。
    - 限制：尚未重建 `artifacts/PersonaWorkbench-FreePreview`，未做候选包 HTTP 烟测或真人浏览器视觉验收；正式预览包仍是上一版。
    - next_action：短任务 `PWB-reaction-commitment-package-20260818`，重建候选包，执行首页/app.js/API 预览门禁，安全切换正式包并精确停止服务。
  - 用户调整：`PWB-disposition-axis-unification-20260818`。第 1～3 组也统一采用第 4、5 组的双极程度形式，不再以单向标签强度作为主要编辑方式；原发布候选包因此停止提升，正式包保持不变。
  - 已完成短任务：`PWB-disposition-axis-core-20260818`。
    - 文件：`tools/persona-workbench/src/PersonaWorkbench.Core/PersonaCore.cs`、`PersonaDocumentStorage.cs`、`tests/PersonaWorkbench.Core.Tests/Program.cs`、计划附录。
    - 结果：新增 `traitProfile`、`expressionProfile`、`behaviorProfile`，共 17 条可空 `-2..2` 双极轴；覆盖冒进↔谨慎、安于现状↔野心进取、含蓄↔直白、严肃↔戏谑、先行动↔先观察、跟随↔主导等常态倾向。
    - 兼容：旧 `Tags` / `FacetStrengths` 仍可读取；对应新轴已设置时，新轴替代匹配的旧标签和强度，未设置时继续按旧数据回退。
    - 验证：Core 实际测试 15 项通过，覆盖轴值范围、JSON 往返、显式零值、旧 facet 替代和确定性 DSL。
    - 发布状态：`PWB-reaction-commitment-package-20260818` 候选包只完成烟测，未切换正式包；该候选包不再符合统一 UI 目标。
    - next_action：短任务 `PWB-disposition-axis-web-20260818`，将第 1～3 组改为双极轴控件，接入 Web DTO/app.js/打开保存恢复，并把旧标签移入兼容区域。
  - 已完成短任务：`PWB-disposition-axis-web-20260818`。
    - 文件：`tools/persona-workbench/src/PersonaWorkbench.Web/PersonaPreviewService.cs`、`wwwroot/index.html`、`wwwroot/app.js`、`wwwroot/site.css`、`tests/PersonaWorkbench.Web.Tests/Program.cs`。
    - 结果：第 1～3 组主界面全部改为双极轴，分别覆盖整体倾向、表达方式和做事方式；每条轴同时展示两端含义，并保留未设定与明确平衡。预览、打开、保存和 Provider draft 映射均携带 `traitProfile`、`expressionProfile`、`behaviorProfile`。
    - 兼容：原 17 个 facet strength 控件统一移入“旧标签与旧强度兼容”折叠区；旧文件仍可打开和维护，新轴已设置时对应旧 facet 不重复进入 DSL。
    - 验证：Core 实际测试 15 项通过，Web 实际测试 36 项通过；覆盖 Web DTO、三组轴控件、显式零值恢复、旧 facet 兼容入口和预览 DSL。
    - 限制：尚未重建或切换正式 `artifacts/PersonaWorkbench-FreePreview`，未做统一 1～5 轴页面的 HTTP/API 烟测与真人浏览器视觉验收。
    - next_action：短任务 `PWB-unified-axis-package-20260818`，重新构建干净候选包，验证 1～5 轴页面、保存载荷和 `/api/preview`，再安全切换正式包并精确停止。
  - 已完成短任务：`PWB-unified-axis-package-20260818`。
    - 正式包：`tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview`；20 个文件、19 条 SHA-256 manifest；不含 `.runtime`、日志、源码、测试或 Key。
    - 验证：候选包与正式包均完成启动、`GET /` 200、`GET /app.js` 200、1～5 轴页面标记、`POST /api/preview` 统一轴 DSL、旧 facet 被新轴替代和精确停止门禁。Core 15 项、Web 36 项保持通过；未调用 Provider、未启动 Bannerlord。
    - 哈希：`PersonaWorkbench.Web.dll` SHA-256=`5F8622D676BC951C5344DEA6CF12D62720B747BD9F61E875CA4FF07EBF74E18D`；`PersonaWorkbench.Core.dll` SHA-256=`CF834618441775136C3C9B705D3DF09E52573974C945BECC6CF72AF0BA16BCFD`。
    - 回退包：`tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview.previous-20260818-unified-axis`，保留上一正式版本，未强制删除旧备份。
    - 当前状态：统一 1～5 双极轴 Free Preview 已完成离线交付；真人浏览器视觉、实际手工填写、带启动令牌的打开/保存流程仍待用户试用验收。
  - 已完成短任务：`PWB-provider-settings-clarity-20260818`。
    - 文件：`tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/index.html`、`wwwroot/app.js`、`wwwroot/site.css`、`tests/PersonaWorkbench.Web.Tests/Program.cs`。
    - 结果：AI 设置增加明确操作顺序；端点改名为“Chat Completions 完整接口地址”；补充本机/云端模式、模型 ID 说明、URL 示例和 Key 规则；根据地址自动识别本机回环或 HTTPS 云端，本机端点自动禁用云端确认按钮。
    - 安全边界：未放宽端点白名单、HTTPS、云端确认、Key 临时会话和不自动重试策略；未调用真实 Provider。
    - 验证：Web 实际测试 37 项通过；未做真人浏览器视觉验收，正式发布包尚未包含本轮 AI 设置文案和交互更新。
    - next_action：将 AI 设置清晰度更新合并进下一次统一轴正式包重建，重新做 HTTP/API/停止门禁。

### VAL-20260817-1：当前版本真机验收

- 优先级：P0。
- 状态：`blocked_sync`；历史构建有部分真机通过证据，但当前源码/dist 与游戏 DLL 不一致。
- 前置条件：先将游戏目录同步为已记录哈希的候选 DLL，再由用户启动游戏并生成新日志。
- 完成定义：入口可用、调用链真实执行、结算或持久化成功、存在玩家可观察结果，并有本次运行日志佐证。

#### 验收组 A：启动与基础回归

- 存档能进入大地图，`CampaignSessionReady` 正常出现。
- 首启向导只在 `MapState` 安全弹出，不再阻塞读档。
- `worldbook_runtime_initialized` 正常，启动阶段无持续 CPU 空转或日志停滞。

#### 验收组 B：通讯录与历史

- 地图、场景、遭遇和通讯录对话写入统一 transcript。
- 通讯录历史 Tab、固定、transcript-only 联系人和关系摘要正常。
- 远方联系人显示写信按钮；发送后 `source=letter` 内容出现在历史中。
- 复验 `FB-20260817-10`：地图 AI 对话不再被 transcript source 校验拒绝。

#### 验收组 C：引导与覆盖层

- Welcome → AI Config → Command Deck → First Dialogue → Contact History → Complete 顺序推进并持久化。
- 本局跳过只影响当前战役会话；永久跳过跨会话保持。
- NPC 对话、通讯录、事件收件箱、周报、开发者检查能由统一 Hub 正确判断和关闭。
- Esc、焦点恢复和场景回退不残留输入锁。

#### 验收组 D：AI 路由、存储与记忆

- 复验 `FB-20260817-11`：记忆日结不再出现 `ai.cloud_export_denied`。
- 复验 `FB-20260817-12`：提示词示例 `heroId` 不再出现双重引号。
- 复验 `FB-20260817-7`：实际使用 `AWAKE.route.*`，无 Slaanesh 路由残留或错误 Provider 配置。
- 复验 `FB-20260817-8`：首次缺 key 不刷错误噪音，真实写入不丢失，退出时 final drain 完成。
- 记忆摘要成功后可以落盘，重载后仍可读取。

#### 验收组 E：交互与恢复

- `give_gold` 成功扣除正确金币并写入 interaction ledger。
- 正常保存/重载后不会重复扣款。
- pending 状态按 expected balance 正确完成、补扣或 fail-closed 补偿。
- 账本完成写入失败时金币能退款并记录 compensated。
- promise_request / promise_update 状态转换和去重结果可观察。

#### 验收组 F：既有体验反馈

- 复验 `FB-20260816-1`：场景近/远循环、公开喊话、扇形和高亮足够明显。
- 复验 `FB-20260817-1`：地图对话入口完整，不再与场景/通讯录入口割裂。
- 复验 `FB-20260817-2`：旧 C/U 配置归一化为 V，MCM 显示与运行时一致。
- 复验 `FB-20260817-3`：主动对话由关系、事件、身份、地点或需求驱动，日志能解释触发原因。
- 复验 `FB-20260817-5/6`：开发者检查与世界书管理入口可操作，不是静态占位。

#### 下一动作

1. 用户运行当前版本并完成上述最小验收路径。
2. 收集本次运行的 `Awake.log`、`AwakeProbe.log` 以及 Marcus/Companion 对应日志。
3. Codex 先登记新反馈，再按 P0 → P1 → P2 修复；无新日志时不根据旧日志重复改代码。

## 3. 推进方向与优先级

### P0：稳定当前版本并放行 Level 0

1. 完成 `VAL-20260817-1` 真机验收。
2. 修复新日志确认的崩溃、卡死、存档损坏、重复扣款、持久化丢失和输入锁死。
3. 对修复项重新执行双版本构建、SdkSmoke、本地化、资产边界、XML、release check 和同步哈希检查。
4. 真机复验全部阻断项后，更新内测门槛状态。

### P1：核心机制下一阶段

1. **B1 双模式对话**：PLAN 已有审查基础；开始实现前重新核对锁定范围和用户的明确实施指令，状态 `decision_needed`。
2. **运行时相关性与记忆闭环**：世界书真实命中、TextMappings/persona、记忆摘要落盘和读档回读，优先用真机证据驱动修复。
3. **事件内容与世界效果**：在现有事件引擎、`awake.world.effect.record.v1` 和命令风险策略上增加真实内容批次；属于新内容/机制时重新走 PLAN 与审查。
4. **通讯录交互闭环**：在现有写信、承诺和给金币基础上评估回复延迟、未读和通知；不得绕过统一 transcript 和 interaction ledger。
5. **平台配置诊断**：如新日志仍显示路由或云外发异常，再检查 Marcus `platform.db` / Companion 配置，不凭旧日志推断。

### P2：体验与扩展

1. Messenger 群聊、媒体、TTS、群聊整理：必须新建 PLAN 并通过 `grill-me-codex`；Media 能力不可用时先定义降级路径。
2. 通讯录联系人列表头像、更多关系/身份信息和来信通知。
3. 开发者检查的测试触发、状态刷新、日志跳转和命令诊断增强。
4. 世界书检索调试、重载、校验和管理体验增强；编辑能力需明确内容同步与安全边界。
5. 对话等待动画、状态提示、未读计数和大地图通知。
6. 社区候选：信使距离/时间、善恶与身份差异、记忆整理、借贷、国家态度、阴谋/刺杀、区域文化提示词。

### 暂停或不迁移

- 女神人格和成人内容机制：独立内容包/插件路线，不并入 AWAKE 核心。
- 旧 AF / 爱与恨实现代码：只作设计参考，不建立运行时依赖。
- 未批准的内容注入、背景知识和内容包 RAG 扩展。
- 不具备框架能力或没有明确降级方案的媒体/TTS 实现。

## 4. 反馈状态归一

| ID | 内容 | 当前状态 | 后续动作 |
|---|---|---|---|
| `FB-20260817-9` | 读档无法进入大地图 | `done` | 已有真机日志证明关闭 |
| `FB-20260817-10` | 地图对话不写历史 | `fixed_pending_game` | 验收组 B |
| `FB-20260817-11` | 记忆日结云外发拒绝 | `partially_verified` | 历史构建已完成摘要 Provider 生成；摘要持久化与读档回读仍属验收组 D |
| `FB-20260817-12` | `heroId` 双重引号 | `verified_prior_build` | 历史 Prompt 已显示合法 `heroId`；当前候选仍需随整体验收复查 |
| `FB-20260816-1` | 场景选人和喊话不明显 | `fixed_pending_game` | 验收组 F |
| `FB-20260816-2` | 通讯录关系中心 | `fixed_pending_game` | 核心阶段已实现，验收组 B/E；高级来信另列 P1/P2 |
| `FB-20260817-1` | 地图对话入口割裂 | `fixed_pending_game` | 验收组 F |
| `FB-20260817-2` | C 键冲突 | `fixed_pending_game` | 验收组 F |
| `FB-20260817-3` | 主动对话纯概率 | `fixed_pending_game` | 验收组 F |
| `FB-20260817-4` | 通讯录头像与信息不足 | `queued` | 已交付人物卡/关系摘要，剩余列表增强列入 P2 |
| `FB-20260817-5` | 开发者检查缺少操作 | `fixed_pending_game` | 验收组 F；增强项列入 P2 |
| `FB-20260817-6` | 世界书管理不足 | `fixed_pending_game` | 验收组 F；编辑/调试增强列入 P2 |
| `FB-20260817-7` | 路由残留与 Provider 疑点 | `decision_needed` | 先用新日志确认，再决定是否查平台数据库 |
| `FB-20260817-8` | 缺 key、写丢与 final drain | `fixed_pending_game` | 验收组 D |

## 5. 已完成离线基线

### 运行时和存储

- Messenger 缓存重置、存储回读重试、安全写任务、周报触发日持久化。
- transcript/contact/audit schema、历史迁移、持久对话队列和统一会话 token 生命周期。
- 结构化记忆、衰减、承诺账本、互动 ledger、give_gold 可恢复结算。
- 重复 ID 拒绝、晚注册重载、内容注册预校验、条件解析 fail-closed、容量裁剪。

### AI、世界书和事件

- `AWAKE.route.*` 路由、Prompt/schema 注册、云端对话配置入口。
- 世界书加载、索引、查询、相关性过滤、TextMappings 回退、占位符审计和运行时注入。
- 数据驱动事件引擎、关系命令、世界效果记录命令和事件持久化/周报。
- 主动聊天动机注册与解释性触发基础。

### UI 和体验

- NPC 对话、通讯录、事件收件箱、周报、开发者检查和统一 Hub 生命周期。
- 通讯录人物卡、关系摘要、历史 Tab、写信入口。
- 多步首启引导、跳过与持久化。
- 场景选人循环、公开喊话、扇形/候选/目标高亮和状态提示。

### 最新离线验证

- Bannerlord API 1.3.15 / 1.4.8：0 警告、0 错误。
- SdkSmoke：PASS ALL。
- 本地化：`source=226 en=259 cn=259`。
- 资产边界：`ASSET_BOUNDARY_OK files=111`。
- Prefab 和语言 XML：可解析。
- 发布检查：`RELEASE_CHECK_OK`。
- 游戏目录无误同步 `src`。
- Marcus 确定性 extension validator：因本机缺少同时包含 `src/MarcusAIFramework/Api` 与 `sdk/manifest.json` 的框架源码根目录，未运行，不得宣称通过。

## 6. 开发与切换门禁

- 新功能、新机制、新内容批次：先 PLAN → `grill-me-codex` → 独立只读审查 → `VERDICT: APPROVED` → 用户明确签收 → 才能实现。
- 完成定义统一为：入口 → 调用方 → 结算/效果 → 可观察结果；只有类、路由、命令或 JSON 存在不算完成。
- 当前任务未完成时，新建议只入队，不自动切换；用户明确重定向才允许暂停当前任务。
- 游戏反馈先登记编号和证据，再判定 `blocking_current` / `non_blocking`。
- 不根据旧日志重复修复，不把取消事件描述为网络错误，不在客户端断开后自动续跑。
- 不启动 Bannerlord、不修改启动器、不终止游戏进程；游戏运行时不覆盖模块。
- 普通修复不提版本；提交、推送和正式发布必须由用户明确要求。

## 7. 后续候选

1. `VAL-20260817-1`：当前版本真机验收；当前为 `blocked_sync`，先统一候选 DLL 与内容哈希。
2. 新日志反馈修复：只在验收产生证据后开始。
3. B1 双模式对话：`decision_needed`，需明确实施指令。
4. Messenger 群聊/媒体/TTS：需新 PLAN、能力门禁和审查。
5. 正式 Level 0 内测发布整理：仅在真机验收通过后执行。

## 8. 最新检查点

- 2026-08-17 已重新整理任务队列：旧逐轮记录归档，当前文件成为唯一推进权威。
- `ROADMAP-20260817-1` 已完成：版本重心、功能包、内容包并行轨、延期规则和升版门槛均已写入并确认。`VAL-20260817-1` 经 2026-08-20 复盘调整为 `blocked_sync`。
- 历史可验证事实：旧构建修复 `effects` 后 NPC 对话可完成；后续旧构建的记忆任务也已使用 `awake.npc.memory.summary.output.v1` 返回 `Result: completed`。但缺少摘要 persisted/读档回读事件，且这些日志早于当前 DLL。
- 路线图文档：`docs/AWAKE-Version-Roadmap-0.2.0-to-1.0.0-20260817.md` 已写入并确认，状态 `approved`。
- 下一动作：先完成候选构建指纹与三地哈希统一，再由用户运行该候选生成新日志；重点确认正式地图入口 transcript、记忆摘要 persisted/读档回读、联系人身份标签和世界书 335 文件的运行时加载数，不启动 0.3.0。
## 新增游戏反馈（2026-08-17）

- `FB-20260817-13`：启动与读档验收通过，但地图 AI 对话在第 2 项开始出问题。
  - 来源：用户本轮真机测试。
  - 证据：`Awake.log` 19:03:50 提交成功；`companion.log` 19:03:55 返回 `ai.output_schema_invalid`，响应含合法 `reply`/`mood` 但省略可选 `effects`。
  - 关联任务：`VAL-20260817-1`，验收组 B / D。
  - 优先级：P0，当前验收阻断。
  - 状态：`fixed_pending_game`；分类：`blocking_current`。
  - 修复：`NpcPromptTemplate` 不再把 `effects` 列为必填；`NpcDialogueOutputValidator` 缺省时归一化为空数组。
  - 离线验证：1.3.15 构建 0 警告/0 错误；`Awake.SdkSmoke` PASS ALL，新增 optional effects 回归通过。
  - 下一动作：新 DLL 已同步；与本轮新日志一起复验地图回复、transcript 与记忆日结。

- `FB-20260817-14`：地图 AI 对话修复通过，但记忆日结输出契约未注册。
  - 来源：`Modules\AWAKE\Logs\Awake.log` 与 `Modules\MarcusAIFramework\log\companion.log` 最新一轮日志。
  - 证据：2026-08-17 19:47:40、19:48:32 两次 `AWAKE.route.npc.dialogue` 均 `Result: completed`；19:48:48 提交 `AWAKE.route.memory.daily`，19:48:50 返回合法 `{"summary":"..."}`，但框架报 `ai.output_schema_not_found`，契约为 `awake.npc.memory.summary.output.v1`。
  - 关联任务：`VAL-20260817-1`，验收组 B / D。
  - 优先级：P0；状态：`partially_verified`；分类：`blocking_current`。
  - 初步定位：`NpcMemoryService` 提交时引用 `awake.npc.memory.summary.output.v1`，当前代码未发现对应 Prompt/输出 schema 注册路径；对话 schema 由 `NpcPromptTemplate` 注册，记忆 schema 没有等价入口。
  - 后续证据：历史后续会话已不再出现 `ai.output_schema_not_found`，Provider 返回 `Result: completed`；仍需当前候选确认摘要持久化和读档回读。

- `FB-20260817-15`：世界书启动持续报告两个警告。
  - 来源：`Awake.log` 的 `worldbook_runtime_initialized rules=336 personas=415 warnings=2`。
  - 证据：离线按当前 Loader 规则审计出两个重复 Rule ID：`rule_巴旦尼亚水之女神` 与 `rule_中原`，各出现两次；337 个规则文件只有 335 个唯一 ID。
  - 关联任务：`VAL-20260817-1`，验收组 A / D。
  - 优先级：P1；状态：`verified_prior_build`；分类：`non_blocking`。
  - 后续证据：历史后续会话已出现 `warnings=0`；当前静态集合为 335 个唯一规则，但旧日志运行时加载 334，当前候选仍需解释加载数。
  - 最终处理：删除 `巴旦尼亚水之女神` 与 `中原` 泛化词条，只保留 `比安芙` 与 `洛泰——贾尔马律斯平原`，并保留重复 ID 内容门禁。

- `FB-20260817-16`：通讯录写信被拒绝，但日志无法说明拒绝原因。
  - 来源：`Awake.log` 2026-08-17 11:00:24 `letter_rejected key=hero:lord_1_18`。
  - 证据：有效 contact key 已记录；`AppendLetterAsync` 将 store 未就绪、空文本和空幂等键统一记录为同一事件，现有日志无法判断是用户空提交、存储未就绪还是调用参数缺失。
  - 关联任务：`VAL-20260817-1`，验收组 B。
  - 优先级：P1；状态：`verified_prior_build`；分类：`non_blocking`；历史构建已出现写信四段成功事件。
  - 后续证据：历史日志已出现 `transcript_write_persisted`、`contact_index_persisted`、`letter_send_succeeded`、`letter_history_reloaded`；当前候选随整体验收复查。

- `FB-20260817-17`：Prompt 注册冲突和 Companion 启动重连仍产生噪音。
  - 来源：`Awake.log` 与 `MarcusAIFramework\log\framework.log`。
  - 证据：最新会话出现一次 `prompt.revision_conflict`，但随后两次 NPC 对话均完成；框架启动后约三秒内出现 pipe broken / EndOfStream 与 profiles list 请求失败，19:47:09 已重新连接，未阻断 19:47:40 之后的 AI 请求。
  - 关联任务：`VAL-20260817-1`，诊断质量。
  - 优先级：P2；状态：`partially_verified`；分类：`non_blocking`。
  - 后续证据：历史后续会话未再把 `prompt.revision_conflict` 作为 AWAKE 阻断；Companion 启动重连噪音仍存在但会自动恢复。
  - 原离线动作：Prompt 冲突按“已存在同 revision”降级为幂等成功或明确日志；Companion 重连仅在影响任务提交时升级，当前不归类为连接故障。

## 9. 潜在问题反向审查（2026-08-17）

- `AUD-20260817-1`：存储就绪与写入结果存在“假成功”链路。
  - 优先级：P0；状态：`fixed_pending_game`；关联：`VAL-20260817-1` 写信与 transcript 验收。
  - 证据：`EnsureWorldStateReadyAsync` 在 12 个命名空间中任意一个打开成功即返回 `true`，没有确认写信必需的 `awake.transcripts` 与 `awake.contacts` 均已就绪；`AppendTranscriptLinesAsync`、`EnsureContactAsync` 在 drain 后不检查 `WorldDrainSummary`，即使最终写入失败或重试耗尽仍返回 `true`；`AppendLetterAsync` 也忽略联系人写入结果。
  - 风险：UI 可提示“发送成功”，但历史或联系人索引实际未持久化；部分命名空间故障时现有日志不足以将成功提示与落盘失败对应起来。
  - 建议：增加指定命名空间 readiness；所有面向调用方的 bool 写入方法依据 owner drain summary 返回真实结果；补 transcript/contacts 缺失、SetAsync 失败、部分成功三类回归测试。

- `AUD-20260817-2`：Prompt 注册失败会污染整个会话的注册状态，并存在注册未完成即放行的竞态。
  - 优先级：P1；状态：`fixed_pending_game`；关联：`FB-20260817-14`、`FB-20260817-17`。
  - 证据：`NpcMemoryService` 在 `RegisterAsync` 前写入 `_promptRegistrationAttempted=true`；`NpcDialogueService` 在 await 前写入静态 `PromptRegistrationAttempts` 与实例 `_promptRegistered`。取消、暂时断连、权限失败或框架未就绪后不会重试；并发初始化时后续调用可在首个注册仍未完成时直接继续提交任务。
  - 风险：一次暂时失败可使本进程后续记忆摘要持续得到 `ai.output_schema_not_found`，或在注册完成前产生偶发 schema 失败。
  - 建议：改为按 prompt key 的 single-flight 注册任务；只在成功或 `prompt.revision_conflict` 后缓存 usable；失败/取消移除状态并按 `FrameworkError.Retryable` 决定后续重试；注册不可用时不提交依赖该 schema 的 AI 任务。

- `AUD-20260817-3`：记忆摘要失败诊断被吞掉。
  - 优先级：P1；状态：`fixed_pending_game`；关联：记忆日结验收。
  - 证据：`NpcMemoryService.SummarizeAsync` 对 `Failed` / `Cancelled` 只返回空字符串，不记录 `AiTaskEvent.Error`；30 秒超时也静默返回空；SDK 的 `AiTaskEvent` 明确提供 `Error.Code`、`Category`、`Retryable`、`CorrelationId` 与 `Details`。
  - 风险：AWAKE 日志无法区分 schema、Provider、权限、取消和超时，必须跨查 Companion 日志，且重试策略无法基于错误类型判断。
  - 建议：记录结构化错误字段；取消与失败分开；超时记录 route/hero/conversation/correlation；不要记录敏感正文。

- `AUD-20260817-4`：存储就绪检查存在重复 I/O 与并发重复打开。
  - 优先级：P2；状态：`fixed_pending_game`。
  - 证据：每次 `EnsureWorldStateReadyAsync` 都重新执行权限 Ensure，并遍历打开全部 12 个命名空间；对话初始化、EnsureReady、命令结算、事件引擎和每封信都会调用，且没有 single-flight/readiness 快路径。
  - 风险：增加 Companion 往返、日志与初始化延迟；并发入口可能重复打开同一组 namespace。
  - 建议：缓存逐 namespace readiness，并用 single-flight 任务合并并发初始化；调用方只要求自身依赖的 namespace 集合。

- `AUD-20260817-5`：写信成功路径缺少可审计日志，导致真机验收误判为“未发送”。
  - 优先级：P1；状态：`fixed_pending_game`；关联：`VAL-20260817-1` 验收组 B。
  - 证据：`AwakeMessengerVM` 在 `SendAsync` 返回成功后只更新 UI；`AwakeLetterService`、`AwakeTranscriptService` 成功路径没有记录发送、transcript 持久化、联系人索引持久化和历史回读事件。用户已实际发送，但日志只能看到无错误，无法证明完整链路。
  - 修复：新增 `transcript_write_persisted`、`contact_index_persisted`、`letter_send_succeeded`、`letter_history_reloaded`；SdkSmoke 增加 `source=letter` 行验证。
  - 离线验证：1.3.15 / 1.4.8 构建 0 警告 / 0 错误；`Awake.SdkSmoke` PASS ALL；跳过运行中游戏目录的 `release_check` 为 `RELEASE_CHECK_OK`；新 1.3.15 DLL SHA-256 为 `748978D17584238159916E3B03E3A8D8253B2FE7E9766110F728E4DC0992D7BF`，已同步 `_build_out` 与 `dist`。
  - 后续证据：历史旧构建已出现四个成功事件；当前源码/dist 与游戏 DLL 再次漂移，当前候选仍需重新验收。

- 已排除：写信调用不传 `displayName` 不会清空已有联系人名称；`ApplyContacts` 仅在非空名称时覆盖 `contactNames`。
- 内容一致性：root、dist、游戏目录、AWAKE-Repo 四处世界书均为 335 文件 / 335 唯一 ID / 0 重复，两个泛化词条已删除，两个指定词条均保留。
- 本轮门禁：1.3.15 与 1.4.8 构建均为 0 警告 / 0 错误；SdkSmoke `PASS ALL`；release check `RELEASE_CHECK_OK`；MAF lint 为 316 条既有 warning、0 blocking；世界书占位符审计仍为 18 条既有内容问题。
- 当时修复结果：`AUD-20260817-1` 至 `AUD-20260817-5` 已完成离线修复并曾完成同步；2026-08-20 复核发现后续开发已使源码/dist 与游戏 DLL 再次漂移，旧同步结论不再代表当前状态。
## 2026-08-20 检查点：AWAKE 开发工作治理重构设计

- 任务：`GOV-20260820-1`；状态：`decision_needed`。
- 设计稿：`docs/AWAKE-Development-Governance-Design-20260820.md`。
- 核心决策：升级现有 `bannerlord-mod-development-orchestrator` 为唯一调度入口，不新增第五套总控 Skill；引入批次执行租约，使已批准批次内的短任务自动接续。
- 状态治理：计划拆分 CURRENT、ROADMAP、VALIDATION 与独立 checkpoints；引入 E0-E5 证据等级、单活动批次、单冻结候选和 BuildId/哈希日志。
- Worker 实测：6 条规则摘要使用 `medium` 等待 180 秒后超时；任务按 `in_doubt`/失败停止，未自动重放。新版路由将少量语义治理任务固定交给云端。
- 设计自查：无 TODO/TBD、无重复标题；职责、停止条件、同步授权和 Worker 熔断规则已明确。
- 下一动作：等待用户最终审阅设计稿；批准后使用 writing-plans/skill-creator 制定精确实施计划，再修改 Skills 和项目状态文件。
## 2026-08-20 检查点：0.2.1 开发日志与测试教训复盘

- 阶段总结：`docs/AWAKE-0.2.1-Development-Lessons-20260820.md`。
- 历史真机证据：旧构建曾完成 NPC 对话和记忆摘要 Provider 生成；写信四段成功事件与 `worldbook warnings=0` 也曾出现。
- 证据边界：日志最后写入早于当前游戏 DLL和当前源码/dist DLL，不能给当前候选背书；记忆摘要生成完成不等于摘要已持久化并在读档后回读。
- 当前漂移：源码/dist DLL=`F863D8EF...`，游戏 DLL=`FF98881E...`；当前静态世界书三地均为 335 文件 / 335 唯一 ID，但历史日志运行时加载数为 334。
- 当前门禁：`VAL-20260817-1` 改为 `blocked_sync`；先统一候选与哈希，再做最小真机闭环，不继续扩大 `0.3.0` 机制范围。

## 2026-08-18 短任务检查点：Persona Workbench AI 设置清晰度

- `PW-20260818-provider-clarity`：`done`；将 AI 设置改为明确的“完整接口地址 → 模型 ID → 云端 Key/确认 → 扩展人物形象”流程，并区分本机回环 Provider 与云端 HTTPS Provider。
- 修改范围：`tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/index.html`、`app.js`、`site.css`、`tests/PersonaWorkbench.Web.Tests/Program.cs`。
- 离线验证：Core 15 项通过；Web 37 项通过；候选包页面文案、`classifyProviderEndpoint`、本机禁用云端确认逻辑均命中。
- 候选运行门禁：`GET /` 200；`GET /app.js` 200；`POST /api/preview` 返回合法 Persona DSL；精确停止后 `51337` 无监听。
- 正式预览包：`artifacts/PersonaWorkbench-FreePreview` 已安全替换；旧包保留为 `PersonaWorkbench-FreePreview.previous-20260818-provider-clarity`。
- 包完整性：20 个文件、19 条 manifest、无运行日志；`PersonaWorkbench.Web.dll` SHA-256=`5F8622D676BC951C5344DEA6CF12D62720B747BD9F61E875CA4FF07EBF74E18D`；`PersonaWorkbench.Core.dll` SHA-256=`CF834618441775136C3C9B705D3DF09E52573974C945BECC6CF72AF0BA16BCFD`。
- 未验证项：未调用真实 Provider、未使用真实 API Key、未做真人浏览器视觉验收；不启动 Bannerlord。
- `next_action`：用户可运行 `tools/persona-workbench/start-free-preview.ps1`，在页面填写本机完整 Chat Completions 地址或云端 HTTPS 地址后试用；若出现问题，提供工作台 `.runtime/server.stderr.log` 与页面操作步骤。

## 2026-08-18 短任务检查点：Provider 失败提示分层

- `PW-20260818-provider-error-clarity`：`done`；修复 Provider 生成失败时把工作台断线、响应解析失败和 Provider 返回错误混成“Provider 请求失败”的问题。
- 根因证据：用户反馈对应的通用提示来自浏览器 `fetch` 异常；工作台日志没有 `/api/provider/generate-draft` 请求，说明请求未到达 Provider Action 服务，不能归类为 Provider 返回失败。
- 修复：网络断线提示“工作台服务连接失败”；会话失效提示重新启动并从启动链接打开；无效响应单独提示；HTTP/Provider 错误保留 `errorCode`；所有失败路径继续不覆盖当前表单。
- 验证：Web 测试 37 项通过；正式包启动/静态资源门禁通过；停止后 `51337` 无监听。
- 正式包：`artifacts/PersonaWorkbench-FreePreview` 已更新；旧包保留为 `PersonaWorkbench-FreePreview.previous-20260818-provider-error-clarity`。
- 未验证项：未调用真实 Provider、未使用真实 API Key；当前工作台停止状态是刻意的，试用前需重新运行启动脚本。
- `next_action`：运行 `tools/persona-workbench/start-free-preview.ps1` 后，从新打开的启动页面进行 Provider 测试；若仍失败，记录页面新提示和 `.runtime/server.stdout.log` 中是否出现 `/api/provider/generate-draft`。

## 2026-08-18 短任务检查点：云端 Key 自动提交

- `PW-20260818-provider-autokey`：`done`；日志显示多次 `/api/provider/generate-draft` 立即返回 409，但当前进程没有 `/api/provider/session-key` 写入记录，生成请求未进入真实 Provider 调用。
- 修复：点击“扩展人物形象”时，如果云端 Key 输入框有内容，工作台先将 Key 写入当前后端会话，再继续生成；不再要求用户额外记住“设置 Key”按钮步骤。
- 安全边界：Key 仍只存在当前工作台进程内存；不写浏览器存储、Persona 文件或日志；云端端点仍需用户显式确认。
- 验证：Web 测试 37 项通过；正式包 20 个文件、19 条 manifest；`app.js` 自动 Key 提交逻辑已由运行中正式实例返回；当前 PID=35328，监听 `127.0.0.1:51337`。
- 回退包：`artifacts/PersonaWorkbench-FreePreview.previous-20260818-provider-autokey`。
- `next_action`：在新启动页面重新确认云端端点，填写 Key 后直接点击“扩展人物形象”；本次不需要再单独点击“设置本次会话 Key”。

## 2026-08-18 短任务检查点：DeepSeek JSON Output 400

- `PW-20260818-deepseek-json`：`done`；真实云端请求已到达 Provider，但返回 HTTP 400。
- 根因：请求启用了 `response_format=json_object`，却只发送玩家自由描述，没有明确的 JSON 输出指令、字段约束和示例；Provider 在进入生成前拒绝请求。
- 修复：新增固定 system message，明确只返回一个 JSON 对象，限定 `id/displayName/core/identityFacts/tags` 五个字段，禁止 Markdown 与额外字段，并提供 JSON 示例；新增 `max_tokens=2048`。
- 验证：Web 测试由 37 项增至 38 项，全部通过且 0 警告；新增门禁直接检查请求体包含 system JSON 指令、`response_format=json_object` 和输出上限。
- 正式包：20 个文件、19 条 manifest；`PersonaWorkbench.Web.dll` SHA-256=`FD3056455FE422FC853F13378A588FDF71E9446201DE86BC3B043AF7CF2A9032`；回退包为 `PersonaWorkbench-FreePreview.previous-20260818-deepseek-json`。
- 当前状态：正式工作台 PID=61360，监听 `127.0.0.1:51337`。
- `next_action`：在新页面重新确认云端端点并重新输入 Key，点击“扩展人物形象”进行真实 Provider 复验；如仍失败，以新的 failure code 继续定位。

## 2026-08-18 short-task checkpoint: Persona canonical renderer
- task_id: PERSONA-UNIFIED-WORKBENCH-01
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Core/CanonicalPersonaTemplateGenerator.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Core/PersonaCore.cs; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Core.Tests/Program.cs; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs
- result: Workbench PersonaDslGenerator.Generate now routes to canonical [PERSONA_LOAD] renderer; legacy generator remains explicitly named GenerateLegacy; axis IDs are English stable IDs and configured axes suppress duplicate legacy tags.
- verification: PersonaWorkbench.Core.Tests PASS; PersonaWorkbench.Web.Tests PASS.
- limitations: AWAKE runtime src/PersonaDslGenerator.cs is not yet migrated; no game validation; no release synchronization performed.
- next_action: migrate AWAKE runtime generator to the same canonical section grammar, then add cross-implementation golden sample.

## 2026-08-18 short-task checkpoint: AWAKE canonical Persona runtime
- task_id: PERSONA-UNIFIED-RUNTIME-02
- status: done
- files_changed: _houkai_merge/AWAKE/src/PersonaDslGenerator.cs; _houkai_merge/AWAKE/src/PersonaModels.cs; _houkai_merge/AWAKE/AWAKE.csproj; _houkai_merge/AWAKE.Tests/Program.cs
- result: approved Persona definitions, runtime dynamic context, and legacy fallback now emit canonical [PERSONA_LOAD] section grammar; no new runtime path emits <persona:v1> or [persona.*]; canonical tag lines use uppercase stable IDs without copying registry prompt text.
- build_fix: AWAKE project compile scope is explicitly limited to src/**/*.cs, preventing Workbench/test sources from entering the net472 runtime assembly.
- verification: AWAKE 1.3.15 Release build PASS 0 warnings/0 errors; AWAKE 1.4.8 Release build PASS 0 warnings/0 errors; Awake.SdkSmoke PASS ALL including canonical Persona template and fallback checks.
- limitations: Workbench Provider draft schema still accepts only the earlier limited fields; cross-implementation shared golden fixture and release packaging remain pending; no game validation or game-directory synchronization performed.
- next_action: expand the Workbench Provider response contract to structured Persona profiles and enforce evidence-backed null/default behavior before canonical rendering.

## 2026-08-18 short-task checkpoint: evidence-backed Provider drafts
- task_id: PERSONA-PROVIDER-CONTRACT-03
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Core/PersonaCore.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftContract.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/index.html; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs
- result: Provider candidates now support all current trait/expression/behavior/reaction/commitment profiles; every populated axis, selected tag, identity fact, or profile text requires a short exact evidence quote from the source description; unsupported fields, invalid axes, missing/orphaned evidence, and whole-prompt verbatim copies are rejected before the draft reaches the editor.
- prompt_policy: Provider is explicitly instructed to use null when evidence is insufficient, use registered tag IDs only, synthesize core text instead of copying the full prompt, and return no DSL or translated axis labels.
- verification: PersonaWorkbench.Core.Tests PASS; PersonaWorkbench.Web.Tests PASS including structured profiles, evidence requirements, verbatim-copy rejection, unknown profile rejection, cooldown, cancellation, endpoint, and transient-key checks.
- limitations: canonical model still lacks first-class self-claim rules, real-self behaviors, examples, food preference, public/private/contradiction authored lists, provenance, and approval fields; free preview artifact has not yet been repackaged.
- next_action: extend the Persona document and editor with the remaining canonical template sections, then make the renderer output those fields deterministically.

## 2026-08-18 short-task checkpoint: canonical authored sections
- task_id: PERSONA-AUTHORED-SECTIONS-04
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Core/PersonaCore.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Core/PersonaDocumentStorage.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Core/CanonicalPersonaTemplateGenerator.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/PersonaPreviewService.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/index.html; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/app.js; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Core.Tests/Program.cs
- result: PersonaDocument now persists template version/status/provenance and canonical authored fields: summary, public/private/contradiction descriptions, self-claim rules, real-self behaviors, self-claim examples, and food preference. The editor can load, edit, preview, and save these fields; sourceDescription is retained as source metadata but never emitted into formal DSL.
- renderer: canonical output now includes metadata in PERSONA_LOAD, authored descriptions in the relevant personality sections, and deterministic SELF_CLAIM_RULES, REAL_SELF_BEHAVIOR, SELF_CLAIM_EXAMPLES, and FOOD_PREFERENCE sections.
- verification: PersonaWorkbench.Core.Tests PASS; PersonaWorkbench.Web.Tests PASS; round-trip and authored-section golden tests pass.
- limitations: Provider parser currently does not populate the new authored fields; approval workflow and runtime loading of these authored sections remain pending; no package rebuild or game validation.
- next_action: extend the Provider candidate schema and UI application path for authored fields, with evidence rules for any generated text.


## 2026-08-18 short-task checkpoint: Provider authored fields verification
- task_id: PERSONA-PROVIDER-AUTHORED-05
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftContract.cs; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs
- result: Provider authored canonical fields now parse only with exact source evidence; local sourceDescription, templateVersion, draft status, and empty sourcePackId remain authoritative.
- verification: PersonaWorkbench.Web.Tests PASS with zero warnings; PersonaWorkbench.Core.Tests PASS. Coverage includes authored-field parsing, missing-evidence rejection, local approval metadata, structured profiles, endpoint safety, cooldown, cancellation, and canonical preview controls.
- limitations: explicit user approval workflow, AWAKE runtime authored-section loading, shared golden fixture, FacetStrengths audit, package rebuild, and game validation remain pending.
- next_action: implement and test explicit local draft review/approval state transitions without allowing Provider output to approve or publish.


## 2026-08-18 short-task checkpoint: explicit local Persona approval
- task_id: PERSONA-LOCAL-APPROVAL-06
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Core/PersonaCore.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/WorkspaceDocumentService.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/Program.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/index.html; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/app.js; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Core.Tests/Program.cs; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs
- result: Persona status is now a validated local state with only draft/approved values. Ordinary save always forces draft, the dedicated authorized approval endpoint forces approved, Provider drafts remain draft, and any browser edit after approval returns the document to draft. The UI exposes a visible review status and an explicit approval button.
- incidental_fix: removed two literal backslash-n sequences that had joined applyAuthorFields calls onto assignment lines in app.js.
- verification: PersonaWorkbench.Core.Tests PASS; PersonaWorkbench.Web.Tests PASS; app.js parsed successfully with Node vm.Script; all runs completed without compiler warnings.
- limitations: AWAKE runtime authored-section loading, shared golden fixture, FacetStrengths legacy-path audit, release package rebuild, and game validation remain pending.
- next_action: align AWAKE runtime Persona models/loader/renderer with the Workbench authored canonical sections and approved-only gate.


## 2026-08-18 short-task checkpoint: AWAKE authored Persona runtime alignment
- task_id: PERSONA-RUNTIME-AUTHORED-07
- status: done
- files_changed: _houkai_merge/AWAKE/src/PersonaModels.cs; _houkai_merge/AWAKE/src/PersonaDataLoader.cs; _houkai_merge/AWAKE/src/PersonaDslGenerator.cs; _houkai_merge/AWAKE.Tests/Program.cs
- result: AWAKE PersonaDefinition now loads summary, public/private/contradiction descriptions, food preference, self-claim rules, real-self behaviors, and self-claim examples. Approved runtime output now includes template version, status, source pack, canonical authored sections, and deterministic list ordering; draft/disabled definitions still use legacy canonical fallback.
- verification: Awake.SdkSmoke PASS ALL, including canonical authored fields, approved metadata, deterministic generation, and unregistered-tag fallback.
- limitations: Workbench and AWAKE still lack a shared golden fixture; FacetStrengths parity audit, free-preview repackaging, release checks, and game validation remain pending.
- next_action: create one shared canonical Persona fixture and assert Workbench and AWAKE produce the same grammar/section ordering without requiring identical context-specific identity values.


## 2026-08-18 short-task checkpoint: shared Persona golden fixture
- task_id: PERSONA-GOLDEN-FIXTURE-08
- status: done
- files_changed: _houkai_merge/AWAKE/docs/fixtures/persona-load-v2-golden.json; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Core.Tests/Program.cs; _houkai_merge/AWAKE.Tests/Program.cs
- result: one shared JSON fixture contains canonical Persona input and expected [PERSONA_LOAD] DSL. Workbench and AWAKE each adapt the same input to their local model and compare exact output, while dynamic runtime context is intentionally excluded from the static grammar comparison.
- verification: PersonaWorkbench.Core.Tests PASS including shared golden fixture; Awake.SdkSmoke PASS ALL including shared golden fixture; fixture JSON structure and sourceDescription non-leak check PASS.
- diagnostic: initial mismatch was an expected runtime-only CURRENT_IDENTITY/ROLE section; the test now documents that boundary by using empty dynamic context, not by weakening canonical comparison.
- limitations: FacetStrengths parity/legacy-path audit, full Workbench Web regression after fixture batch, free-preview repackaging, final AWAKE builds and release checks, and game validation remain pending.
- next_action: audit FacetStrengths and remaining legacy output paths, then run full Workbench/AWAKE validation before packaging.


## 2026-08-18 short-task checkpoint: Facet strength and legacy output audit
- task_id: PERSONA-FACET-AUDIT-09
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Core/CanonicalPersonaTemplateGenerator.cs; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Core.Tests/Program.cs
- result: legacy FacetStrengths values 1-4 were previously validated and persisted but silently dropped from canonical DSL. They now emit stable English lines such as FACET_TRAIT_CAUTIOUS_STRENGTH_4, while configured bipolar axes suppress the matching facet strength and unqualified base tags are not duplicated.
- legacy_path_audit: Workbench Generate() and all AWAKE runtime Persona paths emit [PERSONA_LOAD]. The old [persona.*]/<persona:v1> grammar remains only behind the explicitly named Workbench GenerateLegacy compatibility API; no release path calls it.
- verification: PersonaWorkbench.Core.Tests PASS; PersonaWorkbench.Web.Tests PASS; search audit found no old-format runtime output path; invalid facet/category/unregistered gates remain passing.
- incident: one overly broad test replacement temporarily removed five Core test functions; all were reconstructed from the existing contracts, and the full Core suite now passes again.
- limitations: full AWAKE release build, free-preview repackaging, manifest/hash checks, and game validation remain pending.
- next_action: run final Workbench/AWAKE builds, package the free preview, perform structure/hash/self-audit, and report game-unverified items separately.

## 2026-08-19 short-task checkpoint: Persona Workbench final validation and preview release
- task_id: PERSONA-FINAL-RELEASE-10
- status: done
- files_changed: _houkai_merge/AWAKE/_build_out/1.3.15/Release/Awake.dll; _houkai_merge/AWAKE/_build_out/1.4.8/Release/Awake.dll; _houkai_merge/AWAKE/dist/Modules/AWAKE/bin/Win64_Shipping_Client/Awake.dll; _houkai_merge/AWAKE/tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-final-20260819/*
- result: final Workbench and AWAKE validation completed. Provider drafts, evidence gates, canonical [PERSONA_LOAD] rendering, authored sections, explicit local approval, timeline-safe model boundaries, shared golden fixture, FacetStrengths intensity encoding, and legacy-path isolation are all covered by tests or static audits.
- verification: PersonaWorkbench.Core.Tests PASS; PersonaWorkbench.Web.Tests PASS; Awake.SdkSmoke PASS ALL; AWAKE 1.3.15 Release build PASS 0 warnings/0 errors; AWAKE 1.4.8 Release build PASS 0 warnings/0 errors; package manifest PASS 19/19 hashes; package whitelist PASS; canonical source audit PASS; golden structure audit PASS; AWAKE Release Check PASS for dist/non-game-synchronization scope; localization PASS source=226 en=259 cn=259; asset boundary PASS files=117.
- hashes: AWAKE 1.3.15 build F863D8EF5D53B6FA44BA6B4311BA16FE53AADCA17EF3B8D5BA5F5AC1269D1CE4; AWAKE 1.4.8 build 830383362263ABA1E9CBE3E8EA73639BDB6A5B5DF6B813672B63FA1E48239B0; dist synced to 1.3.15 hash F863D8EF5D53B6FA44BA6B4311BA16FE53AADCA17EF3B8D5BA5F5AC1269D1CE4.
- package: _houkai_merge/AWAKE/tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-final-20260819; 20 files including 19 manifest entries; no source, symbols, logs, maps, tests, or key-like files.
- limitation: the existing Workbench process on 127.0.0.1:51337 was not stopped or overwritten. Bannerlord game directory DLL remains older hash FF98881E58102E992CFFE2B959222B406165762BEA2E2E3819E1647AF8F61021 because project rules require explicit user authorization before game-directory synchronization. Game-internal behavior and final package launch were not newly observed in this task.
- next_action: user may stop the old Workbench instance, launch the final preview package, and explicitly authorize game-directory sync only after Bannerlord is closed; then perform game-log acceptance separately.

## 2026-08-19 short-task checkpoint: Free Preview live smoke
- task_id: PERSONA-FREE-PREVIEW-LIVE-11
- status: done
- files_changed: none
- result: Started the final packaged executable from PersonaWorkbench-FreePreview-final-20260819 on 127.0.0.1:51337 (PID 2628). Confirmed the package serves index.html, app.js, and site.css; executed the real HTTP preview path with a populated Persona document and received valid canonical [PERSONA_LOAD] DSL; invalid unregistered tags returned HTTP 400 without DSL output.
- provider_smoke: Local Ollama at 127.0.0.1:11434 responded to a real OpenAI-compatible chat request with structured JSON using qwen2.5:latest. The final Workbench Provider UI request path was not executed because its one-time bootstrap grant was consumed by the launched browser session and no browser automation driver is available in this environment.
- runtime: final packaged process remains running and listening on 127.0.0.1:51337; server stderr is empty; server stdout shows successful bootstrap, static asset, preview, and validation requests.
- limitations: save/approve UI clicks and Workbench-mediated Provider draft generation still need one interactive browser pass; Bannerlord game-internal validation and game-directory synchronization remain separate.
- next_action: use the already-open final Workbench page to run save draft -> reopen -> approve, then perform one real Provider draft generation if a valid local/cloud model is selected.

## 2026-08-19 short-task checkpoint: Provider confirmation false error fix
- task_id: PERSONA-PROVIDER-SESSION-12
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/WorkbenchSessionManager.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/app.js; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs; artifacts/PersonaWorkbench-FreePreview-final-20260819-r1/*
- root_cause: logs showed repeated HTTP 401 on /api/provider/confirm-cloud. The frontend collapsed every non-2xx response into “端点确认失败，请检查地址”, so an expired one-time workbench session was falsely reported as a bad DeepSeek URL.
- fix: active authorized requests now renew the session idle timeout; the confirmation handler reports 401 as an expired local session and preserves endpoint-specific error codes for actual validation failures.
- verification: PersonaWorkbench.Web.Tests PASS; PersonaWorkbench.Core.Tests PASS; r1 package publish and whitelist/manifest packaging PASS.
- runtime: old PID 2628 was stopped by its exact package stop script; r1 package is running on 127.0.0.1:51337 as PID 1396.
- user_action: close/refresh the stale Workbench page and open the newly launched page from r1. Do not reuse a page that was opened before the fix.
- limitation: no real DeepSeek request was sent because no API key was used in this repair turn; the endpoint policy accepts HTTPS and the official DeepSeek OpenAI-compatible base is https://api.deepseek.com, with /chat/completions as the operation path.

## 2026-08-19 short-task checkpoint: DeepSeek empty candidate fix
- task_id: PERSONA-DEEPSEEK-THINKING-13
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftContract.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/app.js; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs; artifacts/PersonaWorkbench-FreePreview-final-20260819-r2/*; Desktop/Persona Workbench.lnk
- root_cause: the observed provider.candidate_size_invalid represented an empty message.content, not an oversized candidate. With DeepSeek reasoning-capable models, thinking can consume the bounded output budget and leave no structured candidate content.
- fix: official api.deepseek.com draft requests now add thinking={type:disabled}; empty candidate content reports provider.candidate_empty separately; UI explains the recovery instead of calling it oversized.
- verification: PersonaWorkbench.Web.Tests PASS including DeepSeek request-body and empty-candidate regressions; PersonaWorkbench.Core.Tests PASS; static provider fix audit PASS; r2 package manifest PASS 19 entries; HTTP root PASS.
- runtime: r1 PID 34484 stopped; r2 PID 9524 running from PersonaWorkbench-FreePreview-final-20260819-r2 on 127.0.0.1:51337; desktop shortcut updated to r2.
- limitation: real DeepSeek generation still requires the user to re-enter the transient API key in the newly launched r2 session; no key was retained or reused by the repair process.
- next_action: user retries the same description in the newly opened r2 page after confirming the endpoint and entering the session key.

## 2026-08-19 short-task checkpoint: Provider JSON compatibility fix
- task_id: PERSONA-PROVIDER-JSON-COMPAT-14
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftContract.cs; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs; artifacts/PersonaWorkbench-FreePreview-final-20260819-r3/*; Desktop/Persona Workbench.lnk
- observed_errors: provider.candidate_json_invalid; provider.candidate_field_invalid
- root_cause: the strict parser accepted only bare JSON and required the Provider to invent a stable id. DeepSeek sometimes returned a single JSON code fence or null id; both were rejected before the actual content validation stage. The bounded draft output was also raised from 2048 to 4096 tokens to reduce truncation risk.
- fix: normalize one outer ```json fence only; assign free.generated.persona locally when id is null; keep non-null id validation, unknown-field rejection, registered-tag checks, evidence gates, and approval isolation unchanged; request max_tokens=4096.
- verification: PersonaWorkbench.Web.Tests PASS including fenced JSON and local-ID regressions; PersonaWorkbench.Core.Tests previously PASS; r3 package generated and started; root HTTP 200; manifest entries=19; desktop shortcut points to r3.
- runtime: r2 PID 9524 stopped; r3 PID 58732 running on 127.0.0.1:51337.
- next_action: retry DeepSeek from the newly opened r3 page. If it still fails, report the new single error code; do not reuse old failure entries as current evidence.

## 2026-08-19 short-task checkpoint: Provider narrative field compatibility
- task_id: PERSONA-PROVIDER-FIELD-COMPAT-15
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftContract.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/app.js; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs; artifacts/PersonaWorkbench-FreePreview-final-20260819-r4/*; Desktop/Persona Workbench.lnk
- observed_error: provider.candidate_field_invalid after r3.
- review: independent read-only review confirmed the shared error could only originate from displayName, core, or identityFacts and identified ordinary JSON newline/tab characters as the safest high-probability compatibility issue; arbitrary object/array coercion was rejected as unsafe because it would break evidence-path semantics.
- fix: split field errors into provider.candidate_display_name_invalid, provider.candidate_core_invalid, provider.candidate_identity_invalid, and provider.candidate_id_invalid; allow empty string/null id to use local free.generated.persona; normalize CR/LF/TAB and repeated whitespace in textual candidate fields; clarify that core must be one non-empty string and only the three authored list fields may be arrays.
- verification: PersonaWorkbench.Web.Tests PASS including multiline normalization; PersonaWorkbench.Core.Tests PASS; r4 package generated and HTTP root PASS.
- runtime: r3 PID 58732 stopped; r4 PID 49836 running on 127.0.0.1:51337; desktop shortcut points to r4.
- next_action: retry once in the newly opened r4 page. Any remaining failure now identifies the exact offending field rather than returning candidate_field_invalid.

## 2026-08-19 short-task checkpoint: Provider profile text arrays
- task_id: PERSONA-PROVIDER-PROFILE-TEXT-16
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftContract.cs; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs; artifacts/PersonaWorkbench-FreePreview-final-20260819-r5/*; Desktop/Persona Workbench.lnk
- observed_error: provider.candidate_profile_text_invalid.
- root_cause: reactionProfile and commitmentProfile contain naturally plural narrative fields such as sensitiveConditions, conditionalResponses, priorityOrder, and protectedValues. DeepSeek may return these as bounded string arrays, while the parser accepted only one string or null.
- fix: profile narrative fields now accept one string, null, or an array of up to eight non-empty strings; arrays are normalized deterministically with Chinese semicolon separators. Objects, numbers, booleans, oversized values, and empty array items remain rejected. Evidence is still required for every populated profile field.
- verification: PersonaWorkbench.Web.Tests PASS including profile text array normalization; r5 package generated and HTTP root PASS.
- runtime: r4 PID 49836 stopped; r5 PID 59908 running on 127.0.0.1:51337; desktop shortcut points to r5.
- next_action: retry in the newly opened r5 page after re-entering the transient API key.

## 2026-08-19 short-task checkpoint: Evidence-tolerant Provider drafts
- task_id: PERSONA-PROVIDER-EVIDENCE-17
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftContract.cs; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs; artifacts/PersonaWorkbench-FreePreview-final-20260819-r6/*; Desktop/Persona Workbench.lnk
- observed_error: provider.candidate_evidence_missing.
- root_cause: DeepSeek produced a structurally valid candidate but populated one or more fields without the required evidence key. Rejecting the complete response made evidenced fields unusable even though the safety rule only requires unsupported fields to remain unset.
- fix: missing-evidence fields are pruned before final validation. Single tags are removed individually; authored text/list fields are cleared; profile groups with any missing evidence are cleared conservatively as a unit. Evidence entries for removed fields are also discarded. Evidence that exists but is not a quotation from the source still fails closed.
- safety: unknown fields, invalid types, unregistered tags, out-of-range axes, verbatim prompt copies, false evidence quotes, approval metadata, and orphaned evidence remain rejected.
- verification: PersonaWorkbench.Web.Tests PASS including partial-evidence pruning; PersonaWorkbench.Core.Tests PASS; r6 package generated; HTTP root PASS; manifest entries=19.
- runtime: r5 PID 59908 stopped; r6 PID 4384 running on 127.0.0.1:51337; desktop shortcut points to r6.
- next_action: retry from the newly opened r6 page after re-entering the transient API key.

## 2026-08-19 short-task checkpoint: Persona Workbench AI feasibility audit
- task_id: PERSONA-AI-FEASIBILITY-AUDIT-18
- status: done
- files_changed: task queue checkpoint only; no runtime code changed in this audit.
- conclusion: AI Provider transport and local safety gates are viable, but the current one-shot “free description -> large evidence-backed Persona JSON” contract is not reliable enough for release. Recent candidate_json_invalid, candidate_field_invalid, candidate_profile_text_invalid, and candidate_evidence_missing errors demonstrate contract mismatch rather than a single isolated parser bug.
- decision: pause iterative parser widening. Treat AI as optional draft assistance until the request contract is split into a small deterministic first pass and local validation/assembly; keep manual editing and deterministic DSL generation as the authoritative path.
- next_action: design a provider capability matrix and two-stage generation contract before further implementation. Required gates: minimal response schema, provider-specific request adapter, field-level diagnostics, evidence matching done locally, and a no-AI/manual fallback that remains first-class.

## 2026-08-19 short-task checkpoint: Persona text expansion split
- task_id: PWB-AI-01-TEXT-EXPANSION
- status: done
- files_changed: tools/persona-workbench/src/PersonaWorkbench.Web/ProviderTextExpansionContract.cs; ProviderDraftActionService.cs; Program.cs; wwwroot/index.html; wwwroot/app.js; tests/PersonaWorkbench.Web.Tests/Program.cs.
- result: the primary “扩展人物形象” action now uses a dedicated prose-only Provider contract. The model receives no Persona field schema, tag registry, numeric-axis contract, approval metadata, or evidence object. Successful output first lands in an isolated editable text area and does not modify Persona fields or invoke DSL preview. Applying it to core personality requires a separate explicit user action.
- compatibility: the old /api/provider/generate-draft large-JSON endpoint remains server-side only for compatibility and regression coverage, but the primary browser flow no longer calls it.
- safety: existing HTTPS/loopback endpoint policy, cloud confirmation, transient session key, redirect rejection, timeout, cancellation, 429 cooldown, DNS recheck, failure quarantine, and no-auto-retry behavior remain active. Expansion rejects empty, unchanged, oversized, JSON/code-fenced, or [PERSONA_LOAD] output.
- verification: PersonaWorkbench.Web Release build PASS 0 warnings/0 errors; PersonaWorkbench.Web.Tests PASS including prose-only request, structured-output rejection, session controls, and UI isolation; PersonaWorkbench.Core.Tests PASS. Node.js is not installed, so a standalone `node --check` was unavailable; browser-script wiring is covered by the Web test source assertions and the C# build does not validate JavaScript syntax.
- runtime: current 127.0.0.1:51337 r6 process and desktop shortcut were not replaced in this short task. No game files were touched and no Bannerlord validation was performed.
- next_action: PWB-AI-02 — design and implement the second independent AI action: confirmed character text -> constrained Persona DSL candidate -> local parser/validator -> canonical renderer. Do not reuse the large Persona JSON contract as the primary path.

## 2026-08-19 short-task checkpoint: Persona text to DSL conversion
- task_id: PWB-AI-02-DSL-CONVERSION
- status: done
- files_changed: tools/persona-workbench/src/PersonaWorkbench.Core/PersonaDslCandidateParser.cs; PersonaWorkbench.Core.Tests/Program.cs; tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDslConversionContract.cs; ProviderDraftActionService.cs; Program.cs; wwwroot/index.html; wwwroot/app.js; tests/PersonaWorkbench.Web.Tests/Program.cs.
- result: added an independent confirmed-text -> Provider DSL candidate -> local Parser/Validator -> canonical Renderer path. The Provider receives only the confirmed character text and a constrained DSL instruction. Local ID/display name are injected by the action service; Provider-owned ID, NAME, TEMPLATE_VERSION, STATUS, SOURCE_PACK_ID, approval metadata, unknown fields, unregistered tokens, invalid axes, and unsupported sections are rejected.
- ui: added “将确认文本转换为 DSL”. The result writes only to the read-only DSL preview and does not save or approve the Persona. Existing local `/api/preview` remains the no-AI manual path.
- compatibility: `/api/provider/generate-draft` and its large JSON parser remain server-side compatibility coverage only; neither primary AI button calls it.
- verification: PersonaWorkbench.Core.Tests PASS; PersonaWorkbench.Web.Tests PASS including candidate canonicalization, invalid candidate quarantine, restricted request prompt, UI endpoint wiring; Web Release build PASS 0 warnings/0 errors.
- runtime: current 127.0.0.1:51337 r6 process and desktop shortcut were not replaced. Source changes are not yet packaged into a new preview artifact. No Bannerlord files were touched; no game validation performed.
- known limitation: current JS runtime syntax was not independently checked because Node.js is unavailable; source-level Web assertions cover endpoint and UI wiring. Provider conversion was tested with deterministic fake HTTP responses, not yet against live Ollama or DeepSeek from the newly built source.
- next_action: PWB-AI-03 — package a new preview from source, launch it on the existing workbench port only after preserving/stopping the old process safely, then run one live local Ollama expansion and DSL conversion smoke. Do not claim cloud or game validation from that smoke.

## 2026-08-19 short-task checkpoint: Persona Workbench r8 package and live Ollama audit
- task_id: PWB-AI-03-PACKAGE-LIVE-SMOKE
- status: done
- files_changed: tools/persona-workbench/src/PersonaWorkbench.Web/ProviderTextExpansionContract.cs; tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDslConversionContract.cs; tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs; tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-final-20260819-r8/*; Desktop/Persona Workbench.lnk; this task queue checkpoint.
- package: `PersonaWorkbench-FreePreview-final-20260819-r8` contains 19 manifest entries and 19 packaged payload files; no source, PDB, map, log, test, key-like, or secret-like file was included. `PersonaWorkbench.Web.exe` SHA-256=`3F3736A1EA9A60D2EA106EBF8CC306FE43DB1166C8ED609228313FCF0124369D`.
- runtime: r7 was stopped by exact executable path; r8 is running on `127.0.0.1:51337`. The desktop shortcut now targets r8. Bannerlord was not launched and no game module was modified.
- live_provider: one local Ollama smoke used `http://127.0.0.1:11434/v1/chat/completions` with model `qwen2.5:latest`. Text expansion returned editable prose, but still invented unsupported hard facts such as exact height, combat history, beliefs, and relationship advice. This output therefore remains creative assistance rather than trusted identity fact input.
- live_conversion: direct Provider-authored DSL improved after prompt tightening but still invented unsupported syntax/tokens such as `BEHAVIOR_OBSERVES_BEFORE_ACTING_STRONG` and unrelated preferences. The strict local parser correctly rejected the result with `persona.dsl_candidate_entry_unknown`; the parser was not widened to accept model-invented grammar.
- conclusion: transport, isolation, local validation, canonical rendering, and failure quarantine are viable; direct DSL authorship is not reliable enough for the default local-model path. No successful end-to-end Persona DSL conversion is claimed from this live smoke, and no cloud Provider validation is claimed.
- next_action: PWB-AI-04 — replace the default confirmed-text conversion request with a small intermediate JSON contract, parse it strictly into `PersonaDocument`, then render canonical `[PERSONA_LOAD]`. Keep direct DSL parsing only as an advanced/import compatibility path.

## 2026-08-19 short-task checkpoint: Persona intermediate JSON conversion
- task_id: PWB-AI-04-INTERMEDIATE-JSON
- status: done
- files_changed: tools/persona-workbench/src/PersonaWorkbench.Core/PersonaIntermediateCandidateParser.cs; tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDslConversionContract.cs; tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftActionService.cs; tools/persona-workbench/tests/PersonaWorkbench.Core.Tests/Program.cs; tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs; this task queue checkpoint.
- result: the default confirmed-text conversion no longer asks the Provider to author Persona DSL. It now requests one small intermediate JSON object, parses it strictly into a local `PersonaDocument`, validates it, and renders canonical `[PERSONA_LOAD]` locally. The existing strict direct-DSL parser remains available as an import/compatibility component but is no longer used by the default Provider conversion action.
- authority: confirmed user text is always copied into `PersonaDocument.Core` and cannot be replaced by Provider output. Provider-owned ID, display name, schema, status, template version, source pack, approval state, DSL metadata, and arbitrary fields are unavailable in the intermediate contract.
- grounding: optional summary, identity, public/private/contradiction, reaction, and commitment text must be null/empty or an exact substring of the confirmed source after whitespace normalization. Invented history, measurements, preferences, beliefs, advice, or other unsupported prose therefore fails with `persona.intermediate_text_not_in_source`.
- structure_gate: only 25 registered axis IDs with values `-2..2|null`, registered tag IDs, two reaction fields, and five commitment fields are accepted. Unknown/duplicate fields, unknown axes, invalid values, duplicate/unregistered tags, malformed JSON, oversized candidates, and unsupported nesting fail closed and are quarantined by the existing action service.
- provider_request: request size was reduced to `max_tokens=1200` and `temperature=0`; endpoint confirmation, transient key, HTTPS/loopback policy, DNS recheck, redirect rejection, timeout, cancellation, single-flight, and 429 cooldown behavior remain unchanged.
- verification: red phase failed on missing intermediate parser and `CandidateJson` as expected. Green phase: PersonaWorkbench.Core.Tests PASS 23 checks; PersonaWorkbench.Web.Tests PASS 58 checks. One parallel Core build attempt was temporarily blocked by Microsoft Defender holding the shared output DLL; the serial rerun passed. Static search found no remaining `CandidateDsl`, direct-DSL system prompt, or `provider.dsl_candidate` reference in the default source/test path.
- limitations: source changes are not yet packaged or running. No real Ollama result has been accepted through the new contract yet; no cloud Provider or Bannerlord validation is claimed.
- next_action: PWB-AI-05 — build and package r9, switch the running preview and desktop shortcut safely, then run one real local Ollama conversion smoke. Success requires either canonical DSL output or an explicit fail-closed diagnostic without form overwrite; do not widen the parser to accommodate invented fields.


## 2026-08-19 short-task checkpoint: Persona Provider JSON and semantic evidence gate
- task_id: PWB-AI-05-JSON-SEMANTIC-GATE
- status: done
- files_changed: tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDslConversionContract.cs; tools/persona-workbench/src/PersonaWorkbench.Core/PersonaIntermediateCandidateParser.cs; tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs; tools/persona-workbench/tests/PersonaWorkbench.Core.Tests/Program.cs; artifacts/PersonaWorkbench-FreePreview-final-20260819-r15/*; Desktop/Persona Workbench.lnk; this task queue checkpoint.
- syntax_root_cause: real local `qwen2.5:latest` output omitted one closing object brace while otherwise following the sparse contract. The Provider transport therefore returned content successfully, but the strict local parser correctly rejected malformed JSON.
- syntax_fix: the conversion request now sends `response_format={"type":"json_object"}` while retaining the existing JSON-only prompt, `temperature=0`, bounded tokens, endpoint controls, transient key isolation, cooldown, cancellation, and strict unknown-field rejection. DeepSeek official API documentation confirms the same Chat Completions JSON Output parameter is supported; no live cloud request is claimed.
- semantic_root_cause: after JSON syntax became valid, the local model still attached unrelated grounded substrings to valid indices, including body size as expression warmth and combat/romantic phrases as legacy trigger or boundary flags. Substring grounding alone could not detect this semantic mismatch.
- semantic_fix: all 25 disposition axes now require a local polarity-specific evidence cue before assignment. The two legacy flags require explicit matching semantics; public humiliation also requires an explicit retaliation cue. Invalid optional items are pruned individually, while confirmed user text remains the authoritative core and still produces canonical DSL.
- verification: PersonaWorkbench.Core.Tests PASS 25 checks, including the real misclassification regression; PersonaWorkbench.Web.Tests PASS 60 checks, including JSON response mode. Real local Ollama smoke against `http://127.0.0.1:11434/v1/chat/completions` with `qwen2.5:latest` returned valid JSON, parsed successfully, and produced canonical `[PERSONA_LOAD]`; only `TRAIT_PRIDE_PROUD_STRONG` survived from the sample, while the unrelated warmth and both invalid flags were removed.
- package: r15 manifest entries=19, payload files=19, manifest errors=0, forbidden files=0. PersonaWorkbench.Core.dll SHA-256=`44100CECEC8F899677E0A7A9A545691A32AA218F34E033E2F35551D0D9C0E465`; PersonaWorkbench.Web.dll SHA-256=`1B7EEA48E73FB10079F6D6D037897CBAFA15528098D57E76A212738470D598FF`.
- runtime: r15 is running on `127.0.0.1:51337`; the desktop shortcut points to r15. Bannerlord was not launched and no game module was modified.
- limitation: local lexical evidence gates intentionally favor precision over recall. Descriptions without a recognized direct cue remain in Persona core but may not receive an axis token until the player edits the axis or the cue registry is expanded. Cloud DeepSeek behavior remains pending one user-side live generation test.
- next_action: user may retry the two AI actions from the current r15 page. If cloud generation fails, record the single current error code and response time; do not weaken the local schema or semantic gates.
## 2026-08-19 short-task checkpoint: Persona Workbench idempotent launch and session recovery
- task_id: PWB-SESSION-06-IDEMPOTENT-LAUNCH
- status: done
- observed_error: the browser repeatedly displayed `本地保存会话未就绪。请从工作台启动链接打开页面。`; the r15 stderr log also recorded `address already in use`.
- root_cause: the old launcher always started a new server process. When an existing instance already owned port 51337, the duplicate process exited, but the launcher accepted the old server's HTTP 200 as proof that the new process had started and overwrote the PID file with the dead duplicate PID. The old server did not open a new tokenized page, while direct or refreshed root pages had no bootstrap token.
- fix: `WorkbenchSessionManager` now issues bounded, single-use launch tokens and reuses the active session grant for repeated launches. The server exposes `/api/session/launch-token`; the browser automatically obtains and exchanges a fresh token when the URL hash is absent or stale. The launcher now passes `--no-browser`, finds and reuses only the exact executable path, checks `HasExited`, repairs stale PID files, rejects a different process on port 51337, and opens the root page itself.
- regression: Web tests now PASS 61 checks, including repeated launch tokens and launcher/browser recovery gates; Core tests PASS 25 checks. Release build completed with 0 warnings and 0 errors.
- runtime_verification: r16 is running as the only PersonaWorkbench process, PID 31500. Direct browser requests in the server log completed `GET /` -> `POST /api/session/launch-token` -> `POST /api/session/bootstrap` -> authorized `GET /api/provider/failures`, all successfully. A second launcher invocation kept process count=1 and PID=31500. A deliberately stale PID file was repaired back to 31500 without an address-in-use error; runtime stderr is empty.
- package: `PersonaWorkbench-FreePreview-final-20260819-r16` has 19 manifest entries, 19 payload files, and 0 hash errors. PersonaWorkbench.Core.dll SHA-256=`44100CECEC8F899677E0A7A9A545691A32AA218F34E033E2F35551D0D9C0E465`; PersonaWorkbench.Web.dll SHA-256=`62976F558EB9CCBFD169B9B1AC4DF964C0EDB094608E85AA6F70C089E54745DD`.
- runtime: desktop shortcut points to r16. Bannerlord was not launched and no game module was modified.
- user_action: use the newest r16 browser tab or reopen the desktop shortcut once. Older r15 tabs still contain the old JavaScript and should be closed or refreshed.
## 2026-08-19 short-task checkpoint: Persona Workbench bug brainstorm and boundary audit
- task_id: PWB-AUDIT-07-BUG-BRAINSTORM
- status: done
- scope: read-only audit; no production source, game directory, or release package modified.
- verification: existing `PersonaWorkbench.Core.Tests` PASS 25 checks; existing `PersonaWorkbench.Web.Tests` PASS 61 checks. Three independent temporary audit harnesses exercised session, file, Provider, and DSL boundaries.
- confirmed_findings:
  - `BUG-SESSION-01` P1: `WorkbenchSessionManager._bootstrapOrder` retains consumed token entries indefinitely; 2000 issue/exchange cycles retained 2001 entries.
  - `BUG-SESSION-02` P1: unauthorized document load/save/approve returns HTTP 401 with empty body; browser file paths call `response.json()` before handling `response.ok`, so expired sessions surface as a generic client exception rather than session recovery.
  - `BUG-SESSION-03` P1: stop script with a stale/dead PID reports process already exited and leaves the live exact-path Workbench process running.
  - `BUG-FILE-01` P1: `WorkspaceDocumentService` lets `PersonaWorkspace.Open`/file I/O exceptions escape for root path that is a file, invalid path characters, and a locked target file; API route can become HTTP 500 instead of a stable workspace error response.
  - `BUG-FILE-02` P2: document-name validation accepts Windows device names such as `CON.persona.json`, `AUX.persona.json`, `NUL.persona.json`, and accepts a NUL-containing candidate; reserved-name and control-character rejection is incomplete.
  - `BUG-PROVIDER-01` P1: an already-cancelled Provider request still performs DNS resolution and sends one HTTP call; cancellation is only observed after the request starts.
  - `BUG-PROVIDER-02` P1: unexpected endpoint-resolver exceptions (for example `InvalidOperationException`) escape `ProviderDraftClient.GenerateAsync` instead of becoming a bounded `provider.transport_error`/resolution failure.
  - `BUG-PROVIDER-03` P2: `ProviderDraftActionService` quarantine failures have no retention bound; 2000 failed calls retained 2000 records.
  - `BUG-DSL-01` P1: duplicate DSL axis assignments silently overwrite; opposite values in one section are accepted and the last one wins.
  - `BUG-DSL-02` P1: duplicate DSL facet-strength assignments silently overwrite; last strength wins instead of rejecting the candidate.
  - `BUG-DSL-03` P1: intermediate evidence matching uses substring search; `间接表达，且不采用直接方式` was accepted as positive directness, proving a polarity collision. English collision candidates (`informal/formal`, `non-tradeable/tradeable`, `swarm/warm`) remain a registry risk and need explicit regression cases after the matcher fix.
  - `BUG-DSL-04` P2: nested unknown intermediate fields are silently discarded while the candidate remains valid; this can hide Provider schema drift instead of surfacing a diagnostic.
- passed_boundaries: corrupt Persona JSON, oversized document, corrupt journal, missing recovery data, workspace lock, 400/401/403/429/500/503 classification, Retry-After cooldown, no automatic Provider retry, single-flight busy gate, cancellation-vs-timeout after request start, valid Provider response, source-order extraction, and existing canonical DSL/golden fixtures.
- not_claimed: no Bannerlord launch, no game-directory sync, no cloud live request, and no production fix in this audit.
- next_action: create the first focused repair batch for `BUG-SESSION-01/02/03` and rerun only the affected Web/session regression checks before proceeding to file/Provider/DSL fixes.

## 2026-08-19 short-task checkpoint: Persona Workbench session token bookkeeping repair
- task_id: PWB-SESSION-07-TOKEN-BOOKKEEPING
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/WorkbenchSessionManager.cs; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs
- fix: replace the bootstrap order Queue with a bounded LinkedList; consuming a token now removes its order node, and eviction removes the oldest node exactly once.
- verification: PersonaWorkbench.Web.Tests PASS 62 checks, including 2000 issue/exchange cycles with retained order entries bounded at <=8.
- runtime: current packaged/running Workbench was not restarted or repackaged in this short task; source/test fix is verified only.
- next_action: PWB-SESSION-08 — normalize document-route 401 responses and update app.js load/save/approve to handle expired sessions without response.json() failure; then test the three routes.

## 2026-08-19 short-task checkpoint: Persona Workbench remaining bug batches completed
- task_id: PWB-SESSION-08-401-AND-STOP-SCRIPT
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/Program.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/app.js; _houkai_merge/AWAKE/tools/persona-workbench/stop-free-preview.ps1; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs
- result: protected document routes now return structured session.unauthorized JSON; the browser handles expired sessions without blindly parsing an empty 401 body; the stop script resolves the exact packaged executable path, fails closed on ambiguous multi-instance state, and removes stale PID state after a successful stop.
- verification: PersonaWorkbench.Web.Tests PASS; session, 401, and stop-script regression coverage included.
- runtime: no Bannerlord launch and no game-directory overwrite.

## 2026-08-19 short-task checkpoint: Persona Workbench file boundary repair
- task_id: PWB-FILE-09-WORKSPACE-BOUNDARIES
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/WorkspaceDocumentService.cs; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs
- result: root-file, invalid-path, access-denied, general I/O, Windows device-name, control-character, trailing-space, and trailing-dot failures are classified into stable workspace error codes instead of escaping as generic HTTP 500 failures.
- verification: PersonaWorkbench.Web.Tests PASS; document-name and filesystem boundary regressions included.
- runtime: no Bannerlord launch and no game-directory overwrite.

## 2026-08-19 short-task checkpoint: Persona Workbench Provider resilience repair
- task_id: PWB-PROVIDER-10-CANCEL-RESOLVER-QUARANTINE
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftContract.cs; ProviderTextExpansionContract.cs; ProviderDslConversionContract.cs; ProviderDraftActionService.cs; tests/PersonaWorkbench.Web.Tests/Program.cs
- result: already-cancelled requests stop before DNS/HTTP; unexpected endpoint resolver failures are converted into stable Provider failures; quarantined failure retention is bounded at 128 records; cancellation and timeout remain distinct and no automatic retry is introduced.
- verification: PersonaWorkbench.Web.Tests PASS; cancellation HTTP-call count, resolver exception classification, and 200-failure bounded-retention regressions included.
- limitation: no live cloud DeepSeek request was issued in this repair batch; fake-provider and local contract tests passed.

## 2026-08-19 short-task checkpoint: Persona Workbench DSL parser hardening
- task_id: PWB-DSL-11-DUPLICATE-AND-POLARITY
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Core/PersonaDslCandidateParser.cs; PersonaIntermediateCandidateParser.cs; tests/PersonaWorkbench.Core.Tests/Program.cs
- result: duplicate semantic axes and facet strengths are rejected; nested unknown reaction/commitment fields are rejected; evidence matching now honors ASCII word boundaries and simple Chinese negation, preventing indirect/direct, informal/formal, swarm/warm, and non-tradeable/tradeable collisions while preserving valid cautious-promise and non-tradeable evidence.
- verification: PersonaWorkbench.Core.Tests PASS with 33 checks, including 8 new focused regressions.
- runtime: no Bannerlord launch and no game-directory overwrite.

## 2026-08-19 short-task checkpoint: Persona Workbench final offline validation and package
- task_id: PWB-RELEASE-12-FINAL-OFFLINE-VALIDATION
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-final-20260819-r17/*; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Core.Tests/Program.cs; this task queue
- result: source fixes are packaged as the new free-preview artifact. The package contains no source, test binaries, PDB, secret, or environment files and has a regenerated SHA-256 manifest.
- verification: Core Release build PASS 0 warnings/0 errors; Web Release build PASS 0 warnings/0 errors; PersonaWorkbench.Core.Tests PASS 33 checks; PersonaWorkbench.Web.Tests PASS all checks; PACKAGE_CHECK_OK files=19; STRUCTURE_CHECK_OK json=3 source_markers=16 package_files=20; package DLL SHA-256 PersonaWorkbench.Web.dll=CC975D8F2956E11914F83CED4DBEEAE41DFA9A7665CEB4CE309A52696706BD4F.
- package: _houkai_merge/AWAKE/tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-final-20260819-r17
- not_claimed: no Bannerlord launch, no game-directory synchronization, no live cloud generation, and no interactive browser acceptance pass in this batch.
- next_action: when convenient, start r17 manually and perform one user-side save/reload/approve smoke plus one live Provider expansion and DSL conversion. Keep cloud API keys transient and do not reuse stale browser tabs.
## 2026-08-19 short-task checkpoint: Persona Workbench r17 runtime replacement
- task_id: PWB-RUNTIME-13-R17-REPLACEMENT
- status: done
- action: stopped the exact r16 executable process (PID 31500), started r17 on 127.0.0.1:51337 (PID 53864), and updated OneDrive Desktop\Persona Workbench.lnk to r17\start-free-preview.ps1.
- verification: root HTTP 200; launch-token and bootstrap succeeded; authenticated preview generated DSL; unauthenticated document load returned structured 401 `session.unauthorized` JSON instead of an empty body; port 51337 is owned by the r17 executable.
- package: _houkai_merge/AWAKE/tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-final-20260819-r17
- limitation: no live cloud Provider request or Bannerlord game validation was performed during replacement.
- next_action: use the newly opened r17 page for one user-side save/reload/approve smoke and, if desired, a live local/cloud Provider test.
## 2026-08-19 short-task checkpoint: Provider intermediate JSON wrapper compatibility
- task_id: PWB-PROVIDER-14-INTERMEDIATE-JSON-WRAPPER
- status: done
- root_cause: Provider conversion accepted only content whose trimmed text started with `{` and ended with `}`. A valid JSON object wrapped by short explanatory text was classified as `provider.intermediate_candidate_format_invalid` before the local intermediate schema parser could validate it.
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDslConversionContract.cs; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs; _houkai_merge/AWAKE/tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-final-20260819-r18/*; OneDrive/Desktop/Persona Workbench.lnk
- fix: recover one balanced, syntactically valid JSON object from Provider content, while retaining strict local root-field, evidence, axis, duplicate, and approval validation. BOM trimming and existing single-fence normalization remain supported. No Provider-authored metadata or invented fields are accepted by the recovery step alone.
- verification: the new wrapped-content regression passed; PersonaWorkbench.Web.Tests PASS; PersonaWorkbench.Core.Tests PASS; Core/Web Release builds PASS with 0 warnings and 0 errors; R18_PACKAGE_OK entries=19; running r18 root HTTP 200, bootstrap PASS, preview PASS, structured unauthorized JSON PASS.
- runtime: r17 PID 53864 was stopped; r18 is running on 127.0.0.1:51337 as PID 20504. Desktop shortcut now targets r18. Bannerlord was not launched and no game directory was modified.
- limitation: real DeepSeek/Ollama conversion was not replayed automatically; restarting the Workbench clears the transient Provider Key. User must set the key again before testing cloud conversion.
- next_action: on the fresh r18 page, set the transient Provider Key, confirm the endpoint if cloud, and retry “将确认文本转换为 DSL”. If it still fails, capture the single newest error code and timestamp; do not reuse the old page or auto-retry.
## 2026-08-19 short-task checkpoint: Free-text DSL fallback and local-model capability assessment
- task_id: PWB-PROVIDER-15-LOCAL-FALLBACK-AND-OLLAMA-ASSESSMENT
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Core/PersonaIntermediateCandidateParser.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDslConversionContract.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDraftActionService.cs; _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/wwwroot/app.js; tests/PersonaWorkbench.Core.Tests/Program.cs; tests/PersonaWorkbench.Web.Tests/Program.cs; artifacts/PersonaWorkbench-FreePreview-final-20260819-r19/*; OneDrive/Desktop/Persona Workbench.lnk
- result: free-form text remains the only required user input. Provider conversion now accepts one-level wrapped or string-encoded JSON objects, reports missing JSON objects distinctly, and preserves strict local schema rejection for invalid fields. If Provider response structure is unusable, the service quarantines the failure and returns a successful deterministic local heuristic DSL with UsedLocalFallback=true and WarningCode; the UI explicitly warns that the result is a basic fallback requiring manual review. Invalid parsed schema is not silently accepted as a fallback.
- local_model_assessment: Ollama HTTP endpoint 127.0.0.1:11434 is reachable and exposes gpt-oss:20b with digest 17052f91a42e97930aa6e28a6c6c06a983e6a58dbb00434885a0cf5313e376f7. A medium free-text semantic extraction batch timed out at 180 seconds with no local commit and was paused as in_doubt; it was not replayed. A low mechanical DSL gate returned the correct ok verdict in about 20 seconds but omitted required content_hash, so cloud validation rejected the result and paused the batch as INVALID_MODEL_OUTPUT.
- conclusion: local gpt-oss:20b is suitable for optional offline health checks, deterministic structural screening, keyword/count checks, and candidate pre-screening only. It is not suitable as the interactive authoritative free-text-to-DSL semantic converter under the current latency and contract reliability. The Workbench fallback therefore uses deterministic local code, not unchecked local-model output.
- verification: Core.Tests PASS 34 checks; Web.Tests PASS all checks including wrapped JSON, string-encoded JSON, precise object-missing diagnostics, heuristic fallback, and UI disclosure; Web Release build PASS 0 warnings/0 errors; R19_PACKAGE_OK entries=19; runtime r18 PID 16684 was stopped and r19 is running on 127.0.0.1:51337; root HTTP 200, bootstrap PASS, preview PASS, structured unauthorized JSON PASS; desktop shortcut targets r19.
- limitation: no real cloud request was replayed after r19 replacement; the current transient Provider Key must be entered again after restart. No Bannerlord launch or game-directory synchronization was performed.
- next_action: use fresh r19 page, set Provider key/confirmation if cloud, retry conversion once. If Provider structure fails again, the page should retain the generated local fallback DSL and show the warning code rather than report total failure.
## 2026-08-19 short-task checkpoint: Persona DSL template budget repair
- task_id: PWB-PROVIDER-16-TEMPLATE-BUDGET-TRIM
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Core/CanonicalPersonaTemplateGenerator.cs; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Core.Tests/Program.cs; _houkai_merge/AWAKE/tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-final-20260819-r20/*; OneDrive/Desktop/Persona Workbench.lnk
- root_cause: CanonicalPersonaTemplateGenerator treated the 4096-byte prompt budget as a hard failure and threw persona.template_budget_exceeded after assembling otherwise valid core, identity, boundary, and optional text.
- fix: deterministic budget handling now removes low-priority optional sections first, then optional descriptions, then public/private background, while retaining core, stable identity, trigger/boundary lines, and trimming long text only as a last resort. Normal inputs under budget keep the exact canonical output.
- verification: Core.Tests PASS 35 checks including oversized-template retention; Web.Tests PASS all checks; Core/Web Release build PASS 0 warnings/0 errors; r20 package manifest PASS 19 entries with 0 SHA-256 mismatches; live r20 loopback root HTTP 200; oversized /api/preview returned HTTP 200 with 4095-byte DSL retaining PERSONALITY_CORE, SELF_IDENTITY, and BOUNDARY_NO_EMPTY_PROMISES.
- runtime: r19 stopped; r20 running on 127.0.0.1:51337 as PID 61228; desktop shortcut targets r20. Bannerlord was not launched and no game directory was modified.
- limitation: no new live DeepSeek request was issued during this repair, so Provider-side output quality remains separately unverified; the transient Provider Key must be entered again after each Workbench restart. No in-game validation was performed.
- next_action: on the fresh r20 page, enter the transient Provider Key, confirm the cloud endpoint if needed, and perform one manual conversion. If the Provider returns invalid structure, verify the deterministic local fallback is shown rather than a budget failure.
## 2026-08-19 short-task checkpoint: Provider intermediate-output resilience
- task_id: PWB-PROVIDER-17-INTERMEDIATE-TRUNCATION-GUARD
- status: done
- files_changed: _houkai_merge/AWAKE/tools/persona-workbench/src/PersonaWorkbench.Web/ProviderDslConversionContract.cs; _houkai_merge/AWAKE/tools/persona-workbench/tests/PersonaWorkbench.Web.Tests/Program.cs; _houkai_merge/AWAKE/tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-final-20260819-r21/*; OneDrive/Desktop/Persona Workbench.lnk
- evidence: r20 user test no longer hit persona.template_budget_exceeded; it reached HTTP 200 local fallback with provider.intermediate_candidate_format_invalid. The quarantined failure contained only that stable code; no raw Provider content is persisted, so no unsafe parser guess was made.
- fix: intermediate conversion output budget raised from 900 to 1800 bounded tokens; OpenAI-compatible finish_reason=length is now classified as provider.intermediate_candidate_truncated and still goes through the existing quarantine/local fallback path. No automatic retry was added.
- verification: Web.Tests PASS including request budget and truncation regression; Core.Tests PASS 35 checks; Web Release build PASS 0 warnings/0 errors; r21 package manifest PASS 19 entries with 0 SHA-256 mismatches; r21 root HTTP 200; r21 process PID 49252; desktop shortcut targets r21.
- limitation: r21 has not yet issued a new live cloud request; the reported invalid response came from r20 and cannot be reconstructed without raw Provider content. No Bannerlord launch or game-directory synchronization was performed.
- next_action: refresh the r21 Workbench page, set the transient API Key again, confirm the cloud endpoint, and retry the same conversion once. If fallback still appears, capture the newest warning code; if it changes to provider.intermediate_candidate_truncated, the prior cause was output cutoff; if it remains format_invalid, the Provider is emitting malformed JSON and needs a prompt/model-specific sample rather than another blind parser relaxation.
## 2026-08-19 short-task checkpoint: Persona Workbench complete Provider conversion restoration
- task_id: PWB-AI-RESTORE-22
- status: done
- root_cause: the browser DSL action had been rerouted from the complete evidence-backed `PersonaDocument` Provider contract to a sparse intermediate candidate. The parser then forced the full confirmed source into `Core`, retained demo identity defaults, and returned heuristic fallback DSL as success when Provider structure failed.
- fix: `/api/provider/convert-to-dsl` now reuses the complete `ProviderDraftClient` contract, overrides Provider identity with the explicit local stable ID/display name, preserves the full source only in `SourceDescription`, rejects core-only/unstructured drafts, generates canonical DSL from the structured draft, and returns the editable Draft to the browser. Provider failures no longer produce heuristic success. New sessions no longer preload the Sable demo identity or demo core.
- regression_gates: added service tests for complete structured conversion, local identity ownership, no full-source copy, Provider-failure isolation, core-only rejection, and a real Chat Completions response parser-to-action-to-canonical-DSL path. Browser gates require structured Draft refill and forbid `usedLocalFallback` success.
- verification: PersonaWorkbench.Core.Tests PASS; PersonaWorkbench.Web.Tests PASS; Release build 0 warnings/0 errors. r22 package manifest entries=19, payload files=19, errors=0. Runtime loopback API smoke produced `free.generated.colin` / `科林`, English pride/reaction/commitment axes, no Provider ID, no Sable identity, no full source copy, DSL bytes=577.
- package: `tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-final-20260819-r22`; PersonaWorkbench.Web.dll SHA-256=`FDF1DB7B6019C5E75A6200EA01E76898D242F9CAA65A8936C8CCEFFC41D6E5CE`; PersonaWorkbench.Core.dll SHA-256=`61AA1E64A6A244BA55E3946BA03C9F523F9395B80C1D9654D4D0FA20E1EAA072`.
- runtime: r21 PID 49252 stopped; r22 PID 19164 running at `http://127.0.0.1:51337/`; desktop shortcut points to r22; stderr is empty.
- remaining_validation: no live DeepSeek request was sent because cloud API keys are transient and unavailable to this automated verification. User should re-enter the key and run one real cloud conversion; failures must leave the form and DSL unchanged.
## 2026-08-19 short-task checkpoint: Provider axis tolerance and identity autofill
- task_id: PWB-AI-AXIS-ID-23
- status: done
- observed_error: `provider.candidate_axis_invalid` on live conversion; users also left file name and role identity empty or inherited defaults.
- root_cause: the draft parser required JSON integer tokens exactly, rejecting equivalent numeric strings/integral decimals and one malformed optional axis could reject the entire candidate. The conversion action also required local ID/display name before Provider parsing, preventing Provider name extraction.
- fix: numeric axes now accept integer JSON, integral decimal JSON, and numeric strings in `-2..2`; malformed optional axes are ignored and their orphan evidence is pruned while valid tags/profiles remain. Empty display names use the Provider-extracted name or `未命名角色`; empty stable IDs use a local deterministic SHA-256-derived `free.generated.*` ID. The browser derives a safe `<displayName>.persona.json` filename only when the user has not customized the default, and Unicode names are allowed.
- verification: Web Tests PASS including numeric normalization, malformed-axis pruning, generated identity; Core Tests PASS; Release build 0 warnings/0 errors. Runtime loopback smoke with an invalid pride axis, valid tag/reaction axes, blank local identity returned status success, display name `科林`, ID `free.generated.35d022a34e1d0574`, retained valid axes/tags, and did not use Provider ID. r23 is running at `http://127.0.0.1:51337/`.
- package: `tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-final-20260819-r23`.
- remaining_validation: run one real DeepSeek conversion after entering the transient key; malformed optional axes should no longer block the remaining candidate, while unsupported fields/evidence remain fail-closed.
## 2026-08-20 short-task checkpoint: Provider evidence field pruning and r27 verification
- task_id: PWB-AI-EVIDENCE-PRUNE-24
- status: fixed_pending_live_cloud
- observed_error: `provider.candidate_evidence_not_in_source` rejected an otherwise usable Provider candidate whenever any optional field cited text that was not an exact source quotation.
- root_cause: evidence validation was all-or-nothing after parsing. One hallucinated or paraphrased evidence value invalidated the complete candidate even when other tags and axes had valid source evidence.
- fix: missing or source-invalid evidence now removes only its owned field group before final validation. Tags are pruned individually; profile axes use the existing conservative profile-group clearing rule; authored text/list fields clear only their own group. Orphan evidence is removed with the pruned field. If pruning leaves only `Core` and no structured Persona signal, conversion still fails closed with `provider.persona_structure_empty`; no DSL or editable Draft is returned and the current form remains unchanged.
- runtime_smoke: r27 was exercised through the real loopback HTTP action using a temporary local Chat Completions Provider. Source `科林骄傲，遇到敌人会直接迎击。` retained `trait.proud` and `reactionProfile.confrontation=2`, removed unsupported `traitProfile.caution`, generated local ID `free.generated.333f7e530c6f0abf`, ignored Provider ID, and returned canonical DSL without local fallback. A second candidate with all evidence invalid returned HTTP 409 / `provider.persona_structure_empty`, empty DSL, and null Draft. The temporary Provider was stopped after the test.
- verification: PersonaWorkbench.Web.Tests PASS all checks including partial evidence pruning and all-structure removal; PersonaWorkbench.Core.Tests PASS all checks; Release build 0 warnings/0 errors. r27 manifest entries=20, payload files=20, hash errors=0; root HTTP=200; PID 13568 is the r27 executable; stderr=0 bytes.
- package: `tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-final-20260820-r27`; PersonaWorkbench.Web.dll SHA-256=`776A6F660C24A580F49C087BE7A9F08DCDF1D8B2F130521E162B6CFEBBD9D85E`; PersonaWorkbench.Core.dll SHA-256=`61AA1E64A6A244BA55E3946BA03C9F523F9395B80C1D9654D4D0FA20E1EAA072`.
- launcher: `C:\Users\26811\Desktop\Persona Workbench.lnk` targets `C:\Windows\System32\wscript.exe` and launches r27 `launch-free-preview.vbs`, which opens an independent Edge app window.
- remaining_validation: no new live DeepSeek request was sent because the API key is transient and unavailable to automated verification. Re-enter the key, confirm the endpoint, and retry once; any remaining failure must keep the current form and DSL unchanged. Bannerlord was not started and no game directory was modified.
## 2026-08-20 short-task checkpoint: AI self-audit and r28 field-level pruning
- task_id: PWB-AI-SELF-AUDIT-25
- status: fixed_pending_live_cloud
- audit_scope: Provider evidence pruning, candidate diagnostics, structure fail-closed behavior, browser failure isolation, endpoint safety, and release/runtime consistency.
- root_cause_found: the previous evidence cleanup cleared an entire profile object when one axis had missing or invalid evidence. That discarded valid sibling axes and contradicted field-level evidence ownership.
- fix: `PruneUnprovenFields` and `PruneInvalidEvidenceFields` now remove exact evidence keys and call `ClearEvidenceField`; all 41 registered evidence-backed fields are covered, tags retain a dynamic `tags.*` branch, and orphan evidence is removed. A bad axis no longer deletes valid axes in the same profile.
- fix: corrected parser diagnostics for non-text `core` and `identityFacts`; they now return `provider.candidate_core_invalid` and `provider.candidate_identity_invalid` instead of the unrelated display-name code.
- tests: red tests reproduced both regressions; green Web Tests PASS all checks including same-profile preservation and diagnostic-code checks. Core Tests PASS all checks. Release build 0 warnings / 0 errors.
- local_worker: task `pwb-ai-self-audit-20260820-01`, model `gpt-oss:20b`, digest `17052f91a42e97930aa6e28a6c6c06a983e6a58dbb00434885a0cf5313e376f7`; batch 001 audited 8/8 records as `ok`, batch 002 audited 4/4 post-fix records as `ok`; cloud layer validated IDs, hashes, counts, enums, and evidence requirements; task completed.
- runtime_smoke: r28 loopback HTTP tests passed. Partial invalid evidence retained `trait.proud`, `traitProfile.pride=2`, and `reactionProfile.confrontation=2`, while removing unsupported caution. All invalid evidence returned HTTP 409 / `provider.persona_structure_empty` with empty DSL and null Draft. Non-text core returned HTTP 409 / `provider.candidate_core_invalid`.
- package: `tools/persona-workbench/artifacts/PersonaWorkbench-FreePreview-final-20260820-r28`; manifest entries=20, payload files=20, hash errors=0; PID 28860 runs the r28 executable; HTTP 200; stderr=0 bytes.
- launcher: `C:\Users\26811\Desktop\Persona Workbench.lnk` targets `C:\Windows\System32\wscript.exe` and launches r28 `launch-free-preview.vbs` in an independent Edge app window.
- ui_smoke: page title `Persona Workbench · Free Preview`, meaningful DOM rendered, no browser warning/error logs, and manual DSL preview produced canonical `[PERSONA_LOAD]` with the supplied local ID/name. A user-path conversion against unavailable loopback port `59999` displayed `provider.transport_error` while preserving both the existing core and DSL exactly. Provider mode is intentionally inferred from endpoint; manually changing the mode alone is not authoritative until the endpoint matches.
- remaining_validation: no real DeepSeek request was issued in this audit because the API key is transient. User must re-enter the key, confirm the endpoint, and test one cloud conversion. Bannerlord was not started and no game directory was modified.

## 2026-08-20 short-task checkpoint: visible EXE launcher and desktop entry
- task_id: PWB-LAUNCHER-26
- status: done
- request: replace the hidden VBS-only entry with an obvious, visible launcher suitable for ordinary users.
- root_cause: the desktop shortcut still targeted the hidden VBS flow. After retargeting, it also retained stale r21 PowerShell arguments and WindowStyle=7, so Windows opened the new EXE minimized and made it appear as if nothing happened.
- implementation: added a .NET Framework WinForms PersonaWorkbench.Launcher.exe with a visible 626x420 window, service status, one large green start/open button, open-page, stop-service, open-folder, and close-launcher actions. The launcher invokes the existing bounded start/stop scripts and does not change Provider keys, Persona data, or Bannerlord state.
- packaging: package-free-preview.ps1 now compiles the launcher as a small WinExe and includes it in the manifest. launch-free-preview.vbs prefers the visible EXE and remains only as a compatibility fallback. README startup instructions now lead with the EXE.
- shortcut_fix: C:\Users\26811\OneDrive\Desktop\Persona Workbench.lnk now targets the r30 launcher directly, has empty Arguments, WorkingDirectory set to the r30 desktop folder, and WindowStyle=1.
- tests: launcher package smoke PASS; visible-window/button regression PASS; missing-start-script failure contract returned expected exit code 2; real cold-start click PASS with HTTP 200 and service path bound to the selected package; desktop shortcut opened a full 626x420 window.
- regression: PersonaWorkbench.Core.Tests PASS all checks; PersonaWorkbench.Web.Tests PASS all checks; Release publish completed without build errors.
- release: desktop folder C:\Users\26811\OneDrive\Desktop\PersonaWorkbench-FreePreview-r30-20260820; desktop ZIP C:\Users\26811\OneDrive\Desktop\PersonaWorkbench-FreePreview-r30-20260820.zip; ZIP SHA-256=3FFCD4E988B288BC1DEF4C7F0E4A05052CE570959D2531157C3EBF3A2765BB87; files=22, manifest entries=21, hash errors=0, forbidden files=0.
- runtime: PID 40292 runs the desktop r30 PersonaWorkbench.Web.exe; loopback HTTP 200. Bannerlord was not started and no game directory was changed.
- remaining_validation: the broader Persona Workbench task remains fixed_pending_live_cloud because no transient DeepSeek key was available for a new real cloud conversion; the launcher-specific request is complete.

## 发布前反馈（2026-08-20）
- `FB-20260820-1`：停止脚本在 PID 文件缺失且仅有一个匹配 Web 进程时失败。
  - 来源：r30 切换到 r31 的真实停止操作。
  - 证据：`stop-free-preview.ps1:42` 在严格模式下访问单个 `Process` 对象的 `.Count`，报 `The property Count cannot be found on this object`。
  - 关联任务：`PWB-RELEASE-MANUAL-27`。
  - 优先级：P0；状态：`done`；分类：`blocking_current`。
  - 修复：将停止脚本的函数调用结果在赋值边界显式包装为数组，避免 PowerShell 单项解包后访问 `.Count` 失败。
  - 验证：旧 r31 场景稳定复现；r32/r33 专项测试覆盖“无 PID 文件 + 单个精确匹配进程”，启动、停止、PID 删除和端口关闭均通过。

## 2026-08-20 short-task checkpoint: r33 public handbook and release package
- task_id: PWB-RELEASE-MANUAL-27
- status: done
- request: write a user-facing manual and prepare Persona Workbench for distribution.
- documentation: added PersonaWorkbench-使用说明.md with system requirements, EXE startup, no-AI quick start, separate AI text-expansion and text-to-DSL flows, local/cloud Provider setup, Key/privacy rules, Persona field guidance, draft/approved workflow, history/conflict recovery, error-code troubleshooting, security boundaries, preview limitations, and file-verification instructions.
- accessibility: README-FreePreview.md now directs first-time users to the full handbook; the handbook is included in package-free-preview.ps1 and protected by a Web test assertion.
- release_blocker_fixed: FB-20260820-1 exposed a strict-mode stop-script failure when the PID file was absent and exactly one matching process existed. The function result is now explicitly array-wrapped at the call boundary.
- verification: Core Tests PASS all checks; Web Tests PASS all checks; launcher visible-window test PASS; cold-start click PASS; stop-script stale-PID/single-process regression PASS; normal stop removed the process and PID file and closed port 51337.
- package: desktop folder C:\Users\26811\OneDrive\Desktop\PersonaWorkbench-FreePreview-r33-20260820; desktop ZIP C:\Users\26811\OneDrive\Desktop\PersonaWorkbench-FreePreview-r33-20260820.zip; ZIP SHA-256=35067B82DC838BB16B047613E8C0B38F3263400A3CFB5E655427C77F919C250E.
- package_audit: files=23, manifest entries=22, hash errors=0, forbidden files=0, single root PersonaWorkbench-FreePreview-r33, manual UTF-8 content/version checks PASS.
- shortcut: C:\Users\26811\OneDrive\Desktop\Persona Workbench.lnk targets the r33 visible launcher with empty Arguments and WindowStyle=1.
- runtime_state: service stopped after verification; desktop r33 .runtime and test Edge profiles removed; Bannerlord was not started and no game directory was changed.
- remaining_validation: one live cloud conversion still requires a user-supplied transient API Key; this does not block distributing the manual/offline-capable Free Preview, but should remain disclosed as preview risk.

## 2026-08-20 检查点：GOV-20260820-1 离线完成

- 状态：`offline_verified`；执行租约完成，不再要求逐短任务回复“继续”。
- Skills：orchestrator 为唯一调度入口；continuity、short-task、Worker 已收窄到单一职责；结构与职责扫描通过。
- 状态：新增 `AWAKE-CURRENT.md`、`AWAKE-ROADMAP.md`、`AWAKE-VALIDATION.md`，历史队列降为追溯来源。
- 运行时：BuildId=`awake-20260820-governance-001`；模块加载记录版本/BuildId/DLL SHA-256，世界书加载记录 BuildId/manifest SHA-256。
- 离线证据：双 API 构建 0w/0e；SdkSmoke PASS ALL；JSON/XML parse 0；localization 226/259/259；asset files=117；release check OK/BLOCKED_SYNC；MAF Preview lint exit 0。
- 内容债：世界书占位符审计仍有 15 处既有问题，未在治理批次静默改写。
- 候选：源码/dist DLL=`9A9284ECBF96102BC681E8DBF937C523E7A397B0CE85F1BF52531E361121FA65`；游戏 DLL=`FF98881E58102E992CFFE2B959222B406165762BEA2E2E3819E1647AF8F61021`。
- 下一动作：用户明确授权后同步冻结候选到游戏目录，再由用户启动游戏并提供包含匹配 BuildId 的日志；`0.2.1` 仍为 `blocked_sync`。