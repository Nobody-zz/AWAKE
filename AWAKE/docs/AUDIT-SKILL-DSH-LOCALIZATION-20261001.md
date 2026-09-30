# AUDIT — 既有 skill 的 DSH 本地化筛选

> 日期：2026-10-01　作者：巡检会话
> 范围：本机两套既有 skill 共 **93 个**（`~/.workbuddy/skills` 56 个 + `~/.codex/skills` 37 个）
> 方法：机械扫描（引用/路径/工具名）+ 9 批并行逐读审计（workflow，9 个 agent）
> 配套：`REVIEW-DESIGN-20261001.md`、`REPORT-THREE-CLAIMS-VERIFICATION-20261001.md`

---

## 0. 为什么不能直接挂上去

**DSH 的五个 skill 扫描根全空**（实测）：

| Rank | 来源 | 路径 | 状态 |
|---|---|---|---|
| 100 | project-dsh | `D:\AWAKE-Dev\.dsh\skills` | ❌ 不存在 |
| 200 | project-agents | `D:\AWAKE-Dev\.agents\skills` | ❌ 不存在 |
| 300 | custom | `Config.customSkillDirs` | ❌ 未配置 |
| 400 | **user-dsh** | **`<dshHome>\skills`** ＝ `%APPDATA%\dsh-desktop\harness\skills` | ❌ **不存在** |
| 500 | user-agents | `~/.agents/skills` | ❌ 不存在 |

`dsh-base` 里 `skill` / `skill-filesystem` / `skill-badge` / `tool-skill` **都已挂载**（provider id ＝ `skill-filesystem`）⇒ 管道是活的，只是**没东西可扫**。

**关键发现：不需要改任何配置。** rank 400 的 `<dshHome>/skills` 是**默认根**，只要把目录建出来就有内容。所以正确做法不是 `customSkillDirs` 指向 `~/.workbuddy/skills`（那会把 93 个原封不动塞进来），而是：

> **把筛出来有用的 skill 复制到 `%APPDATA%\dsh-desktop\harness\skills\`，在副本上做本地化。**
> 原库（`~/.workbuddy/skills`）保持不动 —— WorkBuddy 那边还在用，就地改会同时改坏它。

---

## 1. 环境差异（本地化的靶子）

| 差异 | 影响面 |
|---|---|
| **没有 bash 工具** | 几乎所有 skill 都有 ```bash 代码块（含 `wc -l`/`grep`/`ls`/`sed`/`tail`/`/usr/bin/*`） |
| **`pwsh` 工具实际是 Windows PowerShell 5.1** | 少数 skill 直接写 `pwsh -File x.ps1` ⇒ 会 CommandNotFoundException |
| **无 MCP 客户端**（`mcp-client` 未挂载） | 凡依赖 MCP server 的 skill 全部不可用 |
| 无 `apply_patch` / Codex shell / Claude `Task`·`TodoWrite` | Codex 与 Claude 系 skill 的工具名要换 |
| 路径 | `~/.workbuddy/`、`~/.codex/`、`D:\卡拉迪亚金融大鳄\`、`C:\...\BannerlordSage-main\` 等外来绝对路径 |
| 游戏版本 | 多数 Bannerlord skill 的读数与行号锚在 **v1.3.15**，现行安装版是 **v1.4.8** |

机械扫描结果：**93 个里 47 个带"可操作的外来引用"**（bash 代码块 35、WorkBuddy 23、Codex 13、Git Bash 12、MCP 8、`pwsh` 3、Claude 3、`.codex` 路径 3、zcode 3、Trae 2、Cursor 1、`apply_patch` 1）。

---

## 2. 筛选结果

> 判定口径：`verdict` = keep（照搬可用）/ localize（需改）/ merge（并入别的）/ drop（对本环境无用）
> `value` = 对 `D:\AWAKE-Dev` 这个项目的价值
> ⚠️ 两处口径差异已按**详细版**（第一批 workflow 的逐条审计）为准，见 §4 注。

### 2.1 第一批：高价值 + 零改动或小改（S）——**建议先做这批**

| skill | 来源 | 说明 |
|---|---|---|
| `windows-powershell-scripting` | wb | **今天两次弄坏 `build.ps1` 的那个 BOM 坑就在里面**；本机唯一 shell 是 PS 5.1 |
| `memory-encoding-health` | wb | 本仓 `.workbuddy/memory` 有 28 份中文记忆、两次坏字节事故；体检脚本可直接跑 |
| `local-agent-change-attribution` | wb | 多会话共用同一仓库，判定"这段未提交改动是谁留的"是本仓日常 |
| `awake-batch-apply-halfcommit` | wb | 世界书线半提交缺陷的处置定式（阳性对照那条外部审计写不出来） |
| `awake-test-red-triage` | wb | 把红钉到行与提交、判"判据陈旧 vs 实现回退"、变异检验 |
| `awake-offline-csharp-harness` | wb | 本仓有 6 个消费 `src/**/*.cs` 的验台工程，判据全是本仓实测 |
| `awake-worldbook-new-compile-input` | wb | 世界书是最活跃的线，这是"新增编译输入件"的唯一手册 |
| `bannerlord-gauntlet-ui-brushes` | wb | 本仓 11 条 Brush 已按它落地，现役资产 |
| `bannerlord-gauntlet-ui-runtime-textures` | wb | 本仓已实现全套（Provider/Widget/Cache/Overlay），应从"怎么造"改成"改这里" |
| `local-embedding-onnx-dotnet` | wb | 正对应在跑的语义臂（bge 模型 + RRF 合并都在本仓） |
| `ui-design-gate` | wb | 专为 Gauntlet 游戏 UI 写，含分层判据与资产预算门 |
| `abort-aware-execution` | wb | 长构建与后台作业会中断，控制面把中断终态唯一交给它 |
| `requirements-and-acceptance` | wb | 计划门要求可验收行为契约，已写明 Bannerlord 证据路由 |
| `software-development-orchestrator` | cx | 多个已装 skill 依赖其 `control-plane-contract`，是共享契约枢纽 |
| `csharp-dotnet-build-runtime` | cx | net472 + MSBuild + 游戏程序集正是主线，证据分层正确 |
| `bannerlord-module-sync-integrity` | cx | 脚本自洽、无硬编码路径，正好配套 `sync_module.ps1` 与打包哈希 |
| `bannerlord-runtime-evidence` | cx | E4/E5 证据契约与项目实机验证流程一致 |
| `awake-content-runtime-boundary` | cx | 内容与运行时边界正是本工作区铁律 |
| `marcus-framework-contract-change` | cx | 正对 AWAKE 内嵌 Marcus 框架契约变更 |
| `grill-me-codex` | cx | AWAKE 的 PLAN+评审门核心实践，只需修掉缺失的 review-state 路径 |

### 2.2 第二批：高价值 + 中等改动（M）

| skill | 说明 |
|---|---|
| `awake-all-lines-status-review` | 全局主控线作业法；全文按 Git Bash 写，且"MSBuild/dotnet 被 LOLBin 拦"这条前提**在本环境是假的** |
| `awake-persona-card-ab-probe` | 355 张源卡时代的现行 A/B 装置；抓出的"未注册 tag ⇒ 静默 fallback"是当轮最可执行的发现 |
| `awake-port-marcus-capability` | 框架仍有 12 个 `Unavailable*` 面未实现，这是唯一可复用的 port 手册 |
| `awake-runtime-service-data-plane-client` | RAG 数据面已接完，Storage/Assets/Media 仍未接；加新数据面的唯一手册 |
| `worldbook-encyclopedia-rollout-batch` | 铺开批管线；生成器路径与档数已漂移（483→800） |
| `worldbook-retrieval-redtest` | 检索入口是产品核心；文中 `src/*.cs` 行号已整体漂移 |
| `bannerlord-game-data-access` | 查游戏事实的**默认入口**；索引版本停在 v1.3.15 而安装版是 v1.4.8 |
| `bannerlord-gauntlet-ui-prefab-layout` | 本仓 8 个预制件用了 237 处 StretchToParent，正是它管的几何面 |
| `bannerlord-gauntlet-ui-viewmodel` | 本仓 139 处 `@` 绑定、29 处 `Command.Click`；§12 的静默失效面真在用 |
| `bannerlord-gauntlet-ui-input-focus` | 本仓 `InformationManager` 30 处、`ShowInquiry` 13 处；★ 但必须删掉"AnimusForge 先例"整节（违反命名铁律） |
| `bannerlord-ui-sprite-assets` | **与本仓现状咬得最紧**：Config.xml 三条静默杀手全中、28 张 PNG 未登记、3 张图集无 tpac |
| `ai-application-engineering` | 需按本项目密钥/日志边界改写 |
| `bannerlord-mod-development-orchestrator` | Bannerlord 路由与证据分级正对项目，但 `apply_patch`/子代理措辞须改 |

### 2.3 第三批：中价值（按需）

`awake-persona-card-gates`(L, 732 行里约 2/3 是 09-13 流水与"76 张"旧口径) ·
`awake-prose-qc` · `awake-local-model-prompt-probe` ·
`bannerlord-vanilla-mechanism-recon` · `bannerlord-mod-recon` · `bannerlord-mod-skeleton` ·
`bannerlord-temp-debug-entry` · `bannerlord-tpac-extraction` · `bannerlord-ui-icon-generation` ·
`local-comfyui-imagegen-layering` · `local-comfyui-workflow-intake` · `probe-external-media-api` ·
`redteam-your-own-claims` · `windows-git-pack-loss-recovery` ·
`dependency-supply-chain-audit` · `integration-contract-testing` · `llm-rag-evaluation` ·
`observability-incident-response` · `performance-profiling-load-testing` ·
`post-change-code-debt-audit` · `software-architecture-rfc` · `technical-documentation-maintenance` ·
`bounded-review-convergence` · `safe-refactoring-legacy-modernization` ·
`i18n-localization` · `write` · `unclecheng-reduce-ai-perception-v2__skillhub` ·
`awake-task-continuity`(CURRENT.json 已停维) · `long-horizon-short-task-execution` ·
`marcus-ai-framework-extension-developer-skill` · `persona-workbench-ollama-pretest` ·
`bannerlord-gauntlet-ui-visual-polish`(依赖的子技能不存在) · `web-accessibility-qa` ·
`backend-api-engineering`

### 2.4 合并（5 个）

| skill | 并入 |
|---|---|
| `bannerlord-ui-copy-parity-gate` | `bannerlord-gauntlet-ui-prefab-layout` + `-viewmodel`（§2 的 6 项参考实现实测全在另一个项目） |
| `cross-agent-audit-cycle` | `redteam-your-own-claims`（派单给别的 agent 审整套体系本仓用不上） |
| `read-zcode-session` | `local-agent-change-attribution`（zcode 最后会话停在 09-12） |
| `humanize-zh__skillhub` | `unclecheng-reduce-ai-perception-v2__skillhub`（111 行通用口语化技巧被完全覆盖，口语腔还破中世纪语体） |
| `test-strategy-and-quality` | `bannerlord-mod-development-orchestrator`（通用测试套话，与 AGENTS.md 证据等级重叠） |

### 2.5 丢弃（16 个）

**依赖 MCP（本环境无 MCP 客户端，硬不可用）**：
`beatra__skillhub` · `bannerlord-sage-tools` · `workbuddy-custom-mcp-trust` · `local-ollama-batch-worker`

**场景不符**：
`canvas-design`（艺术海报/PDF）· `frontend-dev`（React/Tailwind）· `github`（本机无 `gh`，仓库只做单向镜像 push）·
`tencent-docx-html-to-docx-windows`（不产 Word）· `ci-cd-release-environments`（无 CI/CD）·
`containers-local-environments`（无 Docker）· `database-schema-migrations`（客户端无数据库，SQLite 在服务侧）·
`deai-promax`（面向自媒体/网文）· `llm-wiki`（与项目 docs 体系重复）·
`windows-github-release-app-install`（不装 Releases 桌面软件）

**重复或已失效**：
`karpathy-guidelines`（已由全局 `AGENTS.md` 强制）· `codex-subagent-orchestration`（与 DSH 内置团队机制重复）

---

## 3. 本地化方案

**不做就地改**（会同时改坏 WorkBuddy 那边的用法）。方案：

```
1) 建目录  %APPDATA%\dsh-desktop\harness\skills\        ← rank 400 默认根，零配置
2) 复制    筛出的 skill（第一批 20 个起步）到该目录
3) 本地化  在副本上改，原库一字不动
4) 验证    skill 工具按名加载一次，确认 frontmatter 解析成功
```

**本地化的机械部分**（可脚本化，覆盖绝大多数改动）：
- ```bash 代码块 → PowerShell 5.1（`wc -l`→`Measure-Object`、`grep`→`Select-String`/grep 工具、`ls`→`Get-ChildItem`、`$?`→`$LASTEXITCODE`）
- `pwsh -File x.ps1` → `powershell -NoProfile -ExecutionPolicy Bypass -File x.ps1`
- 删 `~/.workbuddy/`、`~/.codex/`、`D:\卡拉迪亚金融大鳄\` 等外来绝对路径，改为本仓绝对路径
- 删 MCP 相关整节（本环境无 MCP 工具）
- 删 Claude/Codex 工具名（`Task`/`TodoWrite`/`apply_patch`）

**必须人工判断的部分**（不能脚本化）：
- **版本口径**：v1.3.15 读数 → v1.4.8 待重验（尤其**否定式断言**"原版做不到 X"）
- **行号锚点**：`src/*.cs:123` 全部漂移，要重锚或改按成员名定位
- **失效前提**：如"`build.ps1` 只编译不跑测试"（`d3a4270` 已推翻）、"本机跑不起来 SdkSmoke"（已能跑）、
  "MSBuild/dotnet 被 LOLBin 拦"（假的）、"项目无版本控制"（AWAKE 是 git 仓库）
- **铁律冲突**：`bannerlord-gauntlet-ui-input-focus` §7「AnimusForge 先例」整节必须删（`AWAKE/AGENTS.md` 明令分离）
- **规范冲突**：`unclecheng` 的"禁破折号"等条款与角色卡门禁冲突

---

## 4. 方法说明与已知缺口

- 机械扫描 + **9 批并行审计**（workflow，9 个 agent，逐读 SKILL.md）
- **93 个里 92 个唯一**（分组时 `bannerlord-ui-icon-generation` 被重复放进两批）；**已产出判定 88+62 条**（两轮有重叠）
- ⚠️ **两处口径差异**：`windows-powershell-scripting` 与 `memory-encoding-health` 在**详细版**判 `localize`（列出了具体改动：BOM 补丁块是 bash 内联写法、§8 本机路径需修正、§14 safe-delete 是 WorkBuddy 专属、§15 `install_binary` 不存在、§16 与 `awake-offline-csharp-harness` 重叠），在**精简版**判 `keep`。**本表按详细版取 `localize`。**
- 第一轮 workflow 的返回值在 50,037 字符处被截断（`changes` 字段过长）⇒ 后 5 批改用精简 schema 补跑。**第一批 32 个的详细改动清单已在会话中，未落盘**。
- 未产出判定的 skill 需补审（少量）。
- 本文件**未改动任何 skill、任何配置**；`%APPDATA%\dsh-desktop\harness\skills\` **尚未创建**。
