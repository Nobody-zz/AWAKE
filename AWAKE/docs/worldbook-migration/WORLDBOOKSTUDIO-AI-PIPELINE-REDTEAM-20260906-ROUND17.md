# Worldbook Studio AI 生成管线持续红队——第十七轮

> 日期：2026-09-06  
> 范围：source evidence 的近似匹配、UTF-16 span 和重复引用定位。  
> 状态：`REVISE`  
> 本轮性质：只读静态攻击追踪 + Draft DOM 基线；未修改实现。

## 1. 执行边界与基线

本轮未：

- 启动 Bannerlord；
- 同步游戏目录；
- 访问真实 Cloud Provider、API Key、Token、Worker 或网络；
- 读取/修改 Downloads 源目录；
- 修改五个世界书迁移候选；
- 修改 Worldbook Studio 实现。

本轮基线：

- Draft DOM/state harness：`2/2 PASS`

## 2. 结论

| 严重度 | 数量 | 结论 |
|---|---:|---|
| P0 | 0 | 未发现直接进入 canon/runtime 的无权限路径 |
| P1 | 2 | evidence 近似定位可能被标成 verified，重复 quote 可能绑定错误出现位置 |
| P2 | 1 | UTF-16/Unicode 规范化规则需要在契约中显式声明 |

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
CONTINUE REDTEAM
```

## 3. 新增发现

### RT-R17-01：近似匹配的 evidence 被标记为 evidence_verified=true

**严重度：P1**

`SourceEvidenceMatcher.TryFind` 返回 `SourceEvidenceMatch`，其中包含：

```text
Exact = true/false
```

当 source 与 candidate 只有格式、空白、全角/半角或标点差异时，matcher 可能返回：

```text
Exact = false
```

但 `AuthoringDraftResponseNormalizer.NormalizeEvidence` 只判断 `TryFind` 是否成功：

```csharp
if (SourceEvidenceMatcher.TryFind(sourceText, quote, out var sourceMatch))
{
    quote = sourceMatch.Quote;
    evidenceVerified = true;
}
```

没有使用 `sourceMatch.Exact`。

**攻击输入**

```text
source = "哈尔达尔继承王位，雅尔们仍有怨言。"
provider quote = "哈尔达尔继承王位雅尔们仍有怨言"
```

或者利用 matcher 会忽略的格式/标点差异，使结果走 canonical match 而不是 exact match。

**结果**

经过近似匹配的 quote 会被输出为：

```text
evidence_verified = true
```

但系统实际只证明“规范化字符序列可以对应”，没有证明 Provider 原样提交的 quote 与 source 完全一致。下游 UI 和 provenance 可能把 approximate match 当作逐字验证。

**修复要求**

区分至少三种状态：

```text
exact_verified
normalized_located
unverified
```

只有 `Exact=true` 才允许 `evidence_verified=true`；normalized match 必须产生 warning，或进入 `support_status=partial/unresolved`。

**回归用例**

```text
原文完全相同 → exact_verified
仅空白/格式不同 → normalized_located + warning
标点/全角规范化命中 → 不得标记完整 verified
无法定位 → unverified
```

---

### RT-R17-02：重复 quote 不使用 Provider locator 选择出现位置

**严重度：P1**

`NormalizeEvidence` 从 Provider evidence 中读取：

```text
reference_id
locator
quote
```

但定位时只执行：

```csharp
SourceEvidenceMatcher.TryFind(sourceText, quote, out sourceMatch)
```

`TryFind` 对重复 quote 返回 source 中第一次匹配的位置。Provider 提供的 locator 只被保留为标签，不参与实际 span 选择。

**攻击输入**

```text
source:
  第 1 段：北方军队进入城镇。
  第 8 段：北方军队进入城镇。

provider:
  quote = "北方军队进入城镇"
  locator = "第 8 段"
```

**结果**

- evidence 的 locator 显示“第 8 段”；
- 实际 quote/span 绑定到第 1 段；
- source span、line range 和人工审查位置可能相互矛盾；
- duplicate source 场景下会把正确引用绑定到错误上下文。

**修复要求**

- locator 必须参与候选 occurrence 选择；
- 若 locator 无法解析，返回多个 occurrence 并要求人工选择；
- provenance 同时保存 requested locator 和 resolved span；
- 不得把第一次字符串命中当作唯一证据位置。

**回归用例**

```text
重复 quote + locator 指向第二处 → resolved span 必须是第二处
重复 quote + locator 缺失 → 多处候选，不能静默取第一处
locator 与 quote 冲突 → warning/blocking unresolved
```

---

### RT-R17-03：Unicode/UTF-16 规范化后的 span 语义未在 Quick 契约中显式区分

**严重度：P2**

`SourceEvidenceMatcher` 使用 UTF-16 索引，同时对字符执行 FormKC、全角转换、标点归一化和空白删除。这样能提升格式兼容，但 `AuthoringDraftResponseNormalizer.NormalizeSourceSpans` 最终保留：

```text
start_utf16
end_utf16
verification_status
```

没有记录 span 是：

```text
原文精确范围
规范化匹配映射范围
模型提供但未验证范围
```

在包含代理对、兼容字符、组合字符或重复标点的 source 中，人工看到的 span 与模型提交的 quote 可能不是同一种坐标语义。

**修复要求**

- span 增加 `match_mode`；
- 明确索引基于 normalized source 还是原始 source；
- approximate mapping 默认不作为完整 evidence；
- UI 同时显示原文切片和匹配方式。

## 4. 保留的正向边界

- matcher 会拒绝完全找不到的 quote；
- source span 会检查非负范围和 end > start；
- Draft DOM 基线通过；
- 未发现 evidence 定位问题可直接绕过人工审核进入 canon/runtime 的路径。

## 5. 收敛状态

```text
P0 = 0
本轮新增 P1 = 2
累计待处置 P1 = 36
连续无新增 P0/P1 = 0 轮
下一步 = 继续红队或进入有授权的修复批次
```

