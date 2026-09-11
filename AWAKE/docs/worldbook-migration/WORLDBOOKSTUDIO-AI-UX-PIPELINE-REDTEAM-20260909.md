# Worldbook Studio AI 生成链路用户化重构方案红队报告

> 日期：2026-09-09  
> 方案：`WORLDBOOKSTUDIO-AI-UX-PIPELINE-REFORM-PLAN-20260909.md`  
> 性质：实施前只读方案红队  
> 结论：`REVISE`  
> 代码修改：无  
> 真实 Provider/Worker/网络：未访问

## 1. 红队范围

本轮只审查：

- Quick Authoring 用户入口和流程；
- Quick 与 legacy staged 的分离；
- UI 状态与后端 stage/pass 的关系；
- 空视角和 expressions 行为；
- candidate 多选与 create-document；
- prompt 要求和确定性校验边界；
- Draft 恢复、retry/recovery、CAS 和 review-only 兼容；
- 测试与验收是否能证明真实入口闭环。

未审查：

- Semantic Migration 候选内容；
- AWAKE 模组本体；
- Bannerlord 运行时；
- 真实 Cloud Provider；
- 真实 API Key、Token、Worker 或网络服务。

## 2. 已确认的安全基础

现有后端已经具备以下安全边界：

- `QuickAuthoringOrchestrator` 对 Quick complete 执行 Pass A semantic packet 和 Pass B constrained projection；
- `AuthoringDraftIntentFactory` 已将 intent 纳入 canonical request；
- `requested_content_tier` 缺省为 `unknown`，不是 `base`；
- `create-document` 继续要求人工采纳内容和 review-only 生命周期；
- candidate set、review decision、CAS 和 retry/recovery 已有独立状态；
- Semantic Migration 从 Quick 入口被拒绝。

因此红队结论不是推倒重写，而是方案需要在实现边界上进一步收紧。

## 3. Findings

### P1-1：Quick 必填项目前只是 UI 约束，API 仍可能被绕过

**依据**

- 方案第 5.1、5.4 规定 `authoring_goal` 和 content tier 必填；
- 当前 `AuthoringDraftIntentFactory.Create` 对 `authoringGoal` 允许 `null`；
- 当前 `NormalizeTier` 会把空值规范化为 `unknown`；
- `QuickAuthoringOrchestrator.Prepare` 先创建 Provider 状态和 consent，不在入口处强制要求 goal/tier。

**问题**

如果只改前端，直接调用 `/api/ai/authoring/draft/prepare` 仍可能建立 Quick attempt，直到后续才失败，造成：

- 用户看到“生成中”后才收到错误；
- 无意义的 consent/attempt 状态；
- UI 与 API 行为不一致。

**修正**

- Quick mode 的 `prepare` 在后端校验 `authoring_goal` 和 `requested_content_tier`；
- unknown tier 明确阻断 Provider 生成；
- 增加 API bypass test。

### P1-2：删除默认身份文本不等于禁止未请求 expressions

**依据**

- 当前 UI 的默认 perspectives 位于 `studio-draft.js` 的 draft markup；
- `AuthoringDraftRequestFactory.Create` 接收空 perspectives；
- Provider 返回结果仍可能包含 expressions；
- 现有 `create-document` 以提交的 expressions 为输入，不能仅靠 UI 文本保证为空。

**问题**

如果模型忽略“未请求身份表达”，仍可能生成 expressions。若 normalizer 只接受结构合法结果，用户会看到未主动要求的内容，或内容被带入 needs_review 档案。

**修正**

- 当 Quick intent 的 perspectives 为空时，normalizer/validator 对非空 expressions fail closed 或转 unresolved；
- create-document 再做一次越界检查；
- 添加空 perspectives focused test。

### P1-3：`uiStep`、`pipelineStage`、`generationPass` 的权威边界尚未闭合

**依据**

- 方案第 4.2 同时引入三类状态；
- 当前 Draft 本地恢复会持久化 source、quick fields、stage、candidates 和 selected IDs；
- 服务端 Draft 另有 attempt、stage、generation pass、semantic packet 和 result；
- 当前前端 `draftGenerateFacts()` 在 Quick 模式实际调用 `complete`。

**问题**

如果三套状态互相推导，可能出现：

- UI 显示 review，但服务端仍有 running attempt；
- 页面恢复到旧 uiStep，却绑定新 source hash；
- stale response 把 Quick 结果覆盖到 legacy stage；
- 用户取消后 UI 解锁，但服务端结果未知。

**修正**

- `uiStep` 只做非权威显示状态；
- `pipelineStage/generationPass` 只由服务端 request/attempt/packet 决定；
- 恢复优先以服务端 Draft 为准；
- 明确 stale response、cancel、refresh、reconcile 的 focused tests。

### P1-4：候选多选和建档动作存在用户/契约歧义

**依据**

- 当前前端同时维护 `selectedCandidateId` 和 `selectedCandidateIds`；
- 普通结果工具栏包含多选、merge、split、discard、reorder；
- `create-document` 实际只发送一个 `candidateId`；
- 方案虽提出“普通 Quick 一次只建一个”，但未把该规则写入现有行为契约和验收用例。

**问题**

用户可能勾选多个候选后直接点击建档，以为会同时创建多个档案，或不知道多选只对高级审查有效。

**修正**

- 普通 Quick 只允许一个 candidate；
- 多选必须进入高级候选审查；
- create-document 只接受唯一 candidate；
- 添加 UI 和 HTTP 双层测试。

### P1-5：Prompt 约束与确定性校验的职责仍不够硬

**依据**

- 方案第 9.2 对 prompt 提出了 source claim、quote、rumor、historical、must_not_invent 等多项要求；
- 但部分内容仍以“Prompt 必须要求”表述，未逐项映射到 normalizer、validator 或 candidate lifecycle；
- AI 应用工程规则不允许把 prompt 视为安全边界。

**问题**

模型不遵守 prompt 时，方案可能仍只展示 warning，而不是稳定阻断：

- proposition 超出 quote；
- historical 被写成 current；
- must_not_invent 新增 entity/date/war；
- 未请求视角却返回 expressions。

**修正**

为每条高风险语义要求建立矩阵：

```text
规则 → 结构字段 → 确定性校验 → 用户可见结果 → focused test
```

没有确定性校验的要求只能作为非阻断提示，不能写成安全保证。

### P2-1：方案第一批仍然偏大，可能同时改动两个 UI 世界

**问题**

方案一方面要求 Quick 独立模板，另一方面要求同步入口、结果卡片、作者编辑器、CSS、prompt、validator 和文档。对现有动态 DOM 架构来说，首批变更面过大，容易出现：

- legacy staged 被意外改写；
- 旧 DOM listener 残留；
- 前端 harness 通过静态检查但真实入口未接线。

**修正**

按“Quick 输入/生成闭环 → Quick 结果审查 → 建档提示 → 文档/作者编辑器”分批实施。legacy staged 第一批只保留并改入口说明，不重构。

### P2-2：用户动作“确认”仍可能与“采纳”混淆

**问题**

方案区分了生成、采纳、建档和保存，但还需要明确：

- 逐条采纳是事实/表达级；
- 选择 candidate 是候选级；
- 创建待审核档案是档案级；
- 保存是文件级。

**修正**

结果页显示四级状态和当前层级，不使用单一“确认”按钮覆盖不同语义。

### P2-3：方案缺少明确的可访问性验收

**问题**

Quick 结果页包含动态生成、details、候选 checkbox、dialog、错误提示和 loading 状态，但方案没有写：

- 键盘焦点；
- 对话框关闭后的焦点返回；
- `aria-live`；
- loading/error 状态；
- 无障碍名称。

**修正**

增加 Quick 输入、生成中、结果审查、阻断错误四种状态的键盘和可访问性验收。

### P3-1：Provider 选择是否属于 Quick 主流程仍未决定

**问题**

当前 UI 允许 local/cloud 选择，方案又建议主流程只保留三项输入。若不决定，用户仍会遇到“本机 AI Worker”和“云端 AI Provider”的实现概念。

**修正**

默认使用当前已配置 Provider；高级设置中才允许切换，并用用户语言解释数据边界。

## 4. 验收缺口

本方案原有验收还缺少：

1. API 绕过 UI 的 Quick 必填测试；
2. 空 perspectives 拒绝 expressions 测试；
3. 多 candidate 不能直接 create-document 测试；
4. stale response + local/server draft reconciliation 测试；
5. prompt 不遵守时 deterministic fail-closed 测试；
6. 键盘/焦点/动态状态可达性测试；
7. 真实入口 `button → route → result → create-document → author editor` 的闭环 smoke。

## 5. 红队结论

方案方向正确，后端复用策略正确，未发现需要推倒重写的架构问题。

但上述 P1-1 至 P1-5 影响输入契约、语义安全和建档边界，不能作为实现后再补的 P2。

**VERDICT: REVISE**

## 6. 建议的下一轮最小修订

在代码实现前把方案补成：

1. Quick prepare 后端必填和 unknown tier gate；
2. 空 perspectives 的确定性 expressions gate；
3. 三套状态权威边界；
4. candidate 单选/多选规则；
5. Prompt → validator → UI → test 矩阵；
6. 可访问性和真实入口闭环验收。
