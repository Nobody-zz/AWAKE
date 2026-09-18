# AnimusForge 本地模型（ONNX）使用逻辑分析

> 日期：2026-09-16
> 对象：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge`（v1.3.2）
> 方法：只读取证 —— 模型目录与 config、shipped DLL 的 UTF-16 字符串、`Logs/` 运行时日志。
> 探针脚本：`AWAKE/tools/_af_onnx_probe_20260916.py`
> 前置阅读：`AF-CRITICAL-EVALUATION-20260815.md` §ONNX 教训、`AF-BORROWED-IDEAS-LOG.md` 反模式表、`AI-Retrieval-Memory-Principles-20260815.md` §四。

## 一、结论

1. **它装了一套双模型管道，而且是真跑起来的。** 召回侧 `bge-small-zh-v1.5`（512 维），精排侧 `xlm-roberta-base` + 分类头（cross-encoder reranker）。生效证据：`FreezeWatchdog_Timeline.txt` 里 `onnx=True` **443 次 / `onnx=False` 0 次**，`ShoutPromptContext.semantic_done` **475 次**。
2. **范式是「进程内 ONNX」**：`onnxruntime.dll` 与 `Microsoft.ML.OnnxRuntime.dll` 直接躺在模组 bin 里，由 Bootstrap 启动时预加载，推理跑在游戏自己的线程上（thread=19/5/20/30/33）。**没有 Python / sidecar 进程。**
3. **语义能力是叠在词法之上的增强，不是替代。** 有 `OnnxGate` 门控，有完整降级链（`mode=lexical_fallback` / `mode=semantic_unavailable` / `reason=query_embedding_unavailable`），融合打分里**保留独立的词法锚点分量**（`lexicalAnchor` / `matchedSeed` / `CalculateTokenOverlapScore`）。它没有指望向量去解决"有没有提到某个词"。
4. **真正的语言能力外包给了云端 LLM。** `Token_Stats.txt`（431 MB / 148 万行）记录的是发往 `deepseek-v4-flash` 的请求体；其中一个 preprocessing router 让云端模型"按语义相关性"从候选记忆里挑 3 条 `memory_ids`。本地 ONNX 承担的是**召回与打分**，不是语义判定。
5. **代价：模型 1.2 GB（reranker 占 1.036 GiB），对话热路径上每轮约 400 ms 花在词法实体检索上、语义阶段只报 1.6–4.6 ms。**
6. **2026-08-15 的裁决「不引入本地 ONNX」不需要翻案，但否决它的理由要改** —— 不能再说"收益只是本地向量检索、没有实际价值"（见 §五）。

## 二、它装了什么（模型清单）

| 用途 | 目录 | 结构（config 一手） | 权重体积 | 身份 |
| --- | --- | --- | --- | --- |
| 文本嵌入 | `ONNX/` | `BertModel`；hidden 512 / 4 层 / 8 头 / intermediate 2048 / vocab 21128 / max_pos 512；`dtype: float32` | `model.onnx` 41,689 B（图）+ `model.onnx_data` 94,765,056 B（≈90.4 MiB） | `bge-small-zh-v1.5`（四项结构全吻合） |
| 精排 | `ONNX/reranker/` | `XLMRobertaForSequenceClassification`；hidden 768 / 12 层 / 12 头 / intermediate 3072 / vocab 250002 / max_pos 514 | `model.onnx` 1,112,459,588 B（≈1.036 GiB） | `xlm-roberta-base` + 分类头 |
| 合计 | | | **≈1.2 GB（≈0.98 GiB）** | |

- 分词器：`tokenizer.json`（emb 362,603 B / reranker 17,098,107 B）。**bin 里没有任何分词库**（无 `Microsoft.ML.Tokenizers`）⇒ 分词是自写的，DLL 字符串里有 `BertNormalizer` 坐实。
- `tokenizer_config.json`：`BertTokenizer`、`max_length 512`、`do_lower_case: false`、`tokenize_chinese_chars: true`。
- 代码里存在 `model_quantized.onnx` 字面量 ⇒ **支持量化模型，但随包分发的是 float32 原精度**。用体积换确定性/精度。
- `ONNX/` 目录 1,224,729,882 B；`bin/` 70 MB；模块总计 **1.9 GB**。

## 三、它怎么跑（运行链路）

**① 进程内推理的证据**

`AnimusForge.Bootstrap.log` 每次启动固定输出：

```
Preloaded private dependency 'System.Runtime.CompilerServices.Unsafe, 4.0.4.1' ...
Preloaded private dependency 'System.Buffers, 4.0.3.0' ...
Preloaded private dependency 'System.Memory, 4.0.1.2' ...
Preloaded private dependency 'Microsoft.ML.OnnxRuntime, 0.0.0.0' from '...\bin\Win64_Shipping_Client\Microsoft.ML.OnnxRuntime.dll'.
Private dependency check completed: preloaded/reused 4 managed assemblies and verified 2 native files.
```

⇒ **`Microsoft.ML.OnnxRuntime` 是官方托管封装，正在被使用**（与原生 `onnxruntime.dll` 10,807,216 B / `onnxruntime_providers_shared.dll` 配套，同为 2024-05-15）。另加 `System.Memory`/`System.Buffers`/`Unsafe` 三个 shim 补齐老 Mono 运行时缺失的 API。

**② 双版本加载器**

`SubModule.xml` 只装配 `AnimusForge.Bootstrap.dll`（`BootstrapSubModule`）；真正的实现体在 `versions/1.3/` 与 `versions/1.4/` 两份 `AnimusForge.dll`（9,091,584 / 9,091,072 B）。Bootstrap 按 `Game version=v1.3.15.110062` 选版本（日志一手：`selected API=1.3`）。`*.build.json` 保留 `ReferenceGameVersion` 与 `Sha256` 供校验。

> 附带发现：`bin/` 与 `bin/bin/` 两份完全相同的副本（三个 DLL 的 sha256 逐一对齐）⇒ **纯冗余打包，不是两个版本**。

**③ 组件与子系统（DLL 字符串一手）**

- 模型侧：`OnnxEmbedding`、`OnnxReranker`、**`OnnxGate`**（门控）、`RagWarmup`、`KnowledgeIndexWarmup`、`GuardrailWarmup`
- 分词侧：`BertNormalizer`
- 检索侧：`WorldEntityRetrieval.*`（`alias_cache_start/done`、`match_category_start/done`、`build_blocks_start`、`hard_budget_stop`、`soft_budget_exceeded`、`FindMatches`、`FindRulerTitleMatches`、`AliasCache`）、`KnowledgeRetrieval`、`PolicyContext.KnowledgeRetrieval`、`KnowledgeGrounding`、`ImportKnowledgeData`、`KnowledgeRules.json`
- 守卫/统计：`GuardrailSemantic`、`HitRate_Stats.txt`、`preprocessHits`

**④ 每轮对话的阶段流水**（`ShoutPromptContext.*`，约 470–490 次/会话）

```
start → entity_context_start → mentions_done → semantic_done → lore_start → lore_done
     → world_runtime_done → triggered_rules_start/done → relationship_blocks_done
     → policy_context_done → shared_resource_done → preprocess_ids_done
     → aux_preprocess_done → weekly_short_done → weekly_full_lore_append_done
     → gccz_runtime_start/done → runtime_init_done → Build
```

典型的 detail：

```
semantic_done      stageMs=4.64  totalMs=1315.27  duel=False reward=False loan=False
mentions_done      stageMs=0.52  totalMs=1332.56  hasMentions=True directCount=7
preprocess_ids_done stageMs=0.04 totalMs=2104.90  ids=marriage,kingdom_service,kingdom_agenda,npc_major_actions
runtime_init_done  stageMs=1.54  totalMs=22.89    semanticContextLen=746  targetKingdom=empire_s
```

⇒ 本地模型的产出**确实进了提示词**：`semanticContextLen` 484 次、`loreLen` 474 次、`WorldEntityRetrieval` 8661 次。

**⑤ 门控与降级**

- `mode=onnx eligible=` / `OnnxGate` ⇒ 条件启用
- 降级链：`hit=True mode=lexical_fallback matched=` / `hit=False mode=semantic_unavailable` / `source=lexical_fallback matchedSeed=` / `; fallback=semantic` / `reason=query_embedding_unavailable queryLen=` / `reason=title_embedding_empty scanned=`
- 其中 `reason=title_embedding_empty` 与 08-15 记的「`BertTokenizerLite` 出错回退 `[CLS][SEP]`」是同一个失败模式：**分词失败不抛错，而是退化成常量向量**。后果是"所有文本的嵌入彼此相同"，检索退化为等权返回全部候选 —— 即本项目反复强调的**静默成功**。

**⑥ 融合打分**（DLL 里的真实格式串）

```
raw={0:0.000} ctx={1:0.000} mixed={2:0.000} rerank={3:0.000} amp={4:0.000}
rank={5} candidate={6} other={7}@{8:0.000} mean={9:0.000} reason={10}
lexicalAnchor={11} matchedSeed={12} intent={13}
```

多分量融合 + 独立词法锚点（`lexicalAnchor`）+ 命中的语义种子（`matchedSeed`）+ 意图（`intent`）。

## 四、代价实测

| 项 | 数值 | 出处 |
| --- | --- | --- |
| 模型体积 | 1.2 GB（reranker 1.036 GiB） | `du -sb ONNX` |
| 模块总计 | 1.9 GB | `du -sh .` |
| 词法实体检索耗时 | 每轮 **~400–490 ms**（`WorldEntityRetrieval.done totalMs=398.74 / 406.42 / 409.27 / 489.86`） | 时间线 |
| 语义阶段耗时 | 每轮 **1.6–4.6 ms**（`semantic_done stageMs=4.64 / 2.05 / 1.79 / 1.62`） | 时间线 |
| 提示词组装累计 | `lore_done totalMs≈1364–1505`（累计值，非单阶段） | 时间线 |
| 进程内存 | `workingSetMB=3465 privateMB=10866 gcMB=396` | 时间线（**整个游戏进程，含其他 mod，不可单独归因**） |
| 启动预加载 | 4 个托管程序集 + 2 个原生文件，每次启动 | Bootstrap 日志 |

⚠️ **两条需要标注的读数**：
- `semantic_done` 只花 1.6–4.6 ms，而 1 GB 的 xlm-roberta 在 CPU 上跑一次 cross-encoder 至少是百毫秒量级。**这个数字与该模型体量不相称** ⇒ 语义阶段的推理很可能不在对话热路径上（结果来自缓存/预热），或该轮走的是降级分支。**待验证**：日志没有覆盖预热阶段的耗时标记（`warmup` / `OnnxGate` 在时间线里 0 命中）。
- `privateMB=10866` 是进程总量，不能当作"模型占用 10 GB"。

## 五、与 AWAKE 既有裁决的关系

`AF-CRITICAL-EVALUATION-20260815.md` 已把「ONNX 本地 embedding/rerank」列为**红线**，并给出四条斯拥规则（不引入本地 ONNX / 语义只走 Marcus RAG / 必须立项前先论证 / 登记反模式表）。**本次分析不推翻这条裁决**，但要把否决理由修正：

| 08-15 的表述 | 本次实测 | 处置 |
| --- | --- | --- |
| 「最终收益只是本地向量检索」 | 收益**真实存在**：`semanticContextLen` 每轮注入、`onnx=True` 443 次、双模型管道完整 | **改口径**：不是"没用"，而是"**收益/代价比不划算，且职责与 Marcus RAG 重叠**" |
| 过度工程反模式 | 成立：自写分词器、自建 embedding+reranker、自建预热、硬塞 native 依赖、per-version 加载器 | **维持** |
| 不引入本地 ONNX | 维持 | **维持**，并按 08-15 的规则 3 走论证前置 |

⇒ 结论：**红线保留，理由从"没有价值"换成"价值已被 Marcus RAG 覆盖，而代价是 1.2 GB + 一套自维护的推理栈"**。这样下次有人再提"加个本地语义检索"时，反驳的是代价与职责，而不是收益 —— 后者会被真实日志打脸。

## 六、对本轮（09-16）结论的影响

1. **`LOCAL-MODEL-SURVEY-20260916.md` 里的 .NET 落地建议需修正。** 真机证明该路线可行（`Microsoft.ML.OnnxRuntime` + 原生 `onnxruntime.dll` + shim + per-version DLL），并且本机 `AnimusForge/ONNX` 就是一份**可直接对照的现成实现**。技术可行性不再是问题 —— **问题在代价与职责边界**。
2. **"换 embedding 就自动免疫 `doc` / `war` 过匹配"仍然不成立**，但理由更清楚了：AnimusForge 的做法是**词法锚点与语义分列**（`lexicalAnchor` 是独立分量），它从设计上就没有让向量承担"是否提到某词"的判定。指望换模型解决关键词过匹配，方向就是错的。
3. **它把"语义选择"交给了云端 LLM**（preprocessing router 挑 `memory_ids`），本地只做召回打分。这解释了为什么它的本地模型可以很小、也可以降级 —— **关键判断不在游戏侧**。

## 七、待验证 / 未覆盖

- **预热阶段的推理耗时**：`RagWarmup` / `KnowledgeIndexWarmup` / `OnnxGate` 在时间线里 0 命中，无法量化模型加载与首次推理的成本。
- **reranker 是否每轮都被调用**：`OnnxReranker` 类名存在、`rerank=` 打分分量存在，但没有对应的阶段标记；`semantic_done` 的 2 ms 与其体量不相称。
- **`onnx=True` 的确切语义**：字面是"该阶段 ONNX 启用"，不等于"嵌入成功"。日志未覆盖跨 `onnx=False` 的会话，无法判断门控在什么条件下关闭。
- **`raw / ctx / mixed / amp` 四个分量的语义**：从格式串无法确定，可能是非检索用途（如音频/情绪统计），不宜按检索解读。

## 八、成本结构：token 花在哪、该省哪一处（09-16 补，甲方提出"云端 token 要花钱"）

### 8.1 我们这边：检索不花 token，花钱的是"辅助调用"

| 环节 | 实现 | token 成本 |
| --- | --- | --- |
| 世界书检索 | `WorldKnowledgeQueryService.FindCandidates` 纯 C# 子串匹配 | **0** |
| RAG 检索 | SQLite FTS5 `tokenize='unicode61'`，跑在进程外 runtime service，走 IPC | **0** |
| NPC 对话 | `AWAKE.route.npc.dialogue` → `deepseek.deepseek-v4-flash` | 按 token 计费 |
| 每日记忆 | `AWAKE.route.memory.daily` → 同上 | 按 token 计费 |

- **结论：我们的检索是免费的，没有可省的 token。** 上本地 embedding 是**纯增支**（体积 + 复杂度），不省钱。
- ⚠️ **我们目前没有 token 账单**：`Awake.log` 里 `token` / `Token` **0 命中**，即花了多少看不见。**先装统计，再谈省。**
- 真机实测的调用结构（`Awake.log` 一手）：**一次对话 = 2 次云端调用** —— `npc_dialogue_submit_accepted` 之后约 9 秒跟着一次 `memory.daily`（例：18:28:50 dialogue → 18:29:01 memory.daily）。

### 8.2 路由现状：本地通道已铺好，只是关着

`AWAKE/docs/profiles.awake.deepseek.routes.json`：

| 连接 | 性质 | 状态 |
| --- | --- | --- |
| `deepseek`（`https://api.deepseek.com`） | `isCloud: true` | **enabled** |
| `ollama-local`（`http://127.0.0.1:11434`） | `isCloud: false` | **disabled** |
| 模型 `deepseek.deepseek-v4-flash` | 云端 | enabled |
| 模型 `qwen2.5-local` | 本地 | **disabled** |

四条路由**全部** `allowCloud: true` + `pinModel: true` + 只指向 deepseek：
`AWAKE.route.npc.dialogue` / `.preprocess` / `.postprocess` / `.memory.daily`。

**本机 Ollama 正在运行，模型已就位**（`curl 127.0.0.1:11434/api/tags` 实测）：
`gpt-oss:20b`（13.8 GB）｜`qwen2.5:latest`（4.68 GB / 7.6B Q4_K_M）｜`qwen3:4b`（2.50 GB / 4B Q4_K_M）｜`qwen:latest`（2.33 GB / 4B）。

⇒ **"零 token 成本跑本地模型"这件事，机器、模型、路由抽象全都具备了，只差把开关打开。**

### 8.3 AnimusForge 那边：两头付费，而且钱花反了

一手数据（`Token_Stats.txt`，3024 次调用）：

| 项 | 数值 |
| --- | --- |
| 调用总次数 | **3,024** |
| 输入 token | **66,255,499** |
| 输出 token | 686,937 |
| **输入 / 输出 比** | **≈ 96 : 1** |

按 mode 分组（节选）：

| mode | 次数 | 性质 |
| --- | --- | --- |
| `universal_api` | 660 | 主对话 |
| `stream` / `stream_pending` | 470 / 470 | 主对话（流式） |
| **`action_postprocess_http`** | **588** | 辅助 |
| **`auxiliary_router_http`** | **492** | 辅助（即"按语义相关性挑 memory_ids"那一步） |
| **`auxiliary_simple_dialogue_http`** | **225** | 辅助 |
| `world_diplomacy_api` | 56 | 辅助 |
| `npc_policy_api` | 27 | 辅助 |
| `non_stream` | 29 | |

- **辅助调用合计 1,305 次 > 主对话 1,130 次。** 每轮对话至少 2–3 次云端往返。
- **平均每次调用输入 ≈ 21,900 token**（66.2 M ÷ 3,024）。省钱主战场是**压输入**，不是加快检索。
- **`auxiliary_router_http` 那 492 次，干的正是本地小模型最擅长也最便宜的活**（从候选里挑几条）。
  它把 1.2 GB 本地模型用来"找候选"，却把"挑候选"交给云端按 token 计费。
  ⇒ **两头付费，且本地模型该承担的那一步反被推给了云端。**

### 8.4 该省哪一处

**正解不是"上检索模型"，而是"把辅助调用挪到本地"。** 两件事很容易混，别混：

1. **`AWAKE.route.memory.daily` 切到 `ollama-local`**（本机 `qwen3:4b` 已装）：每日记忆归纳是结构化压缩、不需要世界知识与文采，是最安全的本地化对象。**省 token，不动对话质量。**
2. **先补 token 统计**：现在 `token` 在日志里 0 命中 ⇒ 花了多少、花在哪条路由上，全是估的。没有这个，任何"省 token"的说法都无法验证。
3. **`AWAKE.route.npc.dialogue` 保持云端**：这条决定玩家感知质量，不该为省钱先动。
4. **检索保持本地词法**（零成本）。真要语义检索，正规入口是把 embedding 模型挂到路由的 `embeddingDimensions`（现在全是 `[]`，即未启用），走 Ollama HTTP —— **而不是在游戏进程里塞 ONNX**。

> **一句话**：AnimusForge 证明了"本地模型"这条路能跑通；但它的账也证明，**本地化的正确对象是辅助 LLM 调用，不是检索**。我们该做的是开那个已经写好的开关，不是搬那 1.2 GB。

---

## 九、性能与内存实测（09-16 补，甲方提出"AF 内存占用大、游戏被大幅拖慢"）

数据源：`Logs/FreezeWatchdog_Timeline.txt`（36.5 MB / 36,113 行 / 36,077 个内存采样）＋ `FreezeWatchdog_LastCheckpoint.txt` ＋ `FreezeDumps/`。
探针：`AWAKE/tools/_af_watchdog_timeline_20260916.py`。

> ⚠️ **归因纪律**：`workingSetMB` / `privateMB` 是**整个游戏进程**的量，本机装了 30+ 个 mod（Diplomacy、SETS、AIInfluence、ALifeOfIceAndFire…），**不能全算到 AnimusForge 头上**。下面能单独归因的只有"它自己的阶段计时"。

### 9.1 内存：占用确实高，但不是"越玩越涨"的泄漏

| 指标 | min | max | 首 | 尾 |
| --- | --- | --- | --- | --- |
| `workingSetMB` | 1,882 | **8,021** | 3,469 | 6,299 |
| `privateMB` | 7,719 | **12,705** | 10,924 | 10,673 |
| `gcMB` | 327 | 968 | 389 | 518 |

走势是**高位震荡**而非单调上行（3,469 → 6,704 → 3,181 → 6,120 → 6,299），中间多次回落。
⇒ **"占用大"成立**（工作集常态 3–6 GB、峰值 8 GB；提交量常年 8–12 GB 不降）；
⇒ **"越玩越卡"不成立**（没有持续增长特征）。
⇒ 真实体感更可能来自下面两项，而不是内存曲线。

### 9.2 慢在哪：**不是本地模型，是"组装提示词"这个流程**

按累计耗时排序（AF 自己的阶段计时，`elapsedMs`）：

| 阶段 | 次数 | 累计 | 单次最长 |
| --- | --- | --- | --- |
| **`ShoutPromptContext.Build`**（提示词组装） | **82** | **155,102 ms** | **5,160.74 ms** |
| `NativeConversation.preprocess_context.background` | 6 | 21,477 ms | 4,292.23 ms |
| `ShoutMissionBehavior.DrainMainThreadActionsForMissionTick` | 6 | 1,777 ms | — |
| `ShoutMissionBehavior.OnMissionTick` | 5 | 1,281 ms | — |
| `WorldEntityRetrieval.BuildPromptContext` | 4 | 1,161 ms | — |

- **`ShoutPromptContext.Build` 单次最慢 5.16 秒，82 次累计 155 秒** —— 组装一次发给云端的提示词，本地要花 1–5 秒。
- 而**语义阶段（本地 ONNX）只占 1.6–4.6 ms**（§四）。
  ⇒ **慢的锅不在那 1.2 GB 模型上，在"组装流程堆叠"上。** 这个 Build 里串了：实体检索 + 语义 + lore + 触发规则 + 关系块 + 政策上下文 + 周报 + preprocess ids + runtime init（§三④ 的完整流水）。
- 帧间隔（`dtMs`，149 个样本）：中位 5.61 ms、p95 16.54 ms、**max 66.67 ms**，>33.3 ms 的 7 次（4.70%）。
  ⚠️ 这是 `SubModule.OnApplicationTick` 的 tick 间隔，暂停/地图模式下会偏高，**不可直接当帧率读**。

### 9.3 真的卡死过（物证）

- **`FreezeDumps/` 里有 2 个真实冻结转储**：
  `AnimusForge_Freeze_20260804_231919_377_pid2884.dmp`（5,135,663 B）、
  `AnimusForge_Freeze_20260804_232203_826_pid2884.dmp`（5,166,871 B）
  ⇒ **2026-08-04 23:19 与 23:22，相隔 3 分钟，游戏冻结了两次**，看门狗抓了转储。
- **主线程真阻塞**：最大 `activeMs = 338.11 ms`，出自 `ShoutMissionBehavior.OnMissionTick`（mission 内）；
  `activeMs > 50` 的采样 **73 次**。⇒ 主线程确实有可感的停顿。
- **心跳断过**：最大 `heartbeatAgeMs = 1,798,803 ms`（≈30 分钟）；但 `>60 s` 的采样只有 **4 条**（占 36,077 的 0.01%）⇒ **个别事件，不是常态**（更像存档/加载/暂停所致）。

### 9.4 长等待：最大的一笔其实在等云端

看门狗触发记录里 `elapsedMs > 5 s` 的 Top：

| 任务 | 耗时 |
| --- | --- |
| `NativeConversation.submit_done` | **115,687.8 ms**（≈116 秒） |
| `NativeConversation.submit_done` | 99,892.2 ms |
| `NativeConversation.submit_done` | 85,046.4 ms |
| `AuxActionPostprocess.complete` | 63,283.3 ms |
| `PrimaryChat.stream.complete` | 51,190.8 ms |

⇒ **"游戏被拖慢"的体感有很大一块是等云端 LLM 生成**（流式产出 500–1500 字中文要几十秒），
**这不是本地算力问题，是网络+生成时间**。而玩家感受上是一样的"卡"。

### 9.5 专项否证：不是那 1.2 GB 模型占内存/显存导致的

甲方追问"会不会是 1.2G 模型太占内存、显存导致的？"——**实测否掉**，两条分开看。探针：`AWAKE/tools/_af_model_mem_20260916.py`。

**① 显存：完全不占。** `bin/` 里只有 `onnxruntime_providers_shared.dll`，**没有任何 CUDA / DirectML / TensorRT 后端**（全部 dll 清单见 §三①）⇒ **纯 CPU 推理**。显存这条直接划掉。

**② 内存：跑 ONNX 的那一刻，内存零变化。**

| 观测 | 结果 |
| --- | --- |
| 首次 `onnx=True`（08-03 01:13:29，seq=70）前后的 `workMB` | 3448 → 3465 →（其后 40 条）3465 ⇒ **跳变 ≈ +0 MB** |
| 443 条 `onnx=True` 的 `workMB` | min 1882 / 中位 3930 / max 7987 / 均值 **4707** |
| 35,634 条 `onnx=False` 的 `workMB` | min 1882 / 中位 3940 / max 8021 / 均值 **4725** |
| 两组均值差 | **−18 MB**（可忽略） |

⇒ **模型不是"对话时按需加载"，是启动时装好常驻**（时间线第一条 seq=2 时就已是 3469 MB）——所以跑与不跑，水位一样。

**③ 卡顿与内存相关，但不是因果。** `activeMs > 50` 的 73 条采样，`workMB` 中位 **6130**，明显高于全体中位 **3940**。但看最卡的 10 条：全部落在 **mission / 战斗场景**（`ShoutMissionBehavior.OnMissionTick`），且最大也只有 **338.11 ms** ⇒ 是"高内存时段恰好也是重场景"，**不是内存把帧拖慢**。

**④ 它真正的代价是这两条，都不是"运行时占地"**：
- **启动时**读 1.2 GB 权重 + 建推理会话 ⇒ 一次性停顿。`AnimusForge.Bootstrap.log` 显示模块加载段 `09:06:49.35 → 09:07:02.40` ≈ **13 秒**（含全部初始化，非单独归因模型）。
- **永久抬高约 1.2 GB 内存水位** ⇒ 对 8 GB 内存机器更容易触发换页。**这才是"内存大→卡"的唯一真实路径，且取决于玩家机器配置。**

**结论：它占内存是真的，但"占内存"和"拖慢游戏"在数据上对不上。** 拖慢的是 §9.2 的 5 秒组装流程与 §9.4 的云端等待。

### 9.6 结论（对我们的含义）

把"卡"拆成四份，AF 各占一份，但**权重完全不同**：

| 来源 | 权重 | 性质 |
| --- | --- | --- |
| 提示词组装流程堆叠（82×1.9 s） | **最大** | 本地、可避免 |
| 等云端 LLM 生成（30–116 s） | **体感最大** | 网络，换不了 |
| 主线程瞬时阻塞（≤338 ms，73 次） | 中 | 本地 |
| 1.2 GB 模型 + 内存占用 | 体积大、但**不解释慢** | 体积成本，不是时间成本 |

⇒ **学它就要连"臃肿的组装流程"一起继承**；而那个流程才是真正吃掉帧时间的东西。
⇒ 这条**强化**了 08-15 的红线，并把否决理由说得更准：**拦我们的不该只是"1.2 GB 太大"，而是"一大套为支撑它而生的流程，会把对话链路的本地耗时从毫秒级推到秒级"。**

#### 9.6.1 ⚠️ 澄清：问题不是"要不要组装"

"慢在组装"这个说法容易被读成"别组装"——**错。组装必须做，没有任何替代方案**（不组装就没有提示词）。要分清的是两件不同的事：

| | 是什么 | 成本 | 能不能省 |
| --- | --- | --- | --- |
| **拼装** | 把**已选好**的内容按预算拼成一段文本 | 字符串合并，**微秒级** | 不必省，也不该省 |
| **选料** | 决定这一轮**该放哪些**世界书 / 记忆 / 关系 / 规则 | AF 这里是 18 个阶段、**1–5 秒** | **该省的是这个** |

- AF 的错在**选料**：每轮把 18 个阶段全跑一遍（实体检索 ~400 ms ＋ lore 5061 字 ＋ 周报 ＋ 政策 ＋ 规则触发 ＋ preprocess ids），而其中绝大部分**一轮之内不会变**。
- 正确做法**我们 2026-08-15 已经写过**（`AI-Retrieval-Memory-Principles` §六.8「会话级免检」、§六.9「场景/角色知识预载」）：**进对话时预载一次，之后只做增量**。
- 我们自己的链路（`NpcDialogueService.BuildPromptInputAsync`）结构上也是每轮组装，但每轮工作量远小于 AF —— **世界书查询是内存里的子串比对**，不是 400 ms 的实体检索 ＋ 语义推理。
- ⚠️ **但我们没有每轮耗时的账**：`Awake.log` 里带 `ms` 的行只有 1 条（`image_probe_ok ... elapsed_ms=6644`，生图）。
  ⇒ **探针本身会记账（生图那条就记了），只是对话链路没接上。** 这与"没有 token 统计"是同一个病。
- 顺带一条支持性证据：生图走的是 **`http://127.0.0.1:4315/v1/image/generate`** ⇒ **我们的重活本来就放在本机服务**，与"辅助调用交给 `ollama-local`"同向，也与"往游戏进程内塞 ONNX"相反。

#### 9.6.2 "我们的活小"——逐项代码依据（甲方追问"活小在哪里、为什么快"）

**结论：活小是硬的，有代码可查；"快"我只给依据，不给结论（没记账，见下）。**

我们的组装入口是 `NpcDialogueService.BuildPromptInputAsync`（`NpcDialogueService.cs:1010`）。逐项核过：

| 步骤 | 我们 | 是否出进程 | AF 对应 |
| --- | --- | --- | --- |
| 查世界书 | `WorldbookRuntime.Knowledge.Query(...)` → **`WorldKnowledgeQueryService`**（本仓 `WorldKnowledgeQueryService.cs:9`，读内存快照 `_snapshot` 子串匹配） | **否，纯内存** | `WorldEntityRetrieval`：**1801 次分类匹配**，`done totalMs≈399–490 ms` |
| 编译提示词 | `_host.Prompts.CompileAsync` → **`AwakePromptRegistry`**（本仓 `AwakePromptRegistry.cs:46`） | **否，本地注册表** | 多阶段渲染 ＋ **额外一次云端 preprocess** |
| 读取角色状态 | `PersonaSessionHydrationAdapter.HydrateAsync` → `WorldStateStore.GetPersonaStateAsync` → `IKeyValueStore` | **否，本地文件** | `shout_prompt` 全量重跑 |
| 注入量 | `NpcDialoguePromptPipeline.BuildBounded` ＋ `MaximumRetrievedBlockBytes` / `MaxPromptUtf8Bytes` **硬上限** | — | lore **5061 字**固定注入 |
| 每轮云端往返 | **无**（`AWAKE.route.preprocess` 在 `Awake.log` **0 命中**＝未接） | — | **`auxiliary_router_http` 492 次** |

**关键一条（决定"出不出进程"）**：`AwakeHostComposition.cs:71` 明确 **`Storage = new AwakeFileStorageService()`** —— 后端是本地文件（内部 `JsonFileKeyValueStore`，`AwakeFileStorageService.cs:116`）；同文件 `:73` 注释说"**RAG 数据面经 IPC 转发**"，即**只有 RAG 走 IPC，而对话组装链路不碰 RAG**。

⇒ **活小在哪**：我们每轮最大的一笔开销是**一次小 JSON 文件的读**（persona state），其余全是内存操作；AF 每轮是 **400 ms 检索 ＋ 18 个阶段 ＋ 一次云端往返 ＋ 5061 字 lore**。
⇒ **为什么快**：**没有一样需要"出门"**——不出进程、不出网、不查数据库。AF 每轮要出门三趟（进程外检索、云端预处理、云端主对话）。

**⚠️ 两个"我们自己的"慢点嫌疑（待测）**：
1. `BuildPromptInputAsync` 里有多处 `AwakeLog.Write`（`npc_dialogue_knowledge_decision` **每轮必写**）—— 若同步写盘，是个稳定开销。
2. 每轮一次 `JsonFileKeyValueStore` 读（persona state）—— 本地 IO，但仍是 IO。

**仍缺的那件事**：以上只有"结构可证"，**没有一条 `elapsed_ms` 实测**（§9.6.1）。所以"快"目前是**推断**，不是结论。

---

## 附：取证命令

```bash
# 模型清单与体积
du -sb ONNX ONNX/reranker ; ls -l ONNX ONNX/reranker

# 进程内推理证据
grep -n "Preloaded private dependency" AnimusForge.Bootstrap.log | head

# 生效证据（关键：在 FreezeWatchdog 里，不在业务日志里）
grep -c "onnx=True" FreezeWatchdog_Timeline.txt          # 443
grep -c "onnx=False" FreezeWatchdog_Timeline.txt         # 0
grep -o "name=ShoutPromptContext.semantic_done detail=.\{0,240\}" FreezeWatchdog_Timeline.txt | head

# 组件与类名（DLL 字符串，UTF-16LE）
python AWAKE/tools/_af_onnx_probe_20260916.py strings
python AWAKE/tools/_af_onnx_probe_20260916.py logs

# 内存走势 / 主线程阻塞 / 最耗时阶段 / 冻结转储（§九）
python AWAKE/tools/_af_watchdog_timeline_20260916.py

# 实际业务链路（走云端）
grep -n -m 1 -A 4 "preprocessing router" Token_Stats.txt
```
