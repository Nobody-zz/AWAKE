# Worldbook Studio 内容作者体验实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended; current session will execute inline only after review). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让零基础内容编辑者可以在中文作者模式中直接完成世界知识内容，并把 AI 候选安全地放入对应表单，而不是被迫阅读或搬运 YAML。

**Architecture:** 复用现有 authoring projection、AI suggestion envelope、CAS 保存和本地检查链路。服务端把 AI patch 目标投影为作者字段，前端将候选放回中文表单；高级模式继续作为原始文件兜底。领域说明只来自编辑器 catalog，不改变运行时世界书契约。

**Tech Stack:** ASP.NET Minimal API、C# `JsonNode`、现有 `JsonPatchEngine`/`DocumentCas`、原生 HTML/CSS/JavaScript、现有无框架测试 harness。

## Global Constraints

- 只修改 `tools/worldbook-studio/**` 及本计划、检查点和证据文档；不修改 Marcus、AWAKE 主工程、冻结候选、游戏目录或运行时同步文件。
- 普通模式不显示或要求填写内部 ID、revision、registry hash、authority 等技术字段。
- AI 只生成候选；应用候选只改变当前作者编辑缓冲区，保存仍走现有 CAS、校验和人工确认流程。
- 不引入新的世界书运行时字段，不宣称人物/家族权限已经接入运行时；该项另立 B2 计划。
- 不启动 Bannerlord，不同步游戏目录，不结束用户进程。

## Behavior Contract

| Field | Contract |
| --- | --- |
| Trigger | 作者在作者模式打开档案，选择 AI 检查重点并取得建议；或进入基本信息、事实、表达步骤。 |
| Observable result | AI 建议卡片显示“放入作者表单”；点击后标题、摘要、客观事实或 NPC 表达出现在对应中文输入框，页面跳到对应步骤并标记未保存。领域步骤显示填写目的、示例和避免事项。 |
| Invariants | 原始文件不因应用候选自动改变；高级模式仍可用；来源型内容仍只读；AI 建议仍绑定原档案版本和 buffer。 |
| Failure behavior | 候选路径无法映射、文档版本变化或候选不适合作者模式时，不覆盖当前模型，保留建议并显示可理解提示；作者可改用高级模式查看。 |
| Evidence | Core 映射测试、AI apply endpoint 投影测试、前端静态行为测试、Release build、现有 Studio/Batch/Draft/Launcher 测试。 |
| Non-goals | 本轮不做人物/家族具体权限写入、不做知识条目互相引用、不改运行时读取器、不做真实云端 Provider 或游戏内验证。 |

## Implementation Tasks

### Task 1: Define author-field candidate projection

- [ ] Add a server-side projection from JSON Patch paths to author-mode targets.
- [ ] Return `authorTarget` and friendly target text in the suggestion DTO.
- [ ] Reject or mark unsupported paths without mutating the document buffer.
- [ ] Add focused Core tests for supported top-level, fact, expression, and unsupported paths.

### Task 2: Apply candidates into the author model

- [ ] Extend the apply response with the projected editor document and target step.
- [ ] Keep the existing CAS-bound buffer and apply nonce checks unchanged.
- [ ] Update the browser apply handler to replace `state.authorModel`, retain dirty state, and focus the target field.
- [ ] Preserve the current advanced-mode fallback for unsupported or explicitly technical candidates.

### Task 3: Improve domain content guidance

- [ ] Add author-facing purpose, example, and “不要这样写” guidance to taxonomy catalog entries.
- [ ] Render the guidance beside the domain/subdomain controls and in the facts step.
- [ ] Keep the five main categories and existing subdomain values unchanged.
- [ ] Add a catalog regression fixture/test proving every domain has readable guidance.

### Task 4: Verify the closed loop

- [ ] Run focused Core and frontend tests.
- [ ] Run Studio Core/Web/CLI Release builds and existing test projects.
- [ ] Run release package static checks without changing the candidate package or game directory.
- [ ] Record evidence, limitations, and code-debt findings in a checkpoint.

## Review Gate

- `plan_status`: ready_for_read_only_review
- `review_status`: pending
- `user_signoff_required`: satisfied by the current explicit request to proceed with worldbook editor content, subject to this bounded scope
- `primary_executor`: current session
- `minimum_evidence`: E2 offline verification