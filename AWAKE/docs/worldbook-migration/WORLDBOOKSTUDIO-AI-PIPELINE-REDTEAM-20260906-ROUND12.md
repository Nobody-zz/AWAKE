# Worldbook Studio AI 生成管线持续红队——第十二轮

> 日期：2026-09-06  
> 范围：完整 Draft candidate 写入 authoring 文档时的分类字段、内容层级和用户确认边界。  
> 状态：`REVISE`  
> 本轮性质：只读静态攻击追踪 + Draft 离线基线；未修改实现。

## 1. 执行边界与基线

本轮未：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问真实 Cloud Provider、API Key、Token、Worker 或网络；
- 读取/修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改 Worldbook Studio 实现。

本轮基线：

- DraftTests：`33/33 PASS`

## 2. 结论

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现直接进入 canon/runtime 的无权限路径 |
| P1 | 2 | 完整候选落盘会丢失分类语义，UI 无法让作者确认完整分类 |
| P2 | 1 | AI metadata 与用户/作者权威字段的覆盖顺序尚未冻结 |

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
CONTINUE REDTEAM
```

## 3. 新增发现

### RT-R12-01：完整 candidate 的 subdomain 和 related_domains 在建档时丢失

**严重度：P1**

**攻击链**

```text
complete candidate.metadata
  ├─ domain = politics
  ├─ subdomain = succession
  └─ related_domains = [war]
→ create-document
→ CreateGeneratedDocumentFromDraft
→ AuthoringTemplateFactory.Create(..., subdomain omitted, relatedDomains omitted)
→ AuthoringDraftDocumentBuilder.Build
→ document 只重新设置 domain
```

**代码证据**

`Application.cs:310-319` 的 `CreateGeneratedDocumentFromDraft` 没有接收 `subdomain` 或 `relatedDomains` 参数。

`Application.cs:341` 调用：

```csharp
AuthoringTemplateFactory.Create(
    ...,
    registries)
```

没有传入模板工厂支持的可选 `subdomain` 和 `relatedDomains`。

`AuthoringDraftDocumentBuilder.cs:78-81` 只写入：

```csharp
document["domain"] = domain;
document["status"] = "needs_review";
document["revision"] = 1;
```

没有恢复 candidate metadata 中的二级主题和相关领域。

**结果**

AI 已生成的完整 candidate 在进入 authoring 文档后会丢失：

```text
subdomain
related_domains
```

这会导致用户重新进入作者表单后必须重新分类，也可能让 authority/编译阶段看到一个分类不完整的档案。

当前 UI 只显示 `draftDomainCandidate`，没有 subdomain 选择控件，也没有保存 related domains。

**修复要求**

- `create-document` 请求必须明确接收 subdomain/related domains；
- 这些字段必须由作者确认后的值作为权威来源；
- 服务端用 taxonomy 校验，不直接信任 AI metadata；
- 建档后 readback 必须保留分类字段；
- 缺失 subdomain 时阻断或明确进入 partial 状态；
- related domains 不能在落盘时静默丢弃。

**回归用例**

```text
candidate metadata 含 subdomain/related_domains
→ create-document
→ authoring readback 完整保留

subdomain 缺失/非法
→ 稳定 422

AI domain 与作者选择冲突
→ 作者选择优先，结果可审计
```

---

### RT-R12-02：complete candidate 的 content tier 没有进入建档权威链路

**严重度：P1**

**攻击链**

```text
complete candidate
→ metadata 中没有规范 content tier
→ create-document 请求没有 contentTier
→ endpoint 固定调用 contentTier = "base"
→ AuthoringTemplateFactory 写入 base
```

**代码证据**

`AuthoringDraftEndpoints.cs` 的 `create-document` handler 只接收 title、summary、domain、facts、expressions，没有 content tier。

`Application.cs:317` 默认参数为 `"base"`，而 endpoint 调用时显式传入 `"base"`。

**结果**

未知内容层级会被静默解释为 base。该问题此前已在审计中发现，本轮从“完整 candidate 落盘保真”角度重新确认，不能视为已解决。

**修复要求**

- content tier 必须成为 Draft/authoring 请求中的显式字段；
- AI 返回值只能是建议，不能直接成为权威；
- 作者未明确选择时保留 unknown；
- unknown 不允许 create-document；
- base/adult_optional 选择必须进入 request hash、审计和 provenance。

---

### RT-R12-03：AI metadata、UI 编辑值和服务端请求值的优先级不明确

**严重度：P2**

当前有三份可能的 metadata 来源：

```text
AI result.metadata
UI draftTitleCandidate/draftSummaryCandidate/draftDomainCandidate
create-document request.title/summary/domain
```

`AuthoringDraftEndpoints.cs` 当前逻辑对 title、summary、domain 有部分覆盖关系：

```text
request.Title 优先于 selectedResult.Metadata.Title
request.Summary 优先于 selectedResult.Metadata.Summary
request.Domain 优先于 selectedResult.Metadata.Domain
```

但没有统一的“作者确认版本”结构，也没有记录：

```text
AI 原值
作者修改值
最终采用值
```

如果后续加入 subdomain/tier/related domains，继续以散落字段覆盖，容易出现：

- UI 显示一套；
- request 提交一套；
- authoring readback 又是一套；
- provenance 无法解释变化来源。

**修复要求**

冻结 metadata authority model：

```text
AI suggestion
→ author confirmed metadata
→ server taxonomy/tier validation
→ authoring document
```

并保存必要的 source-of-change 摘要。

## 4. 保留的正向边界

- 当前文档 builder 会强制写入 `needs_review`；
- 表达身份仍经 registry 校验；
- 现有 DraftTests 全部通过；
- 未发现分类丢失可以直接绕过 canon/authority gate 的路径。

## 5. 收敛状态

```text
P0 = 0
本轮新增 P1 = 2
累计待处置 P1 = 29
连续无新增 P0/P1 = 0 轮
下一步 = 继续红队或进入有授权的修复批次
```

