# AWAKE 世界书接口与格式规范 v0.1 草案

> 日期：2026-08-16
> 状态：设计草案，供确认后落成代码接口与校验。

## 1. 目标

- 提供内容包注册世界书的统一接口。
- 定义世界书 JSON 格式，让事件、角色、场景、关系规则都有稳定元数据。
- 让 AI 按 NPC 身份背景读取世界书，而不是把整本世界书塞进提示词。
- 针对 AF 的关键词检索、RAG 短句召回问题给出可落地的替代方案。

## 2. 世界书格式规范

### 2.1 根结构

```json
{
  "schemaVersion": "awake.worldbook.v1",
  "id": "slaanesh.worldbook.calradia",
  "culture": "calradia",
  "rules": []
}
```

### 2.2 Rule 结构

```json
{
  "id": "rule.empire.lord.honor",
  "kind": "background",
  "scope": "npc",
  "persistence": "persistent",
  "priority": 100,
  "when": {
    "heroIds": [],
    "characterIds": [],
    "cultures": ["empire"],
    "kingdomIds": [],
    "settlementIds": [],
    "roles": ["lord"],
    "identityIds": [],
    "isFemale": null,
    "minAge": 18,
    "maxAge": null,
    "isClanLeader": null,
    "contentTier": "pure"
  },
  "context": {
    "sceneKeywords": ["court", "keep"],
    "contextModes": ["settlement_keep", "town"]
  },
  "keywords": ["荣誉", "封臣", "誓言"],
  "ngrams": ["荣誉", "封臣誓言"],
  "ragShortTexts": ["帝国贵族以誓言和荣誉衡量自身地位"],
  "semanticPrototypes": ["贵族不会公开违背誓言"],
  "content": "帝国领主视荣誉高于私利，但不等于不会背叛；背叛必须由具体事件推动。",
  "variants": [
    {
      "when": {
        "isFemale": true
      },
      "content": "女领主同样以誓言立身，但宫廷更常质疑她的权威。"
    }
  ],
  "textMappings": []
}
```

### 2.3 字段边界

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `schemaVersion` | 是 | 固定 `awake.worldbook.v1` |
| `id` | 是 | 内容包内唯一 |
| `rules[].id` | 是 | 全局唯一 |
| `rules[].kind` | 是 | `persona` / `background` / `world` / `relationship` / `scene` |
| `rules[].scope` | 是 | `global` / `npc` / `kingdom` / `settlement` / `culture` |
| `rules[].persistence` | 是 | `persistent` = NPC 长期知识；`contextual` = 仅当前场景/事件补充 |
| `rules[].priority` | 否 | 0-1000，默认 0 |
| `rules[].when` | 是 | 身份与内容适用条件 |
| `rules[].context` | 否 | 场景/事件条件，只影响当前注入优先级 |
| `rules[].content` | 是 | 注入正文 |
| `rules[].variants` | 否 | 按额外条件替换正文 |
| `rules[].keywords` | 否 | 精确词/近义词，用于本地索引 |
| `rules[].ngrams` | 否 | 2-3 字中文词，用于 n-gram 回退 |
| `rules[].ragShortTexts` | 否 | RAG 检索短句，不作为权威正文 |
| `rules[].semanticPrototypes` | 否 | 语义原型，后续可映射到 RAG 查询 |

### 2.4 知识 vs 提示词

- 知识：NPC 长期拥有的身份、文化、王国、定居点、角色、关系、个人背景。`persistent` 规则一旦绑定到该 NPC，就一直属于这个 NPC 的知识，不因场景切换或年龄增长而消失。
- 提示词：当前对话实际注入给 AI 的文本。受 token 预算限制，只能选取知识层的一部分；场景只决定“这次优先注入什么”，不影响 NPC 知道什么。

### 2.5 When 条件

`when` 是持久知识的适用条件，不参与全文模糊匹配，也不是“临时想起”的开关：

- `heroIds`：稳定 HeroId
- `characterIds`：CharacterObject StringId
- `cultures` / `kingdomIds` / `settlementIds`
- `roles`：领主、商贩、士兵、酒馆老板等
- `identityIds`：无名 NPC 的确定性身份键
- `isFemale` / `minAge` / `maxAge` / `isClanLeader`
- `contentTier`：内容档位门控

年龄是内容适用条件，例如 18+ 门控、童年记忆、成年礼仪，不是“NPC 一出门就忘了”的检索开关。年龄变化后重新评估一次绑定，而不是每轮对话重新过滤。

### 2.6 Context 条件

`context` 只用于 `contextual` 规则或持久规则的“当前注入优先级”：

- `sceneKeywords`：当前场景关键词
- `contextModes`：城镇、城堡、村庄、海上、扎营、遭遇等
- 命中 context 的规则在当前对话中优先注入，不命中不会从 NPC 知识中删除

例如：帝国领主知道自己的封臣义务，这属于持久知识；在领主府对话时，这条规则会优先注入。离开领主府后，他仍然知道这些，只是当前对话不一定需要占用提示词预算。

## 3. 世界书接口

### 3.1 注册接口

```csharp
public interface IWorldbookRegistry
{
    bool Register(WorldbookManifest manifest);
    bool Unregister(string worldbookId);
    IReadOnlyList<WorldbookManifest> List();
}
```

`WorldbookManifest` 包含：

- `Id`
- `SchemaVersion`
- `ContentTier`
- `CorpusFingerprint`
- `RuleCount`
- `Owner`

### 3.2 查询接口

```csharp
public interface IWorldbookQueryService
{
    Task<WorldbookQueryResult> QueryAsync(WorldbookQuery query, CancellationToken ct);
}
```

`WorldbookQuery` 包含：

- `NpcTarget`：HeroId / CharacterId / identity
- `CultureId` / `KingdomId` / `SettlementId`
- `Role` / `Gender` / `Age`
- `SceneKeywords`
- `PlayerText`
- `ContentTier`
- `MaximumBytes`

`WorldbookQueryResult` 包含：

- `IdentityRules`：命中的身份绑定规则
- `TopicRules`：命中的话题/场景规则
- `RetrievedText`：最终注入文本
- `MatchMode`：`identity` / `keyword` / `ngram` / `rag` / `mixed`
- `ByteBudget`：各层消耗
- `HitIds`：用于日志与调试

## 4. NPC 身份读取链路

1. NPC 身份确定后，按 `when` 构建持久知识池。
2. 持久知识池进入该 NPC 的知识缓存，不随场景切换清空。
3. 打开对话时，用当前玩家文本和场景关键词对知识池做“注入排序”。
4. 排序命中后按优先级和字节预算组装提示词，进入 `NpcPromptTemplate` 的 `retrieved_knowledge`。
5. 同一 NPC 在城镇、扎营、海上都知道自己的文化、家族、身份；只是不同场景优先注入不同切片。

这样“一个帝国的商贩”不会读到“帝国女领主”的宫廷规则；但商贩离开城镇后不会忘记自己是帝国商贩。

## 5. 对 AF 检索问题的突破

### 5.1 AF 的问题

- `Keywords` 只做词面命中，没有身份绑定前置。
- `RagShortTexts` 被拼接成一条检索文本，中文 FTS5 分词弱，短句召回不稳定。
- 所有规则放在同一检索池，缺少 per-NPC worldbook slice。
- 规则命中与 RAG 命中没有明确权威顺序，知识层截断可能把规则一起截掉。
- 没有 n-gram 回退，中文问法偏离关键词时基本靠运气。

### 5.2 AWAKE 方案

| 层 | 作用 | 解决什么 |
| --- | --- | --- |
| 身份绑定 | 先按 `when` 构建持久知识池 | 避免全员关键词竞争 |
| 结构化索引 | 只索引 `id / kind / when / keywords / ngrams` | 避免整本世界书暴力扫描 |
| 中文 n-gram | 2-3 字本地索引 | 解决中文 FTS5 token 退化 |
| RAG | 仅作为话题补充 | 不承担身份权威 |
| 预算保护 | 身份规则独立预算 | 避免知识层截断规则 |
| 缓存 | 战役内按 NPC 缓存持久知识池 | 避免每轮全量检索 |
| 日志 | 记录命中模式与规则 ID | 可观测、可调参 |

### 5.3 能否突破

能，但有两个前提：

- Marcus RAG 当前不是真正的语义 embedding，不能把 RAG 当作唯一语义层；它只能作为弱信号。
- 不引入本地 ONNX / embedding 推理；语义增强只能等框架提供或由规则质量承担。

所以突破不是“换一个更强的检索模型”，而是：

- 减少需要检索的范围。
- 让规则先于 RAG。
- 让中文检索有确定性回退。
- 让每次命中都可审计。

## 6. 与 Marcus 的边界

- 世界书文件加载、校验、指纹由 AWAKE 负责。
- RAG 写入与搜索继续走 Marcus `RagIngestRequest` / `RagSearchRequest`。
- 世界书注册表不替代 Marcus Storage；规则本体可存 ModuleData，运行状态走 Storage。
- 内容包通过 AWAKE 注册接口接入，不能直接调用 Marcus。

## 7. 落地顺序建议

1. 锁定 `WorldbookManifest` 与 Rule schema。
2. 实现注册表与校验。
3. 实现身份绑定检索。
4. 实现中文 n-gram 本地索引。
5. 接入 Marcus RAG 作为第二层。
6. 内容包把 AF 世界书迁移到新格式。
