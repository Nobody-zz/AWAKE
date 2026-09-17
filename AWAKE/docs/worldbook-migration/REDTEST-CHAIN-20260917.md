# 链路缺口红测（2026-09-17）

> 靶：今天的链路（448 条包，真实对话路径 `NpcDialogueService` → `WorldKnowledgeQueryService.FindCandidates`）。
> 探针：`worldbook-runtime-sim probe`，**真代码零替身**。样本 69 条，按目标条目**真实 keywords** 造。
> 两档：**不挂语义臂**（＝改动前）／**挂语义臂**（＝今天上线的合并形态）。

## 〇、口径与限制（先说清楚，免得被当成绩单读）

1. **这批样本是照着设计造出来的**（按目标条目的真实 keywords 造错别字/同音/繁简/空格/标点/简写/大小写/全半角/整句），**不是真人日志** ⇒ 只说明「哪些形状会漏」，**查全率／查准率都不能从这批数字里推**。
2. **两档跑的是同一批样本、同一个包、同一份真代码**，只差语义臂挂没挂 ⇒ 两档之差可以当收益／代价读。
3. **过匹配组与边界组是「不得命中」口径**：只要吐出条目就算红。所以这两组的红**多数是设计上的对抗题**，不是新故障——但两档之间**数量的变化**是真代价，必须一起报。
4. **本报告的两个数字曾经是假的**，原因是仪器不是产品（见下）。已修、已重跑，本版数字出自修好之后的探针。

### 仪器修了一处（平行实现分叉）

探针里原先挂着一份**手抄的**「身份 → scope/detail」平表。它与真件 `WorldbookIdentityCapabilityRules` 分叉：
手抄表把 `profile.noble` 封在 `detail`，而真件对 45 岁以上贵族给的是 `secret`。
后果：全包**唯一一条 secret 档正文**（`politics.clans-charas-cortain-secret`）在验台里永远取不到，「细档有没有漏给低档身份」这根闸整根空转。已删掉平表、改走真件，并补上入参回显（`player_text`）。

## 一、总数

| 档 | RED/已判 | 仪器没跑到 | MISS-RISK RED | OVER-RISK RED |
|---|---|---|---|---|
| 不挂语义臂 | **32/69** | 0 | 12/42 | 20/27 |
| 挂语义臂（上线形态） | **25/69** | 0 | 4/42 | 21/27 |

### 按组

| 组 | 不挂 | 挂上 |
|---|---|---|
| 错别字 | 2/5 | 1/5 |
| 同音 | 1/2 | 0/2 |
| 繁体 | 2/4 | 1/4 |
| 空格 | 1/4 | 0/4 |
| 标点 | 1/7 | 0/7 |
| 简写 | 3/6 | 1/6 |
| 大小写 | 0/4 | 0/4 |
| 全半角 | 1/1 | 1/1 |
| 整句 | 0/5 | 0/5 |
| 过匹配 | 17/18 | 18/18 |
| 边界 | 3/5 | 3/5 |
| 权限档位 | 1/8 | 0/8 |

## 二、两档翻转（只看 MISS-RISK）

- 挂上语义后 **由 RED 转 OK**：8 条（收益）
  - `毛匹`（错别字）—— OK
  - `拉买萨`（同音）—— OK
  - `閉面軍閥盔`（繁体）—— OK
  - `拉 迈 萨`（空格）—— OK
  - `毛，皮`（标点）—— OK
  - `盔阀军面闭`（简写）—— OK
  - `皮子`（简写）—— OK
  - `科尔坦家的帳`（权限档位）—— OK
- 挂上语义后 **由 OK 转 RED**：0 条（代价）
  - （无）

### 代价：过匹配面（判据同批同口径，只比较数量）

| | 不挂 | 挂上 | 差 |
|---|---|---|---|
| 过匹配＋边界 红 | 23 | 24 | +1 |
| 过匹配组 红 | 20/27 | 21/27 | +1 |

⇒ **收益与代价必须一起读**：MISS-RISK 少了 8 条，过匹配面多了/少了 1 条。

## 三、档位闸（脏输入下，细档有没有漏给低档身份）

判法：某档独有的正文，只许出现在 detail 上限达到该档的身份那里。
**一条闸如果一次都没出现＝空转**，那说明这一档的正文根本没被取到过——「没泄漏」这时不能读成「闸有效」。

### 不挂语义臂

- 「毛皮基准价 400」（detail 档独有，出自 `economy.goods-fur`）出现在：`KWC4`×profile.noble、`KWE7`×profile.noble、`KWG4`×profile.noble、`KWH2`×profile.noble、`KWH4`×profile.noble、`KWI10`×profile.noble、`KWI12`×profile.noble、`KWI16`×profile.noble、`KWJ1`×profile.noble、`KWJ2`×profile.noble、`KWJ5`×profile.noble
  - ⇒ 未出现在任何低档身份
  - 低档对照（同条目的低档身份）：`KWK8`×profile.commoner(state=partial，拿到 2 条)
    - ⇒ 对照 1 条里有 **1 条拿到了条目、但没拿到该档正文** ⇒ 这是**强对照**：同一条目、同一个问题，低档身份拿得到条目却拿不到细档。
- 「操办科尔坦家的图谋」（secret 档独有，出自 `politics.clans-charas-cortain-secret`）出现在：`KWI6`×profile.noble、`KWK1`×profile.noble、`KWK2`×profile.noble
  - ⇒ 未出现在任何低档身份
  - 低档对照（同条目的低档身份）：`KWK4`×profile.commoner(state=not_found，拿到 0 条)、`KWK5`×profile.commoner(state=not_found，拿到 0 条)、`KWK6`×profile.commoner(state=not_found，拿到 0 条)、`KWK7`×profile.commoner(state=partial，拿到 2 条)
    - ⇒ 对照 4 条里有 **1 条拿到了条目、但没拿到该档正文** ⇒ 这是**强对照**：同一条目、同一个问题，低档身份拿得到条目却拿不到细档。

### 挂语义臂

- 「毛皮基准价 400」（detail 档独有，出自 `economy.goods-fur`）出现在：`KWA4`×profile.noble、`KWC4`×profile.noble、`KWE4`×profile.noble、`KWE7`×profile.noble、`KWF5`×profile.noble、`KWG4`×profile.noble、`KWH2`×profile.noble、`KWH4`×profile.noble、`KWI10`×profile.noble、`KWI12`×profile.noble、`KWI16`×profile.noble、`KWJ1`×profile.noble、`KWJ2`×profile.noble、`KWJ5`×profile.noble
  - ⇒ 未出现在任何低档身份
  - 低档对照（同条目的低档身份）：`KWK8`×profile.commoner(state=partial，拿到 3 条)
    - ⇒ 对照 1 条里有 **1 条拿到了条目、但没拿到该档正文** ⇒ 这是**强对照**：同一条目、同一个问题，低档身份拿得到条目却拿不到细档。
- 「操办科尔坦家的图谋」（secret 档独有，出自 `politics.clans-charas-cortain-secret`）出现在：`KWI6`×profile.noble、`KWK1`×profile.noble、`KWK2`×profile.noble、`KWK3`×profile.noble
  - ⇒ 未出现在任何低档身份
  - 低档对照（同条目的低档身份）：`KWK4`×profile.commoner(state=partial，拿到 1 条)、`KWK5`×profile.commoner(state=not_found，拿到 0 条)、`KWK6`×profile.commoner(state=partial，拿到 1 条)、`KWK7`×profile.commoner(state=partial，拿到 2 条)
    - ⇒ 对照 4 条里有 **3 条拿到了条目、但没拿到该档正文** ⇒ 这是**强对照**：同一条目、同一个问题，低档身份拿得到条目却拿不到细档。

## 四、逐条

| # | 组 | 输入 | 期望 | 不挂语义 | 挂语义 | 性质(不挂/挂上) | 不挂命中 | 挂上命中 |
|---|---|---|---|---|---|---|---|---|
| KWA1 | 错别字 | `闭面军伐盔` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed | items-sturgian_helmet_closed, items-western_plated_helmet |
| KWA2 | 错别字 | `闭面军阀魁` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-battania_warlord_boots, items-battania_warlord_bracers | items-sturgian_helmet_closed, items-battania_warlord_boots, items-battania_warlord_bracers |
| KWA3 | 错别字 | `拉迈莎` | villages-lamesa | OK | OK | — | castles-shibal-zumr-castle, villages-lamesa | villages-lamesa, castles-shibal-zumr-castle, castles-medeni-castle |
| KWA4 | 错别字 | `毛匹` | goods-fur | RED | OK | 漏报 / — | — | goods-fur |
| KWA5 | 错别字 | `斯特基亚` | military-sturgia | RED | RED | 命中错条目 | tales-kachar-rulership, items-heavy_round_shield, items-sturgian_helmet_closed | tales-kachar-rulership, towns-iyakis, items-heavy_round_shield |
| KWB1 | 同音 | `闭面军阀亏` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-battania_warlord_boots, items-battania_warlord_bracers | items-sturgian_helmet_closed, items-battania_warlord_boots, items-battania_warlord_bracers |
| KWB2 | 同音 | `拉买萨` | villages-lamesa | RED | OK | 漏报 / — | — | villages-lamesa, villages-sagora, villages-lavenia |
| KWC1 | 繁体 | `閉面軍閥盔` | items-sturgian_helmet_closed | RED | OK | 漏报 / — | — | items-sturgian_helmet_closed, items-western_plated_helmet, items-battania_mercenary_armor |
| KWC2 | 繁体 | `拉邁薩` | villages-lamesa | RED | RED | 漏报 / 命中错条目 | — | villages-lavenia, villages-drapand, castles-lavenia-castle |
| KWC3 | 繁体 | `斯特吉亞軍事力量` | military-sturgia | OK | OK | — | military-sturgia, tales-kachar-rulership, items-heavy_round_shield | military-sturgia, tales-kachar-rulership, military-khuzait |
| KWC4 | 繁体 | `毛皮` | goods-fur | OK | OK | — | goods-fur, goods-deriat | goods-fur, goods-deriat |
| KWD1 | 空格 | `闭面 军阀盔` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-battania_warlord_boots, items-battania_warlord_bracers | items-sturgian_helmet_closed, items-battania_warlord_bracers, items-battania_warlord_boots |
| KWD2 | 空格 | `闭 面军 阀盔` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed | items-sturgian_helmet_closed, items-western_plated_helmet, items-battania_warlord_bracers |
| KWD3 | 空格 | `闭面	军阀盔` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-battania_warlord_boots, items-battania_warlord_bracers | items-sturgian_helmet_closed, items-battania_warlord_bracers, items-battania_warlord_boots |
| KWD4 | 空格 | `拉 迈 萨` | villages-lamesa | RED | OK | 漏报 / — | — | villages-lamesa, villages-drapand, towns-saneopa |
| KWE1 | 标点 | `闭面，军阀盔` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-battania_warlord_boots, items-battania_warlord_bracers | items-sturgian_helmet_closed, items-battania_warlord_bracers, items-battania_warlord_boots |
| KWE2 | 标点 | `闭面-军阀盔` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, items-battania_warlord_boots, items-battania_warlord_bracers | items-sturgian_helmet_closed, items-battania_warlord_bracers, items-battania_warlord_boots |
| KWE3 | 标点 | `拉，迈萨` | villages-lamesa | OK | OK | — | castles-shibal-zumr-castle, villages-lamesa | villages-lamesa, castles-shibal-zumr-castle, villages-drapand |
| KWE4 | 标点 | `毛，皮` | goods-fur | RED | OK | 漏报 / — | — | goods-fur, goods-deriat, goods-velvet |
| KWE5 | 标点 | `闭面军阀盔。` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed | items-sturgian_helmet_closed, items-western_plated_helmet, items-battania_warlord_bracers |
| KWE6 | 标点 | `，拉迈萨` | villages-lamesa | OK | OK | — | villages-lamesa, castles-shibal-zumr-castle | villages-lamesa, castles-shibal-zumr-castle, towns-saneopa |
| KWE7 | 标点 | `毛皮？！` | goods-fur | OK | OK | — | goods-fur, goods-deriat | goods-fur, goods-deriat |
| KWF1 | 简写 | `闭面` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed | items-sturgian_helmet_closed |
| KWF2 | 简写 | `盔阀军面闭` | items-sturgian_helmet_closed | RED | OK | 漏报 / — | — | items-sturgian_helmet_closed, items-western_plated_helmet, items-battania_warlord_bracers |
| KWF3 | 简写 | `bmjfk` | items-sturgian_helmet_closed | RED | RED | 漏报 | — | — |
| KWF4 | 简写 | `拉迈` | villages-lamesa | OK | OK | — | villages-lamesa, castles-shibal-zumr-castle | villages-lamesa, castles-shibal-zumr-castle, towns-sanala |
| KWF5 | 简写 | `皮子` | goods-fur | RED | OK | 漏报 / — | — | goods-fur |
| KWF6 | 简写 | `斯特吉亚` | military-sturgia | OK | OK | — | military-sturgia | military-sturgia, towns-tyal, towns-iyakis |
| KWG1 | 大小写 | `closed warlord helmet` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed | items-sturgian_helmet_closed |
| KWG2 | 大小写 | `CLOSED WARLORD HELMET` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed | items-sturgian_helmet_closed |
| KWG3 | 大小写 | `lamesa` | villages-lamesa | OK | OK | — | castles-shibal-zumr-castle, villages-lamesa | castles-shibal-zumr-castle, villages-lamesa |
| KWG4 | 大小写 | `FUR` | goods-fur | OK | OK | — | goods-fur, villages-etirfurd, villages-furbec | goods-fur, villages-etirfurd, villages-furbec |
| KWG5 | 全半角 | `Ｃｌｏｓｅｄ Ｗａｒｌｏｒｄ Ｈｅｌｍｅｔ` | items-sturgian_helmet_closed | RED | RED | 漏报 | — | — |
| KWH1 | 整句 | `闭面军阀盔是啥玩意？` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed | items-sturgian_helmet_closed, items-western_plated_helmet, items-battania_warlord_bracers |
| KWH2 | 整句 | `北方头领戴的那种连脸都罩住的盔是啥？` | items-sturgian_helmet_closed | OK | OK | — | items-sturgian_helmet_closed, goods-fur, items-western_plated_helmet | items-sturgian_helmet_closed, goods-fur, items-western_plated_helmet |
| KWH3 | 整句 | `阿塞莱最好的马在哪儿吃草？` | villages-lamesa | OK | OK | — | villages-lamesa, castles-barihal-castle, castles-sahel-castle | villages-lamesa, castles-barihal-castle, villages-hoqqa |
| KWH4 | 整句 | `北方有什么又暖又贵的皮货？` | goods-fur | OK | OK | — | goods-fur, goods-velvet, castles-kaysar-castle | goods-fur, goods-velvet, castles-kaysar-castle |
| KWH5 | 整句 | `盾墙加飞斧是哪个国家的打法？` | military-sturgia | OK | OK | — | military-sturgia, troops-royal-guard, goods-velvet | military-sturgia, troops-royal-guard, items-heavy_round_shield |
| KWI1 | 过匹配 | `盔` | — | RED | RED | 过匹配 | items-sturgian_helmet_closed, items-western_plated_helmet | items-sturgian_helmet_closed, items-western_plated_helmet, items-scale_shoulder_armor |
| KWI2 | 过匹配 | `军阀` | — | RED | RED | 过匹配 | items-battania_warlord_bracers, items-sturgian_helmet_closed | items-battania_warlord_bracers, items-sturgian_helmet_closed, military-sturgia |
| KWI3 | 过匹配 | `head` | — | RED | RED | 过匹配 | items-sturgian_helmet_closed, items-western_plated_helmet | items-sturgian_helmet_closed, items-western_plated_helmet |
| KWI4 | 过匹配 | `HeadArmor` | — | RED | RED | 过匹配 | items-sturgian_helmet_closed, items-western_plated_helmet | items-sturgian_helmet_closed, items-western_plated_helmet |
| KWI5 | 过匹配 | `sturgian` | — | RED | RED | 过匹配 | items-sturgian_helmet_closed, military-sturgia | items-sturgian_helmet_closed, military-sturgia |
| KWI6 | 过匹配 | `doc` | — | RED | RED | 过匹配 | items-battania_mercenary_armor, items-battania_warlord_bracers, territories-dawn-stewardship | items-battania_mercenary_armor, items-battania_warlord_bracers, territories-dawn-stewardship |
| KWI7 | 过匹配 | `村` | — | RED | RED | 过匹配 | villages-deriat, villages-ab-comer, villages-abba | villages-hunab, villages-neocorys, villages-ov |
| KWI8 | 过匹配 | `村庄` | — | RED | RED | 过匹配 | villages-ab-comer, villages-abba, villages-abghan | villages-kvol, villages-larnac, villages-neocorys |
| KWI9 | 过匹配 | `castle_village` | — | RED | RED | 过匹配 | villages-aeoria, villages-agalmon, villages-amycon | villages-aeoria, villages-agalmon, villages-amycon |
| KWI10 | 过匹配 | `皮` | — | RED | RED | 过匹配 | goods-deriat, goods-fur | goods-deriat, goods-fur |
| KWI11 | 过匹配 | `货` | — | OK | RED | — / 过匹配 | — | items-mule |
| KWI12 | 过匹配 | `Goods` | — | RED | RED | 过匹配 | goods-fur, goods-salt, goods-silver | goods-fur, goods-salt, goods-silver |
| KWI13 | 过匹配 | `军事` | — | RED | RED | 过匹配 | military-sturgia, military-vlandia, military-khuzait | military-sturgia, military-vlandia, military-khuzait |
| KWI14 | 过匹配 | `力量` | — | RED | RED | 过匹配 | military-sturgia, military-vlandia, military-khuzait | military-sturgia, military-khuzait, military-vlandia |
| KWI15 | 过匹配 | `war` | — | RED | RED | 过匹配 | items-battania_warlord_bracers, territories-dawn-stewardship, items-battania_warlord_boots | military-sturgia, military-khuzait, items-battania_warlord_bracers |
| KWI16 | 过匹配 | `economy` | — | RED | RED | 过匹配 | items-battania_mercenary_armor, items-battania_warlord_bracers, items-battania_warlord_boots | items-battania_mercenary_armor, items-battania_warlord_bracers, items-battania_warlord_boots |
| KWI17 | 过匹配 | `geography` | — | RED | RED | 过匹配 | castles-oristocorys-castle, castles-shibal-zumr-castle, castles-llanoc-hen-castle | castles-oristocorys-castle, castles-shibal-zumr-castle, castles-llanoc-hen-castle |
| KWI18 | 过匹配 | `entry` | — | RED | RED | 过匹配 | throne-paravenos | throne-paravenos |
| KWJ1 | 边界 | `(空)` | — | RED | RED | 过匹配 | tales-charas-origins, tales-dawn-taboo, tales-husn-fulq | tales-charas-origins, tales-dawn-taboo, tales-husn-fulq |
| KWJ2 | 边界 | `   ` | — | RED | RED | 过匹配 | tales-charas-origins, tales-dawn-taboo, tales-husn-fulq | tales-charas-origins, tales-dawn-taboo, tales-husn-fulq |
| KWJ3 | 边界 | `？？？` | — | OK | OK | — | — | — |
| KWJ4 | 边界 | `zzzzzz` | — | OK | OK | — | — | — |
| KWJ5 | 边界 | `doc.economy.goods-fur` | — | RED | RED | 过匹配 | goods-fur, goods-salt, goods-silver | goods-fur, goods-salt, goods-silver |
| KWK1 | 权限档位 | `科尔坦家的账` | clans-charas-cortain-secret | OK | OK | — | clans-charas-cortain-secret, territories-charas-reign | clans-charas-cortain-secret, territories-charas-reign, villages-takor |
| KWK2 | 权限档位 | `戴·科尔坦家靠什么发财？` | clans-charas-cortain-secret | OK | OK | — | clans-charas-cortain-secret, territories-charas-reign | clans-charas-cortain-secret, territories-charas-reign, villages-takor |
| KWK3 | 权限档位 | `科尔坦家的帳` | clans-charas-cortain-secret | RED | OK | 命中错条目 / — | territories-charas-reign | territories-charas-reign, clans-charas-cortain-secret |
| KWK4 | 权限档位 | `科尔坦家的账` | clans-charas-cortain-secret | OK | OK | — | — | villages-takor |
| KWK5 | 权限档位 | `科尔坦家的帳` | clans-charas-cortain-secret | OK | OK | — | — | — |
| KWK6 | 权限档位 | `戴·科尔坦家靠什么发财？` | clans-charas-cortain-secret | OK | OK | — | — | villages-takor |
| KWK7 | 权限档位 | `沙拉斯` | clans-charas-cortain-secret | OK | OK | — | towns-charas, bays-charas | towns-charas, bays-charas |
| KWK8 | 权限档位 | `毛皮值钱吗？` | goods-fur | OK | OK | — | goods-fur, goods-deriat | goods-fur, goods-deriat, goods-velvet |

## 五、缺口清单（每条都挂 case 号，可倒着复核）

### 5.1 空查询会兜底返回全库
- `KWJ1` 输入 `''` ⇒ 不挂 `partial`／挂上 `partial`，命中 `tales-charas-origins、tales-dawn-taboo、tales-husn-fulq`
- `KWJ2` 输入 `'   '` ⇒ 不挂 `partial`／挂上 `partial`，命中 `tales-charas-origins、tales-dawn-taboo、tales-husn-fulq`
- 出处：`src/WorldKnowledgeQueryService.cs:272` —— `IsNullOrWhiteSpace(text)` 时**返回全库**，
  再靠身份闸筛、按字节预算截断。**兜底本身可以是有意的**（「你知道些什么」），
  可真缺口是 **`matchMode` 从不报「兜底」**（同文件 :69）：不挂语义时报 `keyword`、挂上时报 `hybrid`，
  两种读起来都像「检索命中了」⇒ 上游**分不清**「检索命中」和「兜底给了全库」，日志里看不出来。
  空串一旦因上游 bug 传进来，会**静默**返回一批看着像答案的条目。

### 5.2 全角英文没归一化（`KWG5`）
- `KWG5` 输入 `Ｃｌｏｓｅｄ Ｗａｒｌｏｒｄ Ｈｅｌｍｅｔ` ⇒ 两档都 `not_found`。
- 同一条的半角写法在 `KWG1`／`KWG2` 是 OK ⇒ 差的不是检索能力，是**缺一个全角→半角折叠**。

### 5.3 形近／变形词会把检索带到**形近的错条目**（比不给更糟）
- `KWA5` 输入 `斯特基亚` ⇒ 命中 `tales-kachar-rulership、towns-iyakis、items-heavy_round_shield`（不是 `military-sturgia`）
- `KWC2` 输入 `拉邁薩` ⇒ 命中 `villages-lavenia、villages-drapand、castles-lavenia-castle`（不是 `villages-lamesa`）
- 对设计初衷（谁知道）来说，**给错条目**比 `not_found` 更坏：模型会照着错条目的正文答。

### 5.4 语义臂的代价，具体到一条（`KWI11`）
- `KWI11` 输入 `货` ⇒ **不挂 `not_found`（对）／挂上 `partial`，命中 `items-mule`**
- 成因：向量近邻把泛词牵到了语义相近的条目。**这是今天上线换来的新增误召回，不是旧故障。**

### 5.5 内部标识符能当查询词用（18 条过匹配）
- 中文泛词：`盔`、`军阀`、`村`、`村庄`、`皮`、`货`、`军事`、`力量` —— 短词必泛，属可预期。
- **内部标识符**：`head`、`HeadArmor`、`sturgian`、`doc`、`castle_village`、`Goods`、`war`、`economy`、`geography`、`entry` —— 这些是命名空间／字段名／id 分片，真人不会这么说；
  它们进得了索引，是 `doc.<domain>.<slug>` 那类兜底关键词的代价（见 `RuntimePackageCompiler.cs:108-121` 的 K1 注释）。

### 5.6 权限面在脏输入下没破（正面结论）
- 权限档位组 `0/8` 红，且两条档位闸都没漏到低档身份 ⇒ **脏输入不会绕过身份闸**。
- 换言之：**变形输入能骗过「找哪条」，骗不过「给谁看」**。这两件事在链路上是分开的。

## 六、这批**没**覆盖到的（免得被当全覆盖）

1. **真人日志**：没有。样本是按 keywords 设计造的 ⇒ 只能回答「这些形状会不会漏」。
2. **多轮／上下文指代**：没测（「他」「那个」这类靠上文补全的问法）。
3. **身份维只用了 2 个**（64 条贵族 + 5 条平民）。12 个身份的全覆盖在 `identity-gate` 那条门禁里，不在这批。
4. **时效／到达时间**：没测（设计初衷第 2 层「何时知道」这条轴尚未接）。
5. **性能**：没测（语义臂的 IPC ＋ 编码开销）。

