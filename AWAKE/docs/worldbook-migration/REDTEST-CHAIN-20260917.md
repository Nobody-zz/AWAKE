# 链路缺口红测（2026-09-17）

> 靶：对话链路（448 条包，`NpcDialogueService` → `WorldKnowledgeQueryService.FindCandidates`）。探针 `worldbook-runtime-sim probe`，**真代码零替身**。
> 样本 68 条，按目标条目**真实 keywords** 造；**拼音首字母这类已按甲方口径移出样本**（链路上没有拼音通道，拿它判红量到的是设计如此，不是缺口）。
> **三档**：改动前（还原自入库报告 `66649c5`）／改动后 × 不挂语义腿 ／ 挂语义腿（上线形态）。

## 〇、口径与限制

1. 样本是**照着设计造的**，不是真人日志 ⇒ 只说明「哪些形状会漏」，**查全/查准率不能从这批数字推**。
2. 四档跑的是同一批样本、同一个包、同一份真代码（改动前后只差那几个补丁）⇒ 前后之差可以当收益读。
3. **过匹配组与边界组是「不得命中」口径**：吐出任何条目就算红。这两组的红多数是设计上的对抗题。
4. 改动前那一档缺**返回正文**（入库时为瘦身剔掉了）⇒ 它只进判定表，**不进档位闸**。

## 一、五条缺口验收（本次的主问题）

**逐条报**：一条 case 不中不等于整条缺口没补 —— 所以下表一行一个 case，缺口级结论看「小计」。

| 缺口 | case | 判据 | 改动前 | 改动后 | 结论 |
|---|---|---|---|---|---|
| 缺口 1 · 空查询不再冒充「命中」 | `KWJ1` | match_mode 必须占一个独立值 blank（以前报 keyword） | ❌ | ✅ | **补上** |
| 缺口 1 · 空查询不再冒充「命中」 | `KWJ2` | 纯空格同上 | ❌ | ✅ | **补上** |
| 缺口 1 · 空查询不再冒充「命中」 | **小计** | 2 条 | 0/2 | **2/2** | **全部补上** |
| 缺口 2 · 全角→半角折叠 | `KWG5` | 必须命中原条目 —— 半角写法本来就中，差的只有这一层 | ❌ | ✅ | **补上** |
| 缺口 2 · 全角→半角折叠 | **小计** | 1 条 | 0/1 | **1/1** | **全部补上** |
| 缺口 3 · 形近词不再抢走错条目 | `KWC2` | 命中原条目（繁体问法） | ❌ | ✅ | **补上** |
| 缺口 3 · 形近词不再抢走错条目 | `KWA5` | 至少不许再吐出无关条目 | ❌ | ❌ | **没补上** |
| 缺口 3 · 形近词不再抢走错条目 | **小计** | 2 条 | 0/2 | **1/2** | **1 条未关** |
| 缺口 4 · 单字查询不给语义腿 | `KWI11` | 挂语义腿也不得吐出任何条目（以前命中 items-mule） | ❌ | ✅ | **补上** |
| 缺口 4 · 单字查询不给语义腿 | **小计** | 1 条 | 0/1 | **1/1** | **全部补上** |
| 缺口 5 · 内部标识/兜底词不进索引 | `KWI6` | `doc` 不得命中（以前 448） | ❌ | ✅ | **补上** |
| 缺口 5 · 内部标识/兜底词不进索引 | `KWI17` | `geography` 不得命中（以前 408） | ❌ | ✅ | **补上** |
| 缺口 5 · 内部标识/兜底词不进索引 | `KWI9` | `castle_village` 不得命中（以前 131） | ❌ | ✅ | **补上** |
| 缺口 5 · 内部标识/兜底词不进索引 | `KWI18` | `entry` 不得命中 | ❌ | ✅ | **补上** |
| 缺口 5 · 内部标识/兜底词不进索引 | `KWI16` | `economy` 不得命中（以前 21） | ❌ | ❌ | **没补上** |
| 缺口 5 · 内部标识/兜底词不进索引 | **小计** | 5 条 | 0/5 | **4/5** | **1 条未关** |

> ⚠️ **别把「缺口 1 已补」与 `KWJ1`/`KWJ2` 在 §二/§五 里仍是红看成矛盾。** 缺口 1 的原话是「空查询兜底全库**且** matchMode 谎报」——上一版报告自己就写了「**兜底本身可以是有意的**（玩家问『你知道些什么』），可真缺口是 `matchMode` 从不报兜底」。所以本次补的是**后半截**：`matchMode` 现在占一个独立值 `blank`（以前不挂语义报 `keyword`、挂上报 `hybrid`，两种读起来都像「检索命中了」），上游从此分得清「检索命中」和「兜底给了全库」。**兜底返回全库照旧**，而 §二/§五 的「边界组＝不得命中」口径没变 ⇒ 这两条按**组口径**仍计红，属口径问题，不是未修。

### 没关掉的那几条，逐条写明成因（不写成「待办」两字糊过去）

- **`KWA5`（缺口 3 · 形近词不再抢走错条目）**：字面两条腿已归零（kw=0，term 过滤后 0 候选），残留来自**语义腿**。而语义腿带不出相似度（`RagHit` 只有 `Rank`，框架协议里没有分数）⇒ 没有可卡的阈值。要关掉得先在 RAG 契约里开放相似度，属框架级改动。
- **`KWI16`（缺口 5 · 内部标识/兜底词不进索引）**：关键词侧已归零；残留来自**综述正文**：`geography.mines-lycaron` 的 summary 里写着「承 IMPL §3.1 留痕：economy 档承载一条政治沿革」—— 内部记账文字进了正文，而 term 索引是从标题＋综述建的 ⇒ **数据问题，不是检索规则问题**。改它要动世界书源档并重编译（编译器那份 K1 修正目前还没入库，属另一条线）。

逐条明细：

- **缺口 1 · 空查询不再冒充「命中」**
  - `KWJ1` 「」：改动前 `partial` → 改动后 `partial`（mode=blank，通道 kw=-1 term=-1），命中 tales-charas-origins、tales-dawn-taboo、tales-husn-fulq
  - `KWJ2` 「   」：改动前 `partial` → 改动后 `partial`（mode=blank，通道 kw=-1 term=-1），命中 tales-charas-origins、tales-dawn-taboo、tales-husn-fulq
- **缺口 2 · 全角→半角折叠**
  - `KWG5` 「Closed Warlord Helme」：改动前 `not_found` → 改动后 `partial`（mode=keyword，通道 kw=1 term=0），命中 items-sturgian_helmet_closed
- **缺口 3 · 形近词不再抢走错条目**
  - `KWC2` 「拉迈萨」：改动前 `partial` → 改动后 `partial`（mode=hybrid，通道 kw=1 term=3），命中 villages-lamesa、castles-shibal-zumr-castle、villages-drapand
  - `KWA5` 「斯特基亚」：改动前 `partial` → 改动后 `partial`（mode=semantic，通道 kw=0 term=2），命中 towns-iyakis、castles-ustokol-castle、towns-diathma
- **缺口 4 · 单字查询不给语义腿**
  - `KWI11` 「货」：改动前 `partial` → 改动后 `not_found`（mode=identity，通道 kw=0 term=0），命中 —
- **缺口 5 · 内部标识/兜底词不进索引**
  - `KWI6` 「doc」：改动前 `known` → 改动后 `not_found`（mode=identity，通道 kw=0 term=0），命中 —
  - `KWI17` 「geography」：改动前 `partial` → 改动后 `not_found`（mode=identity，通道 kw=0 term=0），命中 —
  - `KWI9` 「castle_village」：改动前 `partial` → 改动后 `not_found`（mode=identity，通道 kw=0 term=0），命中 —
  - `KWI18` 「entry」：改动前 `partial` → 改动后 `not_found`（mode=identity，通道 kw=0 term=0），命中 —
  - `KWI16` 「economy」：改动前 `partial` → 改动后 `partial`（mode=keyword，通道 kw=0 term=1），命中 mines-lycaron

## 二、总账（同一批 68 条 × 四档）

| 档 | RED/已判 | 仪器没跑到 | MISS-RISK 红 | OVER-RISK 红 |
|---|---|---|---|---|
| 改动前·不挂语义腿 | **31/68** | 0 | 11/41 | 20/27 |
| 改动前·挂语义腿 | **24/68** | 0 | 3/41 | 21/27 |
| 改动后·不挂语义腿 | **25/68** | 0 | 9/41 | 16/27 |
| 改动后·挂语义腿（上线形态） | **17/68** | 0 | 1/41 | 16/27 |

### 按组（改动前·挂 → 改动后·挂）

| 组 | 改动前 | 改动后 |
|---|---|---|
| 错别字 | 1/5 | 1/5 |
| 同音 | 0/2 | 0/2 |
| 繁体 | 1/4 | 0/4 |
| 空格 | 0/4 | 0/4 |
| 标点 | 0/7 | 0/7 |
| 简写 | 0/5 | 0/5 |
| 大小写 | 0/4 | 0/4 |
| 全半角 | 1/1 | 0/1 |
| 整句 | 0/5 | 0/5 |
| 过匹配 | 18/18 | 13/18 |
| 边界 | 3/5 | 3/5 |
| 权限档位 | 0/8 | 0/8 |

### 红转绿 / 绿转红（改动前·挂 → 改动后·挂）

- **红转绿 7 条（收益）**
  - `拉邁薩`（繁体）—— 命中错条目
  - `Ｃｌｏｓｅｄ Ｗａｒｌｏｒｄ Ｈｅｌｍｅｔ`（全半角）—— 漏报
  - `doc`（过匹配）—— 过匹配
  - `castle_village`（过匹配）—— 过匹配
  - `货`（过匹配）—— 过匹配
  - `geography`（过匹配）—— 过匹配
  - `entry`（过匹配）—— 过匹配
- **绿转红 0 条（代价）**
  - （无）

## 三、档位闸（细档有没有漏给低档身份）

判法：某档独有的正文，只许出现在 detail 上限达到该档的身份那里。**一次都没出现＝空转**，不能当证据。

### 改动前·不挂语义腿 —— **跳过**（这一档没有返回正文，闸不成立）

### 改动前·挂语义腿 —— **跳过**（这一档没有返回正文，闸不成立）

### 改动后·不挂语义腿

- 「毛皮基准价 400」（detail 档独有）出现在：`KWC4`×profile.noble、`KWE7`×profile.noble、`KWG4`×profile.noble、`KWH2`×profile.noble、`KWH4`×profile.noble、`KWI10`×profile.noble、`KWI12`×profile.noble、`KWJ1`×profile.noble、`KWJ2`×profile.noble、`KWJ5`×profile.noble
  - ⇒ 未出现在任何低档身份
- 「操办科尔坦家的图谋」（secret 档独有）出现在：`KWK1`×profile.noble、`KWK2`×profile.noble
  - ⇒ 未出现在任何低档身份

### 改动后·挂语义腿（上线形态）

- 「毛皮基准价 400」（detail 档独有）出现在：`KWA4`×profile.noble、`KWC4`×profile.noble、`KWE4`×profile.noble、`KWE7`×profile.noble、`KWF5`×profile.noble、`KWG4`×profile.noble、`KWH2`×profile.noble、`KWH4`×profile.noble、`KWI10`×profile.noble、`KWI12`×profile.noble、`KWJ1`×profile.noble、`KWJ2`×profile.noble、`KWJ5`×profile.noble
  - ⇒ 未出现在任何低档身份
- 「操办科尔坦家的图谋」（secret 档独有）出现在：`KWK1`×profile.noble、`KWK2`×profile.noble、`KWK3`×profile.noble
  - ⇒ 未出现在任何低档身份

## 四、逐条（改动前·挂 / 改动后·挂 / 改动后·不挂）

| # | 组 | 输入 | 期望 | 改动前 | 改动后 | 性质(前/后) | 改动后命中 | 通道 |
|---|---|---|---|---|---|---|---|---|
| KWA1 | 错别字 | `闭面军伐盔` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-western_plated_helmet | kw=0 term=2 mode=hybrid |
| KWA2 | 错别字 | `闭面军阀魁` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed | kw=0 term=3 mode=hybrid |
| KWA3 | 错别字 | `拉迈莎` | villages-lamesa | OK | OK | — | villages-lamesa, castles-medeni-castle | kw=0 term=1 mode=semantic |
| KWA4 | 错别字 | `毛匹` | goods-fur | OK | OK | — | goods-fur | kw=0 term=0 mode=semantic |
| KWA5 | 错别字 | `斯特基亚` | military-sturgia | RED | RED | 命中错条目 | towns-iyakis, castles-ustokol-castle | kw=0 term=2 mode=semantic |
| KWB1 | 同音 | `闭面军阀亏` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed | kw=0 term=3 mode=hybrid |
| KWB2 | 同音 | `拉买萨` | villages-lamesa | OK | OK | — | villages-lamesa, villages-sagora | kw=0 term=0 mode=semantic |
| KWC1 | 繁体 | `闭面军阀盔` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-western_plated_helmet | kw=1 term=5 mode=hybrid |
| KWC2 | 繁体 | `拉迈萨` | villages-lamesa | RED | OK | 命中错条目 / — | villages-lamesa, castles-shibal-zumr-castle | kw=1 term=3 mode=hybrid |
| KWC3 | 繁体 | `斯特吉亚军事力量` | military-sturgia | OK | OK | — | military-sturgia, military-khuzait | kw=1 term=8 mode=hybrid |
| KWC4 | 繁体 | `毛皮` | goods-fur | OK | OK | — | goods-fur, goods-deriat | kw=2 term=1 mode=hybrid |
| KWD1 | 空格 | `闭面 军阀盔` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-battania_warlord_bracers | kw=0 term=3 mode=hybrid |
| KWD2 | 空格 | `闭 面军 阀盔` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-western_plated_helmet | kw=0 term=2 mode=hybrid |
| KWD3 | 空格 | `闭面	军阀盔` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-battania_warlord_bracers | kw=0 term=3 mode=hybrid |
| KWD4 | 空格 | `拉 迈 萨` | villages-lamesa | OK | OK | — | villages-lamesa, villages-drapand | kw=0 term=0 mode=semantic |
| KWE1 | 标点 | `闭面,军阀盔` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-battania_warlord_bracers | kw=0 term=3 mode=hybrid |
| KWE2 | 标点 | `闭面-军阀盔` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-battania_warlord_bracers | kw=0 term=3 mode=hybrid |
| KWE3 | 标点 | `拉,迈萨` | villages-lamesa | OK | OK | — | villages-lamesa, castles-shibal-zumr-castle | kw=0 term=1 mode=hybrid |
| KWE4 | 标点 | `毛,皮` | goods-fur | OK | OK | — | goods-fur, goods-deriat | kw=0 term=0 mode=semantic |
| KWE5 | 标点 | `闭面军阀盔。` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-western_plated_helmet | kw=1 term=5 mode=hybrid |
| KWE6 | 标点 | `,拉迈萨` | villages-lamesa | OK | OK | — | villages-lamesa, castles-shibal-zumr-castle | kw=1 term=3 mode=hybrid |
| KWE7 | 标点 | `毛皮?!` | goods-fur | OK | OK | — | goods-fur, goods-deriat | kw=1 term=1 mode=hybrid |
| KWF1 | 简写 | `闭面` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed | kw=1 term=1 mode=hybrid |
| KWF2 | 简写 | `盔阀军面闭` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-western_plated_helmet | kw=0 term=0 mode=semantic |
| KWF4 | 简写 | `拉迈` | villages-lamesa | OK | OK | — | villages-lamesa, castles-shibal-zumr-castle | kw=1 term=1 mode=hybrid |
| KWF5 | 简写 | `皮子` | goods-fur | OK | OK | — | goods-fur | kw=0 term=0 mode=semantic |
| KWF6 | 简写 | `斯特吉亚` | military-sturgia | OK | OK | — | military-sturgia, towns-tyal | kw=3 term=3 mode=hybrid |
| KWG1 | 大小写 | `closed warlord helmet` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed | kw=1 term=0 mode=keyword |
| KWG2 | 大小写 | `CLOSED WARLORD HELMET` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed | kw=1 term=0 mode=keyword |
| KWG3 | 大小写 | `lamesa` | villages-lamesa | OK | OK | — | castles-shibal-zumr-castle, villages-lamesa | kw=1 term=0 mode=keyword |
| KWG4 | 大小写 | `FUR` | goods-fur | OK | OK | — | goods-fur, villages-etirfurd | kw=3 term=0 mode=keyword |
| KWG5 | 全半角 | `Closed Warlord Helmet` | items-sturgian_helmet_closed | RED | OK | 漏报 / — | items-sturgian_helmet_closed | kw=1 term=0 mode=keyword |
| KWH1 | 整句 | `闭面军阀盔是啥玩意?` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-western_plated_helmet | kw=1 term=5 mode=hybrid |
| KWH2 | 整句 | `北方头领戴的那种连脸都罩住的盔是啥?` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, goods-fur | kw=0 term=4 mode=hybrid |
| KWH3 | 整句 | `阿塞莱最好的马在哪儿吃草?` | villages-lamesa | OK | OK | — | villages-lamesa, castles-barihal-castle | kw=0 term=8 mode=hybrid |
| KWH4 | 整句 | `北方有什么又暖又贵的皮货?` | goods-fur | OK | OK | — | goods-fur, goods-velvet | kw=0 term=2 mode=hybrid |
| KWH5 | 整句 | `盾墙加飞斧是哪个国家的打法?` | military-sturgia | OK | OK | — | military-sturgia, troops-royal-guard | kw=0 term=3 mode=hybrid |
| KWI1 | 过匹配 | `盔` | — | RED | RED | 过匹配 | items-sturgian_helmet_closed, items-western_plated_helmet | kw=2 term=0 mode=keyword |
| KWI2 | 过匹配 | `军阀` | — | RED | RED | 过匹配 | items-battania_warlord_bracers, items-sturgian_helmet_closed | kw=2 term=1 mode=hybrid |
| KWI3 | 过匹配 | `head` | — | RED | RED | 过匹配 | items-sturgian_helmet_closed, items-western_plated_helmet | kw=1 term=0 mode=keyword |
| KWI4 | 过匹配 | `HeadArmor` | — | RED | RED | 过匹配 | items-sturgian_helmet_closed, items-western_plated_helmet | kw=1 term=0 mode=keyword |
| KWI5 | 过匹配 | `sturgian` | — | RED | RED | 过匹配 | military-sturgia | kw=2 term=0 mode=keyword |
| KWI6 | 过匹配 | `doc` | — | RED | OK | 过匹配 / — | — | kw=0 term=0 mode=identity |
| KWI7 | 过匹配 | `村` | — | RED | RED | 过匹配 | villages-deriat | kw=2 term=0 mode=keyword |
| KWI8 | 过匹配 | `村庄` | — | RED | RED | 过匹配 | villages-deriat, villages-kvol | kw=1 term=1 mode=hybrid |
| KWI9 | 过匹配 | `castle_village` | — | RED | OK | 过匹配 / — | — | kw=0 term=0 mode=identity |
| KWI10 | 过匹配 | `皮` | — | RED | RED | 过匹配 | goods-deriat, goods-fur | kw=2 term=0 mode=keyword |
| KWI11 | 过匹配 | `货` | — | RED | OK | 过匹配 / — | — | kw=0 term=0 mode=identity |
| KWI12 | 过匹配 | `Goods` | — | RED | RED | 过匹配 | goods-fur, goods-salt | kw=1 term=0 mode=keyword |
| KWI13 | 过匹配 | `军事` | — | RED | RED | 过匹配 | military-sturgia, military-vlandia | kw=5 term=1 mode=hybrid |
| KWI14 | 过匹配 | `力量` | — | RED | RED | 过匹配 | military-sturgia, military-khuzait | kw=3 term=1 mode=hybrid |
| KWI15 | 过匹配 | `war` | — | RED | RED | 过匹配 | items-battania_warlord_bracers, military-sturgia | kw=4 term=0 mode=hybrid |
| KWI16 | 过匹配 | `economy` | — | RED | RED | 过匹配 | mines-lycaron | kw=0 term=1 mode=keyword |
| KWI17 | 过匹配 | `geography` | — | RED | OK | 过匹配 / — | — | kw=0 term=0 mode=identity |
| KWI18 | 过匹配 | `entry` | — | RED | OK | 过匹配 / — | — | kw=0 term=0 mode=identity |
| KWJ1 | 边界 | `(空)` | — | RED | RED | 过匹配 | tales-charas-origins, tales-dawn-taboo | kw=-1 term=-1 mode=blank |
| KWJ2 | 边界 | `   ` | — | RED | RED | 过匹配 | tales-charas-origins, tales-dawn-taboo | kw=-1 term=-1 mode=blank |
| KWJ3 | 边界 | `???` | — | OK | OK | — | — | kw=0 term=0 mode=identity |
| KWJ4 | 边界 | `zzzzzz` | — | OK | OK | — | — | kw=0 term=0 mode=identity |
| KWJ5 | 边界 | `doc.economy.goods-fur` | — | RED | RED | 过匹配 | goods-fur, goods-salt | kw=2 term=1 mode=keyword |
| KWK1 | 权限档位 | `科尔坦家的账` | clans-charas-cortain-secret | OK | OK | — | clans-charas-cortain-secret, territories-charas-reign | kw=3 term=6 mode=hybrid |
| KWK2 | 权限档位 | `戴·科尔坦家靠什么发财?` | clans-charas-cortain-secret | OK | OK | — | clans-charas-cortain-secret, territories-charas-reign | kw=2 term=3 mode=hybrid |
| KWK3 | 权限档位 | `科尔坦家的帐` | clans-charas-cortain-secret | OK | OK | — | territories-charas-reign, clans-charas-cortain-secret | kw=1 term=4 mode=hybrid |
| KWK4 | 权限档位 | `科尔坦家的账` | clans-charas-cortain-secret | OK | OK | — | villages-takor | kw=3 term=6 mode=hybrid |
| KWK5 | 权限档位 | `科尔坦家的帐` | clans-charas-cortain-secret | OK | OK | — | villages-takor | kw=1 term=4 mode=hybrid |
| KWK6 | 权限档位 | `戴·科尔坦家靠什么发财?` | clans-charas-cortain-secret | OK | OK | — | villages-takor | kw=2 term=3 mode=hybrid |
| KWK7 | 权限档位 | `沙拉斯` | clans-charas-cortain-secret | OK | OK | — | towns-charas, bays-charas | kw=8 term=3 mode=hybrid |
| KWK8 | 权限档位 | `毛皮值钱吗?` | goods-fur | OK | OK | — | goods-fur, goods-deriat | kw=1 term=1 mode=hybrid |

## 五、还剩下的（补完五条之后仍红）

- **错别字**：1 条
  - `斯特基亚` → 命中错条目（走的是兜底 term 通道，不是关键词通道）
- **过匹配**：13 条
  - `盔` → 过匹配
  - `军阀` → 过匹配
  - `head` → 过匹配
  - `HeadArmor` → 过匹配
  - `sturgian` → 过匹配
  - `村` → 过匹配
  - `村庄` → 过匹配
  - `皮` → 过匹配
  - `Goods` → 过匹配
  - `军事` → 过匹配
  - `力量` → 过匹配
  - `war` → 过匹配
  - `economy` → 过匹配（走的是兜底 term 通道，不是关键词通道）
- **边界**：3 条
  - `` → 过匹配
  - `   ` → 过匹配
  - `doc.economy.goods-fur` → 过匹配

## 六、这批没覆盖到的

1. **真人日志**：没有。样本按 keywords 设计造 ⇒ 只回答「这些形状会不会漏」。
2. **繁→简只做到「系统能折的那部分」**：走系统 `LCMapStringEx`（不造表），折不动就原样返回。`KWC2「拉邁薩」` 折对了；`斯特基亚` 那类字序差异（斯特 vs 斯提）不是繁简问题，折不了，残留走语义腿。
3. **语义腿带不出相似度**：`RagHit` 只有 `Rank`（`framework/.../StorageAndRagApi.cs:135`），协议里没有分数 ⇒ 无法对语义腿卡阈值。缺口 3 / 5 的残留都堵在这儿。
4. **多轮／指代**、**身份维只用了 2 个**（全覆盖在 `identity-gate` 那条门禁）、**性能**：都没测。

