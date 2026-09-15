# AWAKE 对话离线接入夹具

- 批次：`AWAKE-DIALOGUE-INTEGRATION-HARNESS-20260913`
- 状态：`approved_for_implementation`
- 范围：仅 `tools/worldbook-runtime-production-smoke/` 的测试、fixture 与测试适配器；不修改 `src`、游戏目录、角色卡/世界书权威内容或 Provider 配置。

## 目标

使本地测试可用少量复制的候选样本，验证三段真实接入边界：资产读取并进入提示词、模拟 AI 回合进入现有服务、第一轮产生的记忆可在第二轮读取。

## 方案

1. **冻结资产 fixture。** 仅允许在实施时读取一次 `tools/persona-workbench/characters/` 中已通过其自身校验的单一候选卡；复制为 `tools/worldbook-runtime-production-smoke/fixtures/dialogue/` 下的离线样本，记录原绝对相对路径、SHA-256、复制时间。之后测试只读该副本。禁止读取 `ModuleData/**`、`docs/worldbook-migration/**`、worldbook-studio 工作目录及其他正在编辑的内容根。世界事实使用测试自带、带来源标签的最小投影 JSON，不冒充权威世界书正文。
2. **真实服务的确定性假边界。** 为 `ProductionSmokeHost` 增加仅测试用的 `IAiGateway`、`IPromptRegistry`、玩家绑定与 in-memory WorldState 初始化适配器；它必须捕获 `SendAsync` 提交的已编译 prompt、返回 accepted 再回调 Completed，保留 correlation。不得反射调用 `HandleCompleted` 代替本测试。
3. **拆分两条记忆用例。** 未确认提案：只到 confirmation/reject，断言命令桥、状态和记忆均未写入。已确认记忆：独立 fixture 安装 `NpcMemoryService.Current`、in-memory command/store 和确定性摘要 route；确认后等待现有后台 close/memory task，再创建第二个 service，断言第二轮 prompt 读取首轮写入的事实。不得用预置存储数据声称“首轮产生”。

## 验收

| 场景 | 断言 |
| --- | --- |
| 资产读取 | fixture 的来源 hash 匹配、人格 DSL 与事实标记分别进入渲染结果 |
| 完整回合 | `SendAsync` 接受；适配器捕获编译 prompt 后回调 Completed；服务输出一条 `TurnCompleted`，并带原 correlation |
| chat | chat 结果抑制 command，不执行命令桥 |
| 未确认提案 | negotiation 仅出现 confirmation；reject 后不进入第二轮记忆、不写状态、不调用命令桥 |
| 两轮记忆 | 独立确认用例实际执行 in-memory 命令结算和现有 memory close；新 service 的第二轮 prompt 读取首轮写入的事实 |
| 敏感日志 | fixture 的玩家话语、人格正文、记忆正文和回复正文均不出现在 CapturedLogs |

## 非目标

- 不评价真实模型语言质量或人格表现，不调用任何 Provider。
- 不改运行时 Loader、存储结构、命令效果、UI 或内容格式。
- 不以固定 fixture 代替 E4/E5。
