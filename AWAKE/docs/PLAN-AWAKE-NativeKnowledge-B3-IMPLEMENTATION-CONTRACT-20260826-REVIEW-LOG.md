# B3 实施合同 v1 独立审查日志

- `contract`: `docs/PLAN-AWAKE-NativeKnowledge-B3-IMPLEMENTATION-CONTRACT-20260826.md`
- `review_scope`: 防止实施阶段因合同模糊而偏离；不重新审查 B3 产品方向
- `source_scope`: 实施合同文本；本轮不修改源码

## Round 1 — 2026-08-26

### Reviewer

- 只读审查授权状态、联系人入口、强类型模式、时间字段、询问 ID、幂等、写集和停止条件。
- 未修改合同或运行时代码。

### Findings

#### P0

- 无。

#### P1

1. **实际签收状态字段缺失**
   - 合同顶部只有 `user_signoff_required: true`，授权门槛却要求 `user_signoff = true`。
   - 实施者无法判断合同是否已经完成用户签收。
   - 修订：增加 `user_signoff: false`，并保留第 18 节的三条件授权门。

2. **Storage 幂等键未规范化**
   - `inquiryId`、`conversationId` 和“同一幂等键”已描述，但没有写死 `idempotencyKey` 的具体公式。
   - 修订：固定 `id = conversationId`、`conversationId = inquiry|heroId|inquiryId`、`idempotencyKey = conversationId + ":facts"`，复用现有 `WorldStateStore.BuildMemoryCommand(...)`。

#### P2

- 联系人卡片的具体按钮布局仍为 `OPEN`；不影响业务合同，但实现验收必须检查按钮可见、可点击和不会打开普通对话。

### Verdict

```text
VERDICT: REVISE
```

### 修订结果

- 已补充 `user_signoff: false`。
- 已固定 memory command、entry ID、conversation ID 和 idempotency key 的映射。
- 未修改运行时代码、构建产物、游戏目录或冻结候选。

## Round 2

### Reviewer

- 只读复核上一轮两个 P1 的修订结果。
- 未修改合同或运行时代码。

### Findings

#### P0

- 无。

#### P1

- 无。`user_signoff` 已作为实际布尔状态字段记录，且与 `review_status`、`implementation_authorized` 和第 18 节授权门一致。
- 无。`inquiryId`、`conversationId`、`id` 和 `idempotencyKey` 已固定为唯一公式，并与现有 `WorldStateStore.BuildMemoryCommand(...)` 的 `conversationId + ":facts"` 规则一致。

#### P2

- 无新增。按钮布局仍可由 UI 实现决定，但入口、命令语义和禁止回退路径已经固定。

### Verdict

```text
VERDICT: APPROVED
```

### Review conclusion

本轮只批准实施合同进入用户签收阶段，不批准直接写代码。当前合同状态为 `approved_for_user_signoff`，`user_signoff=false`，`implementation_authorized=false`。
