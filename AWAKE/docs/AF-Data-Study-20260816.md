# AF 数据层学习笔记

> 日期：2026-08-16
> 目的：从 AnimusForge 的 ModuleData 学习“数据驱动 AI 玩法层”的组织方式，并提炼 AWAKE 可借鉴、不可照搬的点。

## 1. AF 数据文件在做什么

AF 把 AI 玩法拆成四类数据：

| 文件 | 角色 | 核心作用 |
| --- | --- | --- |
| `PreprocessPrompts.json` | 前处理路由 | 决定当前对话命中哪些规则、选择哪些记忆、抽取哪些实体 |
| `RuleBehaviorPrompts.json` | 规则正文 | 每个规则包含启用开关、话题编号、关键词、正文指令、后处理标签、运行时模板 |
| `ActionPostprocessPrompts.json` | 后处理提取 | 从 NPC 回复中提取“已成立的动作标签”，防止把报价、反问、未来承诺当成事实 |
| `ProactiveNpcRequestPrompts.json` | NPC 主动意图 | 按缺粮、缺钱、求婚、邀请入队等意图生成开场白/来信/同伴对话 |
| `UnnamedNpcProfiles.json` + 目录 | 无名 NPC 档案 | 按兵种/王国/文化/性别给无名 NPC 生成回退档案 |
| `animusforge_scene_gold_items.xml` | 场景资产 | 定义金币/金锭等可用于场景内掉落和交接的物品 |

## 2. 学习顺序

### 2.1 先学 PreprocessPrompts.json

关注：

- `Version`：数据版本控制，旧配置可兼容回退。
- `TemplateVariables`：所有提示词变量先集中声明，运行时填值。
- `StrictJson`：要求模型只输出 JSON，且附带实体 schema。
- `TopicRouting`：从玩家最新输入 + 历史中选 `rule_codes`。
- `MemorySelection`：让模型从候选记忆里选 `memory_ids`，而不是把全部记忆塞进正文。

对应源码：

- `AIConfigHandler.cs`
- `PreprocessPromptsConfigModel.cs`
- `PromptComposer.cs`

### 2.2 再学 RuleBehaviorPrompts.json

每个规则条目值得学习的字段：

- `IsEnabled`：规则级开关。
- `TopicNumber` / `TopicLabel` / `Code`：规则身份和路由代码。
- `Instruction`：注入正文的世界观/玩法规则。
- `TriggerKeywords`：触发检索关键词，不写进正文。
- `AcceptKeywords`：判断玩家请求是否命中。
- `PostprocessRules`：动作标签及其严格判定条件。
- `RuntimeInstructionTemplates`：把运行时数值/状态变成角色可感知的提示，而不是直接写数值。
- `RuntimeConstraintTemplates`：强约束，比如“清单没有的资产不能转移”。
- `NonHeroInstruction`：无名 NPC 的降级行为。
- `PreprocessExcludedInstruction`：规则未命中时给模型的“不可同意”说明。

对应源码：

- `AIConfigHandler.cs`
- `KnowledgeLibraryBehavior.cs`
- `PostprocessRuleEntry.cs`

### 2.3 再学 ActionPostprocessPrompts.json

这是 AF 最有价值的动作安全设计：

- 只有“本轮已经成立的动作”才输出标签。
- 报价、反问、设想、未来考虑、未完成条件都禁止输出动作。
- 标签必须从标签表里选，不能自创。
- 情绪标签与动作标签分离。

对应源码：

- `ActionPostprocessPrompts.json` 消费方
- `PostprocessRuleEntry.cs`

### 2.4 再看 ProactiveNpcRequestPrompts.json

学 NPC 主动对话的组织方式：

- `Default` 提供通用意图。
- `Requests` 按具体困境分类。
- 每个意图同时提供 `OpeningPrompt`、`LetterIntent`、`CompanionIntent`，适配当面/来信/同伴三种入口。
- 提示词反复强调“不要假定结果已成立”。

对应源码：

- `ProactiveNpcRequestPromptsConfigModel.cs`
- `ProactiveNpcRequestBehavior.cs`
- `NpcInitiatedOpeningRouter.cs`

### 2.5 最后看无名档案和场景资产

- `UnnamedNpcProfiles` 是“有名英雄档案之外的降级层”，按运行时身份组合查找。
- `animusforge_scene_gold_items.xml` 说明场景结算可以依赖自定义物品，而不是只改数值。

## 3. AWAKE 可借鉴、不可照搬

### 可借鉴

- 数据驱动：规则、提示词、NPC 主动意图全部外置 JSON，带版本号和内置回退。
- 变量契约：提示词模板先声明变量，运行时校验占位符完整。
- 规则正文与动作后处理分离：正文让 NPC 自然说话，后处理负责提取可结算动作。
- 动作门：报价/反问/未来承诺不算成立，只有明确同意才进入结算。
- 运行时事实强约束：清单外资产、未存在实体、未达成条件都不能被模型编造成事实。
- 主动对话三态：当面开场、来信、同伴对话共用一套意图，但分别给提示词。
- 无名 NPC 回退档案：避免“查不到档案就让 AI 瞎编”。

### 不可照搬

- AF 的 `[ACTION:...]` 标签体系不能直接抄，AWAKE 应使用 Marcus 的 Command + Output Schema。
- `RuleBehaviorPrompts.json` 体量过大，直接照搬会变成维护灾难；AWAKE 应分批、按内容包组织。
- AF 的 ONNX 检索不可取；AWAKE 走 Marcus RAG + 本地关键词回退。
- 内容类标签（性、暴力、权力关系）必须由内容包提供，不能进入 AWAKE 核心运行时。

## 4. 后续落地建议

1. 把这份学习笔记固化为 AWAKE 的数据层设计依据。
2. 设计 `RuleData` / `PreprocessData` / `PostprocessData` / `ProactiveIntentData` 四类 schema。
3. 每类 schema 走一次 grill-me，再实现加载、校验、版本回退和 SdkSmoke。
4. 规则正文由内容包注册，运行时只提供加载、门控、预算和结算机制。
