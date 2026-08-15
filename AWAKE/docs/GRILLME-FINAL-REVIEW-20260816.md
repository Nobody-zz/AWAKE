# AWAKE 最终对抗审查与阶段总结

> 日期：2026-08-16
> 方法：只读审查，不修改代码。检查 `src/`、`docs/`、测试、git 状态与任务队列。

## 0. 当前证据

- `dotnet build -c Release -p:BannerlordApi=1.3.15`：0 警告 0 错误。
- `Awake.SdkSmoke`：`PASS ALL`，12 条路径通过。
- 本地化：`LOCALIZATION_OK source=36 en=61 cn=61`。
- `AWAKE-Repo` 工作区干净。
- 工作区存在未提交世界书代码骨架：`WorldbookModels.cs`、`WorldbookLoader.cs`，尚未接入测试与对话链路。

## 1. 对抗审查发现

### P0：会直接阻碍可用性

1. **世界书只有规范与代码骨架，没有完整服务**
   - 有 `WorldbookModels` / `WorldbookLoader`，但没有索引、身份绑定查询、关键词/ngram 检索、预算组装。
   - 代码未提交、未加入 SdkSmoke。

2. **NPC 对话尚未接入世界书**
   - 当前提示词里的 `retrieved_knowledge` 仍来自旧的 `KnowledgeService`。
   - 世界书检索结果还没有进入 `NpcDialogueService`。

3. **AF 兼容语义仍未完全锁定**
   - `Variants` 在 AF 中是“按 when 匹配分数选最佳”，AWAKE 默认 `first/all/random` 会改变语义。
   - `TextMappings` 依赖 AF 运行时动态替换，AWAKE 尚未实现。
   - `RagShortTexts`、`SemanticPrototypes`、`VoiceId` 只是保留原始值，没有真正使用。

4. **真实游戏链路未验收**
   - 存储、记忆、事件冷却、关系命令都只有离线 SdkSmoke。
   - Companion 存储、读档持久化、真实 AI 对话未在游戏内确认。

### P1：会拖慢后续开发

5. **事件引擎没有内容规则**
   - 引擎能跑，但没有内容包注册，游戏里不会弹出任何事件。

6. **Messenger 仍是半成品**
   - 统一会话、持久化、写信/来信、未读都未完成。

7. **知识检索暂停，但世界书设计与其重叠**
   - `KnowledgeService`、`KnowledgeModels`、`Worldbook*` 三套概念需要合并或明确边界。

8. **Preprocess / Postprocess 路由仍为空**
   - 路由已注册，但没有提示词、输出 schema、调用方。

9. **SDK 版本漂移**
   - 游戏目录马库斯框架已更新，AWAKE 仍引用 `SDK_20260815`。

### P2：质量与体验

10. 开发者检查仍是文本报告，没有完整诊断面板。
11. 周报 / `NarrativeReportBuilder` 没有接入。
12. 无名 NPC 没有永久记忆和命令边界。
13. 任务队列依赖执行纪律，没有自动化检查。

## 2. 建议的决策

1. **世界书与 KnowledgeService 合并**
   推荐让 `WorldbookService` 成为唯一身份/知识检索入口，`KnowledgeService` 只保留 RAG 管道或直接退役。

2. **AF 导入必须显式**
   推荐 `sourceFormat=af` 时严格保留 AF Variants/TextMappings 语义，不能自动套用 AWAKE 规则。

3. **值大小写敏感，字段名不敏感**
   `empire` 与 `Empire` 是不同游戏代码；`HeroIds` / `heroIds` 是同一字段名。

4. **场景只影响注入优先级，不影响知识池**
   持久知识缓存到 NPC，场景切换不删除。

5. **先做代码闭环，再谈内容**
   世界书应先在 SdkSmoke 中完成加载、索引、身份绑定、关键词检索、预算组装，再接 NPC 对话。

## 3. 阶段总结

### 已完成

- AWAKE 运行时身份与 Marcus 扩展注册。
- 场景 T/Y 三维距离选人。
- 遭遇面谈、通讯录基础 UI。
- NPC 对话、提示词、输出校验、取消/流式。
- 事件引擎骨架、事件类型枚举、事件弹窗“参与话题”、事件关系结算。
- `awake.relationship.delta.v1` 命令、权限、风险、幂等。
- 存储离线验证：memory / event_meta / relationships。
- NPC 记忆逻辑离线验证。
- 任务连续性 skill 与任务队列。
- 世界书格式、接口、AF 兼容、检索与优先级设计。

### 半成品

- 世界书检索代码。
- Messenger 统一会话与持久化。
- 知识语料与 RAG。
- 游戏内真实验收。
- 内容包公开 API。

### 未开始

- 内容包接入。
- 写信/来信。
- 周报/世界事件。
- 群聊、媒体、TTS。
- 记忆分级、承诺账本、秘闻传播。

### 版本判断

- 当前仍为 `v0.2.0` 过渡态。
- 不建议提版：世界书、Messenger、真机验收、内容包 API 都没有闭环。

## 4. 最终结论

AWAKE 已经从“只有壳”进入“运行时骨架完整、玩法局部闭环、内容未接入”的阶段。

当前最值得继续的是：世界书检索代码闭环，然后接入 NPC 对话；这一步决定 AWAKE 能否成为真正的 AI 世界运行时。
