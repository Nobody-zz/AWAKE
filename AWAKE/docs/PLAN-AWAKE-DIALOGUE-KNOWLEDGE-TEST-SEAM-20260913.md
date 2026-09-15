# AWAKE 对话知识分支测试注入点

- 批次：`AWAKE-DIALOGUE-KNOWLEDGE-TEST-SEAM-20260913`
- 状态：`draft_for_independent_review`
- 范围：`WorldbookRuntime` 的 internal 测试注入/复位方法及 production smoke；不增加生产菜单、配置、内容加载路径或游戏目录写入。

## 目标

让离线 smoke 能明确安装一个已构造、只读的 `IWorldKnowledgeQuery`，从而覆盖 `NpcDialogueService.SendAsync` 的“知识充分→调用 AI”分支；现有“知识缺失→直答回退”测试保持不变。

## 契约

- 方法为 `internal`，名称带 `ForTesting`，只能设置 `_knowledge`，不得改 activation、persona、overlay、bundle 或当前内容对象。
- 仅接受 `WorldKnowledgeQueryService` 或 `null`；`null` 必须恢复缺失知识状态。
- `AwakeRuntime.ResetSessionStateForTestingAsync` 和测试 harness cleanup 均复位它。
- production 调用点不得引用该方法；静态 smoke 检查限定引用者仅在 production smoke。

## 验收

1. 无知识时：既有 `SendAsync` 直答回退、AI adapter 未收到请求。
2. 安装已知事实后：真实 `SendAsync` 提交 prompt 给确定性 AI adapter，prompt 包含事实标记；Completed 回调产生一条 `TurnCompleted`。
3. 清理后：下一用例重新回到无知识直答；不得跨测试泄漏。

## 非目标

- 不读取世界书权威内容、不修改 Loader、不评价模型文本、不修改 Persona 或存档。
