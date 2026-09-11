# Worldbook Studio 编辑器内容一致性修复计划 — 2026-08-27

> 本文件是 `PLAN-WorldbookStudio-EditorContent-20260827.md` 的实现修复增补，不改变世界书运行时契约、批量创作合同或冻结候选。

## 目标

在保持中文作者模式和“AI 只给候选”的前提下，消除 AI 候选应用时会丢失手工内容、误改来源档案和错误跳转的问题。

## 已确认事实

- 当前 `/api/ai/apply` 每次从磁盘重新建立临时 CAS 缓冲区。
- 高级模式可连续显示多个候选，但应用一个候选后其余候选仍可能基于同一旧版本。
- 作者模式已有 `state.authorModel` 投影回填，但 AI 结果生成后用户仍可继续编辑。
- 来源型档案在作者模式只读，但 AI apply 路径没有同样的来源门控。
- 多操作 patch 当前只返回第一个作者目标。

## 选择的最小方案

1. **一次分析只采纳一条候选**：成功应用任意一条后，服务端令同一分析批次的其他候选失效，前端清空候选并提示重新检查；避免引入新的长期服务端草稿库。
2. **用户编辑即失效**：作者模式或高级模式发生手工输入/选择后，清除当前 AI 同意令牌、范围和候选，避免覆盖刚刚的手工修改。
3. **来源档案拒绝 AI apply**：带 `sources` 的来源/冲突档案不接受 AI 候选应用；作者仍可在高级模式手工维护，AI 不替代来源维护流程。
4. **作者模式只接受单操作 patch**：含多个 JSON Patch 操作的候选转入高级缓冲区，不再只聚焦第一个字段；避免跨步骤部分应用造成误解。

## 接受标准

- 同一 `bufferId` 的第二个候选在第一个候选成功应用后返回 `WB-AI-SUGGESTION-404`，不覆盖前一个缓冲结果。
- AI 分析完成后用户修改作者字段，再点击旧候选时不会发出应用请求，旧候选已清除并有明确提示。
- 来源/冲突档案的 AI apply 返回 `WB-EDITOR-SOURCE-403`，不消费候选，不返回修改后的内容。
- 多操作 patch 的 `authorTarget` 为 null，前端显示“只能放入高级缓冲区”，不跳转到第一个字段。
- 原有作者表单、分类提示、保存 CAS、批量工作台和发布包检查全部保持通过。

## 非目标

- 不引入持久化 AI 草稿数据库或新的跨进程协议。
- 不修改人物/家族运行时权限、AWAKE 主工程、Marcus、游戏目录和冻结候选。
- 不实现真实云端 Provider/本机 Worker 请求验证。

## 实现范围

- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/SuggestionStore.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringCandidateProjection.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AiCandidateGuards.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/Program.cs`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/wwwroot/studio-ai.js`
- `tools/worldbook-studio/src/Awake.WorldbookStudio.Web/wwwroot/studio-authoring-ux.js`
- 对应 Core、EditorContent 和前端回归测试

## 审查记录

- 初始独立审查：`VERDICT: REVISE`，指出四项真实缺口。
- 修订决策：采用一次采纳失效策略，拒绝为当前编辑器引入过度复杂的长期缓冲架构。
- 当前状态：offline_verified；核心、前端、A4 CLI/Web Smoke、全量回归和独立修复包均已通过。
