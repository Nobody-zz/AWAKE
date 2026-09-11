# Worldbook Studio AI 生成管线持续红队——第八轮

> 日期：2026-09-06  
> 范围：Batch facts Provider 边界、缓存绑定、双路由共享和空结果结算。  
> 状态：`REVISE`  
> 本轮性质：只读静态攻击追踪；未修改实现。

## 1. 执行边界与基线

本轮未：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问真实 Cloud Provider、API Key、Token、Worker 或网络；
- 读取/修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改 Worldbook Studio 实现。

已知离线基线：

- DraftTests：`33/33 PASS`
- BatchTests：`21/21 PASS`
- Worldbook Studio harness：`113/113 PASS`

## 2. 结论

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现直接进入 canon/runtime 的无权限路径 |
| P1 | 2 | Batch 可能错误结算空结果，缓存/源单元绑定需要更强约束 |
| P2 | 2 | Batch facts 语义降级和双路由兼容证据仍需显式化 |

本轮不改变总体状态：

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
CONTINUE REDTEAM
```

## 3. 新增发现

### RT-R8-01：facts stage 收到 candidates 结构时可能静默提交空 facts 成功

**严重度：P1**

**攻击输入**

Provider 在 `facts` 请求中返回：

```json
{
  "stage": "facts",
  "review_only": true,
  "facts": [],
  "metadata": null,
  "expressions": [],
  "candidates": [
    {
      "id": "candidate.x",
      "facts": [{"id": "fact.x", "...": "..."}],
      "metadata": null,
      "expressions": [],
      "review_status": "pending"
    }
  ],
  "warnings": []
}
```

**代码链路**

```text
AuthoringDraftResponseNormalizer.Normalize
→ AuthoringDraftResultParser.Parse
→ BatchProviderService.ExtractFactsAsync
→ TranslateFacts(result.Facts)
→ BatchFactReviewRepository.CommitFacts
```

`BatchProviderService` 只消费 `result.Facts`，不会把 `result.Candidates[*].Facts` 作为 facts stage 的结果。若顶层 facts 为空，可能得到空 `BatchFactInput` 集合并继续 commit。

**结果**

- Provider 实际返回了候选事实，但 Batch 结算为空；
- item 可能被标记为成功，队列继续推进；
- warning 没有明确说明“响应形状错误”；
- Quick/Complete 语义误进入 Batch facts 结算；
- 后续作者看到“没有提取事实”，但无法区分模型空结果和结构错路由。

**修复要求**

- Batch facts stage 明确拒绝非空 `candidates`；
- 或从 candidates 提取 facts，但必须显式转换并记录降级；
- `facts=[]` 且 candidates 非空时不得静默成功；
- 空 facts 成功必须有明确的 `zero_output` 结算原因；
- Batch 不得被标记为 Quick Authoring 或 Semantic Migration。

**回归用例**

```text
facts + 顶层 facts 非空 → 正常
facts + candidates 非空 → 稳定格式/阶段冲突错误
facts + facts=[] + candidates=[] → 显式 zero_output
```

---

### RT-R8-02：Batch Provider 请求使用 sourceUnit 文本，但 cache key 依赖外部声明 hash

**严重度：P1**

**攻击前提**

调用 `BatchProviderService.ExtractFactsAsync` 时：

```text
item.normalized_content_hash = H1
item.unit_hash = U1
sourceUnit.text = 已被替换的文本
```

而 `sourceUnit` 的实际文本哈希没有在该方法入口重新与 item 声明绑定。

**代码证据**

`BatchProviderService.cs:56-73` 先用 `item` 字段建立 cache key，再用 `sourceUnit["text"]` 构造 `AuthoringDraftRequest` 和 snapshot。

**结果**

- 缓存身份指向 H1/U1；
- 实际 Provider 请求内容来自另一份 sourceUnit；
- 缓存命中、Provider 请求、packet snapshot 三者可能不是同一源；
- 在上游校验失效或未来复用该 service 时会产生跨源结果复用。

当前 Batch 上游通常会提供已验证 source unit，因此这是入口防御缺口，不是已证明的外部用户直接利用路径。

**修复要求**

- service 入口重新计算 sourceUnit 文本 hash；
- 与 item 的 normalized/unit hash 严格比较；
- 不匹配时返回 CAS/binding conflict；
- cache key 必须基于已验证 snapshot，而非只信任调用者字段。

**回归用例**

```text
item hash 与 sourceUnit 一致 → 正常
item hash 与 sourceUnit 不一致 → 409/422，不读写 cache、不 commit
```

---

### RT-R8-03：Batch facts 只保留 risk/notes，无法携带 Quick Authoring 的 unresolved 归属

**严重度：P2**

Batch 当前通过 `TranslateFacts` 转换为 `BatchFactInput`，保留：

```text
text
kind
certainty
risk
evidence
notes
```

但不保留 Quick/Complete 结果可能携带的：

```text
candidate_id
warning scope
unresolved id
proposition/claim
source span explanation
```

这本身符合 Batch facts-only 设计，但必须在契约上明确禁止把 Batch 结果升级为 Quick Authoring 或 Semantic Migration 结果，否则后续 UI 可能误用 facts review 结果作为完整候选。

**修复要求**

- Batch public projection 明确 `mode=batch_facts`；
- 丢弃的字段记录为非适用，而不是静默消失；
- 不允许 Batch create-doc 路径声称拥有完整 candidate 证据。

---

### RT-R8-04：双 Draft 路由共享实现，但兼容契约没有独立一致性测试

**严重度：P2**

当前两组路由都由同一方法注册：

```text
/api/ai/draft/...
/api/ai/authoring/draft/...
```

这降低了复制实现风险，但新增 Quick Authoring 字段后仍需证明：

- 两个前缀接收完全相同 DTO；
- 相同请求得到相同 request hash；
- 失败码、retry/recovery、审查和建档行为一致；
- 旧路由没有被某个中间件或 UI 分支单独处理。

**修复要求**

- 为两个前缀增加参数化 HTTP contract test；
- 对每个生成/审查/建档路由比较 status、error、body shape；
- 旧路由只能是兼容别名，不得形成第二套语义。

## 4. 保留的正向边界

- Batch cache 已有 key、result hash、commit marker 三件套校验；
- Batch item commit 仍有 revision/CAS；
- Draft Provider 工厂仍区分 cloud/local；
- 两组 Draft 路由当前共享同一 endpoint implementation；
- 既有 Draft/Batch 基线测试全部通过；
- 未发现直接发布或运行时注入路径。

## 5. 收敛状态

```text
P0 = 0
本轮新增 P1 = 2
累计待处置 P1 = 21
连续无新增 P0/P1 = 0 轮
下一步 = 继续红队或进入有授权的修复批次
```

