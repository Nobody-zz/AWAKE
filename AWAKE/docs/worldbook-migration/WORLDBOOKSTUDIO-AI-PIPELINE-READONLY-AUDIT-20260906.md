# Worldbook Studio 现有 AI 生成链路只读审计

> 日期：2026-09-06  
> 状态：`read_only_audit / needs_review`  
> 范围：读取现有 Worldbook Studio AI 助手、单份参考资料草稿、完整候选生成、批量事实提取和相关测试；不修改 Studio、AWAKE、源文件或运行时产物。

## 1. 审计结论

当前系统已经存在两条不同的 AI 路径：

```text
A. AI 助手检查链路
档案 → AssistanceRequest → Provider → Suggestion → 人工应用/拒绝

B. 参考资料生成链路
参考资料 → facts → metadata → expressions → create-document
```

另外还有一条批量路径：

```text
扫描资料 → 批量事实提取 → 事实审核 → 批量档案/元数据流程
```

重要结论：

1. 当前系统**确实支持“用简单提示词/参考资料生成完整候选文件”**，入口是 `complete` draft stage。
2. 当前 `complete` 是“完整 authoring 草稿生成”，不是语义迁移意义上的完整证据链。
3. 当前生成契约已经有 `facts`、`metadata`、`expressions`、`candidates`、逐字 evidence、quote hash 和 `review_status=pending`。
4. 当前生成契约还没有把 `Proposition → Claim → SourceOrigin → TargetSpan` 作为一等输出对象。
5. 因此当前链路适合：
   - 从一份简单资料生成待审核完整草稿；
   - 由作者逐条审核事实、简介和身份表达；
   - 生成多个自然内容边界候选。
6. 当前链路不适合直接承担：
   - 高保真旧世界书迁移；
   - 视角、认识论、历史时间和 polarity 的严格保持；
   - claim-level 语义审计；
   - 自动确认正式 entity/profile、content tier 或权限。

结论不是“当前链路无效”，而是：**它是 authoring draft pipeline，不是 semantic migration pipeline。两者应共享底层证据和状态契约，但不能混为一条路径。**

## 2. 实际入口与调用链

### 2.1 单份参考资料草稿

代码入口：

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Web\AuthoringDraftEndpoints.cs`

当前路由：

```text
POST /api/ai/draft/prepare
POST /api/ai/draft/generate
GET  /api/ai/draft/attempts/{draftId}/{attemptId}
POST /api/ai/draft/attempts/{draftId}/{attemptId}/reconcile
GET  /api/ai/draft/review-projection/{draftId}
POST /api/ai/draft/create-document
POST /api/ai/draft/review-decision
```

同一组路由也挂在：

```text
/api/ai/authoring/draft
```

流程：

1. `prepare` 检查 Provider 状态、创建/读取 Draft、核对 source content hash。
2. `prepare` 根据 stage 生成一次性 consent 和 attempt。
3. `generate` 消费 consent，创建 Provider，调用 `GenerateAsync`。
4. Provider 返回 JSON 后，经过 response extraction、normalization 和 parser。
5. `AuthoringDraftStore.SaveResult` 持久化结果和候选状态。
6. 用户审核事实、候选、metadata、expressions。
7. `create-document` 只接收已采纳内容，写入 `needs_review` 作者草稿。
8. `review-decision` 记录候选合并、拆分、丢弃或排序决定。

### 2.2 四种 draft stage

核心契约位于：

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Core\AuthoringDraftContracts.cs`

当前 stage：

```text
facts
metadata
expressions
complete
```

UI 默认引导的是前三步：

```text
1. 提取客观事实
2. 生成档案简介
3. 生成身份表达
```

UI 位置：

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Web\wwwroot\studio-draft.js`

和：

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Web\wwwroot\index.html`

`complete` stage 已存在于后端契约和 prompt，但当前普通 UI 主要暴露分阶段工作流，没有形成清晰的“简单指令 → 完整文件草稿”前台入口。

## 3. 当前 prompt 实际能力

### 3.1 分阶段 prompt

`AuthoringDraftRequestSerializer` 中的 `SystemPrompt` 对不同 stage 施加了不同限制：

- `facts`：只提取资料明确写出的客观事实；
- `metadata`：只根据已采纳事实生成标题、摘要和分类；
- `expressions`：只根据已采纳事实和 metadata 生成身份表达；
- 所有结果要求 `review_only=true`；
- 所有 fact/expression 要求 `review_status=pending`；
- evidence 要求有 `reference_id`、`locator`、`quote`、`quote_hash`；
- 无法满足契约时允许返回空数组并写入 warnings。

这是一个比较稳健的“候选生成”约束，尤其避免了 AI 直接覆盖正式档案。

### 3.2 完整文件 prompt

`CompleteSystemPrompt` 已明确要求：

- 根据参考资料生成完整但待人工审核的世界知识候选；
- 不补造没有依据的世界事实；
- 返回 `facts`、`metadata`、`expressions`、`candidates`；
- 资料存在多个自然边界时，优先返回多个 candidate；
- 每个 candidate 的 `review_status` 必须为 `pending`；
- 每条 fact/expression 要带可定位逐字 evidence 和 quote hash；
- 无法形成可靠候选时返回空 candidates 和 warnings。

这说明当前链路已经具有“简单指令生成完整文件草稿”的后端基础。

但它的完整文件语义仍然是：

```text
facts + metadata + expressions + evidence
```

而不是：

```text
propositions + claims + origins + target_spans + semantic review
```

## 4. Provider 和输出处理

Provider 实现：

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Core\AuthoringDraftProviders.cs`

当前支持：

```text
cloud → OpenAI-compatible Provider
local → Local Worker Provider
```

共同处理流程：

```text
请求构造
→ Provider HTTP 调用
→ JSON 提取
→ response normalization
→ result parser
→ binding validation
→ candidate lifecycle projection
```

已有保护：

- endpoint/provider 配置检查；
- response 大小限制；
- JSON 格式检查；
- request hash/source hash 绑定；
- 可恢复输出失败的有限重试；
- timeout、transport、Provider 错误分类；
- candidate fingerprint；
- provider fingerprint；
- source snapshot/content hash；
- review-only 状态。

当前缺口：

- 不同 semantic stage 的独立 prompt revision 尚未形成；
- 没有 proposition coverage validator；
- 没有 claim/source-origin/target-span 三向检查；
- binding validation 仍主要验证 request/source hash 和候选结构；
- source quote 可定位不等于 proposition 被完整覆盖。

## 5. Normalizer、审核和落盘行为

response normalizer：

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Core\AuthoringDraftResponseNormalizer.cs`

当前行为包括：

- 允许有限的兼容字段；
- 规范化 candidates；
- 默认候选 review status 为 pending；
- 检查 evidence quote；
- 尝试在当前资料中定位 quote；
- 无法定位时保留候选并生成 warning；
- source span 被作为 evidence/compatibility data 处理；
- 不把未定位 quote 静默升级为已验证事实。

这对一般 authoring 很有用，但要注意：

```text
quote 可定位 ≠ 该 quote 支持整条 fact
quote 可定位 ≠ 没有遗漏同句中的其他 proposition
quote 可定位 ≠ 视角、时间和极性已保持
```

create-document：

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Core\AuthoringDraftDocumentBuilder.cs`

当前会：

- 只使用已采纳 submission；
- 生成作者草稿；
- 保留 source content hash、packet hash、provider fingerprint、candidate fingerprint 和 evidence；
- 写入 `review_status=draft`；
- 不直接写入正典。

这是正确的安全边界，但它仍然没有保存迁移所需的 proposition/claim/target span 图谱。

## 6. 批量 AI 路径

批量核心：

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Core\BatchProviderService.cs`

当前批量路径主要做：

```text
source unit
→ facts extraction
→ cache
→ fact review repository
→ later metadata/authoring flow
```

已有：

- `prompt_revision`；
- `output_schema_revision`；
- cache key；
- provider fingerprint；
- model parameters；
- registry snapshot hash；
- accepted fact set hash；
- retry/recovery；
- item lease；
- failed/unknown_result；
- 不把失败结果当作成功结果。

批量 prompt 当前是 facts-first，适合大批量资料预处理。

但它没有直接承担：

- 完整文件生成；
- claim-level semantic synthesis；
- source perspective conflict resolution；
- target span 对齐；
- 人工 candidate 语义决策。

## 7. 现有测试覆盖的真实范围

主要测试目录：

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio\tests\Awake.WorldbookStudio.Draft.Tests`

当前已覆盖的方向包括：

- response parser；
- wrapped JSON extraction；
- empty output；
- unknown fields；
- review-only evidence；
- quote 无法定位时保留并 warning；
- Provider retry；
- candidate 结构；
- pending review status；
- 请求/响应 hash；
- Draft 生命周期。

这些测试证明的是：

```text
AI 输出能否被安全解析和保存
```

尚未充分证明：

```text
AI 输出是否完整覆盖源命题
AI 是否新增了源文没有的命题
AI 是否抹平了来源视角
AI 是否把历史写成当前
AI 是否把传闻写成事实
AI 是否把一个 span 中的多个命题错误合并
```

## 8. 与迁移方法论的对应关系

| v1.2 要求 | 当前链路情况 | 判断 |
|---|---|---|
| SourceAtom | 有 source text、hash、quote、locator | 部分满足 |
| PropositionInventory | 没有独立对象 | 缺失 |
| Claim normalization | fact kind/text/inferred | 部分满足 |
| Epistemic kind | `kind` + `certainty` + `inferred` | 不够细 |
| Perspective | expression 有，fact 没有统一字段 | 不完整 |
| Time scope | 没有一等字段 | 缺失 |
| Polarity | 没有统一字段 | 缺失 |
| Source origin | evidence/reference_id/locator | 部分满足 |
| Target span | source_spans/evidence 兼容字段 | 不等价 |
| Candidate split | candidates + segmentation reason codes | 已有 |
| Human review | pending/accept/reject/review decision | 已有 |
| Permission safety | Provider/consent/authority 有边界 | 结构有，语义映射缺失 |
| Entity ID safety | profile_ids 有检查，但 source marker 迁移未闭合 | 部分满足 |

## 9. 对“简单提示词生成全文件”的正确定位

用户补充的需求应纳入完整链路，但不应取代迁移链路。

建议定义两种模式：

### Mode A：Quick Authoring

适用：

- 用户提供一份简单资料；
- 用户给出简短目标，例如“整理成一份地理档案”；
- 不要求逐项追踪旧 Variant 的全部语义；
- 允许 AI 生成待审阅的完整新档案。

目标输出：

```text
完整 authoring candidate
facts
metadata
expressions
evidence
warnings
review_status=pending
```

### Mode B：Semantic Migration

适用：

- 旧世界书、多 Variant、规则、人物背景；
- 要求保留视角、时间、认识论、冲突和未决状态；
- 需要 source atom → proposition → claim → target span 闭环。

目标输出：

```text
semantic worksheet
claim graph
conflict groups
rewritten candidate
semantic review decision
```

Quick Authoring 可以在资料不足时生成较完整的草稿，但不能把它伪装成 Semantic Migration 的证据等级。

## 10. 推荐的完整链路改造方向

### 10.1 保留现有 complete stage

不要删除现有 `complete`。它已经是简单提示词生成完整候选的自然入口。

需要做的是补充明确的请求模式：

```text
mode=quick_authoring
mode=semantic_migration
```

默认：

```text
quick_authoring → needs_review
semantic_migration → needs_review + semantic evidence required
```

### 10.2 Quick Authoring 的输入

建议输入增加：

```text
authoring_goal
requested_domain
requested_subdomain
requested_audience
requested_perspectives
style_constraints
must_preserve[]
must_not_invent[]
```

其中最重要的是：

```text
must_preserve
must_not_invent
```

示例：

```text
must_preserve:
  - 地理位置
  - 物产
  - 来源中的传说限定
  - 当前状态未知

must_not_invent:
  - 新人物
  - 新年份
  - 新战争
  - 当前归属
  - 正式 entity ID
```

### 10.3 Quick Authoring 的输出

最小可行输出：

```json
{
  "schema_version": "worldbook.quick-authoring.result.v1",
  "review_only": true,
  "status": "needs_review",
  "metadata": {},
  "facts": [],
  "expressions": [],
  "evidence": [],
  "warnings": [],
  "unresolved": []
}
```

建议额外增加：

```text
proposition_count
unsupported_risk_count
unresolved_count
source_coverage
```

这些字段用于审阅和排序，不用于自动批准。

### 10.4 Semantic Migration 的输入输出隔离

Semantic Migration 不应复用 Quick Authoring 的 fact-only 输出作为最终证据。应增加独立 stage：

```text
propositions
claims
conflicts
target_spans
```

只有 semantic stage 完成后，才允许调用现有 `complete` authoring 组织正文；而且正文必须引用已生成的 claim IDs。

## 11. 可落地的最小方案

在不立即修改代码的前提下，最小实施顺序是：

1. 把当前 `complete` 明确命名为 `quick_authoring` 语义，不改变其 `needs_review` 安全状态。
2. 在请求中增加用户目标、受众、必须保留和禁止新增字段。
3. 在返回中增加 unresolved、coverage 和 risk summary。
4. 对现有 `facts` 做一层 proposition coverage 检查。
5. 用五候选作为 semantic migration 专用 fixture，不能混入 quick authoring 通过率。
6. 未来再增加 semantic migration route，复用 Provider transport、hash、consent、retry 和 review state。

## 12. 当前不要做的事

- 不要把 `complete` 直接改成自动写正式世界书；
- 不要把 quick authoring 的 `fact` 当成迁移 claim；
- 不要把 quote 定位成功当成语义覆盖成功；
- 不要让简单提示词生成结果自动获得 canon 或 runtime 状态；
- 不要因为 UI 已有“完整草稿”概念，就声称迁移链路已闭合；
- 不要在本轮修改 Studio 或真实 Provider。

## 13. 只读审计文件清单

本次读取的关键文件：

- `AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Core\AuthoringDraftContracts.cs`
- `AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Core\AuthoringDraftProviders.cs`
- `AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Core\AuthoringDraftResponseNormalizer.cs`
- `AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Core\AuthoringDraftSubmissionValidator.cs`
- `AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Core\AuthoringDraftDocumentBuilder.cs`
- `AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Core\BatchProviderService.cs`
- `AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Web\AuthoringDraftEndpoints.cs`
- `AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Web\AuthoringDraftStore.cs`
- `AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Web\Program.cs`
- `AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Web\wwwroot\studio-draft.js`
- `AWAKE\tools\worldbook-studio\src\Awake.WorldbookStudio.Web\wwwroot\index.html`
- `AWAKE\tools\worldbook-studio\新手指引_世界书内容编辑者.md`
- `AWAKE\tools\worldbook-studio\tests\Awake.WorldbookStudio.Draft.Tests\Program.cs`

## 14. 未完成与限制

- 本次是只读链路审计，不修改 Studio 代码。
- 没有访问真实 Cloud Provider、API Key、Token 或 Worker。
- 没有启动 Bannerlord。
- 没有运行 UI 或真实 Provider。
- 没有把 `complete` route 证明为已在普通 UI 中完整暴露；目前确认后端契约和 prompt 已存在，UI 主要引导分阶段流程。
- 没有把 Quick Authoring 和 Semantic Migration 代码化区分；本报告只提出可落地设计。

## 15. 最终判断

当前 AI 链路的正确描述是：

> **它已经能够根据简单提示和参考资料生成一份完整的、带证据、待人工审核的 authoring 草稿；但它还不是能够自动完成高保真语义迁移的链路。**

因此，后续设计应采用“双轨制”：

```text
简单提示词 → Quick Authoring → 完整待审核文件
旧世界书迁移 → Semantic Migration → 逐命题审计后再生成待审核文件
```

两条路径共享：

- Provider transport；
- request/source hash；
- consent；
- retry/recovery；
- candidate fingerprint；
- review-only；
- 人工审核状态。

两条路径不共享：

- 语义通过标准；
- claim/proposition 证据等级；
- 自动生成范围；
- 是否允许多源归纳；
- 发布前门禁。

