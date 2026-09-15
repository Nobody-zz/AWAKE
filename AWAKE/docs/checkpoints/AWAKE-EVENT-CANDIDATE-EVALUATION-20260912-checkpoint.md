# AWAKE 事实驱动事件候选判定批次检查点

- 日期：2026-09-12
- 计划：`docs/PLAN-AWAKE-EVENT-CANDIDATE-EVALUATION-20260912.md`
- 状态：`approved_e2_complete`
- 用户门禁：已签收
- 作用域：模组本体事件候选筛选；世界书、角色卡、人物对话、记忆保持独立

## 已落地

- `src/AwakeEventCandidateEvaluator.cs`
  - 增加可选事实触发模型与纯内存判定结果。
  - 按 `allowedKinds`、事实 campaign day 和最大年龄判定。
  - 稳定去重 `factId`，并按事实日、timeSlot、factId 排序。
  - 对查询失败、取消、legacy fallback fail closed；无触发条件规则保持旧行为。
- `src/AwakeEventEngineCore.cs`
  - `AwakeEventRule` 承载可选 `FactTrigger`。
- `src/AwakeEventDataLoader.cs`
  - 解析并严格校验 `factTrigger`。
- `src/AwakeContentApi.cs`
  - 公开 `AwakeContentEvent.FactTrigger`，复用同一 Loader 路径。
- `src/AwakeEventEngine.cs`
  - 小时入口在既有 cooldown/事实筛选范围内执行候选判定并记录摘要。
  - 链式事件未接入事实再查询；UI 显示前保留取消和 session 检查。

## 验证

- focused smoke：PASS
- production smoke：PASS，`22/22`
- AWAKE Release build：PASS，`AWAKE/_build_out/1.3.15/Release/Awake.dll`
- 未同步游戏目录、未启动游戏；当前证据为 E2

## 复审结论

- 独立实现复审：`APPROVED`
- P0：0；P1：0；P2：0
- 本批 E2 已收口。若需游戏内行为证据，另行授权并走 E3/E4/E5 流程。
