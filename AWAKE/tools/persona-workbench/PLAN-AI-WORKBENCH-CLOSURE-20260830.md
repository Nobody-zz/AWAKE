# Plan: Persona Workbench AI Closure 20260830

Status: `APPROVED / IMPLEMENTATION_ALLOWED`

## Goal

把 Persona Workbench 从“单条自由描述转换”推进到可实际使用的资料生产闭环：用户导入或粘贴资料，看到可编辑的本地分段，明确进入批量 AI 生成，逐条查看结果，并能把选中的结果载入现有编辑器或导出批量结果。启动入口必须绑定当前源码/当前发布包，禁止静默回退到旧固定包。

## Current facts

- 当前 `/api/provider/convert-to-dsl` 已能通过 Provider 生成单个 `draft + dsl`，并由浏览器回填表单。
- 当前工作台没有资料导入、分段结果、批量路由、批量结果列表。
- 当前源目录 `start-free-preview.ps1` 会在源码项目存在时仍优先寻找旧的 `artifacts\\PersonaWorkbench-FreePreview`，造成用户运行旧版本。
- 发布包使用 self-contained 单文件；源码目录只用于开发验证，不应作为普通用户发布入口。
- 本批不修改 AWAKE、Bannerlord、游戏目录、Worldbook、冻结候选或旧契约。

## Minimum closed loop

`Launcher/启动脚本 -> current source or package -> 资料导入 -> 本地分段 -> 用户确认分段 -> batch AI conversion -> result cards -> load one result / export batch JSON`

## Locked scope

1. **唯一入口**
   - 包目录只有在同时存在 `PersonaWorkbench.Web.exe`、`PersonaWorkbench.Launcher.exe`、`PACKAGE-MANIFEST.sha256.txt`、`BUILD-ID.txt` 和 `BUILD-SOURCE-MANIFEST.sha256.txt`，且包内校验通过时才被视为发布包；缺任一项或校验失败即拒绝启动，不回退到其他目录。
   - 源码目录只允许显式当前 source root → 当前 `src\\...\\bin\\Release\\net10.0` 输出；先构建当前源码，不搜索或回退到任何 `artifacts\\` 目录。
   - 每次启动生成唯一 `runId`，启动日志写入 `executionMode`、`exePath`、`exeSha256`、`packagePath/packageVersion`（包模式）、`BuildId/sourceManifestHash`（可用时）、PID、端口、开始时间、ready 时间和退出/错误结果。旧日志不作为本次启动证据。

2. **资料导入与分段**
   - 支持浏览器粘贴文本和选择多个 `.txt`/`.md` 文件；不上传到外部服务。
   - 本地分段按标题、空行和长度上限做确定性切分；每段带稳定的本地序号、标题、正文和来源文件名。
   - 用户可删改段落、调整顺序，只有点击“进入批量生成”才提交 Provider。
   - 空资料、无有效段、超限和不支持扩展名要给出中文可操作提示。

3. **批量 AI 生成**
   - 新增一个受保护的批量路由，复用现有 Provider endpoint、协议、session key、云端确认和单条 `convert-to-dsl` 解析器。
   - 服务端严格限制单次段数、单段字节数和总输入字节数；按稳定顺序串行调用，避免并发击穿 Provider 单操作闸门。
   - 请求固定包含 `batchId`、稳定有序的 `items[]`，每项包含 `itemId`、`sourceOrdinal`、`title`、`sourceFile`、`sourceText`；响应固定返回整体 `status`、`batchId`、`total`、`completed`、`succeeded`、`failed`、`cancelReason` 和按输入顺序排列的 `results[]`。
   - 每个结果固定绑定 `itemId/sourceOrdinal`，返回 `success` 或明确 `errorCode`，成功时带 `draft/dsl`，失败时保留来源段；一段失败不抹掉其他已完成结果。
   - 批量请求可由客户端断开或显式取消；取消传播到当前 Provider 请求，服务端停止后续项目，返回已完成结果和 `cancelReason=client_cancelled`，不自动重试、不写入当前 Persona、不保存草稿。

4. **结果与导出**
   - 页面显示总进度、成功/失败数量、每段来源和错误说明。
   - 成功结果可“载入当前编辑器”，沿用现有未保存保护；不会自动保存或批准。
   - 提供一个批量 JSON 下载，包含来源段、生成时间、draft、dsl、状态和错误；不包含 API Key。
   - 批量结果只存在当前页面，刷新页面后不假装持久化。

## Non-goals

- 不做自动批准、游戏运行时导出、AWAKE authoring-v2 批量迁移、数据库持久化或跨用户任务队列。
- 不让本机 Worker 代替最终 AI 生成；本地分段只负责快速预处理。
- 不修改现有单条 AI 路由的语义，不重写旧契约，不整理历史 artifacts。

## Acceptance

- `start-free-preview.ps1 -NoWindow` 在源码目录不触碰旧固定包，日志能证明运行文件路径和模式。
- 包目录的启动脚本只启动包内 self-contained exe；包缺文件时明确失败。
- 粘贴两段资料后，页面能显示可编辑分段；空资料和错误文件有明确提示。
- 使用受控本机 OpenAI-compatible Provider 时，批量两段返回两张成功卡，Provider 收到两次稳定顺序请求。
- 第二段 Provider 返回错误时，第一段仍可载入/导出，第二段显示错误。
- 批量响应的 `batchId/itemId/sourceOrdinal` 与输入稳定对应；显式取消或客户端断开后不会启动下一段。
- 取消、Provider 失败和 malformed candidate 均不覆盖当前编辑器表单和现有 DSL。
- 下载 JSON 可解析且不含 `apiKey`、Bearer token 或会话密钥。
- 现有 Core/Web 测试、浏览器 smoke、构建和新批量测试全部通过。

## Evidence limit

本批最多宣称：Workbench 本地/受控 Provider 的离线端到端 E2 证据。不得宣称真实云端兼容、Bannerlord 实机、AWAKE 运行时或内容质量已完成。

## Write set

- `start-free-preview.ps1`
- `src/PersonaWorkbench.Web/Program.cs`
- `src/PersonaWorkbench.Web/BatchGenerationService.cs`
- `src/PersonaWorkbench.Web/wwwroot/index.html`
- `src/PersonaWorkbench.Web/wwwroot/app.js`
- `src/PersonaWorkbench.Web/wwwroot/site.css`
- focused tests and browser smoke fixtures only
- package metadata/README only after source validation
