# 世界书管线 · 发现全集

> 由 `AWAKE/tools/gen_skill_refs.py` 从既有 skill 的发现提取生成。**不要手改**——改了会被下次重跑覆盖。
>
> **这是跨 agent 的共享知识**（见 `AWAKE/AGENTS.md`〈跨 agent 共享知识〉）。任何 harness 的 agent 都应读这里，不要各自维护私有副本。
>
> 共 **384** 条发现，其中 **57** 条标了 `needs_reverify`（测量于旧版本，引用前复核）。

## 头部规则

1. **`valid_for` 是测量版本，不是当前版本。** 本机游戏已从 v1.3.15 升到 **v1.4.8**；凡 `valid_for=v1.3.15` 的，引用前先复核。
2. **`未标版本` 的条目 = 提取时未记录版本**，请**按 v1.3.15 对待**（即同样需要复核）。
3. **否定式断言（`negative-claim`）风险最高**——最容易因升级变成假话。先看文末〈待重验队列〉。
4. **`出处` 只到「旧 skill 名 # 小节名」一级。** 旧 skill 在各 harness 的私有目录里（`~/.workbuddy/skills` 等），**不在本仓库**，故不保留行号——留着是假的可点性。
5. 本文件是**世界里的事实**，不是程序。怎么做任务看对应 skill 或 `AWAKE/AGENTS.md`。

## 按 kind 索引

| kind | 条数 | 待重验 |
|---|---|---|
| `engine-behavior` | 91 | 0 |
| `byte-layout` | 17 | 2 |
| `db-schema` | 11 | 4 |
| `measured-number` | 130 | 10 |
| `silent-failure` | 34 | 8 |
| `api-quirk` | 33 | 3 |
| `negative-claim` | 24 | 23 |
| `version-fact` | 6 | 2 |
| `path-fact` | 38 | 5 |
| **合计** | **384** | **57** |

---

# 引擎行为（engine-behavior）

> 共 91 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F001 | 对话路径的检索实现是 NpcDialogueService.SendAsync 调用 WorldKnowledgeQueryService.FindCandidates。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §0 步骤表 |
| F002 | FindCandidates 用双向子串 text.IndexOf(kw)>=0 \|\| kw.IndexOf(text)>=0（:206-223）。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §0 步骤表 |
| F003 | WorldbookService 是另一套检索：单向 playerText.IndexOf(ngram)，另有 _keywordIndex/_ngramIndex 两张索引。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §0 步骤表 |
| F006 | 繁体问法自 09-17 起走系统 LCMapStringEx 折叠后可以命中。 | 09-17 起（未标游戏版本） |  | worldbook-retrieval-redtest §2 样本表·繁体行 |
| F007 | 全半角折叠（NFKC）自 09-17 起生效；修之前全角版本 not_found 而半角版本命中。 | 09-17 前后对照（未标游戏版本） |  | worldbook-retrieval-redtest §2 样本表·全半角行 |
| F017 | 全库条目把内部文档 id doc.<domain>.<slug> 写进了 keywords。 | 09-16 当前包 |  | worldbook-retrieval-redtest §5 |
| F018 | RuntimePackageCompiler.cs:108-121 注释 K1：keywords 来源＝标题+别名+实体锚点可读名称，内部 doc id 只作兜底且排最后。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §5b |
| F019 | FindCandidates 只做集合判定、MatchQuality 只看匹配串长度，数组位置不参与匹配语义，「排最后＝兜底」是不存在的机制。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §5b |
| F020 | WorldKnowledgeEntry.Keywords 仍是数据不变；被剔掉的只是 KeywordIndex 里「能不能当检索键」。 | 09-17 |  | worldbook-retrieval-redtest §5c |
| F021 | 索引剔除三规则唯一建法收敛在 WorldbookKeywordIndex，两处 RebuildKeywordIndex 都调它。 | 09-17 |  | worldbook-retrieval-redtest §5c |
| F022 | R1 覆盖率上限：覆盖条目数 > 40 的词不进索引。 | 09-17 |  | worldbook-retrieval-redtest §5c 表·R1 |
| F026 | R3 判据：StartsWith("doc.", OrdinalIgnoreCase) 的键不进索引。 | 09-17 |  | worldbook-retrieval-redtest §5c 表·R3 |
| F031 | R1 是零表判据，与 WorldbookTermIndex.MaxTermDocumentFrequency 同值同理。 | 09-17 |  | worldbook-retrieval-redtest §5c R1 说明 |
| F032 | R2 刻意放过 Goods／HeadArmor／Husn Fulq —— 无可信数据说它们是内部 id。 | 09-17 |  | worldbook-retrieval-redtest §5c R2 说明 |
| F033 | R3 是双保险：编译器已决定不写 doc.，但出厂包可能仍是旧编译器编的，运行时再拦一道。 | 09-17 |  | worldbook-retrieval-redtest §5c R3 说明 |
| F035 | term 兜底通道会把已剔掉的关键词从另一条腿漏回来（entry／economy／geography 曾如此）。 | 09-17 |  | worldbook-retrieval-redtest §5c ⚠️剔完必须再跑 |
| F037 | FormatEntry（WorldKnowledgeQueryService.cs:509）把 entry 级 summary 拼进 RetrievedText 喂给模型。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §5e |
| F038 | summary 同时进 term 索引与向量（WorldKnowledgeLoader.EnumerateTerms / WorldKnowledgeSemantic.JoinParts）。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §5e |
| F040 | summary 拼在该身份可见的正文层之前且不单独门控，所以把 secret 层的事写进摘要等于对所有人越权透露。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §5e ★★ |
| F045 | deploy_worldbook_to_game.ps1 头注写明 Bytes are copied, never re-serialized，手工改产物会破 canonical hash。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §5e 修法 |
| F048 | R1 可被「把泛词包进稀有复合词」绕过：deriat 的 aliases 含「德里亚特·村庄」覆盖 1 不被剔，村庄是其子串 ⇒ 唯一候选。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5f 机理 |
| F049 | 绕过 R1 后比不剔更坏：不剔是 272 条一起过匹配，剔了之后是唯一候选、理直气壮答错。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5f 结论 |
| F054 | 「专名＋类别词」是全库正当形态（Mecalovea Castle／阿特费尼亚城堡／沙拉斯湾），子串继承分不清它与「德里亚特·村庄」。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5f 为什么否掉 |
| F055 | 已落地修法：WorldbookKeywordIndex.WrapsGenericCategoryWord（IsExcluded 第④条）只剔裹住整个泛问词的键。 | 09-17 起 |  | worldbook-retrieval-redtest §5f 修法方向② |
| F062 | FindLiteralCandidates 只在关键词层一无所获时才走兜底（WorldKnowledgeQueryService.cs:363）。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §5g 回归① |
| F064 | 兜底通道按 2-gram 共享数排序，打平之后按 id 排序。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §5g 回归② |
| F066 | 撞字门禁要求「概念词条 title+summary 的 term」∩「门禁题集查询的 term」= 0（同一套 EnumerateTerms + df>40 剔除）。 | 09-17 起 |  | worldbook-retrieval-redtest §5g 检查器 |
| F071 | 自命中测试的身份要照条目自己的 grants 挑：无条件 grant 优先，再按 DETAIL_RANK／SCOPE_RANK 取最宽。 | 09-17 |  | worldbook-retrieval-redtest §5h 三条口径 |
| F072 | 自命中测试的 requested_detail 要取该 grant 的 min_detail，否则 detail 不够会直接 partial。 | 09-17 |  | worldbook-retrieval-redtest §5h 三条口径 |
| F081 | 那 4 条假命中是因为词里恰好含 Crossbow／Shield／Horse／Bow（被别条当关键词且覆盖低没被 R1 剔）；问 crossbow_c 命中 steppe_war_bow。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5h ⚠️ASCII 侧 |
| F083 | 上游 NpcDialogueService.cs:225 用 IsNullOrWhiteSpace 挡住空输入，返回 ImmediateFail("对方在等你开口。")。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §6 边界核查 |
| F090 | 任何模糊手段（拼音／编辑距离／向量）都会让命中面变大。 | 09-17 |  | worldbook-retrieval-redtest §7 三个具体的坑 |
| F097 | 折叠必须降级：written <= 0 或抛异常时原样返回，不能让可选的语言折叠把整条检索带崩。 | 09-17 起 |  | worldbook-retrieval-redtest §7d 必须降级 |
| F099 | 繁简折叠只折同一个字的繁体写法：拉邁薩→拉迈萨 折对了。 | 09-17 起 |  | worldbook-retrieval-redtest §7d 边界 |
| F102 | 低于 MinimumScore 的语义候选根本不返回，上游拿不到也就进不了池子。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §7e ① |
| F103 | 0.45 是这个模型＋这份语料的性质，换模型或换拼法必须重量。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §7e ①⚠️ |
| F108 | FallbackMinSharedTerms = 1：关键词层一无所获后，共享一个 term 就算候选。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §7e ③ |
| F113 | 「该空手」的题分三类根因：① 指代眼前这个地方 ② 指代前文的人/事 ③ 类别词没有概念条。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7e ③⚠️ |
| F114 | MinSemanticQueryLength 让单字不给语义腿；单字「货」的向量近乎均匀，最近邻其实是任意的。 | 09-17 起 |  | worldbook-retrieval-redtest §7e ④ |
| F115 | 出口判据 E 量的是结果会不会被送去调模型（真开关 WorldKnowledgeDecision.AllowsAi），不是捞回几条。 | 09-17 起 |  | worldbook-retrieval-redtest §7e ⑥ |
| F116 | WorldbookIdentityCapabilityRules.ResolveNoble 对 45 岁以上贵族给的是 secret（不是 detail）。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §7b ① |
| F124 | 子串匹配要求逐字连续，任何插入／替换／变换都归零（错字、繁简、词中空格标点、倒装、拼音、全角）。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest 三类根因 1 |
| F125 | 分界：标点在词后无害，插在词中间才致命。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest 三类根因 1★ |
| F126 | 碎片子串会被别的卡抢走：「链甲 围帽」命中了「布制围帽与头巾」，因为碎片「围帽」是它的关键词，打平后排序让它赢。 | 09-17 当前包 |  | worldbook-retrieval-redtest 三类根因 2 |
| F127 | 反向匹配 kw.IndexOf(text) 让任何短词命中所有含它的关键词。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest 三类根因 3 |
| F001 | RuntimePackageCompiler.cs:112-122 的 keywords 按 title → aliases → 锚点可读名 依次去重生成，title 恒在最前 | 未标版本（AWAKE 代码线） |  | worldbook-encyclopedia-rollout-batch 步骤2·硬约束·别名(09-20) |
| F005 | CLI 单档启动慢的真因是每个新进程都跑 AuthorityGate.Recover()，逐条解析 operations/ 下 4000+ 条账目 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤3·旧的归因是错的 |
| F006 | operations/ 里每条 compile 结算记录还会再调 CleanupCompiledPrevious 遍历一次整目录，单次启动十几万次文件读 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤3·旧的归因是错的 |
| F012 | runner 会把请求层钳到身份能力上限：villager 求 detail 回落 rumor 层，结果是 partial 而非 blocked | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤4·矩阵探针 |
| F013 | SelectExpression（src/WorldKnowledgeQueryService.cs:476）对每一档只在 entry.Expressions 里挑一条 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥一档只送一条表达 |
| F014 | 表达排序值 = ruleScore*10 + 层号，用 > 比较 ⇒ 同分取先者 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥ |
| F018 | 排序值里的不是 layer 枚举，而是编译器按该表达各 grant 的 min_detail 定出的 expression.Detail | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥b① |
| F020 | 身份 EffectiveDetail/Scope 必须 ≥ grant 的 min_detail/scope，否则该表达直接出局并记 PermissionLimited | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥b② |
| F021 | 能力上限实测：villager=local/rumor、townsfolk=regional/summary、merchant=faction/detail、noble=elite/detail | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥b② |
| F022 | 给低能力身份挂高级别 grant（如 townsfolk 挂 min_detail=detail）等于死表达，该受众永远够不着 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥b② |
| F023 | 同一受众挂两条并列表达时层号高者通吃，另一条永远送不到（如 merchant/summary 压 merchant/rumor） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥b② |
| F025 | --pure 模式把 scope/detail/requested_detail 钉到 grant 自身，只测「谁知道」这一维 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥b③ |
| F026 | --pure 若把 requested_detail 钉成 summary，服务会走条目级 summary 分支（state=known）而不是表达 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥b③★坑 |
| F028 | culture/kingdom 匹配走 LastSegment（按 : 切）⇒ 裸名 vlandia 可命中 awake:culture:vlandia | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥b③ |
| F048 | awake-prose-qc 只扫 assertions[].text 与 expressions[].text 两处正文；summary/title/aliases 不扫 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·文本质检 awake-prose-qc |
| F059 | manifestHash＝包身份（不改档就不变）／contentHash＝runtime+index／packageHash＝整包；只换包不同步哈希报 WB2-HASH-MISMATCH | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤10·2 注册表三哈希 |
| F060 | 运行时按 canonical-JSON 重算哈希并用 StringComparer.OrdinalIgnoreCase 比对（src/WorldbookPackageIntegrity.cs） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤10·2 注册表三哈希 |
| F061 | deploy_worldbook_to_game.ps1 头注 Bytes are copied, never re-serialized；手工改名/改内容会破 canonical hash | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤10·绝不能手工改产物 |
| F065 | op 记录一少，QuarantineCompileOrphans() 按设计把 compiled/ 下没有 op 记录的一级条目当孤儿移走 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·QuarantineCompileOrphans |
| F066 | 把 op 放回后 validate 又触发 recovery，产出一度丢过的 op 被标 state=quarantined | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·QuarantineCompileOrphans |
| F067 | 09-17 事故中内容没删只是换了位置；真机与仓库上线件 ModuleData/Worldbook/ 全程不受影响 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·QuarantineCompileOrphans |
| F072 | workspace-head.json 里改名后残留的旧 doc_id → 旧记录条目不在任何 selection 里，对编译零影响；判据＝是否出现在 selection 中 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·旧 id 残影 |
| F091 | 检索＝title+aliases+锚点上的双向子串，所以堡档 aliases 收村名 + 村档 aliases 收上级名即互为入口，无需改 C#、无需 content-graph 边 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤8·联系机制 |
| F094 | 旧排序是纯 Id 字母序：geography.castle-* 的 c 恒压 geography.village-* 的 v，问村名先答堡档 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤8·同名聚落与检索权重 |
| F095 | 现排序是 MatchQuality 分档：0 标题即所问 > 1 别名即所问 > 2 标题互为子串 > 3 仅关键词子串；同级比匹配串长度降序，末按 Id | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤8·同名聚落与检索权重 |
| F101 | TaxonomyCatalogService 是唯一 domain/subdomain 校验器，validate/compile/preview 等全走它 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤5·不得有第二份分类清单 |
| F105 | 前缀与 subdomain id 拼写不同源是设计（military_system↔military 等），禁顺手统一 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤5·文件名前缀不是分类轴 |
| F117 | 每条 grant 的 min_detail 必须等于所在层 layer | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤2·硬约束 |
| F120 | slug 取官方英文名的小写形式 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤2·硬约束 |
| F121 | 地理批旧档（river-sethys／mount-iltan）与村庄档会撞同一段官方描述文首句，导致 quote_hash 相撞 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤6·交叉引文 |
| F16 | 已实现且被内容触发：关键词召回 FindCandidates、正向授权 SelectExpression（MatchesIdentityAndConditions） | 未标版本 |  | worldbook-field-liveness-audit / L80 |
| F26 | PersonaDialogueSim.cs 写 KingdomId = definition.CharacterId（拿人物 ID 顶王国 ID） | 未标版本 |  | worldbook-field-liveness-audit / L113 替身顶掉的字段 |
| F42 | 无关键词命中 ⇒ state=not_found ⇒ AllowsAi=false ⇒ 不调 AI，NPC 回一句写死的「这件事我没听说过。」 | 未标版本 |  | worldbook-field-liveness-audit / L185 第七类失效 |
| F52 | ConsolidateDailyForNearbyHeroesAsync 名为「就近」，实现是遍历 AliveHeroes 且 limit>=8 就 break，实为前 8 个活英雄 | 未标版本 |  | worldbook-field-liveness-audit / 9.2 假筛选 |
| F58 | 语义臂入参只有玩家原话 Search(playerText, limit)、不看身份，故七个身份拿到的候选表完全相同；矩阵里变的那几行只可能是身份闸门造成 | 未标版本 |  | worldbook-field-liveness-audit / |
| F59 | 同一次对照中「领主」查询吐出一条讲奴隶兵的条目，属召回率与正确性反向（把诚实的不知道换成了自信的答非所问） | 未标版本 |  | worldbook-field-liveness-audit / |
| f02 | 装配后校验要求包内文件集合恰好等于 manifest.json+runtime.json+index.json，否则 throw | 未标版本 |  | awake-worldbook-new-compile-input §〇-3（:202-204） |
| f05 | 往 runtime.json 的 entries[] 加字段不会破包校验，ValidateIndex 不遍历 entries 的其它字段 | 未标版本 |  | awake-worldbook-new-compile-input §一-雷区4 |
| f06 | contentHash 覆盖 runtime.json 与 index.json 两个文件的规范化内容 | 未标版本 |  | awake-worldbook-new-compile-input §一-雷区4 |
| f07 | packageHash = SHA-256(manifestHash 字节 ‖ contentHash 字节) | 未标版本 |  | awake-worldbook-new-compile-input §一-雷区4 |
| f08 | Studio 测试 F73「A2 records declared read inventory and zero downstream reads」断言声明的输入闭包==实际读入清单 | 未标版本 |  | awake-worldbook-new-compile-input §一-雷区1（测试 F73） |
| f11 | 诊断金标 a3-2-content-graph-golden 按 code/path/order 逐条比对，多报一条 warning 即红 | 未标版本 |  | awake-worldbook-new-compile-input §一-雷区3 |
| f12 | 编译器只在条目有出边时才写 entryExtensions["links"]，无出边不写空数组 | 未标版本 |  | awake-worldbook-new-compile-input §一-代码形状 4 |
| f18 | 扩召回的种子是候选表而不是被问的那一档，候选里坐着别的有边条目时照样会扩 | 未标版本 |  | awake-worldbook-new-compile-input §三-🚨 |
| f23 | loader 对缺失/旧格式/自环的边一律视作「没有数据」不报错，旧包可照常跑 | 未标版本 |  | awake-worldbook-new-compile-input §二-旧包必须能照常跑 |
| f27 | BuildEntry 对每档每个 entity_id 无条件调用 CanonicalEntityRef（无 try/catch 兜底） | 未标版本 |  | worldbook-anchor-verification §三 |

## 证据

- **F001** 读 src/WorldKnowledgeQueryService.cs:206-223 与调用链，确认 FindCandidates 为对话路径实际靶子。
- **F002** 读源码 WorldKnowledgeQueryService.cs:206-223 逐行确认匹配表达式。
- **F003** 读 WorldbookService 源码，与 FindCandidates 的规则对比（§0 表第二行）。
- **F006** 读产品归一化函数（09-17 落地）＋红测繁体组由红转绿。
- **F007** 红测全半角组两档读数：修前全角 not_found、半角命中。
- **F017** 扫编译包 runtime.json 的 keywords 字段，发现 doc. 前缀项。
- **F018** 读 RuntimePackageCompiler.cs:108-121 的设计注释原文。
- **F019** 读 FindCandidates 与 MatchQuality 实现，确认索引顺序不进入匹配判定。
- **F020** 对比数据字段与索引构建代码（WorldbookKeywordIndex）的职责划分。
- **F021** 读 WorldKnowledgeLoader.cs 中 WorldbookKeywordIndex 与两处 RebuildKeywordIndex 调用点。
- **F022** 读 WorldbookKeywordIndex.IsExcluded 的 R1 分支与阈值 40。
- **F026** 读 WorldbookKeywordIndex.IsExcluded 的 R3 分支。
- **F031** 对比 R1 阈值 40 与 WorldbookTermIndex.MaxTermDocumentFrequency 的取值。
- **F032** 读 R2 规则的例外说明与 09-17 落地记录。
- **F033** 读 R3 落地理由注释与 K1 注释的对照。
- **F035** 剔完再跑完整红测，发现上述词经 term 腿重新命中。
- **F037** 读 WorldKnowledgeQueryService.cs:509 FormatEntry 的字符串拼接逻辑，顺序为【标题】+summary+该档正文。
- **F038** 读两处调用点：EnumerateTerms(entry.Summary) 与 JoinParts(Title,keywords,Summary,firstExpression)。
- **F040** 读 FormatEntry 拼接顺序＋权限分层只作用于正文层的事实。
- **F045** 读 deploy_worldbook_to_game.ps1 文件头注原文。
- **F048** _replicate_kw_index_20260917.py 逐字复刻三规则后穷举候选集定位。
- **F049** 对照 R1 开关两种状态的候选集大小与最终答案。
- **F054** 对被继承口径剔掉的条目逐条人工核对，全为正当形态。
- **F055** 读 WorldKnowledgeLoader.cs 中 WrapsGenericCategoryWord 与 IsExcluded 第④条实现。
- **F062** 读 WorldKnowledgeQueryService.cs:363 的分支条件。
- **F064** _probe_fallback_order_20260917.py 复算兜底排序规则并回验台确认。
- **F066** _probe_concept_collision_20260917.py 已挂进重编链当门禁，可对 --pkg 指任意包。
- **F071** _gen_entry_fit_spec_20260917.py 照真件 PickIdentity 逻辑挑身份，不重实现链路。
- **F072** 同上脚本按 grant 的 min_detail 造 spec。
- **F081** 对 4 条命中逐条查命中来源，定位到双向子串捞回邻居条目。
- **F083** 读 NpcDialogueService.cs:225 及其 ImmediateFail 的确切文案。
- **F090** 三类通道各跑一次过匹配组，命中面均只增不减。
- **F097** 读产品函数里的降级分支并实测异常路径。
- **F099** 对拉邁薩 输入实测折叠结果。
- **F102** 读搜索循环的 continue 分支，确认候选在返回前被丢弃。
- **F103** 该常量取值注释记录了由三组余弦实测分布反推。
- **F108** 读 WorldKnowledgeQueryService 中兜底通道的常量与判据。
- **F113** 09-17 对归因文件逐题追之三分，三类对应不同层（对话层/内容层）。
- **F114** 读该常量的落地位置，并观察单字输入在语义腿上的命中随机性。
- **F115** 读 WorldKnowledgeDecision.AllowsAi 开关与判据 E 的落地（tools/worldbook-rag-merge）。
- **F116** 读真件 ResolveNoble 的年龄分支，与探针手抄平表对照。
- **F124** 各类对抗样本在字面两腿上的统一归零现象。
- **F125** 词中/词后两组样本对照：词后全命中、词中全归零。
- **F126** 对该输入跑探针读 hits，再回查命中条目的 keywords。
- **F127** 对单字/短 token 统计命中条目数，与正向规则对照。
- **F001** 阅读 AWAKE 源码 src/RuntimePackageCompiler.cs:112-122 的 keywords 拼装段
- **F005** 09-15 归因：同 op id 幂等重跑只要 0.25 秒，排除 operation-journal.jsonl 回放
- **F006** 09-15 读 AuthorityGate.Recover 调用链 + 计时归因（固定成本占主导）
- **F012** 矩阵探针实测：villager 身份请求 detail 时返回 partial 层表达
- **F013** 读 src/WorldKnowledgeQueryService.cs:476 的 SelectExpression 实现
- **F014** 读 SelectExpression 排序比较代码，09-19 记于步骤11⑥
- **F018** 09-20 读编译产物 runtime.json 与编译器 detail 推导，修正步骤11⑥的「层号」说法
- **F020** 读 CapabilityMatches 判定逻辑，09-20 记于步骤11⑥b②
- **F021** 09-20 从 WorldbookIdentityCapabilityRules 读出并经探针实测确认
- **F022** 09-20 由 CapabilityMatches 规则推导并用逐 grant 探针验证
- **F023** 09-20 由 score 比较规则 + 63 次 probe 读数互证
- **F025** 09-20 tools/_mil_gate_probe_20260920.py 两模式对照实测
- **F026** 09-20 逐 grant 可达性探针跑 --pure 时观察到的分支切换
- **F028** 09-20 逐 grant 探针里按裸名造查询并命中，反推 LastSegment 匹配
- **F048** 09-19 变异法三角定位：往断言与表达塞「地质」均命中，往 summary/title/aliases 塞不命中
- **F059** 步骤10 注册表三哈希同步一节记录的关系与报错码
- **F060** 读 src/WorldbookPackageIntegrity.cs 的哈希比对实现
- **F061** 读部署脚本头注并据 09-17 上线流程确认
- **F065** 09-17 事故复盘：本轮移走 43 个 geo1-* 产物目录 + 4724 个 reports.*；清点见 tools/_inventory_quarantine_20260917.py
- **F066** 09-17 事故复盘：recovery 二次隔离，标 failure_code=WB-AUTHORITY-RECOVERY-409
- **F067** 09-17 复盘确认隔离只动 compiled/ 下位置，上线件未受影响
- **F072** 坑清单「head 账本里的旧 id 残影」一条：selection 按磁盘现役 id 生成
- **F091** 步骤8·联系机制一节记录的检索实现
- **F094** 步骤8·同名聚落与检索权重一节记录的旧 FindCandidates() 行为
- **F095** 读 src/WorldKnowledgeQueryService.cs 的 FindCandidates() MatchQuality 分档
- **F101** 步骤5：读诊断/save merge/new-document/raw import/validate/compile/preview 全走它；09-14 自建的 _classification-registry.v1.json 已删
- **F105** 步骤5·文件名前缀不是分类轴一节记录的既定设计
- **F117** 步骤2·硬约束 与步骤8·编译前结构自检 两处都断 min_detail==layer
- **F120** 步骤2·硬约束 记录的 slug 取法
- **F121** 步骤6·交叉引文 记录的实际撞 hash 现象
- **F16** 四段链路 grep 到 Studio 编译器与运行时读取点，并在内容实测中命中；另含 CapabilityMatches 判据
- **F26** 09-14 读模拟器 PersonaDialogueSim.cs 的替身赋值点；同处还写 Role = definition.Role
- **F42** 09-16 知识门实测：沿 AllowsAi 属性与 ShouldCallAi 调用点一路跟到数据出口
- **F52** 09-17 读方法体，逐行找那个形容词落在哪一行；无任何距离/位置筛选
- **F58** 09-17 实跑对照：变的恰好是条目 grants 里点名的 3 个身份 → partial，另 4 个一行没动
- **F59** 09-17 identity-gate 实跑矩阵中逐条读返回条目内容
- **f02** 读 assemble_worldbook_package.ps1:199-205，$expectedFiles 与 $assembledFiles 做全等比较后抛错
- **f05** 同 :126-134，只取 runtime.entries[].id 与 runtime.indexes 的两张表做规范化比较
- **f06** src/WorldbookPackageIntegrity.cs:98-113 ComputeContentHash 把 runtime+index 按路径排序后 HashCanonical
- **f07** 读 src/WorldbookPackageIntegrity.cs:115-119，manifestBytes.Concat(contentBytes) 后 SHA256
- **f08** 读 tools/worldbook-studio/tests/Awake.WorldbookStudio.Tests/Program.cs:144 用例名原文
- **f11** 读 ValidationServices.cs:79 注释原文「报 warning 会把既有诊断金标打红（a3-2-content-graph-golden 按 code/path/order 逐条比对）」
- **f12** 读 RuntimePackageCompiler.cs:169-183，outgoing.Count > 0 才建 linkArray 并赋值
- **f18** 报告 :117 结论；WorldKnowledgeQueryService.cs:452-476 的 LinkExpand 逻辑按候选表取种子
- **f23** src/WorldKnowledgeLoader.cs:92 注释「召回那侧对空 Links 的处理就是『不扩』」＋报告结论
- **f27** RuntimePackageCompiler.cs:140-150 对 entity_ids 数组直接 Select(CanonicalEntityRef)，无异常吞掉

---

# 字节布局（byte-layout）

> 共 17 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F024 | R2 判据：纯 ASCII（0x20..0x7E）且含下划线的键不进索引。 | 09-17 |  | worldbook-retrieval-redtest §5c 表·R2 |
| F019 | 编译产物 runtime.json 里每条表达都带 detail 键 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥b① |
| F034 | quote_hash = SHA-256(quote 的 UTF-8 字节).hexdigest().upper()，无换行、无规范化 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·从现役档派生试点 |
| F044 | 来源文件格式为 <sid> => <原文>，UTF-8 无 BOM、LF；每条 quote 必须能在该文件里逐字定位 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·WB-SOURCE-001 口径 |
| F046 | 编译包 JSON 用 \uXXXX 转义，grep 或裸读匹配不到中文，判包内文本必须 json.load 后再搜 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·编译包 JSON 转义 |
| F047 | 上线包 entries[] 只有 id/domain/title/summary/keywords/expressions/extensions，assertions[].text 不进包 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·硬约束4 |
| F057 | 改名要同步四处：文件名、doc.<domain>.<slug>、assertion.<slug>-*、expr.<slug>-* | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤9·正确做法 |
| F058 | assertion·expr 保留历史单数起始形态（如 assertion.castle-ab-comer-castle-1），09-14 定案只改文件名与 doc id 首段两层 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·统一用复数范围 |
| F104 | 文件名前缀＝doc_id 第 3 段首个 - 前 token，只是命名取词，不是分类层 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤5·名录 17 列 |
| F107 | 品类前缀分配：物品 IT-／武器甲 IW-／城镇 IS-／村庄 IV-／战争 W-／paravenos I- | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤4·矩阵探针 |
| F112 | tail -c 按字节截断会把 UTF-8 多字节字符切出假乱码 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·tail -c 假乱码 |
| F115 | 来源骨架字段为 source_id/source_version/source_content_hash/locator/quote_hash/quote | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤2·骨架固定 |
| F116 | 文档结构为 schema_version/id/title/aliases/summary/assertions/expressions | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤2·骨架固定 |
| F124 | 兵种装备取数 XML：spnpccharacters.xml 的 <NPCCharacter> → <Equipments> → <EquipmentRoster> → <equipment> | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·兵种装备取数 |
| F125 | 武器部件三处落点：items/weapons.xml、Native/crafting_pieces.xml、Native/crafting_templates.xml | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·武器部件 |
| F09 | authoring 投影 YAML 是 CRLF 行尾，(?m)^field:$ 这类正则在其中匹配不到字段行 | 未标版本 |  | worldbook-field-liveness-audit / L49 内容覆盖率实测 |
| F24 | .NET 程序集字符串常量在 #US 堆里是 UTF-16LE，用 grep -a 搜 Awake.dll 会全 MISSING（连自己刚构建的那份也 MISSING） | 未标版本 |  | worldbook-field-liveness-audit / L101 |

## 证据

- **F024** 读 WorldbookKeywordIndex.IsExcluded 的 R2 分支：ASCII 字节区间判定＋下划线判定。
- **F019** 09-20 直接读 runtime.json 每条表达对象确认含 detail 字段
- **F034** 09-19 用现役档已知 quote/hash 反推校验算法确认
- **F044** 坑清单 WB-SOURCE-001 口径一节记录的文件格式约定
- **F046** 坑清单记录：直接读包 JSON 搜中文 0 命中，json.load 后可搜到
- **F047** 步骤11 硬约束4 记录：改断言玩家看不见，必须同时改 expressions 并重编译
- **F057** 步骤9 正确做法一节列出的四处结构位置
- **F058** 坑清单「统一用复数的范围要问清」记录的 09-14 定案
- **F104** 步骤5·名录 17 列一节对「文件名前缀」的定义与实测
- **F107** 步骤4·矩阵探针一节记录的前缀按品类领新表
- **F112** 坑清单记录：字节级 UTF-8 校验应改用 python open(...,'rb').read().decode('utf-8')
- **F115** 步骤2·骨架固定 与步骤6·整改 两处记录；quote_hash 行在 quote 行上方
- **F116** 步骤2·骨架固定 记录的 doc 结构
- **F124** 坑清单·兵种装备取数：SandBoxCore/ModuleData/spnpccharacters.xml；slot 名 Item0/Item1/Item2/Head/Body/Gloves/Leg/Horse/HorseHarness；技能在同一块 <skills>
- **F125** 坑清单·武器部件：weapons.xml 里 <CraftedItem> + <Piece Type>；部件本体 <Weapon> 属性在 crafting_pieces.xml；模板在 crafting_templates.xml
- **F09** 本次内容覆盖率实测时直接读文件行尾；改行首尾 strip 的逐行状态机后才统计成功
- **F24** 09-14 实测：同一字符串分别按 utf-16-le 与 ascii 编码在 dll 字节里查找

---

# 数据库结构（db-schema）

> 共 11 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F079 | bannerlord_settlements 含 settlementType/culture/name/descriptionText/boundSettlement/villageType | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤1·取数 |
| F080 | name/descriptionText 是 {=token} 形式，token 可与 id 不同名，必须从字段本身解析 token 再查 CNs，不能按 id 猜 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤1·取数 |
| F081 | bannerlord_items 表的列名是 entityId（不是 itemId） | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·兵种装备取数 |
| F086 | 查城堡下辖村的 SQL 必须补 Settlement. 前缀：boundSettlement='Settlement.'+castleSid 才 join 得上 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·下辖村 |
| F092 | awake.worldbook.authoring.v1.schema.json 是 additionalProperties: false，禁加自定义字段 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤8·硬约束 |
| F093 | entity_ids 只收小写，正则 ^entity\.[a-z0-9]+(?:[._-][a-z0-9]+)*$ | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤8·硬约束 |
| F100 | schema $defs.subdomain 是 additionalProperties:false，只许 id/label/help/examples，禁 conflict_hints | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤5·改 taxonomy 硬约束三条 |
| f34 | settlements.xml 的 Settlement 元素属性为 id/name/owner/posX/posY/culture/text/type/gate_* | v1.4.8 |  | worldbook-anchor-verification §一-第2步 |
| f53 | 边表每条边固定 12 个字段，含 from/to/viaName/bucket/mutual/direction/usableAs | 未标版本 |  | worldbook-should-link-judgement §四-输出契约 |
| f54 | bucket 只有 proper/hubproper/star 三种取值；usableAs 只有 ["forward"] 或 ["forward","backward"] | 未标版本 |  | worldbook-should-link-judgement §四-输出契约 |
| f56 | v3 边表的 to 值全部是 awake:entry: 前缀的编译产物条目 id | 未标版本 |  | worldbook-should-link-judgement §一-1 |

## 证据

- **F079** 步骤1 取数：同节另有 localization_entries（language='CNs'）与 bannerlord_items 表
- **F080** 步骤1 取数一节的实测口径
- **F081** 坑清单·兵种装备取数一节记录的表列名实测
- **F086** 步骤8·下辖村一节：不加前缀 join 不到任何村
- **F092** 步骤8·硬约束：读 schema 得 additionalProperties: false，parent_settlement 之类会被拒
- **F093** 步骤8·硬约束抄录的 schema 正则；示例 entity.settlement.castle_b3，id 允许 doc.geography.castle-*
- **F100** 09-14 读 schema $defs.subdomain 定义
- **f34** python 正则抽取 SandBox/ModuleData/settlements.xml 全部 Settlement 开标签属性名去重：culture/gate_posX/gate_posY/gate_rotation/id/name/owner/posX/posY/text/type
- **f53** 读 should-link.v3.json 的 edges[0] 键列表：from/to/viaName/strength/df/bucket/evidence/mutual/direction/usableAs/fromSubdomain/toSubdomain
- **f54** v3 counts.byBucket 与 edges 全表扫描；边样例 usableAs=["forward","backward"]
- **f56** 边样例 from/to 均为 awake:entry:*；技能 §一-1 说明词条取自 runtime.json 的 awake:entry: 对象

---

# 实测数值（measured-number）

> 共 130 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F009 | 泛词的过匹配面会随模糊通道变大：挂上语义臂后单字「货」由 not_found 变成命中 items-mule。 | 09-17 当前包 |  | worldbook-retrieval-redtest §2 过匹配组⚠️ |
| F011 | 干净重编后探针 match_mode 应出现 hybrid/semantic/keyword/identity/blank 等多种值。 | 09-17 仪器 |  | worldbook-retrieval-redtest §3 自检 |
| F016 | 反向规则 kw.lower().contains(token.lower()) 下，内部 id 分片 doc 命中 100% 条目、geography 命中 89%。 | 09-16 当前包 |  | worldbook-retrieval-redtest §5 |
| F023 | R1 实测剔 3 个词：村庄 272、城堡 67、城镇 49，合 388 个槽位。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5c 表·R1 实测列 |
| F025 | R2 实测剔 400 个词（如 castle_village_EN1_2、heavy_round_shield），合 400 个槽位。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5c 表·R2 实测列 |
| F027 | 当前包 R3 实测剔 0 个，grep -c '"doc.' runtime.json 也是 0，编译器 K1 已生效。 | 09-17 当前包 | 是 | worldbook-retrieval-redtest §5c ⚠️R3 实测纠偏 |
| F029 | 当前包入索引 1006 个词，全部 1409 个。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5c 括号实测 |
| F030 | 能进索引的槽位 1328 / 2116，788 个槽位（37%）是死重。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5c 括号实测 |
| F034 | 剔前/剔后实测：doc 448→0、geography 408→0、castle_village 131→0、economy 21→0、村 273→1。 | 09-17（含旧包对照） |  | worldbook-retrieval-redtest §5c 剔前/剔后实测 |
| F036 | 关键词覆盖分布：覆盖 1–2 条的占绝大多数，覆盖 >40 条的极少数才是过匹配祸首。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5d |
| F039 | 半句「…承 IMPL §3.1 留痕：economy 档承载一条政治沿革」就能让 economy 翻出 geography.mines-lycaron。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5e |
| F041 | 全库有 9 档摘要带「两说并录」这类结构语。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5e ★★收尾那句 |
| F042 | 9 档里有 8 档说的是「世界里的多种说法」（towns-charas、tales-lakonis-red-water 等），属正当既有写法。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5e ★★收尾那句 |
| F046 | 泛问词「村庄」的 272 条村庄档全被答成同一个村 geography.villages-deriat，state=partial。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5f 症状 |
| F050 | 在 Python 里逐字复刻三规则建索引得到 1006 个词，与真件侧独立数出的 1006 一致（同源）。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5f 取证方法 1 |
| F051 | 复刻候选数与真探针自报 literal_keyword_hits 对表：三个泛词分别 1/0/0 全部吻合。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5f 取证方法 3 |
| F052 | 反事实停掉 R1 再穷举：村庄捞回 273、城堡 67、城镇 49，证明 R1 确实在干活。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5f 取证方法 4 |
| F053 | R1 改按子串算覆盖度的三种继承口径都会连坐正当入口：段继承多剔 67（全是 X Castle）、题面继承 79、关键词继承 100。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5f 修法方向① |
| F056 | 泛问词表共 9 个：村庄/村子/村落/城堡/城砦/堡垒/城镇/镇子/城市，是编辑判断不是统计量。 | 09-17 起 |  | worldbook-retrieval-redtest §5f 修法方向② 表 |
| F057 | WrapsGenericCategoryWord 在真语料 0 条命中，是一条空转的防复发闸。 | 09-17 当前包 | 是 | worldbook-retrieval-redtest §5f 现状 |
| F058 | KEYWORDGUARD 变异检验直接调真代码，结果 3 剔 / 7 留。 | 09-17 |  | worldbook-retrieval-redtest §5f 必须做变异检验 |
| F060 | 数据侧配套：389 条聚落档各去掉一个裸类别词，原值逐字留痕于 corrections_20260917/REMOVED-BARE-CATEGORY-KEYWORDS-20260917.md。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5f 数据侧配套 |
| F063 | 回归实测：「有大瀑布的村子是哪个？」v12 主路零命中走兜底得 4 条含正确答案，v13 别名进 keywords 后只剩 1 条无信息量命中。 | v12→v13 当前包 |  | worldbook-retrieval-redtest §5g 回归① |
| F065 | v13e 兜底 hits 由 1 恢复到 4，但正确答案掉到第 4 位：概念词条综述的「的村/村子」与查询共享 2 个，与正确答案打平。 | v13e 当前包 |  | worldbook-retrieval-redtest §5g 回归② |
| F067 | 统计口径「语料覆盖 > 40」只捞到城堡（覆盖 69），村庄 12、城镇 14 够不着。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5g 统计口径表 |
| F068 | 统计口径「长度 2~3 字的 title」命中 148 个，会把沙拉斯/吕卡隆/毛皮等真名算进来并剔掉 45 条正当关键词。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5g 统计口径表 |
| F069 | 统计口径「共享 term 罕见度（df）」反向失效：村子 df=4 比大瀑 df=6 更罕见，概念词条反而升到第 1。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5g 统计口径表 |
| F070 | 残留：「西米拉堡是谁的城堡？」概念词条仍占第 3 位（经主路关键词命中），不进前 3 就不影响门禁。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5g 残留 |
| F073 | 阳性对照：title 必须能自命中，448/448 才算仪器可用。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5h 阳性对照 |
| F077 | 中文入口词自命中率 63.7%，挂上语义腿后 64.5%，只补回 8 个。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5h 两档都要跑 |
| F078 | 覆盖度测试：专有入口多为 1 条；通用入口 388 > 40 全被剔。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5h 两类入口表 |
| F079 | 自命中普查三数：条目名打不中自己 0 条；一个入口词都不自命中的条目 0 条；只属于自己却打不中自己的词 0 个 ⇒ 缺口全是泛词。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5h 量完要看的三个数 |
| F080 | 含下划线的原始 id 词 R2 应全剔、自命中应≈0，实测 4/400，全部是假命中。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5h ⚠️ASCII 侧 |
| F084 | 探针 hits 有显示上限：空输入显示 15，实际候选 143+。 | 09-17 仪器 |  | worldbook-retrieval-redtest §6 边界核查 |
| F085 | 09-16 归一化+拼音+编辑距离把漏报 19→2，但过匹配 11→12（更差）。 | 09-16 当前包 |  | worldbook-retrieval-redtest §7 |
| F086 | 同批实测：头盔 2→4、helmet 2→10。 | 09-16 当前包 |  | worldbook-retrieval-redtest §7 |
| F087 | 09-17 语义臂：MISS-RISK 红 12→4（收益 8 条），过匹配红 20→21（代价 +1 条）。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7 09-17 实例 |
| F089 | 单字 token 是结构性的：盔／帽／铁 在归一化／拼音／编辑距离下数字完全不变，只能靠最小长度门槛。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7 三个具体的坑 |
| F091 | 「term 数 ≥ 3 就要求共享 2 个」这刀让 RETRIEVAL_GATE FAIL，B 组 hit@3 从 9/11 掉到 7/11。 | 09-17 |  | worldbook-retrieval-redtest §7c |
| F092 | A/B 隔离：把常量临时改成 int.MaxValue 再跑 ⇒ 代价 100% 来自这一刀，另一处改动（西文不切 2-gram）代价为 0。 | 09-17 |  | worldbook-retrieval-redtest §7c A/B 隔离法 |
| F093 | 收紧判据改按查询形状 LooksLikeSingleShortToken(text, 6)（单段且 ≤6 字）后门禁逐字回绿：A=10/13 B=9/11 ALL=19/24。 | 09-17 |  | worldbook-retrieval-redtest §7c 改对了的判据 |
| F095 | 09-16 版脚本里的 TRAD 表只有 12 个字，是照着样本选的（对样本过拟合）。 | 09-16 |  | worldbook-retrieval-redtest §7d 不造表 |
| F101 | RagSemantic.cs:44 有 private const double MinimumScore = 0.45，低于它的候选被 continue 丢弃。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §7e ① |
| F105 | 「领主」走语义臂（字面臂一无所获），top1 余弦 0.4505，只比门槛 0.45 高 0.0005。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7e ③ 表 |
| F106 | 「这边的人怎么样」两臂都有命中，语义臂 top1 余弦 0.4732，落在真问题命中区间里面。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7e ③ 表 |
| F107 | 「附近有什么好东西」走兜底通道（语义臂没参与），top1 余弦 0.4422 没过 0.45。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7e ③ 表 |
| F109 | 完全无关的话（今天午饭吃什么／帮我写一段排序代码／火车时刻表在哪儿查）top1 余弦 0.3067–0.3935。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7e 余弦分布表 |
| F110 | 真问题命中前 3 的目标余弦为 0.4708–0.7951。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7e 余弦分布表 |
| F111 | 要挡住「这边的人怎么样」得把门槛抬过 0.4732，而真问题命中下界是 0.4708 ⇒ 一刀就砍到真题。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7e ⇒ 措辞 |
| F112 | 门槛抬到 0.60 才不漏探针，真题只剩 12/26。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7e ⇒ 措辞 |
| F118 | detail 档独有正文「毛皮基准价 400」。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7b ② |
| F119 | secret 档独有正文「操办科尔坦家的图谋」。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7b ② |
| F120 | 毛皮基准价 400 在 KWK8×profile.commoner 是强对照：commoner 拿到 3 条、没有 detail 正文。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7b ③ |
| F121 | 「操办科尔坦家的图谋」的 4 条对照里 3 条是强对照。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7b ③ |
| F122 | 脏输入下权限档位组 0/8 红、两条档位闸零泄漏。 | 09-17 当前包 |  | worldbook-retrieval-redtest §7b ④ |
| F123 | 复刻检索逻辑前必须跑 V0 基线并与真引擎逐条对齐，本例校准 48/48，V0 RED 也精确等于 30。 | 09-17 当前包 |  | worldbook-retrieval-redtest §8 |
| F128 | 编辑距离／向量这类模糊通道独立于匹配方向，会把「只允许正向匹配」的过匹配治理整个绕过去：doc 由 0 打回 458、war 0→107。 | 09-17 当前包 |  | worldbook-retrieval-redtest 修法优先级表 P2 |
| F129 | 改 keywords 是全库 458 档的事，需先裁决。 | 09-16 当前包 |  | worldbook-retrieval-redtest 环境与纪律 |
| F002 | 把本档 title 再写进 aliases 会产生永远取不到的死条；全库实测 894 条（已清） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤2·硬约束·别名(09-20) |
| F003 | authoring-register-batch 实测 87 档 46 秒，约 0.53 秒/档 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤3·⚡register批量入口 |
| F004 | 同一批逐档起 authoring-register 进程要 23 分钟 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤3·⚡register批量入口 |
| F007 | full-geo1 完整副本上单档冷启动 51.4 秒、热启动 1.79 秒 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤3·旧的归因是错的 |
| F008 | 同一进程连跑 3 档仅 2.54 秒，固定启动成本占绝对主导 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤3·旧的归因是错的 |
| F015 | 21 档 × 村民/商人/贵族 = 63 次真 probe，其中 62 次送出第 1 条断言，第 2 条及以后 0 次 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥实测 |
| F016 | 63 次探针唯一例外是俄尼拉，它正是 21 档里唯一后续断言授予条件与首条不同款的档 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥实测 |
| F024 | 条件权重实测：identity_ids×10／culture×20／kingdom×25／settlement×30／role×15 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥ |
| F027 | 军事批 09-20 读数：修复前 --cap 182/188（6 条不可达），修复后 --cap 183/183、--pure 183/183 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑥b③ |
| F029 | 全库 482 档的 status 全是 needs_review（不是漏填） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·探针与试点 |
| F030 | 全库 kind 分布：fact 544／interpretation 15／relation 8／rumor 7／state 1 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·探针与试点 |
| F031 | 全库带 rumor 的只有 6 档＝5 档 tales-* 加 territories-varcheg-swap | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·探针与试点 |
| F032 | 聚落类 396 档里 rumor 条数为零 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·探针与试点 |
| F033 | 全库 482 档中 32 档原文带存疑标记，其中 settlements 占 21 档，六类标记齐全 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·探针与试点 |
| F036 | tools/_verify_locators_20260919.py 核 21 档 72 条来源的读数：取不到 0、不在该行 0 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑤ |
| F049 | 09-20 逐字节比对抓到 302 档镜像漏同步（villages 267／towns 34／weapons 1），差异全在 entity_ids | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤2·硬约束·双写复验 |
| F050 | castles 67 档两侧的 entity_ids 都有，未受那次漏同步影响 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤2·硬约束·双写复验 |
| F052 | 09-14 批量改名实测 447 档中只有预期差异、3 档误伤（全是 aliases） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤9·三级验证3 |
| F064 | 09-17 用 k1_op_* 通配清理临时文件，一次移走了 52 个历史 op 记录 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·禁通配清理 op 记录 |
| F076 | docs/worldbook-migration/projection/authoring-out/ 下的 .yaml 已入库 646 份（git ls-files 实测） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤10·镜像档状态已变(09-19) |
| F088 | 堡名与首村名 CN 常不一致（castle_S3＝涅维扬斯克堡 vs 村＝涅夫扬斯克），照官方串写不得修正 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·两个官方数据坑 |
| F089 | 67 堡里 66 座与下属村同名（堡名＝村名＋「堡」），唯一例外 castle_S3 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·同名聚落与检索权重 |
| F090 | 67 堡全量联系探针共 135 条 query，09-14 实测 135/135 命中目标条目 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·全量联系探针 |
| F096 | 主分类 domain 只有 5 个且封闭（宪章 §二「不扩域」） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤5·分类体系只有两层 |
| F097 | 二级主题 subdomain 共 43 个＝已用 16＋未用 27（09-14 taxonomy v2 实测） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤5·分类体系只有两层 |
| F102 | 名录 xlsx 共 17 列，前 4 列＝序号 \| 类别 \| 二级主题 \| 文件名前缀 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤5·名录 17 列 |
| F103 | 名录 xlsx 由生成器整表重建 6 张表：名录／分类总览／分类定义／域汇总／交叉覆盖／刷新说明 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤5·名录生成器 |
| F108 | 名录结构常量联动：freeze_panes=A2、auto_filter=A1:Q449、条件格式列 状态=I、title校验=Q | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤5·结构常量 |
| F03 | 旧代 WorldbookService 对 query.HeroId 打分 +1000（:383 与 :503 各一处） | 未标版本 |  | worldbook-field-liveness-audit / L37 |
| F04 | 旧代 WorldbookService 对 query.CharacterId 打分 +900（:384 与 :504 各一处） | 未标版本 |  | worldbook-field-liveness-audit / L38 |
| F05 | 旧代 WorldbookService 对 query.SettlementId 打分 +400（:387 与 :508 各一处） | 未标版本 |  | worldbook-field-liveness-audit / L37 |
| F06 | 旧代 WorldbookService 对 query.SceneKeywords 打分 +200（:363 与 :487 各一处） | 未标版本 |  | worldbook-field-liveness-audit / L38 |
| F07 | 旧代 WorldbookService 对 query.ContextModes 打分 +150（:370 与 :491 各一处） | 未标版本 |  | worldbook-field-liveness-audit / L39 |
| F10 | 12 档内容共 40 条表达中 secret 层 0 条、unknown 层 0 条 | 未标版本 |  | worldbook-field-liveness-audit / 截至 2026-09-12 的结论 |
| F11 | 40 条表达的 denies 字段 40/40 全为空，而 schema 中 denies 是 required | 未标版本 |  | worldbook-field-liveness-audit / L83 |
| F15 | 内容只走通详细度梯的 rumor / summary / detail 三档 | 未标版本 |  | worldbook-field-liveness-audit / L80 |
| F27 | 离线 DSL [CURRENT_IDENTITY] 仅 KINGDOM_ID="lord_5_1"+ROLE="hero"，KINGDOM_NAME/CLAN_NAME/CULTURE_ID 全空 | 未标版本 |  | worldbook-field-liveness-audit / |
| F29 | 76/76 张 persona 卡 status 全为 draft，且源头 tools/persona-workbench/characters/*.persona.json 就是 draft | 未标版本 |  | worldbook-field-liveness-audit / L131 |
| F30 | 仓库有 77 个 persona definition，游戏目录只有 1 个（hero_default.json） | 未标版本 |  | worldbook-field-liveness-audit / L132 |
| F33 | persona 卡引用 39 tags / 4 bundles，而游戏侧注册表只有 8 tags / 1 bundle | 未标版本 |  | worldbook-field-liveness-audit / L133 |
| F34 | 换成游戏侧 8 标签注册表后选卡仍能精确命中，但 DSL 掉到 80B 的 RUNTIME_FALLBACK IDENTITY_ONLY 并带 persona.tag_unregistered:* | 未标版本 |  | worldbook-field-liveness-audit / L133 |
| F46 | 世界书 Summary 字段填充 448/448 全有，中位 51 字 | 未标版本 |  | worldbook-field-liveness-audit / L209 第八类失效 |
| F49 | 条目 keywords 中位 5 个（区间 3–9），且 448/448 都含 doc.* 前缀的内部编号 | 未标版本 |  | worldbook-field-liveness-audit / L220 |
| F53 | CourierMapUnitsPerHour = 8 是专人专送、驿站换马、昼夜兼程的极限速度 | 未标版本 |  | worldbook-field-liveness-audit / |
| F54 | 消息自然传开的速度常数 BaseSpeed = 4（每游戏小时），靠商队/旅人/行军顺路携带，比专送慢一档 | 未标版本 |  | worldbook-field-liveness-audit / L257 |
| F56 | 题集 JSON 里 identity 字段 0 命中；另一条对照查询固定 IdentityId = "awake:identity:commoner" | 未标版本 |  | worldbook-field-liveness-audit / L270 |
| F57 | 检索命中率实测数字为：字面 19／语义 22／合并 23 | 未标版本 |  | worldbook-field-liveness-audit / L271 |
| F61 | 裸字节摘要行尾敏感：本项目 4 个摘要常量里只有 2 个需要重钉（另 2 个源文件本来就是 LF） | 未标版本 |  | worldbook-field-liveness-audit / |
| f13 | 候选包 geo1-v22-links 实测 558 档、445 档带 extensions.links、包内共 1286 条边 | 未标版本 |  | awake-worldbook-new-compile-input §五-读数 |
| f14 | 该包 source-report.json 记 link_edges=1286 与一个 64 位十六进制 link_registry_hash | 未标版本 |  | awake-worldbook-new-compile-input §一-代码形状 3 ＋ 报告 :79 |
| f15 | 现役上线包 ModuleData/Worldbook/packages/calradia/runtime.json 实测 800 档、0 档带 links 边 | 未标版本 |  | worldbook-should-link-judgement §一-1（取词条来源）＋ 本次实测 |
| f16 | 四对照读数：阳性 439/445、阴性① 0、变异检验 0、前缀破坏 0、末尾多出 461 | 未标版本 |  | awake-worldbook-new-compile-input §三-四对照 |
| f17 | 把「问无出边档」当阴性②的探针实测跑出 31 条 FAIL；113 条无出边档里 31 条仍带 +link | 未标版本 |  | awake-worldbook-new-compile-input §三-🚨 |
| f21 | 生产烟测 store-switch-blocks-old-load 偶发约 1/6 抖动：6 次里 5 次 31/0、1 次 30/1 | 未标版本 |  | awake-worldbook-new-compile-input §四-纪律-② |
| f22 | 扩召回常量 LinkExpandPerSeed=2、LinkExpandMaxTotal=3 | 未标版本 |  | awake-worldbook-new-compile-input §二-常量写成命名常量 |
| f24 | 边表重算读数：边 1077→1394、覆盖档 399→483 | 未标版本 |  | awake-worldbook-new-compile-input §五-读数 |
| f25 | 本代新增 76 档里有出边的从 0 升到 61 | 未标版本 | 是 | awake-worldbook-new-compile-input §〇-2 ＋ §五-读数 |
| f31 | 该登记表实测 890 个实体 | 未标版本 |  | worldbook-anchor-verification §一-第1步 |
| f32 | 该登记表 mapping_status 实测 exact_base 828、exact_official_dlc_not_installed 62 | 未标版本 |  | worldbook-anchor-verification §一-第1步 |
| f41 | should-link.v2.json 实测 1077 边；proper 580 / hubproper 398 / star 99；mutual true 382 | 未标版本 |  | worldbook-should-link-judgement §一-6 ＋ 坑① |
| f42 | should-link.v3.json 实测 1394 边；proper 770 / hubproper 516 / star 108；mutual true 416 | 未标版本 |  | worldbook-should-link-judgement §四 ＋ 本次实测 |
| f43 | v3 counts：entries 558、liveEntries 482、newEntries 76、namesTotal 845、namesKept 841、hubsKept 19 | 未标版本 |  | worldbook-should-link-judgement §一 ＋ §四 |
| f57 | v3 counts：distinctPairs 1102、edgesStrong 1227、edgesWeak 167、entriesWithOutgoing 452 | 未标版本 |  | worldbook-should-link-judgement §一 ＋ §四 |
| f44 | DF 参数固化在边表 params：dfMax=12、nameMin=2、nameMax=10 | 未标版本 |  | worldbook-should-link-judgement §一-4 ＋ §三-4 |
| f45 | 「城堡」出现在 67 条正文里，settlement-types-castle 的入度为 67 | 未标版本 |  | worldbook-should-link-judgement §二-坑① |
| f46 | 「拉科尼斯湖」出现在 29 条正文里（df=29）；其条目 geography.lakes-lakonis 入度 41 | 未标版本 |  | worldbook-should-link-judgement §二-坑① |
| f47 | 《拉科尼斯湖·水为何变红》(awake:entry:culture.tales-lakonis-red-water) 入 29 / 出 0 | 未标版本 |  | worldbook-should-link-judgement §二-坑③ |
| f48 | 不区分 DF 高成因会把 398 条真边当噪声丢掉，技能称占应进图边数的 41% | 未标版本 | 是 | worldbook-should-link-judgement §二-坑① |
| f49 | 孤立档读数随口径变化：任何提及边＋归属边 13、专名＋高频专名＋归属边 18、再减概念词条 15、仅专名强边 34 | 未标版本 | 是 | worldbook-should-link-judgement §二-坑② |
| f50 | settlement-hierarchy.v1.json（20260918）实测 counts：villages 273 / towns 53 / castles 67 / edges 274 | 未标版本 |  | worldbook-should-link-judgement §五 |
| f51 | 技能称交叉验证「290 对里 161 对（56%）被正文互引覆盖」，但现行 hierarchy 文件只有 274 条边，分母对不上 | 未标版本 | 是 | worldbook-should-link-judgement §五 |
| f52 | v1 时代同一批边按「有向对」数 vs 按「边」数会差出 78 条 | 未标版本 | 是 | worldbook-should-link-judgement §四-纪律2 |

## 证据

- **F009** 同一批过匹配样本跑两档（不挂／挂 AWAKE_SIM_SEMANTIC=1）实测。
- **F011** dotnet run -c Release 重编后读探针输出的 match_mode 取值集合。
- **F016** 按反向规则在整包 keywords 上统计单 token 命中条目数。
- **F023** _verify_index_hygiene_20260917.py 在 09-17 晚当前包上打印的逐规则剔除数。
- **F025** _verify_index_hygiene_20260917.py 在 09-17 当前包上打印的 R2 剔除数。
- **F027** _verify_index_hygiene_20260917.py 与 grep -c 双重读数，均为 0。
- **F029** _verify_index_hygiene_20260917.py 在 09-17 当前包上数出的词数。
- **F030** 同批实测：按槽位（词×条目）计数得 1328/2116。
- **F034** 剔除规则上线前后对同一批单 token 统计命中条目数。
- **F036** _keyword_surface_survey_20260917.py 的覆盖区间直方图与 top20。
- **F039** 把该半句写进 summary 后实测查询 economy 的命中条目。
- **F041** 对上线包全部 entry 的 summary 做普查计数。
- **F042** 逐档判据「两说是世界里的还是文件里的」，8 档归世界侧。
- **F046** 用「村庄」跑探针，读 hits 与 state 字段。
- **F050** _replicate_kw_index_20260917.py 打印词数与真件独立计数对比。
- **F051** 把复刻候选数与被测真件的 literal_keyword_hits 逐词对表。
- **F052** 复刻件里关掉 R1 规则后重新穷举候选。
- **F053** 09-17 对三种继承口径各跑一次剔除预览计数。
- **F056** 读 WorldKnowledgeLoader.cs 内的显式表并清点条目数。
- **F057** A 项已把唯一那条「德里亚特·村庄」从数据清掉后复测，命中 0。
- **F058** 验台 KEYWORDGUARD 行直接调用真代码跑变异样本。
- **F060** 对聚落档批量清理并生成留痕文件，计数 389 条。
- **F063** 同一句在 v12／v13 两个包上跑探针比对 hits。
- **F065** 复算兜底排序 + 验台复跑，读 hits 顺序。
- **F067** _probe_b_generic_words_20260917.py 四种口径并排量的第一行。
- **F068** 同脚本量 2~3 字 title 数量与误剔关键词数。
- **F069** 同脚本按 df 排序复算，概念词条位次不降反升。
- **F070** 对含类别词的问句跑探针读 hits 位次。
- **F073** _judge_entry_fit_20260917.py 对全库 448 条跑 title 自命中。
- **F077** 两档跑同一批自命中 spec，比对命中率与绝对增量。
- **F078** _probe_entry_fit_data_20260917.py 对两类入口分别统计覆盖度。
- **F079** _judge_entry_fit_20260917.py 与 _probe_entry_fit_gap_20260917.py 的汇总读数。
- **F080** _show_underscore_selfhit_20260917.py 与 _dump_underscore_entries_20260917.py 逐条溯源。
- **F084** 空串输入跑探针，对比显示条数与真实候选数。
- **F085** 同批样本加该层增强前后各跑一遍红测读数。
- **F086** 同上增强前后对这两个 token 的单点读数。
- **F087** 同一批样本跑不挂／挂 AWAKE_SIM_SEMANTIC=1 两档对比。
- **F089** 同一批样本加三种模糊通道后逐 token 对比命中数。
- **F091** FallbackMinTermsForTwoShared 上线后跑门禁读数。
- **F092** 单变量隔离实验：只回退一个常量，比对门禁读数变化。
- **F093** 改判据后跑 RETRIEVAL_GATE 得到的三组读数。
- **F095** 读旧脚本 TRAD 字典并清点条目数 12。
- **F101** 读 framework/MarcusAwakeStorage/src/SqliteStorageAndRagBackend.RagSemantic.cs 第 44 行与第 177 行附近的搜索循环。
- **F105** tools/_noanswer_arm_attribution_20260917.txt 的通路归因读数。
- **F106** 同批归因文件与余弦分布表对照。
- **F107** 同批归因文件：字面两腿归零后落进 FallbackMinSharedTerms=1 的兜底。
- **F109** 对该组输入跑语义腿取 top1 余弦，得区间。
- **F110** 对探针真题集取命中前 3 的余弦，得区间。
- **F111** 两个区间端点重叠，做门槛扫描后确认无可用分界点。
- **F112** tools/_semantic_floor_20260917.json 的 rows 门槛扫描结果。
- **F118** 读条目各档正文，确认该串只在 detail 档出现。
- **F119** 读条目各档正文，确认该串只在 secret 档出现。
- **F120** 对同一条目用低档身份跑探针，数命中条数与正文层。
- **F121** 逐条判「拿到条目但拿不到该档正文」（强）还是「什么都没拿到」（弱）。
- **F122** 09-17 权限档位组跑红测的逐条读数。
- **F123** 首轮复刻漏了大小写折叠与空串行为，V0 校准立刻抓出偏差。
- **F128** 加模糊通道前后对 doc／war 两个 token 的命中数读数。
- **F129** 对上线包统计条目总数得 458 档。
- **F002** 09-20 按 F001 的去重顺序全库扫描 aliases，逐档计数得 894 条死条
- **F003** 09-15 在 full-geo1 工作区对 87 档 manifest 实跑批量入口并计时
- **F004** 09-15 对同一 87 档逐档起进程实跑计时，得 23 分钟
- **F007** 09-15 在 full-geo1 完整副本上分别测冷/热启动单档 register
- **F008** 09-15 同副本同进程内连跑 3 档计时 2.54 秒，与冷启动 51.4 秒对照
- **F015** 09-19 跑 63 次真 dotnet run -- probe，按返回正文比对实际送出表达
- **F016** 09-19 同款核工具 tools/_v18_grant_tie_check_20260919.py 读数
- **F024** 读 WorldbookIdentityEvaluator.cs:60-94 的权重常量表
- **F027** tools/_mil_gate_probe_20260920.py 两模式跑军事批，读数见 MILITARY-BATCH-REPORT-20260920.md §三
- **F029** tools/_probe_status_kind_20260919.py 全库读数
- **F030** tools/_probe_status_kind_20260919.py 全库逐档统计 assertions[].kind
- **F031** tools/_probe_status_kind_20260919.py 按档列出含 rumor 的档名
- **F032** tools/_probe_status_kind_20260919.py 按 domain 分组的 rumor 计数
- **F033** tools/_probe_doubt_marks_20260919.py 扫全库来源原文的 据传说/据说/听说/名声/宣传说法/说法对不上
- **F036** 09-19 该工具带两处阳性对照实跑，输出取不到 0、不在该行 0
- **F049** 09-20 对 workspace 与 projection/authoring-out 两侧做 diff -rq 逐字节比对
- **F050** 09-20 同一轮逐字节比对中 castles 目录两侧一致
- **F052** 09-14 用 document-revisions 账本 content 做逐行 diff，按预期/误伤分类
- **F064** docs/worldbook-migration/corrections_20260917/INCIDENT-OPRECORD-WILDCARD-20260917.md
- **F076** 09-19 对 projection/authoring-out 跑 git ls-files 计数得 646
- **F088** 步骤8·两个官方数据坑一节的 DB 实测个例
- **F089** 09-14 全量比对 67 堡名与下辖村名，得 66 同名
- **F090** tools/_castle_link_probe_<date>.py 每堡 2 条 + 1 条对照跑 sim 后逐行判 hits
- **F096** 09-14 按原设计纠正 taxonomy：domain 共 5 个封闭
- **F097** 09-14 taxonomy v2 全量统计已用/未用子域数
- **F102** 步骤5 记录的名录列结构（_gen_name_index_20260913.py 生成）
- **F103** 步骤5 记录：_gen_name_index_20260913.py 原地覆盖 WORLDBOOK-NAME-INDEX.xlsx 的 6 表
- **F108** 步骤5 记录的结构常量（列序变动会联动这些值）
- **F03** 读 src/WorldbookService.cs:383、:503 打分表达式
- **F04** 读 src/WorldbookService.cs:384、:504 打分表达式
- **F05** 读 src/WorldbookService.cs:387、:508 打分表达式
- **F06** 读 src/WorldbookService.cs:363、:487 打分表达式
- **F07** 读 src/WorldbookService.cs:370、:491 打分表达式
- **F10** 对 12 档 authoring YAML 跑逐行状态机统计 (layer, profile, scope, denies)，排除 demo.yaml
- **F11** 逐行状态机统计 denies 为空 vs 填充；schema 侧读 awake.worldbook.authoring.v1.schema.json 的 required 列表
- **F15** 12 档 authoring YAML 逐行状态机统计 detail 档取值分布
- **F27** 09-14 跑离线出图，读 DSL 文本中 [CURRENT_IDENTITY] 段实际取值
- **F29** 09-14 统计 status 分布；materialize-definitions.ps1:106 只是原样复制，非物化写坏
- **F30** 09-14 两侧目录文件数对比（源目录 vs 目标目录）
- **F33** 09-14 两侧注册表集合差集统计
- **F34** 09-14 实际装载对照实验：换注册表后读 DSL 长度与标记
- **F46** 09-16 对 448 条条目统计 Summary 填充率与字数中位
- **F49** 09-16 统计 448 条 keywords 个数分布与内容构成
- **F53** 09-17 甲方追问现场核对该常数语义与适用场景
- **F54** 09-17 对照两个常数的语义：信使 8 vs 顺路 4，并注明游戏小时
- **F56** 09-17 统计题集 JSON 的 identity 出现次数并读对照查询的固定身份
- **F57** 09-17 跑 worldbook-runtime-sim 检索臂得到的命中数
- **F61** 09-14 用 git ls-files --eol 看索引/工作树两列，逐个算规范化前后哈希
- **f13** python 计数 workspace/full-geo1/compiled/geo1-v22-links/runtime.json：entries 558、withlinks 445、edges 1286
- **f14** 读 geo1-v22-links/source-report.json 的 link_edges=1286；link_registry_hash=3B249A4EDD615E4BA590AE0EB8FAC1D9AFAFCD3BE5197C978B93C5C9EBD698FD
- **f15** python 计数该文件：entries 800、withlinks 0、edges 0；extensions 键仅 sourceDocumentId/subdomain(800)、entityRefs(502)
- **f16** 读 docs/worldbook-migration/EDGE-RECALL-REPORT-20260920.md:105-115 的 A/B/C/D 表
- **f17** 报告 :117-118 留痕；扩召回种子是候选表而非被问档，故候选里有边条目照样扩
- **f21** 报告 §七-② :137 与 :151 列出六次读数 31/0、30/1、31/0、31/0、31/0、31/0，未深挖定位
- **f22** 读 src/WorldKnowledgeQueryService.cs:461-462 常量声明
- **f24** should-link.v2.json 实测 1077 边；v3.json 实测 1394 边；python 算 v3 两端并集覆盖 483 档
- **f25** 报告 :13/:41 表；v3 counts.newEntries=76 已复算，61 与旧表的 0 未独立复算
- **f31** python 读 entity-registry.v1.json，len(entities)=890
- **f32** python 计数 mapping_status 字段分布
- **f41** python 计数 docs/mappings/worldbook-should-link/20260918/should-link.v2.json
- **f42** python 计数 docs/mappings/worldbook-should-link/20260920/should-link.v3.json
- **f43** 读 should-link.v3.json 顶层 counts 字段原文（同字段还含 namesDroppedAsGeneric 4）
- **f57** 读 should-link.v3.json 顶层 counts 字段原文（同字段还含 entriesWithIncoming 338）
- **f44** 读 should-link.v3.json 的 params 字段
- **f45** v3 边表 viaName df=67；对 to 字段计数得 settlement-types-castle 入度 67
- **f46** v3 边表 viaName df=29；indeg(awake:entry:geography.lakes-lakonis)=41
- **f47** 对 v3 边表 from/to 分别计数得该 id 出度 0、入度 29
- **f48** v2 hubproper=398 已复算；41% 的分母来自技能正文，本次未复算
- **f49** 技能 §二-坑② 表；语料已从 482 档变为 558 档，本次未复算这四个口径
- **f50** python 读 docs/mappings/worldbook-settlement-hierarchy/20260918/settlement-hierarchy.v1.json
- **f51** 实测该文件 edges=274 与技能 290 不符；161 与 56% 未复算
- **f52** 技能 §四-纪律2 记载的历史口径分歧，现无法复现（v1 文件仍存 should-link.v1.json）

---

# 静默失败（silent-failure）

> 共 34 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F010 | 直接调 bin/Release 的旧 exe 跑探针会让两档塌成同一个数，并凭空造出 13 条「变坏」。 | 09-17 |  | worldbook-retrieval-redtest §3 🚨 |
| F014 | 序号解析写成 KW([A-Z]\d+) 只得 A1 而期望是 KWA1，69 条全部落进「没跑到」，两档都报 69/69 红。 | 09-17 |  | worldbook-retrieval-redtest §4 解析序号 |
| F015 | 判定出现 69/69 红、红→绿 0、绿→红 0 这个形状本身就是仪器故障警报，不是产品结论。 | 09-17 |  | worldbook-retrieval-redtest §4b |
| F044 | 第一版筛子只查 IMPL／§／留痕／档承载／TODO／半角文件名，抓不到「D 级裁定（Max 09-12，登记于研究稿）」这种形状。 | 09-17 当前包 |  | worldbook-retrieval-redtest §5e 🚨筛子窄 |
| F059 | 第一次跑 KEYWORDGUARD 报 4 个反例，全部是造样本时的笔误。 | 09-17 |  | worldbook-retrieval-redtest §5f 必须做变异检验 |
| F061 | 曾据「keywords 里有 272 条带裸村庄」断言 R1 没拦住村庄，实际 R1 拦住了；覆盖数必须读真索引，不能从数据侧反推。 | 09-17 |  | worldbook-retrieval-redtest §5f ⚠️顺带纠错 |
| F082 | FindCandidates:208 对空文本返回全表，会答出一条无关知识。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §6 边界核查 |
| F088 | 归一化会造出新的空串：？？？／。。。归一化后为空 ⇒ 命中全库。 | 09-17 |  | worldbook-retrieval-redtest §7 三个具体的坑 |
| F117 | 探针里手抄的「身份→scope/detail」平表把 profile.noble 封在 detail ⇒ 全包唯一一条 secret 档正文永远取不到，那根闸整根空转却一直没报红。 | 09-17 |  | worldbook-retrieval-redtest §7b ① |
| F009 | op id 若用列表序号，断点续跑时序号漂移会撞既有 operation 被幂等短路成假成功（返回旧记录，新档未登记） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤3·op id 规则 |
| F035 | validate 只比 quote_hash↔quote 与 source_content_hash↔文件，查不出 locator 写错，能全绿放过张冠李戴的引文 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤11·⑤ |
| F039 | WB-AUTHORITY-DOCUMENT-422（文档缺少稳定 id）真实原因常是 YAML 解析失败：SafeYamlLoader 失败时 return new JsonObject() | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤9·CLI 参数事实 |
| F051 | 只改现役档不写镜像会造成「镜像缺字段」的静默缺口，只比 title/aliases 看不见，必须逐字节比 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤2·硬约束·双写复验 |
| F055 | YAML 值里出现 ': ' 会被解析成映射（合法 YAML 非字符串），报错由 schema 而非 YAML 解析抛出 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤9·坑2 |
| F068 | WB-AUTHORITY-MUTATION-UNKNOWN（503, side_effect=unknown）是兜底包装码，真因写在 stderr（Program.cs:212） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·MUTATION-UNKNOWN |
| F069 | Python subprocess.run(..., text=True) 调 CLI 时，Windows 下按 GBK 解码会静默丢掉 stderr，只剩一个 503 | 未标版本（Windows） |  | worldbook-encyclopedia-rollout-batch 坑清单·MUTATION-UNKNOWN |
| F070 | WB-AUTHORITY-CAS-409 第二成因：改档内容字段却没动 revision，register 按 {file_revision}.json 同名覆盖 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·CAS-409 第二种成因 |
| F087 | 官方数据坑：个别村的官方 CNs 错挂他文（castle_village_A7_1 乌格巴） | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·两个官方数据坑 |
| F109 | 09-20 实测 ls *.yaml \| wc -l 给出 411，真值 483，静默少算 | 未标版本 | 是 | worldbook-encyclopedia-rollout-batch 步骤5·🪤数档别用 ls|wc -l |
| F111 | Git Bash 的 grep 对含中文的正则可能静默 0 命中（09-14 误判「日志无条目」） | 未标版本（旧 Git Bash harness） | 是 | worldbook-encyclopedia-rollout-batch 坑清单·Git Bash grep 中文 |
| F113 | 单次 Write 一次写太多中文（如 60+ 条 L2）会被长度上限截断，症状是文件尾半句戛然而止 | 未标版本 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·单次 Write 长度上限 |
| F114 | 名录 xlsx 表内首行写着「机器生成，请勿手改数据」；Excel 侧加工会被下次重跑静默覆盖（09-14 sheet-agent 筛选列实证） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤5·名录机器产物 |
| F123 | game-villages-desc-*.txt 里 village.village_B2_1 与 village.castle_village_B2_1 两行都有 | v1.3.15 快照 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·按 id 找来源行 |
| F128 | package.ps1 的 release-check 会被既有坏账拦住（09-14：A3.3 preview golden 是跨仓库副本快照，本仓库一直红） | 未标版本 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·package.ps1 release-check |
| F14 | RuntimePackageCompiler.cs:135 为无 expressions 文档合成 grants=[] 兜底，SelectExpression 需 grant 命中 ⇒ 不可达 | 未标版本 | 是 | worldbook-field-liveness-audit / L85 |
| F17 | ai_chain_sim.py 判据判 case=="soldier:收成"，而 selected 只建 noble:* 用例 ⇒ any(...) 恒 False | 未标版本 |  | worldbook-field-liveness-audit / L93 第三类失效 |
| F18 | 上述恒 False 的根因是用例元组第一字段身兼 profile 键与用例名前缀两职，看漏一半 | 未标版本 |  | worldbook-field-liveness-audit / L93 |
| F21 | 读侧存在 if (persona == null) return true; 形态的静默成功分支，字段缺失时不抛错 | 未标版本 |  | worldbook-field-liveness-audit / L94、L97 |
| F22 | 76 张 persona 卡全部走 BuildRuntimeFallback（仅 ID+NAME），且全程不报错 | 未标版本 |  | worldbook-field-liveness-audit / L94 |
| F37 | 44fcf48 之后、补交之前 HEAD 处于「有类、无调用、无测试」悬空件状态：能编过、无测试会红、git status 不报 | 未标版本 |  | worldbook-field-liveness-audit / L151、L158 |
| F41 | release_check.ps1 的 $required 里只有 registry manifest.json、没有它指向的包，dist 可只带指向不存在包的注册表而门照过 | 未标版本 |  | worldbook-field-liveness-audit / L169 |
| F55 | RetrievalProbeCases.cs 的 PickIdentity(snapshot, target) 从目标条目自己的 grants 里挑身份再问，使命中率在「谁知道」这一维上永远不判红 | 未标版本 |  | worldbook-field-liveness-audit / 第十类失效 |
| f19 | 链式编译若 register 的 operation 不带本轮 OPBASE 会幂等短路、head 记旧 hash，最终报 WB-AUTHORITY-CAS-409 | 未标版本 |  | awake-worldbook-new-compile-input §四-纪律 |
| f35 | settlementId 大小写敏感，写成小写会静默返回 0 行，看着像「不存在」 | 未标版本 | 是 | worldbook-anchor-verification §一-第2步 |

## 证据

- **F010** 09-17 实测：陈旧二进制缺本会话对 src/ 的改动，两档读数相同。
- **F014** 判定脚本正则写错后首跑实测：两档均 69/69 红、零翻转。
- **F015** 09-17 首次判定跑出的形状；两档只差一层开关，真故障不会让两档塌成同一个数。
- **F044** 第一版普查报「孤例」，按类重扫后才拿到真数。
- **F059** 首跑 4 反例逐条排查后确认为样本笔误而非实现缺陷。
- **F061** 09-17 复测真索引与数据侧两种口径对不上，真索引显示已剔。
- **F082** 读 WorldKnowledgeQueryService.cs:208 的空文本分支，实测返回全表。
- **F088** 边界组实测归一化后为空串导致全表命中。
- **F117** 09-17 对照真件 Resolve 后发现手抄表与真件分叉，secret 档正文从未出现。
- **F009** 09-15 断点续跑实测：重算 need 使列表变短、序号整体漂移
- **F035** 09-19 造阳性对照：A 的 locator 配 B 的 quote，validate 仍全绿
- **F039** 读 SafeYamlLoader 失败分支 + 实测：报缺 id 而文档其实有 id
- **F051** 09-20 302 档漏同步实测：差异只在 entity_ids，按标题比完全看不出
- **F055** 09-14 批量改 title 命中 aliases.en；YAML 解析通过，schema 报 additionalProperties: ["en"] + type: object should be string
- **F068** 读 Program.cs:212 的 status==503 时 Console.Error.WriteLine 分支
- **F069** 09-17 实跑：改成 capture_output 拿 bytes 后按 utf-8 解码才看到真因链
- **F070** 09-14 实测：改 subdomain 不动 revision，compile 抛 CAS-409「CompileProof 输入已变化」
- **F087** 步骤8·两个官方数据坑一节记录的 DB 实测个例
- **F109** 09-20 对同一目录分别用 wc -l 与 grep -l '^id: doc\.' 计数对照
- **F111** 09-14 实测：同一文件用 Grep 工具有命中而 bash grep 0 命中
- **F113** 09-14 实测：一次性写 64 条 L2 进单个 .py 被截断
- **F114** 09-14 实证：sheet-agent 加的筛选列只存在于 xlsx，重跑会覆盖，已回写生成器
- **F123** 09-19 实测：用 if key in line 先命中城堡村那行且不报错，迪安托格麦尔被整条判漏；改为按 => 切左半边全等比较后归零
- **F128** 09-14 实测：release-check 红但包目录与 manifest.json/SHA256SUMS.txt 已先写好
- **F14** 仅读码推断（技能原文自述「疑似静默点（仅读码推断）」），未在包上实跑
- **F17** 09-14 读 tools/worldbook-runtime-sim/ai_chain_sim.py 的 negativeKnowledgeBlocked 判据与用例构造两侧对比
- **F18** 09-14 对照 ai_chain_sim.py 中 selected 用例元组与判据比较的字段语义
- **F21** 读 WorldbookRuntime.cs 读侧门控；09-14 实测 76 张卡全落兜底且无报错
- **F22** 09-14 persona 运行时装载实测：写侧无字段 ⇒ 读侧返 true ⇒ 兜底路径
- **F37** 09-14 对该提交做自洽三连检查（调用方/测试/新文件是否同在 HEAD）
- **F41** 09-14 读 release_check.ps1 的 $required 清单，与注册表指向关系对照
- **F55** 09-17 读门禁辅助函数怎么造输入；注释原话「保证目标可被吐出来，否则测到的是权限而不是检索」
- **f19** AuthorityGate.cs:433 抛 WB-AUTHORITY-CAS-409；tools/_link_chain_20260920.py:13 与 ECONOMY-GOODS-REPORT:39 记真因是 op_id 未换代
- **f35** 技能 §一-第2步 声明；本次未定位到 BannerlordSage 库文件，未能独立复验

---

# API 怪癖（api-quirk）

> 共 33 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F005 | runtime.json 顶层是 dict，条目在 ['entries']；直接迭代 dict 得到字符串键并报 string indices must be integers。 | geo1-v9 包 |  | worldbook-retrieval-redtest §1 |
| F012 | 探针 spec 形状为 {"queries":[{name,identity,text,requested_detail,culture}]}。 | 09-17 仪器 |  | worldbook-retrieval-redtest §3 spec 形状 |
| F013 | 探针结果行的 hits 是命中条目 id 列表，判定必须靠它而不是读正文猜。 | 09-17 仪器 |  | worldbook-retrieval-redtest §3 |
| F043 | title／summary／expressions[].text 都是本地化字典 {"zh-CN": …}，isinstance(v,str) 会把它们整段跳过导致普查 0 命中。 | 09-17 当前包格式 |  | worldbook-retrieval-redtest §5e 要扫的字段 |
| F074 | 探针 hits 带前缀 awake:entry:，而 sidecar／自造 spec 用去前缀短名；不归一化会让 exp in hits 恒 False 并报出「全 0 命中」假象。 | 09-17 仪器 |  | worldbook-retrieval-redtest §5h 口径坑 1 |
| F075 | spec 字段 {name,identity,text,requested_detail}；out 字段另含 state／match_mode／hits 等。 | 09-17 仪器 |  | worldbook-retrieval-redtest §5h 口径坑 2 |
| F076 | 探针 out 的 text 被覆盖成拼好的正文，不是原问句。 | 09-17 仪器 |  | worldbook-retrieval-redtest §5h 口径坑 2 |
| F096 | 繁→简走系统 API LCMapStringEx(null, LCMAP_SIMPLIFIED_CHINESE=0x02000000, ...)，仅含非 ASCII 时才调。 | 09-17 起 |  | worldbook-retrieval-redtest §7d 走系统 |
| F010 | 改了共享 CLI 却编译到独立目录、脚本仍调旧 DLL 时，报错文本只显示「用法」且 rc=3 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤3·⚠️改共享CLI后必须编译到标准输出目录 |
| F011 | 后台任务的 TaskOutput 可能返回 not found 且不发出完成通知 | 未标版本（旧 harness） | 是 | worldbook-encyclopedia-rollout-batch 步骤3·后台跑进度只看文件系统 |
| F017 | probe 的 detail 字段显式给了就覆盖身份能力上限（Program.cs:1258） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤4·⚠️probe 的 detail 字段 |
| F037 | validate 的 --path 是无效参数：service.Validate() 不吃 path，恒校验整个工作区 | 未标版本 | 是 | worldbook-encyclopedia-rollout-batch 步骤9·三级验证2 |
| F038 | authoring-register 的 --path 必须工作区相对（authoring/x.yaml），传仓库相对全路径报 WB-DOC-404 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤9·CLI 参数事实 |
| F041 | compile 的成败判定看 manifest_hash，response 里没有 ok 键 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤3·compile 判定 |
| F043 | WB-SOURCE-001 的 source_content_hash 比的是 locator_root 指向的那个文件的 sha256，不是数据源整库 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·WB-SOURCE-001 口径 |
| F056 | 中/英标题含 ': ' 时若不带引号，报 WB-YAML-001；原文带引号的地方改写后要保留引号 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤9·坑3 |
| F062 | deploy_worldbook_to_game.ps1 -ValidateOnly 的语义是断言真机 == 源，刚改完源必然 exit 1 且无输出 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤10·4 推真机 |
| F071 | 六步脚本必须用新 OPBASE，复用旧 OPBASE 会撞 CAS-409 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·六步脚本必须新 OPBASE |
| F098 | 权威 taxonomy 源唯一＝docs/worldbook-studio-plan/knowledge-taxonomy.v1.json，带 CAS 门控 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤5·taxonomy CAS 门控 |
| F099 | taxonomy_version 必须保持 1.0.0（schema const 与 C# 双硬编码），改了即 WB-TAXONOMY-500 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤5·改 taxonomy 硬约束三条 |
| F110 | Windows 自带 find.exe 与 Git Bash /usr/bin/find 同名冲突，不走 /usr/bin/ 会报「参数格式不正确」 | 未标版本（旧 Git Bash harness） | 是 | worldbook-encyclopedia-rollout-batch 步骤5·🪤数档 |
| F118 | WB-YAML-006：YAML 输出必须用 NoAlias(yaml.dumper.Dumper) + ignore_aliases=True，否则出现锚点引用 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤2·硬约束 |
| F119 | 文档正文里出现 &id 会被判违规（生成器需 assert "&id" not in text） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤2·硬约束 |
| F25 | ConvertFrom-Json 把对象键当大小写不敏感，键同时有 Cow/cow 的合法 JSON 直接抛异常，配 try/catch 会被吞成 0 | 未标版本 |  | worldbook-field-liveness-audit / L106 |
| F38 | Git for Windows 下 git commit -F /tmp/x.txt 报 could not read log file（MSYS /tmp 不落在 Windows 盘上） | 未标版本 |  | worldbook-field-liveness-audit / L159 |
| F51 | grep -rn "EventFact" 会命中另一个东西 AwakeEventFactTrigger，造成「有人在用」的假象 | 未标版本 |  | worldbook-field-liveness-audit / L234 |
| F62 | 校验值必须等于上报值：RequireDigest 与 Current*Sha256 必须一起改，否则 MatchesAssets 这类闭包比对会自己跟自己不一致 | 未标版本 |  | worldbook-field-liveness-audit / L298 |
| f03 | 新增数据只能落在 entry.extensions.*，因为包内合法文件恰好三个、不许多开第四个文件 | 未标版本 |  | awake-worldbook-new-compile-input §〇-3 ＋ §一-4 |
| f04 | WorldbookPackageIntegrity.ValidateIndex 只比对 entryIds / keywordToEntryIds / domainToEntryIds 三项 | 未标版本 |  | awake-worldbook-new-compile-input §一-雷区4（:124-135） |
| f09 | Workspace.EnumerateSchemaInputFiles 返回 names.Select(...).Where(File.Exists) ⇒ 输入件不存在时自动跳过 | 未标版本 |  | awake-worldbook-new-compile-input §一-雷区1 |
| f26 | CanonicalEntityRef 只接受 entity.<hero\|clan\|settlement>，其它 kind 抛 WB-DOC-003 | 未标版本 |  | worldbook-anchor-verification §三 |
| f39 | 游戏 settlementId 保留原始大小写（castle_V6/town_V7），项目实体 ID 用小写（town_v7） | 未标版本 |  | worldbook-anchor-verification §四 |
| f55 | 边表 usage 字段明文规定：类别星形边（bucket=star）不进召回，只留审计 | 未标版本 |  | worldbook-should-link-judgement §四-方向规则 |

## 证据

- **F005** 读 tools/worldbook-studio/workspace/full-geo1/compiled/geo1-v9/runtime.json，实测报错串。
- **F012** 读探针 probe 子命令入参解析与实际跑通的 spec 文件；identity=profile.noble 加 requested_detail=detail 才让 content 可见。
- **F013** 读探针输出 JSON 的 hits 字段与判定脚本取值处。
- **F043** 第一版普查脚本对真数据全 0 命中，改用 _flatten(val,out) 递归摊平后才拿到数。
- **F074** 自命中判定首跑全 0，加 PREFIX="awake:entry:" 归一化后恢复。
- **F075** 对比探针入参 spec 与输出 JSON 键名：out 为 {name,state,match_mode,literal_keyword_hits,literal_term_hits,hits}。
- **F076** 读探针输出中 text 字段内容与入参问句对比。
- **F096** 读产品归一化函数里的 kernel32.dll P/Invoke 声明与 0x02000000 常量。
- **F010** 09-15 踩坑实测：输出到 bin/Release/net10.0 之外后跑链得 rc=3
- **F011** 09-15 后台跑六步编译链时观察到的工具行为
- **F017** 读 Program.cs:1258，显式赋值时直接盖过按身份算出的上限
- **F037** 09-14/09-19 两次实测：传 --path 仍被其它档诊断淹没
- **F038** 步骤9 CLI 参数事实一节记录的实测报错
- **F041** 步骤3 编译判定记录：读返回的 manifest_hash 而非 ok
- **F043** 读 ValidationServices.cs:134，并因错写 bannerlord.db 整库 hash 触发编译报错而确认
- **F056** 09-14 把带引号的 en 值改写成无引号，触发 WB-YAML-001
- **F062** 步骤10·4 记录：source sha != target sha ⇒ throw；用它验收刚改完的源必然失败且无输出
- **F071** 坑清单「六步脚本必须新 OPBASE」一条
- **F098** 步骤5：taxonomy 带 raw-byte SHA-256；改它 ⇒ 包 hash/manifest.json 变 ⇒ Studio 返 HTTP 409（WB-TAXONOMY-CAS-409）拒写
- **F099** 09-14 v2 实测：schema const 与 TaxonomyCatalogService.SupportedVersion 双硬编码，改版本号触发 WB-TAXONOMY-500
- **F110** 步骤5·🪤 记录：find 必须走 /usr/bin/ 才不撞 Windows 同名 exe
- **F118** 步骤2·硬约束 记录的生成器必需设置
- **F119** 步骤2·硬约束 与步骤11·套用器断言 两处都断 &id
- **F25** PS 侧同族实证（技能指向 windows-powershell-scripting），表现为「内容为空」假象
- **F38** 本机实测报错原文；改写到仓库内 _ 前缀文件后成功
- **F51** 09-17 只 grep 关键词时命中一堆无关类型，改 grep 完整方法名才暴露零调用
- **F62** 09-14 定位消费者时 grep 摘要常量 + 相关属性，发现两处需同步
- **f03** RuntimePackageCompiler.BuildEntry(:108) 把 sourceDocumentId/subdomain/relatedDomains/entityRefs/links 全写进 entryExtensions
- **f04** 读 src/WorldbookPackageIntegrity.cs:124-135，方法体内只有三处 Require（WB2-INDEX-MISMATCH:entry_ids/keywords/domains）
- **f09** 读 tools/worldbook-studio/src/Awake.WorldbookStudio.Core/Workspace.cs:408-431 原文
- **f26** RuntimePackageCompiler.cs:276-287 的 parts[1] is not ("hero" or "clan" or "settlement") 分支抛该码
- **f39** 登记表 settlement_code 字段保留原大小写；CanonicalConditionId(:293) 对值做 ToLowerInvariant().Replace('-','_')
- **f55** 读 should-link.v3.json 的 usage.note 原文

---

# 否定式断言（negative-claim）

> 共 24 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F008 | 检索链路上没有拼音通道，拼音首字母（gk／bmjfk）不是设计内的输入。 | 未标版本 | 是 | worldbook-retrieval-redtest §2 样本表·拼音首字母行 |
| F047 | 「城堡」「城镇」问出去是 not_found（空手），与「村庄」的行为不一致。 | 09-17 当前包 | 是 | worldbook-retrieval-redtest §5f 症状 |
| F094 | 仓里没有可信的繁简对照表来源。 | 未标版本 | 是 | worldbook-retrieval-redtest §7d 不造表 |
| F098 | 用 Add-Type 探 LCMapStringEx 会被安全策略拦，报 compiles and loads .NET code at runtime。 | 09-17 本机策略 | 是 | worldbook-retrieval-redtest §7d ⚠️Add-Type |
| F100 | 字序差异折不了：斯特基亚 那类（斯特 vs 斯提）不是繁简问题。 | 09-17 起 | 是 | worldbook-retrieval-redtest §7d 边界 |
| F104 | RagHit（framework/MarcusAwakeFramework/src/StorageAndRagApi.cs:135）只有 Rank，协议里没有分数 ⇒ 游戏侧拿不到分数、没法再卡一道。 | v1.3.15 源码基线 | 是 | worldbook-retrieval-redtest §7e ② |
| F045 | entity.lore.* 锚点不可编译（WB-DOC-003：entity_ids 类型仅支持 hero/clan/settlement）；全库 56 档概念型条目同样不写 entity_ids | 未标版本 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·entity.lore 锚点不可编译 |
| F082 | bannerlord_items 漏了近战武器（刀剑枪斧锤在 <CraftedItem> 里，索引器没收），要一手 XML | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·兵种装备取数 |
| F083 | 城堡的 descriptionText 全空（游戏不给城堡写官方描述文） | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·城堡档 |
| F084 | localization 里 text.castle* 全是 castle_village_*，没有城堡自身的描述文 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·城堡档 |
| F106 | 档名规范报告 §一 的词表原表不含 castle 前缀（标为 P5 待补） | 未标版本 | 是 | worldbook-encyclopedia-rollout-batch 步骤5·文件名前缀不是分类轴 |
| F127 | 旧结论「authoring-register 不支持批量」已被 09-15 实测推翻（批量入口存在且 0.53 s/档） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤3·⚡register批量入口 |
| F02 | WorldbookRuntime.Current（旧代入口）在现行仓中已无任何消费者 | 未标版本 | 是 | worldbook-field-liveness-audit / L34 |
| F08 | ContextModes 在现行链路 WorldKnowledgeQueryService 上是死字段，只在旧代 WorldbookService 上是打分项 | 未标版本 | 是 | worldbook-field-liveness-audit / L45 |
| F12 | 运行时 HasMatchingDeny() 的否决分支因 denies 全空而从未执行过 | 未标版本 | 是 | worldbook-field-liveness-audit / L83 |
| F13 | fallback_referral_ids 未接线（记为 C13），且被 Studio golden 钉死，属跨线批次 | 未标版本 | 是 | worldbook-field-liveness-audit / L84 |
| F23 | 本项目 .py 入口硬调 pwsh，而本机只有 Windows PowerShell 5.1，该入口从来没跑过 | 未标版本 | 是 | worldbook-field-liveness-audit / L100 前置先验 |
| F32 | LocateManifest() 返 null 只在仓库侧成立，游戏目录侧是有 manifest 的 | 未标版本 | 是 | worldbook-field-liveness-audit / L132 |
| F35 | 运行时只认包内单数 Definition，不按 characterId 挑卡 | 未标版本 | 是 | worldbook-field-liveness-audit / L134 |
| F48 | 检索函数 FindCandidates 只查 _snapshot.KeywordIndex，完全不碰 Summary | 未标版本 | 是 | worldbook-field-liveness-audit / L211 |
| F50 | NpcMemoryService.RecordEventFactAsync（src/NpcMemoryService.cs:334）全仓零调用方 | 未标版本 | 是 | worldbook-field-liveness-audit / L232 第九类失效 9.1 |
| F60 | referral-registry.v1.json 与 profile-registry.v1.json 被 golden 钉死，改字节即打掉测试套件 | 未标版本 | 是 | worldbook-field-liveness-audit / L295 顺带必查 |
| f28 | 含 entity.lore.* 锚点的档案编译必失败（结构校验全过、Compile() 一跑才炸） | 未标版本 | 是 | worldbook-anchor-verification §三 |
| f33 | 城堡没有 .text.* 描述文；v1.4.8 实测 67 座城堡 0 条 text，城镇 53/53、城堡村庄 132/132、村庄 141/142 有 text | v1.4.8 | 是 | worldbook-anchor-verification §一-第2步 |

## 证据

- **F008** 甲方 09-17 裁定「拼音首字母别测，这个本来也不合理」，链路检查无拼音模块。
- **F047** 同一批泛问词跑探针，三个词读数分别为命中/空手/空手。
- **F094** 排查仓库无权威繁简对照数据，09-16 版脚本表为自造。
- **F098** 09-17 实测被拦的确切提示串。
- **F100** 对斯特基亚 类输入实测折叠后仍查不到，定位为字序差异。
- **F104** 读 StorageAndRagApi.cs:135 的 RagHit 定义，确认无分数字段。
- **F045** docs/worldbook-migration/projection/LORE-ENTITY-REGISTER-20260912.json 的 compiler_behavior（含 corrections_20260912.compile_blocker_lore_kind）
- **F082** 坑清单·兵种装备取数一节：DB 里查不到近战武器，回到 weapons.xml
- **F083** 步骤8·城堡档一节：DB 实测 descriptionText 全空
- **F084** 步骤8·城堡档一节：查 localization_entries 得 text.castle* 全指向村条目
- **F106** 步骤5·文件名前缀不是分类轴一节记录的出处与缺口
- **F127** 09-15 实跑 authoring-register-batch 成功，87 档 46 秒
- **F02** grep -rn 'WorldbookRuntime\.(Current\|Knowledge)' src/ 只见定义处；裁定文档 DECISION-20260916 宣布旧代作废
- **F08** 09-17 先跑 grep -rn 'WorldbookRuntime\.(Current\|Knowledge)' src/ 分辨活链路，再逐段查读取点得出
- **F12** 内容侧实测 40/40 denies 为空，对照 WorldKnowledgeQueryService 中 HasMatchingDeny() 调用路径
- **F13** 四段链路 grep 该字段：契约有、生成器/编译器无写入；golden 测试钉死相关文件
- **F23** 读 .py 启动参数 + 本机解释器实测；跑不起来的探针等于没验过
- **F32** 09-14 两侧目录对比时在游戏目录找到 manifest，纠正「哪里都没有」的推断
- **F35** 09-14 读运行时选卡逻辑，grep 读侧认的单复数形态与写侧产出形态
- **F48** 09-16 读 WorldKnowledgeQueryService.FindCandidates 方法体
- **F50** 09-17 grep -rn 完整方法名（含括号）并排除其定义处，命中 0
- **F60** 先 grep -rin <其 SHA-256 前 8 位> --include=*.cs --include=*.json 命中 golden 常量
- **f28** 白名单同 f26；validate-authoring-closure.ps1:296-307 把该项 C16 直接标 BLOCKED 而非 PASS
- **f33** python 解析 SandBox/ModuleData/settlements.xml：494 个 Settlement；castle 67 with_text 0；town 53/53；castle_village 132/132；village 141/142；hideout 99/0

---

# 版本事实（version-fact）

> 共 6 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F028 | 技能早前记的「R3 剔 448 个」来自旧编译器编的包，对当前包不成立。 | 09-17 |  | worldbook-retrieval-redtest §5c ⚠️R3 实测纠偏 |
| F054 | 游戏城堡英文名恒为「XXX Castle」，所以全文替换 castle→castles 会让 67 个 castle 档的词尾专名一起变 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤9·坑1 |
| F126 | 具装骑兵的 Item1 就是 heavy_horsemans_kite_shield，防御力全游最高档，能顶着箭雨推进 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 坑清单·B级编年史料须对表 |
| f36 | 本机 Bannerlord 版本为 v1.4.8（Native/SubModule.xml <Version value="v1.4.8"/>） | v1.4.8 |  | 环境基线（本批任务给定，本次实测确认） |
| f37 | validate-authoring-closure.ps1 的 C12 已放宽为双通道：lore 查本清单、game_anchored 查 persona-entity 登记表 | 未标版本 |  | worldbook-anchor-verification §二-4 |
| f38 | 同脚本 C16 把 entity.lore.* 可编译性标为 BLOCKED（三态而非 PASS/FAIL） | 未标版本 |  | worldbook-anchor-verification §二-4 ＋ §三 |

## 证据

- **F028** 同一脚本在新旧包上复测对比：旧包 448、当前包 0。
- **F054** 09-14 批量改名实测：castle-ab-comer-castle 词尾 castle 是地名一部分
- **F126** 09-20 从 spnpccharacters.xml 的 <equipment slot="Item1"> 读得，与编年史 B 级说法相反
- **f36** 读 D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\SubModule.xml:5
- **f37** 读该脚本:276-277 注释「2026-09-12 放宽：原口径『必须全部是 entity.lore.*』是德里亚特误分类的镜像」
- **f38** 读该脚本:296-307，Add-Check 'C16' ... 'BLOCKED'

---

# 路径事实（path-fact）

> 共 38 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。

| id | 事实 | 测量版本 | 待重验 | 出处 |
|---|---|---|---|---|
| F004 | 对话路径的检索入口是 WorldbookRuntime.Knowledge（WorldbookRuntime.cs:13）。 | v1.3.15 源码基线 |  | worldbook-retrieval-redtest §0 |
| F130 | 本机 Python 在 C:/Users/26811/.workbuddy/binaries/python/envs/default/Scripts/python.exe（含 PyYAML）。 | 09-17 本机 |  | worldbook-retrieval-redtest 环境与纪律 |
| F131 | AWAKE/docs/evidence/ 在 .gitignore:59 里，报告放那儿不入档。 | v1.3.15 仓库 |  | worldbook-retrieval-redtest 环境与纪律 |
| F132 | 红测报告落 AWAKE/docs/worldbook-migration/REDTEST-*-<日期>.{md,json}（09-16 那两份在此）。 | 09-16/09-17 |  | worldbook-retrieval-redtest 环境与纪律 |
| F133 | 三根门禁：RETRIEVAL_GATE、MERGE_GATE、IDENTITY_GATE（见 tools/ 下三个工具）。 | 09-17 仓库 |  | worldbook-retrieval-redtest 跑法 |
| F040 | schema 路径由 FindSchemaRoot(CWD) 决定：先 <DLL目录>/schemas，再 <CWD>/docs/worldbook-studio-plan | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤9·CLI 参数事实 |
| F042 | compile --out 按进程 CWD（仓库根）解析，必须传工作区相对全路径，裸名字会落到仓库根被拒（WB-PATH-003） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·compile --out |
| F053 | tools/ 被 gitignore、没有 git 可回滚；authoring-v1/document-revisions/<doc_id>/<rev>.json 里存着登记当时的完整 content | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤9·三级验证3 |
| F063 | 真机落点 <Bannerlord>\Modules\AWAKE\ModuleData\Worldbook\（全路径见 evidence） | v1.4.8（本机路径） |  | worldbook-encyclopedia-rollout-batch 步骤10·4 推真机 |
| F073 | 仓库根＝D:\AWAKE-Dev\AWAKE（不是 D:\AWAKE-Dev），docs/worldbook-migration/... 全在其下 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·仓库根 |
| F074 | ModuleData/Worldbook/ 不在版本管理里，上线件（包 index/manifest/runtime.json + 注册表 manifest.json）不会被提交 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·ModuleData 不受版本管理 |
| F075 | tools/worldbook-studio/workspace/ 整棵被 .gitignore:54 排除，源档与编译产物全在版本库外 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤10·workspace 被 gitignore |
| F077 | 现役档在 workspace/full-geo1/authoring/（库外，git 看不见），镜像档在 projection/authoring-out/（库内，有 HEAD 基线） | 未标版本 |  | worldbook-encyclopedia-rollout-batch 步骤10·镜像档状态已变(09-19) |
| F078 | DB 路径 <...>/BannerlordSage-main/dist/games/bannerlord/bannerlord.db，以 mode=ro 只读打开 | v1.3.15 数据快照 | 是 | worldbook-encyclopedia-rollout-batch 步骤1·取数 |
| F085 | 城堡的 A 级锚＝官方名 Settlement.name.castle_*；快照 game-castles-desc.txt 每堡一行含官方 CN 名 | v1.3.15 | 是 | worldbook-encyclopedia-rollout-batch 步骤8·城堡档 |
| F122 | 编年史档的 locator 是 JSON 路径（#/Variants/0/Content），在来源文件行内找不到 | 未标版本 |  | worldbook-encyclopedia-rollout-batch 坑清单·引文归属 |
| F01 | src 里并存两代检索链路：现行 WorldKnowledgeQueryService 与已作废的 WorldbookService.cs | 未标版本 |  | worldbook-field-liveness-audit / 本仓的「无人读」一律要限定现行链路 |
| F19 | persona 运行时字段 awake.persona.runtime-bundle.v1 在读侧有门，位置 WorldbookRuntime.cs:229 | 未标版本 |  | worldbook-field-liveness-audit / L94 |
| F20 | 写侧 RuntimePackageCompiler.cs:76 只写 {contentTier, source}，不产出 persona 运行时字段 | 未标版本 |  | worldbook-field-liveness-audit / L94 |
| F28 | 生产路径 NpcDialogueService.cs:1028-1031 在填 KingdomName = _kingdomName 等，取自 hero.Clan?.Kingdom?.Name | 未标版本 |  | worldbook-field-liveness-audit / L116 |
| F31 | 仓库无 ModuleData\Worldbook\manifest.json ⇒ 不跳过时 Assert-SourceManifest:456 直接 throw | 未标版本 | 是 | worldbook-field-liveness-audit / L132 |
| F36 | 提交 44fcf48 把新件 src/PersonaRootLocator.cs（63 行）与 csproj 的 Compile Include 一起带入 HEAD，引用方留在工作区 | 未标版本 |  | worldbook-field-liveness-audit / L149 第五类失效 |
| F39 | .gitignore 原文以 AWAKE/ModuleData/Worldbook/* 整条排除，导致组出的运行时包永远进不了库 | 未标版本 | 是 | worldbook-field-liveness-audit / L167 第六类失效 |
| F40 | sync_module.ps1 的 $managedWorldbookDirectories 里没有 packages，运行时包永远投不到游戏目录 | 未标版本 | 是 | worldbook-field-liveness-audit / L168 |
| F43 | 该行为的实现链是 NpcDialogueService.cs:1125 → :278 → CompleteDirectKnowledgeTurn，全链无一处是异常或降级分支 | 未标版本 |  | worldbook-field-liveness-audit / L187 |
| F44 | 设计文档 AI-Retrieval-Memory-Principles-20260815.md:23 要求无命中时「用女神本知/角色本知作答」而非留空 | 未标版本 |  | worldbook-field-liveness-audit / L186 |
| F45 | 验收把负路径写成通过项：计划 D-13 与 ai-chain-sim-fix-20260914/result.json 的 expectedGate=not_found | 未标版本 |  | worldbook-field-liveness-audit / L188 |
| F47 | Summary 唯一读取点是 WorldKnowledgeQueryService.cs:312 的 entry.Summary + Environment.NewLine，只被拼进给模型的输出文本 | 未标版本 |  | worldbook-field-liveness-audit / L210 |
| F63 | authoring 契约 schema 的 $defs 含 assertion / expression / knowledge_rule / target_span 四个定义 | 未标版本 |  | worldbook-field-liveness-audit / L16 四段链路表 |
| F64 | Studio 编译器核心目录 Awake.WorldbookStudio.Core 下的 RuntimePackageCompiler.cs 等 5 个文件参与运行时包编译 | 未标版本 |  | worldbook-field-liveness-audit / L18 |
| F65 | 只读复验器 validate-authoring-closure.ps1 的证据落在 docs/evidence/authoring-closure-20260912.json | 未标版本 |  | worldbook-field-liveness-audit / L74 内容覆盖率实测 |
| F66 | identity-gate 子命令建于 2026-09-17；语义臂靠 AWAKE_SIM_SEMANTIC=1 打开，变异检验靠 AWAKE_GATE_MUTATE_ASSUME_ALLOWED=1 | 未标版本 |  | worldbook-field-liveness-audit / |
| f01 | assemble_worldbook_package.ps1 第 35 行写死 $requiredEntryFiles = @('runtime.json','index.json') | 未标版本 |  | awake-worldbook-new-compile-input §〇-3（:35） |
| f10 | RegistrySnapshot 有逐字节金标基线 tests/fixtures/a3-4-registry-snapshot-golden.v1.json | 未标版本 |  | awake-worldbook-new-compile-input §一-雷区2 |
| f20 | 编译门把被判回收的产物整体挪进 compiled/quarantine/<SafeId(op_id)>/artifact/，产物名不是永久地址 | 未标版本 |  | awake-worldbook-new-compile-input §四-纪律 |
| f29 | 技能所引编译器行号已陈旧：CanonicalEntityRef 从 :253 移到 :276，BuildEntry 的 entity_ids 处理从 :147 移到 :140 | 未标版本 |  | worldbook-anchor-verification §三 |
| f30 | persona-entity 登记表位于 docs/mappings/persona-entity/generations/<buildid>/entity-registry.v1.json | 未标版本 |  | worldbook-anchor-verification §一-第1步 |
| f40 | AWAKE/tools/worldbook-studio/workspace/ 被 gitignore，projection/authoring-out/ 未被忽略 | 未标版本 |  | worldbook-anchor-verification §二-2 |

## 证据

- **F004** 在 src/ 下搜 WorldbookRuntime.Knowledge 追到 WorldbookRuntime.cs:13。
- **F130** 技能环境小节给出的解释器绝对路径，脚本均用它跑通。
- **F131** 读 AWAKE/.gitignore 第 59 行确认该目录被忽略。
- **F132** 09-16 两份报告的实际存放路径。
- **F133** 跑法小节给出的工具路径：worldbook-runtime-smoke／worldbook-rag-merge／worldbook-runtime-sim identity-gate。
- **F040** 读 FindSchemaRoot 实现，顺序：<DLL目录>/schemas → <DLL目录>/../schemas → <CWD>/docs/worldbook-studio-plan → <CWD>/../../docs/worldbook-studio-plan
- **F042** 步骤3 与坑清单记录的 WB-PATH-003 成因
- **F053** 步骤9 三级验证3 记录：账本是唯一可用 baseline
- **F063** D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\ModuleData\Worldbook\；与 AGENTS.md 基线一致
- **F073** 坑清单「仓库根」一条，与 AGENTS.md 的 AWAKE\ 权威副本一致
- **F074** 坑清单「ModuleData/Worldbook 不在版本管理里」一条
- **F075** 步骤10 末尾记录：.gitignore 第 54 行排除整棵 workspace
- **F077** 09-19 实测两层结构：git status --short 只能判镜像档
- **F078** C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db（mode=ro）
- **F085** 步骤8·城堡档一节记录的锚点来源与快照形态
- **F122** 坑清单·引文归属 记录：编年史 locator 无法按行定位，需回退用 quote 前 24 字
- **F01** 读 src 目录与裁定文档 docs/DECISION-20260916-旧代检索链路作废.md；现行入口 WorldbookRuntime.Knowledge，旧代 WorldbookRuntime.Current
- **F19** 读 src/WorldbookRuntime.cs:229 读取点
- **F20** 读 tools/worldbook-studio/src/Awake.WorldbookStudio.Core/RuntimePackageCompiler.cs:76 写入语句
- **F28** grep -n "= .*KingdomName" 全 src 找到真实赋值点
- **F31** 09-14 读 sync_module.ps1:53（该目录已列但 -SkipWorldbook 当前禁用）并核实仓库缺 manifest
- **F36** 09-14 用 git log --oneline -1 -- <新文件> 与 git show <commit> --name-only 核实代管；引用方为 WorldbookRuntime.cs
- **F39** 09-14 用 git check-ignore -v 与 git status --porcelain -uall 核实新放文件不出现
- **F40** 09-14 对「部署脚本列的东西」与「产出的东西」做集合差
- **F43** 09-16 读实现侧正常路径（不只看 catch），逐跳确认无异常分支
- **F44** 09-16 读设计文档原话；同文 :368 另写「把预算让给记忆或角色状态，而不是留空」
- **F45** 09-16 读 PLAN-AWAKE-DIALOGUE-CHAIN-010-20260911.md:78 与 evidence/ai-chain-sim-fix-20260914/result.json（全场 status=pass）
- **F47** 09-16 用 grep -n 列全部读取点并逐点标注消费链路
- **F63** 读 AWAKE/docs/worldbook-studio-plan/awake.worldbook.authoring.v1.schema.json 的 $defs 键
- **F64** 读 AWAKE/tools/worldbook-studio/src/Awake.WorldbookStudio.Core/ 文件清单，另含 AuthoringDraftDocumentBuilder.cs 与 Application.cs 等
- **F65** 技能记录的复验器输出路径，跑法为 powershell -NoProfile -ExecutionPolicy Bypass -File <路径>
- **F66** 09-17 建 tools/worldbook-runtime-sim 的 identity-gate 子命令并记录两个环境变量开关
- **f01** 读 D:\AWAKE-Dev\AWAKE\tools\assemble_worldbook_package.ps1:35，原文逐字一致
- **f10** 该 fixture 文件实存；读 ValidationServices.cs:72 类头注释说明投影有金标基线
- **f20** workspace/full-geo1/compiled/quarantine/ 下实存多个 customer.compile.v1.<sha256> 子目录；报告 :75 警告
- **f29** grep 本工作区 RuntimePackageCompiler.cs 实际行号（文件已 498 行），技能写 Core/RuntimePackageCompiler.cs:253/147
- **f30** glob 命中该目录且仅此一代；本机最新 buildid 为 b1-7590085e3662512d7c1c6646c529694bda9850146234827b2dafbd9f20c6a2b3
- **f40** git check-ignore -v：workspace/authoring/ 命中 .gitignore:54；docs/worldbook-migration/projection/authoring-out/ exit 1（未忽略）⇒ 后者是唯一入库的那份

---

> **待重验**：本文件中标 `待重验=是` 的共 **57** 条。
> 完整队列（跨全部领域）见 `AWAKE/docs/reference/reverify-queue.md` —— 本文件不重复内联，避免同一事实出现两次。

---

## 程序要点（从既有 skill 提取）

**仍可执行**

- 跑探针一律 dotnet run -c Release 重编，别直接调 bin/Release 的陈旧 exe。
- 两档跑同一批 spec：第二档加 AWAKE_SIM_SEMANTIC=1 挂语义腿。
- 自命中 spec 的身份照条目 grants 挑，requested_detail 取该 grant 的 min_detail。
- 含中文的脚本先 Write 落 .py 再跑，用 python.exe 绝对路径（本机无 bash）。
- 判定脚本单列 harness 未跑到的条目，并留一条必中的阳性对照。
- 改产品代码后同批复跑 RETRIEVAL_GATE／MERGE_GATE／IDENTITY_GATE 并逐字比对。
- 工作脚本一律先用 write 工具落 .py/.json 再执行，禁内联传中文与反引号（本机无 bash）
- register 走批量入口 authoring-register-batch：写 manifest 后 --workspace，CWD=仓库根
- 每批收尾对现役目录与镜像目录做逐字节比对，先结构化 diff 确认镜像无独有内容再覆盖
- 长链后台跑；进度只看文件系统（operation-journal.jsonl 计数、workspace-head.json mtime）
- 大批素材分组落多份 .json，生成器合并读取并 assert set(L2)==set(inv) 断全集
- 验单档 schema 必须搭空工作区跑 validate（--path 无效，全工作区恒校验）
- 判字段死活前先 grep -rn 'WorldbookRuntime\.(Current\|Knowledge)' src/ 认清哪条链路是现行的
- 四段链路各查一遍：契约 schema → 内容生成器 → Studio 编译器 → 游戏运行时
- 统计 authoring YAML 用逐行状态机，先 rstrip('\r\n')，别用 (?m)^field:$ 正则
- 只读复验器：powershell -ExecutionPolicy Bypass -File validate-authoring-closure.ps1
- 探 .NET 二进制字符串时同时试 utf-16-le 与 ascii 两种编码
- grep -n "\.<字段名>" 列出全部读取点，再逐点标注属于检索/排序/输出/校验
- 判据重算跑在编译产物 runtime.json/documents.json 上，不在 authoring YAML 上另写一套
- 新输入件文件名登记进 Workspace.EnumerateSchemaInputFiles，靠 Where(File.Exists) 兼容有无
- 动手前先量覆盖缺口：本代新增 N 档里有边的几条（做之前通常为 0）
- 新增数据只落 entry.extensions.*，不新开包内第四个文件；无数据时不写空数组
- 探针落 .py 文件再跑；造题用每档自己的标题，身份固定为高能力
- 阴性对照用变异检验（清空该数据、其余不动、同一份代码），不拿无数据档替代

**已失效**（因 harness 变更）

- bash 内联跑含中文 python（反引号会被吃）——DSH 无 bash 工具。
- 用 grep -rn／grep -c 直接搜仓——DSH 用 grep 工具而非 shell grep。
- 用 Add-Type 探 LCMapStringEx——被安全策略拦，不许绕。
- 手工改 runtime.json 等产物——必须改源档重编译，否则破 canonical hash。
- 直接调 bin/Release/*.exe 跑探针——陈旧二进制会伪造回归。
- 拿 Python 复刻件的复算结论当定论——必须回验台再验一次。
- bash 内联命令、sed 派生脚本、cd 与命令写同一条 bash —— 本机已无 bash 工具
- Git Bash /usr/bin/grep、/usr/bin/find、tail -c、ls *.yaml \| wc -l 数档
- 用 TaskOutput 轮询后台任务或长有界 sleep 等完成通知
- 逐档起进程跑 authoring-register（单档 6–51 秒），应改用批量入口
- 用 deploy_worldbook_to_game.ps1 -ValidateOnly 验收刚改完的源
- 对整份档做裸 str.replace 批量改名、手工改编译包/名录 xlsx
- 用 grep -a 在 Awake.dll 里搜字符串（#US 堆是 UTF-16LE，必全 MISSING）
- 用 (?m)^field:$ 正则扫 authoring YAML（文件是 CRLF，匹配不到）
- 跑 .py 入口（硬调 pwsh，本机只有 Windows PowerShell 5.1，从来没跑过）
- git commit -F /tmp/x.txt（MSYS /tmp 不落盘，报 could not read log file）
- 只 grep 关键词 EventFact 判方法死活（会命中无关的 AwakeEventFactTrigger）
- 只改本机 core.autocrlf 治摘要行尾（保护不了新鲜克隆）
- 用 bash 内联命令跑探针并传中文（本机无 bash 工具）
- 用 bash grep -rn 复核编译器 kind 白名单（改用 grep 工具）
- 引用前用 ls 查产物目录（无 ls，改用 glob/read 工具）
- 把 compiled/<op_id> 当永久路径写进脚本（会被挪进 quarantine）
- 链式编译只换 select/approve/proof 而 register 的 operation 不带 OPBASE
- 凭印象断言产物已签收（须读 review-state/*.review.json 的 userSignoff）
