# AWAKE · AF Batch 1 审查日志

> 对象：`PLAN-Awake-AF-Batch1-20260816.md`
> 日期：2026-08-16

## Round 1

### 发现 1：主动评估需要更多前置门

问题：`OnHourlyTickAsync` 只写“候选池 + 冷却”，没有明确禁止在弹窗/覆盖层/输入框打开时创建 pending。
修订：评估前检查 `NpcDialogueOverlay.IsOpen`、`AwakeMessengerOverlay.IsOpen`、`InformationManager.IsAnyInquiryActive()`，任一为真直接返回。

### 发现 2：持久化不能每 tick 无脑写

问题：每小时都写整份 candidates 会造成无意义存储写入。
修订：只在候选集合发生实际变化时保存；加载后清理过期/疲劳条目，若清理后无变化则不写。

### 发现 3：弹窗重复风险

问题：pending 转 opening 后如果用户不点击，下一次 tick 不应再次弹。
修订：`OnApplicationTick` 只处理 `state == pending`；显示弹窗前立即把内存态置为 opening，并异步保存。

### 发现 4：长等待状态清理要完整

问题：`WaitingSinceUtc` 若在 Dispose/Cancel/FinishTurn 中漏清，会导致 overlay 状态错误。
修订：所有结束路径统一清空：`FinishTurn`、`CancelActiveAsync`、`ClearActive`、`Dispose`。

### 发现 5：Esc 门控不能只判断服务字段

问题：overlay 直接调用 `ExecuteClose`，可能绕过 60 秒门控。
修订：overlay 收到 Esc 后先判断 `service.CanEscCancel || !service.IsSending`，否则只显示提示，不关闭。

## Round 2

### 结论

上述修订已并入 PLAN。范围、存储、UI 门控、本地化和测试路径完整，无阻塞项。

`VERDICT: APPROVED`
