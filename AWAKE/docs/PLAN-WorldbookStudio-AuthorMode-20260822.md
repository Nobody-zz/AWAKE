# Worldbook Studio 作者模式实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将世界书工作室默认编辑入口改为中文五步作者模式，同时保留高级 YAML 编辑并复用现有校验、预览、编译和导出链路。

**Architecture:** 分三阶段交付：先在 Core 建立严格的 authoring v1 只读投影和 round-trip 测试，再增加带 source hash/revision/registry hash 的 CAS 保存，最后启用原生 HTML/JavaScript 五步作者界面。作者模式只覆盖明确字段，其他合法 v1 字段保留并显示只读摘要；未知字段、来源/原创冲突、来源型对象修改和未批准 canon 均 fail closed。运行时、游戏目录和冻结候选完全隔离。

**Tech Stack:** ASP.NET Minimal API、C# `JsonNode`、现有 `YamlDotNet` 序列化、原生 HTML/CSS/JavaScript、现有 PowerShell 测试与 loopback smoke。

---

### Task 1: 建立 authoring v1 只读投影与契约矩阵

**Files:**
- Create: `_houkai_merge/AWAKE/tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringEditorModel.cs`
- Modify: `_houkai_merge/AWAKE/tools/worldbook-studio/src/Awake.WorldbookStudio.Core/Application.cs`
- Test: `_houkai_merge/AWAKE/tools/worldbook-studio/tests/Awake.WorldbookStudio.Tests/Program.cs`

- [ ] **Step 1: 定义完整的编辑快照模型与中文 catalog 映射**

定义 `EditorDocumentSnapshot`、`EditorAssertionModel`、`EditorExpressionModel`、`EditorPermissionModel`、`EditorAdvancedSummary` 和 `EditorCatalogModel`。快照必须包含 `path`、`format`、`documentId`、文档 `revision`、`sourceHash`、`universe`、`contentTier`、`authority`、来源/原创模式、registry 版本/hash、事实/表达稳定 ID 和未覆盖字段摘要。目录项同时返回中文显示名、内部值和帮助说明；缺失中文显示名时返回“未本地化身份”警告。

- [ ] **Step 2: 实现严格的递归 `ProjectDocument`**

按 `document → assertions[] → expressions[] → grants/denies/fallback_referral_ids` 读取 `title.zh-CN`、`domain`、`universe`、`era`、`summary.zh-CN`、`status`、`content_tier`、来源/原创审核信息和全部权限条件。缺失可选数组返回空集合；保留原始 ID、revision、sources/author_created 和条件字段作为只读绑定，不把嵌套字段提升到文档级。

- [ ] **Step 3: 实现来源/原创安全规则和 `MergeDocument` 设计**

复制原始 `JsonObject` 后按稳定 ID 只替换作者模型明确且 dirty 的字段。浏览器新增卡片立即生成不可变 `clientKey`，失败重试复用；服务端第一次接受时生成内部 ID 并返回绑定，重试不能再生成第二个对象，禁止使用可变中文正文生成 ID。来源型对象在作者模式中只读，若请求试图改变其事实、表达或权限则返回明确错误并保留原缓冲区；高级模式继续使用旧保存路径。禁止同层同时写入 `sources` 与 `author_created`。已有 author_created 修改时只递增实际改变对象的 revision，清除旧 review_event_id；文档 revision 仅在规范化语义树实际变化时递增，YAML 排版变化不触发。保存前先完整 v1 schema 校验，再执行对象层级作者字段白名单检查；未知字段 fail closed，合法但未覆盖字段原样保留。

- [ ] **Step 4: 先写 Core 投影/round-trip 回归测试并运行**

使用来源型、原创型、`canon`/`needs_review`、JSON/YAML、aliases/redirects/lifecycle/entity_ids、全部权限条件的 fixture 矩阵，覆盖 project→merge→parse→schema validate；断言客户端 key 到内部 ID 的幂等绑定、逐层 revision、规范化 no-op、来源/原创二选一、来源型对象作者模式只读、authority/universe/registry 保留、条件规则保留、editable/readonly/advanced-only dirty 语义和未知字段 fail closed。运行 `dotnet run --project tests\\Awake.WorldbookStudio.Tests\\Awake.WorldbookStudio.Tests.csproj --configuration Release`。

### Task 2: 接入带 CAS 的编辑模型 API

**Files:**
- Modify: `_houkai_merge/AWAKE/tools/worldbook-studio/src/Awake.WorldbookStudio.Web/Program.cs`
- Modify: `_houkai_merge/AWAKE/tools/worldbook-studio/src/Awake.WorldbookStudio.Core/Application.cs`
- Test: `_houkai_merge/AWAKE/tools/worldbook-studio/tests/Awake.WorldbookStudio.Tests/Program.cs`

- [ ] **Step 1: 增加读取路由和 registry 绑定**

增加 `GET /api/editor-document?path=...`，返回路径、格式、编辑快照、稳定诊断目标、原始校验报告和 `advancedAvailable=true`。原有 `GET /api/document` 保持不变供高级模式和 CLI 使用。读取只接受 authoring 目录内的 YAML/JSON，复用现有路径策略和 reparse 检查。

- [ ] **Step 2: 增加 CAS 保存路由和原子校验写入**

增加 `POST /api/save-editor-document`，请求包含 `path`、编辑模型、`sourceHash`、expected revision、profile/referral registry 版本/hash。Core 重新读取并 CAS 校验，先写临时文件、解析/schema 校验通过后再原子替换；冲突返回 409 并保留原文件，失败不留下坏文档。成功返回重新投影的快照并使同文档旧 AI buffer 失效。

- [ ] **Step 3: 增加稳定 ID 诊断投影**

将现有 `ValidationReport.Diagnostics` 转成 `module`、`assertionId`、`expressionId`、`ruleId`、`field`、`message`、`example` 和可选 `code`。业务错误必须在产生时携带对象 ID；schema 路径按候选文档数组位置解析到对象 ID；无法可靠映射时 ID 为空、索引只作展示，前端不得伪造目标，改显示通用高级诊断。

- [ ] **Step 4: 运行 API/CAS smoke**

增加独立 `scripts/editor-api-smoke.ps1`，在临时 workspace 和随机 loopback 端口启动 Web，健康检查后验证读取、保存、重新读取、hash/revision 冲突、registry 冲突、路径越界、未知字段、来源型对象只读和失败定位；保存后验证旧 AI suggestion apply 被 CAS 拒绝，最后关闭进程并清理临时目录。确认高级 `/api/save-authoring`、预览、编译、导出和 AI 路由响应契约不变。

### Task 3: 实现中文五步作者模式前端（最后启用默认入口）

**Files:**
- Modify: `_houkai_merge/AWAKE/tools/worldbook-studio/src/Awake.WorldbookStudio.Web/wwwroot/index.html`

- [ ] **Step 1: 增加模式开关和单一服务端快照**

保留左侧档案列表和右侧检查器；中央区域增加作者/高级模式开关、步骤导航、步骤内容容器和完成度。两种模式各自持有临时缓冲区，但成功保存后都从服务端重新读取并刷新另一模式；切换 dirty 时提供保存、放弃、取消。

- [ ] **Step 2: 实现基本信息步骤和安全默认**

使用中文标签、帮助文本和示例；“所属时代”统一显示为“知识适用时期”。领域、时期、确定程度和内容层使用下拉框；状态默认 `needs_review`，`canon` 只读或需要审核事件确认，成人层要求显式确认，不显示内部枚举值。

- [ ] **Step 3: 实现事实与 NPC 表达卡片及来源只读门禁**

事实卡片提供正文、事实类型、删除和新增；来源型事实/表达默认只读，尝试编辑时显示“来源型内容请进入高级模式维护”的明确提示并保留缓冲区。每条事实内部显示表达卡片，表达卡片提供详细程度、NPC 说法、身份权限、推荐对象和完整高级权限条件折叠区。空事实/空表达不能静默保存，删除需要确认并支持撤销提示。

- [ ] **Step 4: 实现中文权限控件与条件保留**

身份使用 catalog 的中文名称和说明；scope/min_detail 使用中文选择器；denies 使用“禁止知道”身份选择器；referral 使用“如果不知道，可以询问谁”选择器。年龄、管理技能、性别和族长条件可编辑；文化/王国/城镇/职务无实体 catalog 时显示数量摘要并由高级模式维护。内部 ID 只保存在浏览器模型的 value，不显示给作者；catalog hash 变化时阻止保存并要求刷新。

- [ ] **Step 5: 实现检查、稳定定位与高级模式切换**

错误卡片显示模块、原因、示例和“立即前往”，按 assertion/expression/rule 稳定 ID 定位；高级模式显示原有 textarea，并在切换时提示未保存修改和作者模式未覆盖字段。作者模式保存调用新 API，高级模式保存调用旧 API；列表、读取、保存、校验使用 request generation/AbortController，响应提交前确认当前 path 和 revision，避免旧响应覆盖新档案。

### Task 4: 分阶段验证、浏览器闭环和发行包更新

**Files:**
- Modify: `_houkai_merge/AWAKE/tools/worldbook-studio/scripts/test.ps1` only if a new deterministic check needs registration.
- Modify: `_houkai_merge/AWAKE/tools/worldbook-studio/scripts/smoke.ps1` only if the existing browser flow needs an author-mode assertion.
- Generated outside source: `artifacts/WorldbookStudio-win-x64.zip` and sidecar after source verification.

- [ ] **Step 1: 运行 Core/Web 契约测试**

运行 `scripts\\test.ps1`，预期来源/原创、round-trip、CAS、路径保护、权限条件、AI buffer 失效和旧 API 响应回归全部通过；Release 构建保持 `0 warnings / 0 errors`。

- [ ] **Step 2: 增加并运行真实浏览器闭环**

在前端加入稳定 `data-testid`；使用当前可用的 in-app Browser/Playwright 交互路径执行“新建 → 填写 → 添加两条事实 → 添加两种表达 → 设置身份 → 保存 → 校验 → 预览 → 高级模式 → 返回作者模式”，并覆盖空事实、删除确认、来源型对象编辑被拒且缓冲区保留、保存冲突、作者模式保存后高级字段保留、AI suggestion 失效。采集 URL/title、DOM 非空、页面错误、控制台错误、网络状态、截图和关键控件状态；浏览器不可用时，`editor-api-smoke.ps1` 仍作为可重复的确定性门禁，不把静态 HTML 检查冒充浏览器闭环。

- [ ] **Step 3: 运行发布检查**

执行现有 package/release-check，确认自包含启动器仍能启动 Web 子进程。只更新 Studio 自己的 artifacts，不同步游戏目录、`Modules\\AWAKE`、`PlayerExports`、dist 或冻结运行候选。

- [ ] **Step 4: 更新 checkpoint**

记录修改文件、Core/Web/smoke/build/package 证据、未进行游戏验证的限制和下一步。最终只声明 Studio 离线验证等级，不把它包装成游戏内验证。

### 分阶段启用规则

- [ ] Phase A：只读投影、catalog/registry 绑定和 schema round-trip 通过后才能进入 Phase B。
- [ ] Phase B：CAS 保存、原子校验写入、来源型对象只读门禁和旧 AI buffer 失效通过后才能进入 Phase C。
- [ ] Phase C：浏览器五步流程和高级模式切换通过后，才把作者模式设为默认入口；任何阶段失败都保留原始 textarea 默认回退。
