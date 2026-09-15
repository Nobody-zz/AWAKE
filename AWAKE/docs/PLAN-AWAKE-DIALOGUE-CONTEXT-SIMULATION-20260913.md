# AWAKE 对话上下文离线模拟

- 批次：`AWAKE-DIALOGUE-CONTEXT-SIMULATION-20260913`
- 状态：`draft_for_independent_review`
- 目标：在既有 production smoke 中，以固定夹具模拟一次人物对话，验证角色人格、世界事实、关系/承诺、记忆和玩家话语能被分层送入当前提示词与输出校验链。

## 边界

修改仅限 `tools/worldbook-runtime-production-smoke/` 的 fixture 和测试；直接调用现有运行时代码，不改 `src`、游戏目录、世界书正文或角色卡文件。测试不调用任何模型或 Provider：模拟输出为夹具提供的结构化 JSON。

## 输入与输出

输入夹具包含：hero id、人格 DSL、当前事实、关系/承诺文本、记忆文本、玩家话语、动作模式和模拟的结构化模型输出。

可观察输出：渲染后的提示词是否含每个分层块；模拟输出是否通过现有 `NpcDialogueOutputValidator`；chat 模式是否抑制命令、negotiation 模式是否保留合法提案；不得输出原始提示词到日志。

## 验收

| 场景 | 本地断言 |
| --- | --- |
| 完整上下文 | 人格、事实、关系/承诺、记忆、玩家本轮均进入各自提示词区块，且不互相覆盖 |
| chat | 含 command 的模拟输出被现有校验器抑制，不产生可执行提案 |
| negotiation | 同一合法输出保留提案，仍须人工确认；测试不执行命令桥 |
| 缺少内容 | 空人格/事实/记忆不阻断链路，不虚构这些数据 |
| 非法输出 | 现有校验器拒绝，夹具不将其视为回复或指令 |

## 非目标

- 不读取或导入正在制作的内容包；后续只将其候选数据作为离线 fixture 副本输入。
- 不评估语言质量，不声称固定 JSON 等同真实模型行为。
- 不修改提示词、Persona DSL 契约、世界书加载、存档、命令效果或 UI。

## 证据

E2：构建 `tools/worldbook-runtime-production-smoke/WorldbookRuntimeProductionSmoke.csproj` 并执行其 EXE；新增场景必须在输出中单独 PASS。E4/E5 不在本批范围。
