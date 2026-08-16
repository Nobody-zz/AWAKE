# 世界书读取规则格式与结构 30 轮自审查

> 日期：2026-08-16
> 对象：`WorldbookModels.cs`、`WorldbookLoader.cs`、`Awake-Worldbook-Interface-Spec-20260816.md`

## 第一组：格式定义

### Round 1
- 问题：`manifest.json` 缺少 `id` / `schemaVersion` / `sourceFormat` 校验，会不会静默接受坏世界书？
- 发现：会。`ParseManifest` 全部用默认值，没有必填校验。
- 建议：manifest 必填 `id` 与 `schemaVersion`；未知 `sourceFormat` fail closed。

### Round 2
- 问题：`kind`、`scope`、`persistence` 是否有白名单？
- 发现：没有。任意字符串都会被接受。
- 建议：定义枚举白名单，未知值直接拒绝或警告。

### Round 3
- 问题：`priority` 是否限制 0-1000？
- 发现：没有 clamp。
- 建议：解析时 clamp，或在校验阶段拒绝越界。

### Round 4
- 问题：`contentTier` 是否校验？
- 发现：没有。
- 建议：只允许 `pure / standard / intense`，AF 导入时映射到默认档。

### Round 5
- 问题：`when` 中空列表语义是什么？
- 发现：没有定义。空数组到底是“不限制”还是“必须为空”？
- 建议：明确空列表 = 不限制；`null` = 不限制。

### Round 6
- 问题：`when.SkillMin` 是否限制非负？
- 发现：没有，负数也会进入字典。
- 建议：拒绝负数，key 使用游戏技能 ID。

### Round 7
- 问题：`variants` 与 `content` 的关系是否明确？
- 发现：不明确。有 variants 时 content 是否仍是兜底？
- 建议：无匹配 variant 时使用 `content`，全部不匹配时规则仍可命中但注入兜底。

### Round 8
- 问题：`variantSelection = random` 是否安全？
- 发现：未定义种子，结果不可复现。
- 建议：random 使用规则 ID + 玩家/NPC ID 的确定性哈希，或先不做 random。

### Round 9
- 问题：`TextMappings` 是否只存 JObject？
- 发现：只存 JObject，没有类型模型。
- 建议：定义 `WorldbookTextMapping` 强类型，至少校验 `SourceText` / `Kind` / `TargetId`。

## 第二组：读取器

### Round 10
- 问题：persona 目录文件解析异常是否捕获？
- 发现：没有。`JObject.Parse` 异常会直接抛出。
- 建议：与 rules 目录一致，逐文件 try/catch 并记录 warning。

### Round 11
- 问题：规则文件重复 ID 是否检测？
- 发现：没有。
- 建议：加载后统一校验，重复 ID 拒绝或按 manifest 策略处理。

### Round 12
- 问题：persona 重复 CharacterId 是否检测？
- 发现：没有。
- 建议：重复时警告，防止角色人格被覆盖。

### Round 13
- 问题：空 rules 目录是否应该失败？
- 发现：不会，可能静默加载空世界书。
- 建议：`RuleCount == 0` 时按 manifest 标记或拒绝。

### Round 14
- 问题：目录路径是否可能穿越？
- 发现：`ResolvePath` 支持绝对路径，可能读取 manifest 之外目录。
- 建议：默认只允许相对路径，绝对路径需显式声明。

### Round 15
- 问题：规则文件是数组时，fallback ID 是否稳定？
- 发现：`filename_0`、`filename_1` 依赖数组顺序，重排会变。
- 建议：数组内规则必须显式 `id`，否则拒绝。

### Round 16
- 问题：persona 文件是否支持数组？
- 发现：不支持，只解析单对象。
- 建议：与规则一致支持单对象或数组。

## 第三组：索引与匹配

### Round 17
- 问题：`ragShortTexts` 是否参与检索？
- 发现：没有。当前只保留，不建索引。
- 建议：作为 RAG 召回种子，未来接入 Marcus RAG；本地检索至少把短句作为候选文本之一。

### Round 18
- 问题：`semanticPrototypes` 是否参与检索？
- 发现：没有。
- 建议：作为 RAG 查询扩展，但不作为本地关键词权重。

### Round 19
- 问题：n-gram 索引是否实现？
- 发现：没有。
- 建议：实现 `ngram -> rules` 多对多索引，并生成查询侧 n-gram。

### Round 20
- 问题：keyword 是否按“全部命中规则”建索引？
- 发现：尚未实现索引，只有数据模型。
- 建议：建立 `keyword -> all rules` 索引，查询时取全部候选。

### Round 21
- 问题：游戏代码值大小写敏感是否落地？
- 发现：模型/加载器已改为 Ordinal，但匹配服务未实现。
- 建议：实现 `Ordinal` 匹配并加测试。

### Round 22
- 问题：身份绑定与关键词检索是否分层？
- 发现：未实现。
- 建议：先身份绑定构建持久知识池，再在池内做关键词/ngram。

### Round 23
- 问题：场景关键词是“排序”还是“过滤”？
- 发现：规范已说明只排序，但代码未实现。
- 建议：实现 context score，不把未命中场景的持久知识排除。

## 第四组：AF 兼容

### Round 24
- 问题：AF 多 Variants 语义是否保留？
- 发现：只加 warning，实际解析仍按通用 `WorldbookVariant`。
- 建议：实现 AF 的 when-match-score + skillMin tie-break 选择器，独立于 AWAKE `first/all/random`。

### Round 25
- 问题：AF `TextMappings` 是否只是保留？
- 发现：是，只存原始 JSON。
- 建议：至少先实现 `clan_leader_name` / `status|hero|is_dead` 等常见 Kind，其余标 unsupported。

### Round 26
- 问题：AF `SkillMin` 是否参与 variant tie-break？
- 发现：没有。
- 建议：AF 兼容模式下，按 AF 规则把 SkillMin 求和用于 tie-break。

### Round 27
- 问题：AF `personality_background` 文件名能否作为 CharacterId？
- 发现：可以，fallback 使用文件名。
- 建议：保留，并同时支持文件内 `CharacterObjectId`。

### Round 28
- 问题：AF 的 `unnamed_persona` / `voice_mapping` / `event_data` 是否加载？
- 发现：manifest 有字段，但 loader 没有读取。
- 建议：先明确这三类数据是否纳入 WorldbookDocument，再实现读取。

## 第五组：运行时闭环

### Round 29
- 问题：`WorldbookQueryResult` 是否包含错误与警告？
- 发现：没有。
- 建议：增加 `Errors` / `Warnings`，避免静默降级。

### Round 30
- 问题：世界书是否已接入 NPC 对话？
- 发现：没有。`NpcDialogueService` 仍走旧 `KnowledgeService`。
- 建议：实现 `WorldbookService.Query`，再替换 `retrieved_knowledge` 数据源。

## 总结：改进方向

1. **先补强类型模型**：manifest、rule、persona、textMapping 都要强类型校验。
2. **读取器统一容错**：rules/personas 都支持单文件或多文件，逐文件捕获异常并输出 warning。
3. **索引必须多对多**：keyword/ngram -> all rules；RAG 只做召回补充。
4. **身份绑定先于话题检索**：持久知识池不随场景清空，场景只影响排序。
5. **AF 兼容模式独立实现**：Variants / TextMappings / SkillMin 不能套用 AWAKE 默认逻辑。
6. **补齐运行时接入**：实现 `WorldbookService`，再替换 NPC 对话知识源。
7. **增加测试覆盖**：格式校验、大小写、多文件、重复 ID、身份匹配、variant 选择、预算组装。

## 落地结果（2026-08-16）

- 类型模型、多文件容错、多对多 keyword/ngram 索引、身份绑定优先、AF Variants `af-best` 选择、运行时接入 NPC 对话均已落地。
- `WorldbookTextMappingResolver` 已覆盖《卡拉迪亚编年史》扫描到的全部 22 种 `TextMappings.Kind`：状态判断、实体名、领地列表、bound lore 占位符。
- 未知 Kind 不再静默消失：保留 `SourceText`，导入时写 `text_mappings_unsupported` warning。
- `NpcDialogueService.BuildMappingContext` 从战役对象填充 Hero/Clan/Kingdom/Settlement 名称、状态与领地列表。
- SdkSmoke 新增覆盖：状态真假、领地列表、家族领袖、定居点统治者、bound 名称、Kind 白名单与整体替换。
