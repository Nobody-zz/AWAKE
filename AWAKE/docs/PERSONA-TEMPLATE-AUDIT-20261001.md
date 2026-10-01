# 角色卡模板化 / AI 味 独立复算（2026-10-01）

标定线 = **git 是否跟踪**：已入库 76 张（人工过审）vs 未跟踪 279 张（批量生成）。

- 卡总数 **355**（已跟踪 76 / 未跟踪 279）
- 跨卡样板判据：某句在 **≥5 张卡**的边界字段（`selfClaimRules` + `realSelfBehaviors`）中出现

## 一、标签集合多样性

| 指标 | 好（已入库 76） | 坏（未跟踪 279） |
|---|---|---|
| 不同标签集合数 | **72** | **5** |
| 最大同集合组 | 4 张（5%） | **235 张（84%）** |

### 未跟踪批次里最大的那个标签集合（占 235 张）

```
behavior.calculating
expression.cold
trait.pragmatic
trait.ruthless
trigger.family_interest
```

### 已入库批次的前 8 个标签集合

| 张数 | 标签集合 |
|---|---|
| 4 | `behavior.conditional_cooperation, expression.indirect, expression.measured, trait.pragmatic, trait.status_conscious, trigger.threat_or_leverage` |
| 2 | `behavior.bargains, behavior.keeps_leverage, behavior.observes_before_acting, expression.indirect, expression.measured, trait.pragmatic, trigger.threat_or_leverage` |
| 1 | `behavior.observes_before_acting, expression.measured, expression.warm, trait.pragmatic, trait.traditional, trigger.family_safety` |
| 1 | `behavior.observes_before_acting, expression.warm, trait.cautious, trait.guardian, trait.kind, trigger.family_safety` |
| 1 | `behavior.administers_fairly, behavior.protects_inner_circle, expression.frank, expression.measured, trait.courageous, trait.guardian, trait.kind, trait.loyal, trigger.family_safety, trigger.loyalty_or_betrayal` |
| 1 | `behavior.acts_before_reasoning, behavior.charges_first, expression.flamboyant, expression.frank, expression.teasing, trait.courageous, trait.proud, trigger.public_humiliation, trigger.reputation_challenge` |
| 1 | `behavior.bargains, behavior.observes_before_acting, behavior.protects_inner_circle, expression.indirect, expression.measured, trait.cautious, trait.pragmatic, trigger.threat_or_leverage` |
| 1 | `behavior.keeps_leverage, behavior.takes_command, expression.blunt, trait.pragmatic, trait.status_conscious, trigger.threat_or_leverage` |

## 二、跨卡样板句负载（边界字段）

| 指标 | 好（已入库） | 坏（未跟踪） |
|---|---|---|
| 样板句负载 mean | **0.000** | **0.600** |
| 零样板卡数 | **76/76（100%）** | 17/279（6%） |

## 三、出现条数最多的样板句（前 25）

| 出现卡数 | 句子 |
|---|---|
| **247** | 与族中上下论事，先听后断，不独断而行 |
| **151** | 在外人面前少谈家事，多谈职守 |
| **111** | 提及家眷时语气温和，不吝赞许 |
| **15** | 逢年节必亲省骑士的誓言的存用，算无遗策 |
| **14** | 逢年节必亲省林间的猎场的存用，算无遗策 |
| **14** | 逢年节必亲省南土的存用，算无遗策 |
| **14** | 逢年节必亲省北疆的存用，算无遗策 |
| **13** | 逢年节必亲省领地的农庄的存用，算无遗策 |
| **13** | 逢年节必亲省族中的商路的存用，算无遗策 |
| **13** | 逢年节必亲省元老院的体面的存用，算无遗策 |
| **12** | 逢年节必亲省队商的驼队的存用，算无遗策 |
| **12** | 逢年节必亲省狄亚特马的田的存用，算无遗策 |
| **12** | 逢年节必亲省河上的渡口的存用，算无遗策 |
| **12** | 逢年节必亲省加里俄斯的征途的存用，算无遗策 |
| **11** | 逢年节必亲省牧场的存用，算无遗策 |
| **11** | 逢年节必亲省山间的石寨的存用，算无遗策 |
| **11** | 逢年节必亲省封邑的存用，算无遗策 |
| **10** | 逢年节必亲省雪原的存用，算无遗策 |
| **10** | 逢年节必亲省草场的泉水的存用，算无遗策 |
| **10** | 逢年节必亲省王汗的号令的存用，算无遗策 |
| **9** | 逢年节必亲省绿洲的泉眼的存用，算无遗策 |
| **9** | 逢年节必亲省族老的议事的存用，算无遗策 |
| **9** | 逢年节必亲省墨利翁的港的存用，算无遗策 |
| **8** | 逢年节必亲省托里俄斯的仓的存用，算无遗策 |
| **8** | 论及涅雷采斯家的事，先问利害再谈情分 |

## 四、每卡 tag 数分布（常数化 = 机器指纹）

| tag 数 | 好 | 坏 |
|---|---|---|
| 4 | 2 | 0 |
| 5 | 4 | 279 |
| 6 | 29 | 0 |
| 7 | 10 | 0 |
| 8 | 11 | 0 |
| 9 | 7 | 0 |
| 10 | 8 | 0 |
| 11 | 5 | 0 |

## 五、样本：未跟踪批次里样板负载最高的 15 张

| 卡片 | 样板句/总句 | tag 数 |
|---|---|---|
| `亚恰娜_Yachana_vezhoving_sturgia` | 4/5 (0.80) | 5 |
| `亚里翁_Arion_osticos_empire_n` | 4/5 (0.80) | 5 |
| `伊兹登卡_Izdenka_ormidoving_sturgia` | 4/5 (0.80) | 5 |
| `伊斯凡_Isvan_ormidoving_sturgia` | 4/5 (0.80) | 5 |
| `佐安娜_Zoana_osticos_empire_n` | 4/5 (0.80) | 5 |
| `佳斯卡_Tyaska_ormidoving_sturgia` | 4/5 (0.80) | 5 |
| `利利扎_Lilizha_gundaroving_sturgia` | 4/5 (0.80) | 5 |
| `博万_Bovan_vezhoving_sturgia` | 4/5 (0.80) | 5 |
| `厄吕斯_Elys_dey_meroc_vlandia` | 4/5 (0.80) | 5 |
| `厄奥狄西娅_Eodisia_neretzes_empire_n` | 4/5 (0.80) | 5 |
| `哈利娅_Chalia_neretzes_empire_n` | 4/5 (0.80) | 5 |
| `喀宋_Chason_neretzes_empire_n` | 4/5 (0.80) | 5 |
| `喀答_Khada_arkit_khuzait` | 4/5 (0.80) | 5 |
| `埃尔杜兰_Erdurand_dey_meroc_vlandia` | 4/5 (0.80) | 5 |
| `埃尔蓓_Elbet_dey_cortain_vlandia` | 4/5 (0.80) | 5 |
