# AWAKE 模组本体主要开发交接

> 日期：2026-09-13
> 接手角色：模组本体主要开发负责人
> 原负责人后续职责：只处理跨层难点、测试设计、架构边界与疑难排障；不再排他性占用本体日常功能开发。

## 1. 接手时先做什么

1. 在 `D:\AWAKE-Dev\AWAKE` 读取 `AGENTS.md`。
2. 读取 `docs/control-plane/CURRENT.json` 和本文件。`CURRENT.json` 是旧 G3-B 批次的记录，不能据此覆盖正在进行的世界书、角色卡或对话工作。
3. 运行 `git status --short`，先确认自己的精确文件范围；当前工作区是多人共享的脏树。
4. 每个小任务只暂存自己拥有的路径，提交前检查 `git diff --cached --name-only`。禁止 `git add .`、`git reset --hard`、`git clean`，也不要回退别人的未提交修改。

## 2. 权威位置与不可跨越的边界

- 运行时权威源码：`D:\AWAKE-Dev\AWAKE`。
- 目标：Bannerlord `v1.3.15`、`net472`；构建入口为 `tools\build.ps1`，日常本地编译可用：

  ```powershell
  dotnet build -c Release -p:BannerlordApi=1.3.15 --nologo -v:q
  ```

- 不把世界书正文、角色卡正文或旧发布包复制进本工作区。世界书和 Persona Workbench 仍有独立负责人持续开发；本体只消费已批准的契约和运行时投影。
- 不同步到游戏目录、不启动游戏，除非用户另行授权。构建成功只是 E2，不等于游戏内验证。

## 3. 已提交、可作为开发基线的交涉链路

最新两个提交：

| 提交 | 内容 |
| --- | --- |
| `054d697` | 交涉模式、结构化结果校验、玩家确认、异步结算观察器与 VM 状态。 |
| `467f434` | 交涉流程的英文/简体中文资源键。 |

主链路为：

`NpcDialogueService` → Prompt/AI → `NpcDialogueOutputValidator` → 候选命令 → 玩家确认 → 游戏状态结算 → `NpcDialogueConfirmedSettlementRunner` 的完成/失败记录。

已经具备的行为：

- 普通“闲聊”不直接提出状态变更；“交涉”才允许模型提出可结算事项。
- 模型提案不是直接写入游戏：必须由玩家确认，且底层结算成功，才报告实际状态变化。
- 对话界面关闭后，已确认结算仍由独立观察器跟踪；结果写入结构化日志，不依赖 UI 仍存在。
- 已补齐开发者“强制附近谈判”以及交涉确认、失败、不可用等中英文显示文本。

关键代码：

- `src/NpcDialogueService.cs`
- `src/NpcDialogueModels.cs`
- `src/NpcDialogueOutput.cs`
- `src/NpcDialogueVM.cs`
- `src/NpcDialogueConfirmedSettlementRunner.cs`
- `ModuleData/Languages/awake_strings.xml`
- `ModuleData/Languages/CNs/awake_strings-zh-HANS.xml`

最近一次完整本地编译命令成功：0 警告、0 错误。它不能证明 UI 在游戏中可见，也不构成 E4/E5。

## 4. 交涉功能尚未收口的部分

以下文件**仍是未提交的并行工作**，接手人不得擅自丢弃或覆盖：

- `GUI/Prefabs/NpcDialogue.xml`：交涉/闲聊切换控件与布局调整。
- `GUI/Prefabs/AwakeMessenger.xml`：通讯录对话的交涉入口与布局调整。
- `src/AwakeMessengerVM.cs`、`src/NpcDialogueLauncher.cs`、`src/NpcDialoguePromptPipeline.cs`、`src/NpcMemoryService.cs`、`src/Prompts/`：对话、记忆与提示词链路的并行改动。
- `src/WorldbookRuntime.cs`、`src/AwakeRuntime.cs`、`tools/worldbook-runtime-production-smoke/`：本地端到端夹具与世界书知识注入试验。

其中有一条故意保留的红测，不能作为发布候选：

- `tools/worldbook-runtime-production-smoke/ProductionSmokeTestsDialogue.cs` 的 `npc-dialogue-send-async-roundtrip`。
- 当前结果：26 项通过、1 项失败；失败原因是测试知识条目查询返回 `not_found`，尚未把测试条目选入 `WorldKnowledgeQueryService`。这不是已提交交涉结算链的编译故障，而是“真实知识 → Prompt → 本地 AI 假件”端到端夹具的未完成问题。

建议不要为了让测试变绿而放宽运行时知识授权；应先定位 fixture 的 identity、scope、detail、entry expression 与查询候选选择条件为何不匹配。

## 5. 推荐的日常开发顺序

1. **UI 收口（独立小提交）**：确认两个 Prefab 的归属；分别检查绑定属性/命令名与 VM 是否一致，做 XML 解析和本地 fixture 验证，再提交。不要混进世界书 UI 的布局改动。
2. **对话链路常规完善**：处理 `chat`、`negotiation`、`command` 的可见状态、失败提示与历史记录；继续保持“提案—确认—真实结算”的三段式约束。
3. **记忆与世界书接入**：只经 `PersonaRuntimeProvider`、`WorldbookRuntime`、`NpcMemoryService` 的既有边界接入；不得把角色卡正文写死到 C# 或 prompt 模板中。
4. **端到端夹具**：每增加一个对话功能，同步补到 `tools/worldbook-runtime-production-smoke`；先跑确定性本地测试，再决定是否申请游戏内验证。
5. **游戏验证**：只有构建、夹具和变更范围清楚后，再单独申请 E3/E4/E5；不要把本地 smoke 称作游戏实测。

## 6. 原负责人保留的难点任务

可直接转回原负责人的事项：

- 世界书事实、记忆、人物对话之间的授权/选择/失效逻辑。
- 本地 AI 模拟、生产 smoke、假件与真实运行时边界的设计或红测排障。
- 保存、读档、并发、权限、generation、LKG 等跨模块持久化难题。
- 新公开契约、存储协议、跨层架构改动的计划、对抗审查和验收设计。

日常 UI 调整、普通对话功能、开发者工具、提示词整理、常规测试和小型缺陷修复，由本体主要负责人直接推进即可。

## 7. 当前风险提示

- Git index 目前已有其他 Agent 的暂存内容；提交前必须检查暂存清单，不要假定 index 只属于自己。
- `CURRENT.json` 的更新时间为 2026-09-11，未反映本次交涉提交或多人并行状态；它只能作为历史控制面参考，不能锁死当前任务方向。
- 世界书与角色卡的持续修改是预期状态。接入点应保持契约化，等待各自负责人的已批准产物；不要以本体开发名义冻结它们。

## 8. 接手后的唯一首要动作

先为 `GUI/Prefabs/NpcDialogue.xml` 与 `GUI/Prefabs/AwakeMessenger.xml` 确认一个明确的所有者和提交范围；完成后以独立提交交付 UI 入口。随后再选择对话、记忆或世界事实中的一个小批次，不要并行启动新的大重构。
