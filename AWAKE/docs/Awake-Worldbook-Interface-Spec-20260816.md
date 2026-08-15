# AWAKE 世界书接口与格式规范 v0.1 草案

> 日期：2026-08-16
> 状态：设计草案，供确认后落成代码接口与校验。

## 1. 目标

- 提供内容包注册世界书的统一接口。
- 定义世界书 JSON 格式，让事件、角色、场景、关系规则都有稳定元数据。
- 让 AI 按 NPC 身份背景读取世界书，而不是把整本世界书塞进提示词。
- 针对 AF 的关键词检索、RAG 短句召回问题给出可落地的替代方案。
- 明确世界书是数据层，不是提示词层；两者不混用。

## 2. 世界书格式规范

### 2.0 世界书放置位置

世界书属于内容数据，不硬编码进运行时：

- 内容包：`Modules/<ContentPack>/ModuleData/Worldbook/manifest.json`
- 运行时测试/兼容目录：`Modules/AWAKE/ModuleData/Worldbook/manifest.json`

推荐目录结构：

```text
Worldbook/
  manifest.json
  rules/
  personality_background/
  unnamed_persona/
  voice_mapping/
  event_data/
  debt/
  dialogue_history/
  compressed_memory/
```

`manifest.json` 中的目录字段可配置，默认与 AF `PlayerExports` 目录名一致。

### 2.1 根结构

```json
{
  "schemaVersion": "awake.worldbook.v1",
  "id": "slaanesh.worldbook.calradia",
  "culture": "calradia",
  "rules": [],
  "personas": []
}
```

### 2.1.1 AF 世界书结构对照

AF 实际使用两类文件：

1. 知识规则文件，结构为：

```json
{
  "Id": "rule_帝国军制",
  "Keywords": ["帝国军制", "常备军", "军团"],
  "RagShortTexts": ["旧帝国依赖常备军团维持边境秩序"],
  "SemanticPrototypes": ["军团是旧帝国秩序的象征"],
  "Variants": [
    {
      "Priority": 0,
      "When": {
        "HeroIds": null,
        "Cultures": ["empire"],
        "KingdomIds": null,
        "SettlementIds": null,
        "Roles": ["lord"],
        "IdentityIds": null,
        "IsFemale": null,
        "IsClanLeader": null,
        "SkillMin": null
      },
      "Content": "帝国领主通常用军团传统解释自己的军事立场。"
    }
  ],
  "TextMappings": []
}
```

2. 角色人格背景文件，结构为：

```json
{
  "Personality": "这个角色对外表现的脾气、欲望、说话方式和判断习惯。",
  "Background": "这个角色真实的出身、经历、立场和秘密。",
  "VoiceId": ""
}
```

### 2.1.2 AF 字段作用分析（以《卡拉迪亚编年史》为例）

| 字段 | 实际作用 | 示例 |
| --- | --- | --- |
| `Keywords` | 规则召回关键词，多个规则可共享同一关键词 | `"“半耳”圭卡"` |
| `RagShortTexts` | 检索问句/短描述，用于 RAG 召回，不是最终正文 | `"哈尔达尔为何忌惮“半耳”圭卡？"` |
| `SemanticPrototypes` | 语义原型，用于检索扩展；当前目录多为空 | `[]` |
| `Variants` | 同一规则按身份/文化/角色给出多个说法，运行时选一个 | 平民、领主、诺德人分别不同说法 |
| `TextMappings` | 正文里的动态占位符，按游戏状态替换 | `SourceText: "A"`、`Kind: "status|hero|is_dead"` |

`TextMappings` 不是静态文本，它依赖游戏状态：

- `SourceText`：正文中的占位符。
- `Kind`：取值来源，如 `status|hero|is_dead`、`clan_all_towns`、`clan_leader_name`。
- `TargetId`：目标实体。
- `TrueText` / `FalseText` / `EmptyValueText`：按状态替换的结果。

AWAKE 对应字段：

- `variants`：已有。
- `ragShortTexts` / `semanticPrototypes`：已有，但只作为召回元数据。
- `textMappings`：已有，但当前只保留原始数据，未实现运行时替换。

### 2.2 AWAKE Rule 结构

AWAKE 保留 AF 字段名和语义，不另造一套：

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
    "skillMin": null,
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
      "priority": 0,
      "when": {
        "isFemale": true
      },
      "content": "女领主同样以誓言立身，但宫廷更常质疑她的权威。"
    },
    {
      "priority": 1,
      "when": {
        "isFemale": true
      },
      "content": "另一种同等成立的女领主表达，按优先级或顺序解析。"
    }
  ],
  "textMappings": []
}
```

### 2.2.1 Persona 结构

```json
{
  "characterId": "CharacterObject_1795__图卢勒",
  "personality": "角色对外表现的脾气、欲望、说话方式和判断习惯。",
  "background": "角色真实的出身、经历、立场和秘密。",
  "voiceId": ""
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
| `rules[].variants` | 否 | 允许多条；同一 when 允许多个 variant，按 priority 再按数组顺序解析 |
| `rules[].variants[].priority` | 否 | 变体优先级，默认 0 |
| `rules[].variantSelection` | 否 | `first` / `all` / `random`；默认 `first` |
| `rules[].keywords` | 否 | 精确词/近义词，用于本地索引 |
| `rules[].ngrams` | 否 | 2-3 字中文词，用于 n-gram 回退 |
| `rules[].ragShortTexts` | 否 | RAG 召回种子，不作为权威正文；不设 100 字符硬限制，建议单条不超过 512 字 |
| `rules[].semanticPrototypes` | 否 | 语义原型，后续可映射到 RAG 查询 |
| `personas[]` | 否 | 角色人格背景，对应 AF `personality_background` |
| `personas[].characterId` | 是 | 角色稳定 ID |
| `personas[].personality` | 否 | 角色人格正文 |
| `personas[].background` | 否 | 角色背景正文 |
| `personas[].voiceId` | 否 | 预留语音 ID |

### 2.4 世界书、提示词、上下文三层分离

三层必须分开：

1. **世界书层**：纯数据。JSON 规则、身份绑定、检索索引、RAG 文档。世界书不定义 NPC 系统指令，不定义输出 schema，不参与 `PromptDefinition` 注册。
2. **检索层**：把世界书数据变成当前对话可用的上下文片段。输入 NPC 身份、玩家文本、场景；输出 `WorldbookQueryResult`。检索层只负责“选什么、花多少预算”，不改变世界书内容。
3. **提示词层**：底层提示词只包含角色边界、对话要求、输出契约。它不包含世界书正文，也不包含世界书检索逻辑；只留一个上下文占位符，例如 `{{retrieved_knowledge}}`。

知识属于世界书层：NPC 长期拥有的身份、文化、王国、定居点、角色、关系、个人背景。`persistent` 规则一旦绑定到该 NPC，就一直属于这个 NPC 的知识，不因场景切换或年龄增长而消失。

上下文属于检索层：受 token 预算限制，只能选取世界书知识的一部分；场景只决定“这次优先注入什么”，不影响 NPC 知道什么。

提示词属于提示词层：不存世界书，不改世界书，不负责世界书召回。

### 2.5 When 条件

`when` 是世界书层持久知识的适用条件，不参与全文模糊匹配，也不是“临时想起”的开关：

- `heroIds`：稳定 HeroId
- `characterIds`：CharacterObject StringId
- `cultures` / `kingdomIds` / `settlementIds`
- `roles`：领主、商贩、士兵、酒馆老板等
- `identityIds`：无名 NPC 的确定性身份键
- `isFemale` / `minAge` / `maxAge` / `isClanLeader`
- `skillMin`：技能下限条件，对应 AF `SkillMin`
- `contentTier`：内容档位门控

年龄是内容适用条件，例如 18+ 门控、童年记忆、成年礼仪，不是“NPC 一出门就忘了”的检索开关。年龄变化后重新评估一次绑定，而不是每轮对话重新过滤。

### 2.6 Context 条件

`context` 只用于 `contextual` 规则或持久规则的“当前上下文排序”：

- `sceneKeywords`：当前场景关键词
- `contextModes`：城镇、城堡、村庄、海上、扎营、遭遇等
- 命中 context 的规则在当前对话中优先注入，不命中不会从 NPC 知识中删除

例如：帝国领主知道自己的封臣义务，这属于持久知识；在领主府对话时，这条规则会优先注入。离开领主府后，他仍然知道这些，只是当前对话不一定需要占用提示词预算。

### 2.7 世界书不做什么

- 不定义 NPC 系统指令。
- 不决定输出 schema。
- 不参与 `PromptDefinition` 注册。
- 不写入底层提示词模板。
- 不直接执行命令或修改游戏状态。
- 不承担对话流程、取消、超时、权限等运行时逻辑。

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
- `PersonaCount`
- `SourceFormat`：`awake` / `af`
- `RulesDirectory`
- `PersonaDirectory`
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
- `MatchedKeywords`：命中的关键词/n-gram
- `ResolvedVariantIds`：实际解析的 variant 标识
- `HitIds`：用于日志与调试

## 4. NPC 身份读取链路

1. 世界书层按 `when` 构建该 NPC 的持久知识池。
2. 持久知识池进入检索层缓存，不随场景切换清空。
3. 打开对话时，检索层用当前玩家文本和场景关键词对知识池做“上下文排序”。
4. 检索层按优先级和字节预算组装 `WorldbookQueryResult`。
5. 提示词层只接收检索层提供的上下文片段，不直接读取世界书。
6. 同一 NPC 在城镇、扎营、海上都知道自己的文化、家族、身份；只是不同场景优先注入不同切片。

这样“一个帝国的商贩”不会读到“帝国女领主”的宫廷规则；但商贩离开城镇后不会忘记自己是帝国商贩。

## 5. 对 AF 检索问题的突破

### 5.1 AF 的问题

- `Keywords` 只做词面命中，没有身份绑定前置。
- 单一 keyword 只对应一条 rule，导致同 keyword 的多条规则无法全部进入候选。
- 同一规则相同 `when` 只允许一个 variant，创作上限制过大。
- `RagShortTexts` 有固定短句字数限制，例如 100 字符，无法承载更完整的事实。
- `RagShortTexts` 被拼接成一条检索文本，中文 FTS5 分词弱，短句召回不稳定。
- 所有规则放在同一检索池，缺少 per-NPC worldbook slice。
- 规则命中与 RAG 命中没有明确权威顺序，知识层截断可能把规则一起截掉。
- 没有 n-gram 回退，中文问法偏离关键词时基本靠运气。

### 5.2 AWAKE 方案

| 层 | 作用 | 解决什么 |
| --- | --- | --- |
| 身份绑定 | 先按 `when` 构建持久知识池 | 避免全员关键词竞争 |
| 结构化索引 | 只索引 `id / kind / when / keywords / ngrams` | 避免整本世界书暴力扫描 |
| 多对多关键词 | keyword -> 全部包含该 keyword 的 rules | 避免“一词只出一条规则” |
| Variants 多值 | 同一 when 允许多个 variant，按优先级/顺序解析 | 放宽创作限制 |
| RAG 预算 | 不设 100 字符硬上限，由单条与总预算控制 | 保留完整事实片段 |
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

### 5.4 推荐改进（不要求全部采用）

针对你提出的三个限制，建议如下：

1. **keyword 多对多**
   推荐改为：同一 keyword 命中所有包含该 keyword 的 rules，全部进入候选集。候选集再经过身份绑定、优先级、预算排序，最终只注入最合适的一部分。这样不会“一词只出一条”，也不会把所有命中规则全部塞进提示词。

2. **同一 when 允许多个 variants**
   推荐允许同一规则、相同 `when` 下存在多个 variant。解析顺序为：
   - `priority` 从高到低
   - 相同优先级按数组顺序
   - 可选 `variantSelection = first / all / random`
   - 默认 `first`，避免无脑拼接导致上下文膨胀

3. **RAG 短句不设 100 字符硬限制**
   推荐把 `ragShortTexts` 定义为“召回种子”，不是权威正文。权威正文放在 `content` / `variants`。所以不需要把完整事实硬塞进 100 字；建议单条 `ragShortTexts` 不超过 512 字，总预算由检索层控制。

4. **不建议的做法**
   - 不把所有 keyword 命中规则全部注入。
   - 不把 `ragShortTexts` 当作最终知识正文。
   - 不默认把同一 when 的所有 variants 拼接。
   - 不用 100 字符限制反向约束内容创作。

### 5.5 优先级计算（纯代码，不使用 AI 打分）

检索分两步：

1. **硬过滤**：不满足 `when`、`contentTier`、`persistence` 门控的规则直接排除。
2. **固定权重评分**：用以下纯函数计算，模型不参与排序。

```csharp
long Score(WorldbookRule rule, WorldbookQuery query)
{
    long identity = 0;
    if (Matches(rule.When.HeroIds, query.HeroId)) identity += 1000;
    if (Matches(rule.When.CharacterIds, query.CharacterId)) identity += 900;
    if (Matches(rule.When.IdentityIds, query.IdentityId)) identity += 800;
    if (Matches(rule.When.Cultures, query.CultureId)) identity += 600;
    if (Matches(rule.When.KingdomIds, query.KingdomId)) identity += 500;
    if (Matches(rule.When.SettlementIds, query.SettlementId)) identity += 400;
    if (Matches(rule.When.Roles, query.Role)) identity += 300;
    if (rule.When.IsFemale != null && rule.When.IsFemale == query.IsFemale) identity += 100;
    if (rule.When.MinAge != null && query.Age >= rule.When.MinAge) identity += 100;
    if (rule.When.MaxAge != null && query.Age <= rule.When.MaxAge) identity += 100;
    if (rule.When.IsClanLeader != null && rule.When.IsClanLeader == query.IsClanLeader) identity += 100;
    if (MeetsSkillMin(rule.When.SkillMin, query.Skills)) identity += 100;

    long context = 0;
    foreach (string keyword in query.SceneKeywords)
    {
        if (Contains(rule.Context.SceneKeywords, keyword)) context += 200;
    }
    foreach (string mode in query.ContextModes)
    {
        if (Contains(rule.Context.ContextModes, mode)) context += 150;
    }

    long recall = 0;
    recall += CountHits(rule.Keywords, query.PlayerText) * 100;
    recall += CountHits(rule.Ngrams, query.PlayerText) * 50;

    return identity * 1000 + context * 100 + recall * 10 + rule.Priority;
}
```

排序规则：

- 按 `Score` 从高到低。
- 分数相同按 `rule.Priority` 从高到低。
- 仍相同按 `rule.Id` 字典序，保证确定性。

边界：

- `RAG` 只负责召回候选，不参与分数计算。
- `ragShortTexts` / `semanticPrototypes` 不进入分数。
- 所有权重是固定常量，后续调整只能改常量，不能引入模型打分。
- `persona` / `background` 持久规则拥有独立预算，不与非身份规则竞争。

### 5.6 AF 世界书兼容层

目标：AF 世界书只做“不破坏内容的少量扩展”即可直接接入，不改字段名、不改正文。

兼容原则：

- 解析器同时接受 AF 的 PascalCase 和 AWAKE 的 camelCase。
- AF 单规则文件可以直接作为一条规则加载。
- 缺失字段使用默认值，不要求作者补全。
- AF `personality_background` 文件自动映射为 AWAKE `personas`。
- 不做内容重写，只做字段归一化。

字段默认值：

| AF / AWAKE 字段 | 默认值 |
| --- | --- |
| `Id` -> `id` | 必填 |
| `Keywords` -> `keywords` | `[]` |
| `RagShortTexts` -> `ragShortTexts` | `[]` |
| `SemanticPrototypes` -> `semanticPrototypes` | `[]` |
| `Variants` -> `variants` | `[]` |
| `TextMappings` -> `textMappings` | `[]` |
| AWAKE `kind` | `background` |
| AWAKE `scope` | `npc` |
| AWAKE `persistence` | `persistent` |
| AWAKE `priority` | `0` |
| AWAKE `contentTier` | `pure` |
| AWAKE `ngrams` | `[]` |
| AWAKE `context` | `{}` |

目录布局示例：

```text
worldbook/
  manifest.json
  rules/
    rule_xxx.json
  personality_background/
    CharacterObject_xxx.json
```

`manifest.json`：

```json
{
  "schemaVersion": "awake.worldbook.v1",
  "id": "af.calradia.dark",
  "sourceFormat": "af",
  "rulesDirectory": "rules",
  "personaDirectory": "personality_background"
}
```

导入流程：

1. 读取 `manifest.json`。
2. 扫描 `rules/*.json` 并映射为 AWAKE Rule，缺失字段填默认值。
3. 扫描 `personality_background/*.json` 并映射为 AWAKE Persona。
4. 校验 `id` 唯一、`content` 非空。
5. 构建 `keyword -> 全部 rule` 和 `ngram -> 全部 rule` 的多对多索引。
6. 计算指纹并注册到 `IWorldbookRegistry`。

字段名兼容：

- `HeroIds` -> `heroIds`
- `Cultures` -> `cultures`
- `KingdomIds` -> `kingdomIds`
- `SettlementIds` -> `settlementIds`
- `Roles` -> `roles`
- `IdentityIds` -> `identityIds`
- `IsFemale` -> `isFemale`
- `IsClanLeader` -> `isClanLeader`
- `SkillMin` -> `skillMin`
- `Priority` -> `priority`
- `Content` -> `content`

解析器统一输出 AWAKE camelCase，但输入两种命名都接受。

### 5.7 AF 兼容风险与处理边界

AF 兼容不能只做字段改名，以下内容必须保留语义或明确标记：

| 风险点 | AF 语义 | AWAKE 处理 |
| --- | --- | --- |
| `Variants` | AF 按 when 匹配分数选一个最佳 variant | AF 导入模式使用 `af-best`，不使用 AWAKE `first/all/random` 默认规则 |
| `TextMappings` | 依赖 AF 运行时动态替换，Kinds 与游戏状态绑定 | 先保留原始 JSON，标记 `text_mappings_preserved`；未实现动态替换前不执行 |
| `SkillMin` | AF 在 variant 选择中参与匹配与 tie-break | 保留字段，AWAKE 原生评分只做布尔门槛；两种模式分开记录 |
| `RagShortTexts` | AF 拼接成检索文本，不是权威正文 | AWAKE 只作为召回种子，不进入最终知识正文 |
| `SemanticPrototypes` | AF 用于语义候选 | AWAKE 保留，但不作为检索唯一依据 |
| `VoiceId` | AF TTS 语音 | AWAKE 保留原始值，媒体层未实现前不使用 |
| 未知字段 | AF 可能随版本新增 | 保留在 `Raw`，不静默丢弃 |

导入时必须生成 `WorldbookImportWarning` 报告：

- `source`：文件名或规则 ID
- `code`：如 `text_mappings_preserved`、`af_variant_semantics`、`voice_id_preserved`
- `message`：说明保留还是降级

未支持项不允许静默消失。

### 5.8 大小写策略

字段名与值必须区分处理：

- **字段名**：大小写不敏感。`HeroIds` / `heroIds`、`Cultures` / `cultures` 都可解析。
- **值**：大小写敏感。`empire` 是游戏原版文化代码，`Empire` 不是同一个值，不能归一化、不能合并。
- 导入时保留原始值到 `Raw`，不做小写化。
- 匹配身份/分类值时使用大小写敏感比较。
- 同一规则内完全相同的值去重；仅大小写不同的值视为不同值，保留两者。
- 关键词、场景关键词这类人类语言文本可另行配置大小写不敏感，但不影响 `Cultures` / `Roles` / `IdentityIds` 等游戏代码。

当前安装的 AF 世界书扫描结果：

- 规则数：1447
- 检查字段：`HeroIds` / `Cultures` / `KingdomIds` / `SettlementIds` / `Roles` / `IdentityIds`
- `TextMappings.Kind` 也检查
- 暂未发现仅大小写不同的重复值

兼容层不会把大小写不同的游戏代码视为同一分类。

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
