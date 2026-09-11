# Worldbook Studio AI 生成管线持续红队——第十三轮

> 日期：2026-09-06  
> 范围：CandidateSet 内部 stable ID、候选间 fact/expression 绑定，以及 merge 的冲突处理。  
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
| P1 | 2 | 候选集合内部重复 ID 可造成错误选择或静默丢失事实 |
| P2 | 1 | 候选内自动生成的默认 ID 缺少集合级唯一性保障 |

```text
PLAN_STATUS = NEEDS_REVISION
IMPLEMENTATION = NOT_APPROVED
CONTINUE REDTEAM
```

## 3. 新增发现

### RT-R13-01：不同候选之间重复 fact/expression ID 会导致 merge 静默丢内容

**严重度：P1**

**攻击输入**

两个候选都包含：

```text
fact_id = fact-001
expression_id = expr-001
```

但文本或 evidence 不同。

**代码证据**

`AuthoringReviewDecisions.cs:80-87` 的 merge 使用：

```csharp
selected.SelectMany(item => item.Facts).DistinctBy(item => item.Id)
selected.SelectMany(item => item.Expressions).DistinctBy(item => item.Id)
```

**结果**

只保留第一个相同 ID 的事实/表达，第二个候选的内容被静默丢弃。用户看到的是“合并完成”，但合并结果并不包含全部输入。

这在模型未提供稳定全局 ID、多个 candidate 各自默认生成 `fact-001` 时尤其容易发生。

**修复要求**

- candidate 内和 CandidateSet 全局分别执行 ID 唯一性检查；
- 冲突 ID 不得使用 `DistinctBy` 静默解决；
- 冲突时返回稳定 `422`，或生成明确的重映射记录；
- 派生候选必须保存 source candidate ID → derived fact/expression ID 映射；
- evidence/source span 也必须随重映射同步更新。

**回归用例**

```text
两个候选含相同 fact ID、不同内容 → merge 阻断或显式重映射
两个候选含相同 expression ID、不同内容 → 不得静默丢失
相同 ID、完全相同内容 → 也必须有明确 dedup 规则和证据
```

---

### RT-R13-02：缺失 candidate/fact/expression ID 时按位置生成默认 ID，跨候选必然碰撞

**严重度：P1**

**代码证据**

`AuthoringDraftResponseNormalizer.cs:100`：

```csharp
var id = OptionalString(candidate, "id", "candidate_id")
    ?? $"candidate-{index + 1:000}";
```

`AuthoringDraftResponseNormalizer.cs:207`：

```csharp
var id = OptionalString(source, "id", "fact_id")
    ?? $"fact-{index + 1:000}";
```

`AuthoringDraftResponseNormalizer.cs:276`：

```csharp
var id = OptionalString(source, "id", "expression_id")
    ?? $"expr-{index + 1:000}";
```

这些 index 都是各自数组内的局部位置，因此两个 candidate 都缺失 ID 时会得到：

```text
candidate-001
fact-001
expr-001
```

**结果**

- CandidateSet 中可能出现重复 candidate ID；
- merge 的 `DistinctBy` 进一步丢失内容；
- UI 以 candidate ID 选择时无法唯一定位；
- review ledger 的 candidate_ids 无法稳定描述一个对象；
- retry/derived candidate 的身份关系不完整。

**修复要求**

- 缺失 ID 时基于 draft/request/candidate scope 生成稳定 ID；
- 生成后立即做 candidateSet 全局唯一性检查；
- 不能只在 submission 阶段才发现冲突；
- 默认 ID 必须参与 candidate fingerprint，但不能代替集合级唯一性验证。

**回归用例**

```text
两个无 ID candidate → 生成稳定不同 ID
同一 request 重试 → 生成相同 scoped ID
不同 request → ID/fingerprint 不冲突
```

---

### RT-R13-03：CandidateSet 生成阶段没有强制校验 candidate ID/fingerprint 唯一

**严重度：P2**

`AuthoringLifecycleFactory.FromDraftResult` 会为每个 payload 计算 fingerprint 和 candidate ID，但没有在返回 `AuthoringCandidateSet` 前检查：

```text
CandidateId unique
Fingerprint unique
fact IDs 的冲突语义
expression IDs 的冲突语义
```

后续部分路径使用：

```csharp
ToDictionary(item => item.CandidateId)
```

另一些路径使用 `FirstOrDefault`，导致同一类冲突在不同入口表现不同：

```text
抛异常
静默取第一条
静默去重
```

需要把唯一性检查提前到 CandidateSet 建立边界，避免后续各模块自行处理。

## 4. 保留的正向边界

- CandidateSet 仍绑定 source content hash、packet hash 和 provider fingerprint；
- submission validator 会检查提交集合中的重复 ID；
- 现有 Draft/Batch/DOM 基线未回归；
- 未发现重复 ID 可以直接绕过人工审核进入 canon/runtime 的路径。

## 5. 收敛状态

```text
P0 = 0
本轮新增 P1 = 2
累计待处置 P1 = 31
连续无新增 P0/P1 = 0 轮
下一步 = 继续红队或进入有授权的修复批次
```

