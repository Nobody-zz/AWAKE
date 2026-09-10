# Plan: PersonaWorkbench 批次 A 完整性修复
_Locked via autonomous user directive; revised after Round 1 read-only adversarial review._

## Goal
修复 PersonaWorkbench r71 主流程中的数据完整性问题，使保存、批准、角色切换和 Provider 失败都保持原子、可恢复、可诊断；任何无法生成有效 DSL 的 Persona 都不得被写成 approved，任何失败都不得覆盖用户当前输入或上一次成功结果。

## Scope
本批次只处理保存/批准原子性、失败不覆盖、角色切换状态清理、元数据保留、扩充填充边界、异常输入结构化 4xx、approved 编辑退回 draft、文档操作竞态、冲突和恢复安全。

本批次不改动高密度 DSL 算法、Provider Key 隔离、Ollama 预检、Provider 提示词、发布版本号、游戏模块或 AWAKE 运行时。

## Approach

1. 先冻结不变量并补可复现回归测试：保存可重新加载；草稿保存和批准都先做结构校验；批准额外生成并验证 DSL；失败不改目标文件、状态或前端结果；加载新角色不继承旧 Provider 状态；元数据和内容哈希保持；非法 JSON 不产生空 body 500。
2. 在 `WorkspaceDocumentService` 统一执行文档验证与批准前 DSL 生成。使用候选文档，不在验证前修改请求对象；全部成功后才设置 `approved` 并写盘。`PersonaValidator` 明确拒绝 null/空/重复标签和 null 集合。
3. 在 `Program.cs` 为文档接口映射结构化错误状态：无效输入 400、未找到 404、冲突 409、权限错误 403/401、不可恢复服务错误 500；错误不泄露堆栈。
4. 在 `app.js` 完整保留 `schemaVersion`、`sourcePackId`、`templateVersion`；加载、保存、批准和预览使用文档 epoch/操作序列；切换角色或编辑时使旧请求失效；失败只更新状态，不恢复局部旧快照；加载时清除 Provider 临时状态。
5. 将扩充结果填入定义为覆盖式操作：只写入确认文本和来源描述，并清理旧派生字段；隐式混合不作为默认行为。
6. 加强 `PersonaWorkspace` journal 的旧/新哈希和恢复分支测试，区分“目标未变化”的失败保留与“冲突草稿已生成”的保护性副作用。
7. 运行 Core、Web、Browser Smoke、启动脚本及固定短/长文本的本地 Ollama Worker-low 与真实 Workbench 测试；不自动重试，不使用云端 Provider。

## Key decisions & tradeoffs

- 本批次优先保证不丢数据，不提前引入独立草稿数据库；无效 Persona 保存失败时由前端保留编辑内容。
- 批准是强门禁，不允许先写 approved 再生成 DSL。
- 失败时保留上一次成功 DSL。
- 预览/Provider/文档操作的失败路径不回滚用户编辑；只有成功响应且 epoch 仍匹配时才能应用结果。
- 冲突文件是有意保留的保护性副作用；原目标文件和原内容哈希必须保持不变，并在响应中暴露冲突路径。
- journal 恢复以记录的旧/新哈希决定保留、恢复或报告不可恢复，不以“目标文件存在”单条件删除临时数据。
- 不用提高模型预算掩盖状态和契约错误；生成链路改造单独处理。
- 不擅自提版本，先生成候选并记录证据。

## Validation gates

- 无效 Core 无法批准，磁盘状态和内容哈希不变。
- 批准失败不改变调用方文档对象的状态。
- 保存后的文档可以重新加载。
- `tags: [null]`、缺字段和非法枚举返回结构化 4xx。
- 文档接口错误状态分别符合 400/404/409/500 契约。
- 切换角色不串状态；旧加载、预览和 Provider 响应不能覆盖新编辑；approved 编辑后变为 draft。
- 冲突只生成冲突副本，不改变原目标文件和原内容哈希；journal 恢复按哈希分支验证。
- Ollama 使用 `gpt-oss:20b` 与 `thinking_level: low`；Worker 和 Workbench 结果分开记录；服务和端口测试后清理。

## Risks / open questions

- 本批次不通过放宽校验允许 Core 为空；如果未来需要空 Core 草稿，应另行设计可加载的草稿 envelope。
- 扩充结果清理字段需按现有表单字段逐项确认。
- 批准路径必须收敛到唯一权威 DSL 生成入口。
- 浏览器竞态测试需要可控的 deferred fetch/响应顺序，不以人工点击 Smoke 代替。

## Out of scope

- 高密度 DSL 优先级、语义去重和 UTF-8 预算算法完整重写。
- Provider Key 泄漏、本地模型预检和提示词体系重写。
- AWAKE 游戏运行时、Bannerlord 文件和正式发布包同步。
