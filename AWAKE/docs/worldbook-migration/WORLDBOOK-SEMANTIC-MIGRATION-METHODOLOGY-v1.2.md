# Worldbook 语义迁移方法论 v1.2

> 状态：`red_team_repaired / v1.2_operational_guidance / semantic_review_required`  
> 日期：2026-09-06  
> 继承：v1.1 的 source atom → claim → target span 闭环、权限隔离、快照绑定和人工审批门。

## 1. v1.2 的新增结论

v1.1 已经解决了“不要直接搬运旧 Variant”的原则问题。五候选复审表明，实际执行还缺少三道可操作的中间门：

1. **命题预算门**：正文命题数不能默默超过 worksheet claim 数。
2. **跨度原子性门**：一个 target span 不得同时承载不同主体、视角或认识论类型。
3. **推导显式化门**：由多个源事实归纳出的解释必须单独登记，不能借用最接近的 source fact claim。

因此，v1.2 把迁移拆成六层：

```text
source atom
→ proposition inventory
→ claim normalization
→ epistemic/perspective/time analysis
→ target span authoring
→ human semantic decision
```

任何一层没有完成，都只能是 `needs_review`。

## 2. 可复用撰写流程

### 第一步：锁定证据边界

记录：

- source root；
- parent snapshot；
- child snapshot；
- canonical manifest hash；
- source file hash；
- registry 状态；
- parse gate 状态。

先锁定证据，再开始理解文本。不要让候选反向证明源文件正确。

### 第二步：建立 proposition inventory

逐句而不是逐段记录命题。每条命题至少回答：

- 谁/什么是主体？
- 发生了什么谓词？
- 客体或结果是什么？
- 这是事实、关系、状态、传闻还是解释？
- 谁的视角？
- 适用于过去、现在还是未知时间？
- 是肯定、否定还是未决？

复合句必须拆成多个 proposition。尤其要拆开：

- 地理事实与战略用途；
- 历史状态与当前状态；
- 传闻与考证结论；
- 社会禁忌与物理不可进入；
- 源事实与编辑判断。

### 第三步：规范化 claim

claim 不是句子缩写，而是可审计的语义单位。规范化时：

- 保留主体；
- 保留认识论类型；
- 保留视角；
- 保留时间范围；
- 保留极性；
- 限制对象范围，不把“部分”扩大为“全部”；
- 不把“叙述认为”改成“世界确实如此”。

### 第四步：标注推导距离

每个 proposition 标记：

```text
direct_source
multi_source_synthesis
editorial_interpretation
unresolved
```

规则：

- `direct_source` 可进入 fact/relation，但仍需来源绑定；
- `multi_source_synthesis` 必须保留多个 source origin；
- `editorial_interpretation` 必须使用 interpretation claim；
- `unresolved` 不得写成确定事实。

### 第五步：重写目标正文

目标正文不是旧 Variant 的压缩版，而是面向知识体系的重新组织。推荐顺序：

1. 稳定地理/实体事实；
2. 物产、交通或关系；
3. 视角化历史叙述；
4. 传闻和不确定性；
5. 必要的 unresolved 限制。

目标正文每一段只承载同一类知识。若一句话需要两个不同 claim，优先拆句。

### 第六步：反向审阅

完成重写后，不能只从源到目标检查，还要从目标反向检查：

- 正文是否出现 worksheet 没有的命题？
- 是否出现 source 没有的主体、时间或因果？
- 是否把旧 When 当成运行时权限？
- 是否把 marker 当成实体 ID？
- 是否出现“看起来合理但源文没说”的补全？

## 3. Worldbook Studio AI 生成链路指导意见

### 3.1 不要让模型直接生成最终正文

推荐链路：

```text
source parser
→ source atom store
→ proposition extractor
→ claim normalizer
→ conflict/perspective classifier
→ draft author
→ structural validator
→ semantic review queue
```

模型只能生成中间层产物；最终正文必须由 claim、origin 和 review 状态约束。

### 3.2 采用双通道输出

AI 每次生成应同时返回：

```text
semantic layer:
  propositions[]
  claims[]
  source_origin_ids[]
  epistemic_kind
  perspective
  time_scope
  polarity

authoring layer:
  title
  summary
  target_text
  target_spans[]
```

如果只有自然语言正文，没有语义层，就不能进入候选目录。

### 3.3 加入“命题预算”和“跨度原子性”验证

建议增加离线检查：

```text
正文命题 ⊆ claims
claims ⊆ source origins
target spans 覆盖正文命题
一个 span 不跨越冲突的 epistemic_kind/perspective/time_scope
```

验证失败时输出 `needs_review`，不能自动修补成 `base` 或 `active`。

### 3.4 将“来源不足”作为正常输出

提示词和结构化输出都应允许：

```text
keep
revise
drop
unresolved
```

模型不应被要求“把每个源句都写进正文”。丢弃铺陈、保留不确定性、拆分视角都属于正常迁移结果。

### 3.5 将旧字段隔离在 evidence 层

以下字段默认只能作为证据，不能直接进入正文或权限：

- `Keywords`；
- `RagShortTexts`；
- `When`；
- `TextMappings`；
- 旧 settlement/entity marker；
- 旧 Variant 名称。

它们可以影响检索、审阅排序和 source locator，但不能绕过 claim 和 review。

## 4. 可落地的 Studio 优化方案

### Phase A：先做离线生成契约

新增或确认以下中间对象：

- `SourceAtom`；
- `Proposition`；
- `ClaimDraft`；
- `TargetSpanDraft`；
- `ConflictGroup`；
- `SemanticReviewDecision`。

每个对象都绑定 `source_snapshot_id` 和内容 hash。

### Phase B：把生成拆成可重试的小步骤

不要一次调用模型生成完整 Document。建议按以下粒度：

1. 每个 source unit 提取 proposition；
2. 每组 proposition 生成 claims；
3. 单独做冲突、视角和时间分析；
4. 单独生成 summary；
5. 单独生成 detail expressions；
6. 最后做 span alignment。

这样可以定位错误来源，也能只重跑失败步骤。

### Phase C：增加确定性 gate

模型输出进入下一步前，必须通过：

- JSON schema；
- source locator 存在性；
- origin hash；
- claim/source snapshot join；
- proposition coverage；
- polarity；
- subject/predicate/object；
- perspective；
- time scope；
- provisional ID leakage；
- permission safety；
- content tier safety。

### Phase D：建立评测 fixture

至少维护以下 fixture：

- 传闻不能变事实；
- 历史不能变当前；
- 视角不能被抹平；
- 一个正文句不能隐藏两个 claim；
- 缺 source 时必须 unresolved；
- 旧 When 不得生成 grant/deny；
- 旧 marker 不得生成正式 entity ID；
- source claim 少于正文命题时必须失败。

### Phase E：记录 AI 质量指标

建议记录：

- proposition recall；
- unsupported proposition rate；
- source coverage rate；
- perspective preservation rate；
- time-scope preservation rate；
- polarity preservation rate；
- target-span coverage；
- unresolved honesty rate；
- 人工返工率；
- 每个 source unit 的 token、耗时和重试次数。

单纯记录“JSON 解析成功”或“模型调用成功”不算质量指标。

## 5. 推荐的模型行为约束

模型提示中应明确：

1. 源文本是证据，不是可直接复制的正文。
2. 先列命题，再写正文。
3. 每条命题都要指出证据位置。
4. 无法确定时输出 `unresolved`。
5. 不得补写当前归属、正式 ID、时代、权限或 content tier。
6. 不得把不同文化视角合并成无视角事实。
7. 不得将传闻、神话、贵族宣传或船员说法升级为事实。
8. 不得因为目标知识体系需要完整而发明缺失命题。

## 6. v1.2 的停止条件

以下任一项存在时，停止自动生成下一层：

- source parse gate 未闭合；
- source registry 未闭合；
- candidate 与 child snapshot 不匹配；
- proposition coverage 失败；
- target span 缺失；
- perspective/time/polarity 检查失败；
- candidate revision 未生成新 hash；
- review state 不是 `needs_review` 或 `accepted` 的明确状态；
- 旧 When、旧 marker 或 provisional ID 越过中间层。

## 7. 当前试点的适用结论

五候选说明了 v1.2 的必要性：

- 不是所有“源文有依据”的内容都已经形成可审计 claim；
- 不是所有 claim 都能安全合并到同一个正文 span；
- “合理推导”必须显式化；
- 结构验证通过仍不能代替人工语义判断。

本方法论的目标不是提高自动批准率，而是让错误更早暴露、让每次修订可定位、可重跑、可回滚、可审计。

## 8. 与“简单提示词生成全文件”的关系

完整的 Worldbook Studio 生成链路不能只按迁移问题设计。当前应明确区分两种工作模式：

### Quick Authoring

用户给出一份参考资料和简短目标，由 AI 生成一份完整的待审核 authoring 草稿：

```text
参考资料 + 简单目标
→ facts
→ metadata
→ expressions
→ 完整 candidate
→ needs_review
```

它可以帮助作者从零开始快速建立文件，但不代表已经完成高保真语义迁移。Quick Authoring 至少仍要保留：

- source evidence；
- quote 和 locator；
- warnings；
- unresolved；
- `review_only=true`；
- `review_status=pending`。

### Semantic Migration

旧世界书、多 Variant 或规则库必须经过：

```text
source atom
→ proposition inventory
→ claim normalization
→ perspective/time/epistemic analysis
→ target span
→ human review
```

不能把 Quick Authoring 生成的 fact 数组直接当作迁移 claim，也不能用 Quick Authoring 的完整 candidate 反向证明源世界书正确。

两条模式可以共享 Provider、哈希、授权、缓存和审核状态，但必须分开统计和验收：

```text
Quick Authoring：完整性、可编辑性、证据可见性
Semantic Migration：命题覆盖、语义保持、冲突和视角可追溯
```

如果用户只给出简单提示词，系统可以生成完整文件草稿；如果用户要求迁移旧世界书，系统必须切换到 Semantic Migration，不得以“完整文件”替代语义审计。
