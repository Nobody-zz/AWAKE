# 斯拉涅斯之拥 · AF 批判性评估

> 日期：2026-08-15
> 目的：在“全面学习、自主重写、零运行时兼容”的前提下，先判断 AF 哪些设计值得学、哪些是历史包袱/脆弱实现，避免把不合理的东西一起搬过来。

## 评估结论

AF 是“功能全、内容多、但工程结构偏老”的单体 Mod。**值得学的是领域模型和玩法概念，不值得学的是它的工程形态**：巨型单体文件、大量 Harmony patch、反射式兼容、非正式的外部 API、以及为支撑这些而引入的复杂基础设施。

## 客观证据

| 指标 | 数值 | 含义 |
| --- | --- | --- |
| `MyBehavior.cs` | 约 57,600 行 / 225 个 catch | 巨型单体，领域逻辑、UI、存档、提示词揉在一起 |
| `ShoutBehavior.cs` | 约 37,400 行 / 123 个 catch | 对话/场景链路高度集中 |
| `SubModule.cs` | 884 行 / 70 次 Harmony patch 操作 / 69 个 catch | 初始化面巨大，每个 patch 独立 try/catch，脆弱且难审计 |
| 反射操作 | 84 处 | 大量反射兼容，签名漂移风险高 |
| `DuelSettings` 引用 | 287 处 | 全局配置散落在一处，耦合很深 |
| 对话覆盖层 | 反射 `ScreenBase._layers` 私有字段 | 直接依赖游戏内部 UI 结构 |
| 双版本 Bootstrap | 动态加载 `versions/1.3` 或 `1.4` | 为跨版本自建了一套复杂加载器 |
| 仓库 | 无 LICENSE | 直接复制有授权风险 |

另外，AF 自己的 README 明确说“当前主聊天链路是场景喊话链路，不走原版直接对话界面”，意味着它的对话系统已经和 mission/scene 深度绑定；这套复杂度对我们的 NPC 深谈不一定值得。

## 值得借鉴（学习后重写）

1. **世界书规则模型与检索思路**：`LoreRule`（Keywords / RagShortTexts / SemanticPrototypes / Variants / When / TextMappings）、实体提及查询、候选排序、注入上限、命中率日志。这是 AF 最成熟的部分，值得提炼成我们自己的规则 schema + Marcus RAG 检索。
2. **NPC 主动聊天动机分类**：Care / PartyMorale / PartyWounded / PrisonerPressure / RelationshipEmotion / FollowUp / CasualChat 等动机，以及 Pending/Opening 状态、冷却、动机疲劳。概念好，重写成斯拥动机表。
3. **记忆生命周期**：日结草稿 → 压缩记忆块 → 总览摘要 → 重试队列。思路完整，可重写进 `NpcMemoryService`。
4. **主线程动作队列**：`ConcurrentQueue<Action>` + tick 排空。我们已有 `SlaaneshUiDispatcher`，思路一致。
5. **开发者终端交互**：热键轮询、开菜单前置拦截、记录被拦截原因。UX 值得学，内容自建。
6. **事件收件箱**：大地图通知 + 收件箱弹窗，适合接我们的 `WorldEventLedger`。
7. **长等待解锁**：AI 回复超过 1 分钟后允许 Esc 关闭覆盖层，避免玩家被锁死。
8. **守卫规则概念**：关键词/语义规则评估，可吸收为我们的主题一致性/安全护栏，但用我们自己的配置和实现。
9. **性能看门狗概念**：`FreezeWatchdog` / `PerfProbe` 的思路有价值，但斯拥只需要轻量版本。

## 不值得借鉴 / 需要改造

1. **巨型单体文件**：不学。按领域拆成 `Knowledge / NpcDialogue / Memory / Events / UI / Commands` 等小模块。
2. **70 个 Harmony patch 摊在 SubModule**：不学。斯拥保持少量、可解释的 patch，或尽量走 Marcus 注册机制。
3. **非正式 `ForExternal` API**：不依赖、不学其形式。我们没有对外插件生态，不需要这套约定。
4. **场景喊话作为主对话链路**：不学。斯拥继续用地图/菜单对话 + 自建覆盖层，避免 mission/scene 深度耦合。
5. **双版本 Bootstrap + 动态程序集加载**：不学。我们只面向 v1.3.15，走 Marcus 的版本化 SDK。
6. **反射私有 UI 字段与 API 兼容层**：不学。能用公开 API 就公开 API，不能用就降级。
7. **ONNX 本地 embedding/rerank 依赖**：不学。向量检索交给 Marcus RAG/Companion，离线回退本地关键词。
8. **全局配置集中在 `DuelSettings`**：不学。继续用 MCM + `SlaaneshConfig`。
9. **大量 WARN/fallback/`[Obsolete]` 遗留**：不照搬。斯拥只保留明确的降级语义，不留“说不清为什么存在”的兼容路径。
10. **无 LICENSE**：不逐字复制。只做学习后重写，并在借鉴日志里记录来源。

## ONNX 教训（重点红线）

AF 把本地语义检索做成了“Mod 内嵌迷你 NLP 框架”，是典型的过度工程反模式，绝对不能学：

- 自写 `BertTokenizerLite`：手写 BasicTokenize / WordPiece，解析 tokenizer JSON 的 vocab、added_tokens、normalizer，出错就回退成 `[CLS][SEP]`。
- 自建 embedding 引擎 + cross-encoder reranker：各自维护模型加载、张量、推理、错误状态。
- 自建 `RagWarmupCoordinator`：后台 Task 预热 embedding/reranker，还要再串联一次语义 warmup。
- 硬塞 native 依赖：`onnxruntime.dll`、`onnxruntime_providers_shared.dll`、`Microsoft.ML.OnnxRuntime`，Bootstrap 还要按顺序预加载私有依赖。
- 最终收益只是“本地向量检索”，而这些能力完全应该由 AI 框架/RAG 服务承担。

斯拥规则：

1. **不引入本地 ONNX / embedding / rerank 推理**，不手写 tokenizer、vocab、模型加载或推理代码。
2. 语义检索只走 Marcus RAG / Companion；Companion 不可用时回退本地关键词，不做第二套向量体系。
3. 任何“必须在游戏侧跑本地模型”的需求，先走 grill-me 说明框架能力为何不足、替代方案为何不可行，再决定是否立项。
4. 教训登记进 `docs\AF-BORROWED-IDEAS-LOG.md` 的反模式表，防止以后被“加个本地语义搜索”的冲动带偏。

## 采纳规则

- 只采纳“能独立解释、能写测试、价值大于复杂度”的部分。
- 每个借鉴点先经过评估，再进 `docs\AF-BORROWED-IDEAS-LOG.md`。
- 不为模仿 AF 而引入 AF 的复杂度；如果某个概念在斯拥里要拆成两套系统才能跑，就说明它不适合直接搬。
- 默认立场：**优先做简单可靠的版本，再按需加深**，而不是先把 AF 的完整体系复刻一遍。

## 下一步

按评估通过度排序：

1. P0 世界书检索升级（学规则模型与检索思路，重写进 `KnowledgeService`）。
2. P0 NPC 主动聊天状态机（学动机分类与冷却，重写进 `NpcProactiveBehavior`）。
3. P0 覆盖层长等待解锁（学交互，补进现有覆盖层）。
4. P1 记忆压缩总览、事件收件箱、开发者终端。
