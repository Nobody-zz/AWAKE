# AUDIT · 跨卡一致性核验（第二轮，2026-09-11）

范围：反思清单第 4 项指定的三对双卡关系 + 一项此前误判的更正。

---

## 一、加里俄斯 → 阿庇斯·瓦罗斯（西帝国）

**结论：官方确证，卡内叙述成立。** 此前"无游戏依据、建议移除"系误判，已更正。

- 官方英雄 `lord_1_9` **Apys Varros（阿庇斯·瓦罗斯）**，faction=`clan_empire_west_2`，spouse=`lord_1_10`。
- 官方文本（heroes key `xL95EfpQ`）：
  > Apys Varros was one of the most notoriously debauched, unscrupulous, and wealthy members of the old imperial Senate. But he made an early alliance with Garios ... Garios provided the fame, Apys provided the money.
- 即：阿庇斯是**旧帝国元老院中最富有、臭名昭著、放荡无礼的成员**，与加里俄斯**早年结盟**，加里俄斯供声望、阿庇斯供资金。
- garios 卡 `identityFacts`"早年与阿庇斯·瓦罗斯结为同盟"、`contradictionDescription`"他和阿庇斯·瓦罗斯的联盟实际上是一种新的精英联盟"——**两条均有官方文本支撑**，保留。
- 更正落点：`docs/mappings/persona-names-zh-en.tsv` 该条目改为准确 hero 数据（英文原拼 `Apys Varros`，非旧误 `Apil Varus`）；反思报告第 5 节同步更正。

## 二、朗瓦德 → 奥列克（斯特吉亚）

**结论：互证一致。**

| 朗瓦德卡（lord_2_1） | 奥列克卡（lord_2_3，clan_sturgia_2 库洛夫） |
|---|---|
| privateDescription：“他知道**奥列克等老贵族从未真正接受君主制**” | core：“对**朗瓦德的集权政策深恶痛绝**……不愿向王公低头” |
| core：“与那些**崇尚自主的波耶们**关系紧张” | summary：“坚信波耶自治，反对王公集权” |
| identityFacts：“与**库洛夫家势不两立**” | core：“宁死不屈”/“怨恨……半点消减” |

两卡在“集权王公 vs 自治波耶”这一张力上是同一事件的两侧，无双卡矛盾。

## 三、蒙楚格 → 墨速宜（库赛特）

**结论：互证一致。**

| 蒙楚格卡（lord_6_1，兀儿浑乃特） | 墨速宜卡（lord_6_4，库吉特/clan_khuzait_2） |
|---|---|
| core：“**库吉特等部族**至今仍觉得自己的牺牲没有得到应有的回报” | identityFacts：“在兀儿浑可汗征战中蒙受重大牺牲……土地亦遭其他部族觊觎” |
| core：“渴望的是一位更在意公正、而非更多荣耀的可汗” | identityFacts：“经常为小部族受到的不公待遇打抱不平” |
| identityFacts：“**库吉特领袖墨速宜**等对其心存不满” | core：“她深知当前汗国权力结构中的不公” |

两卡对“兀儿浑部连年征战 → 部族元气大伤 → 牺牲未获回报 → 不满”的因果链完全对齐。

---

## 汇总

| 关系 | 状态 | 证据 |
|---|---|---|
| 加里俄斯–阿庇斯·瓦罗斯 | ✅ 官方确证（含一处误判更正） | heroes `xL95EfpQ` / `lord_1_9` |
| 朗瓦德–奥列克 | ✅ 互证一致 | 两卡正文 |
| 蒙楚格–墨速宜 | ✅ 互证一致 | 两卡正文 |

跨卡一致性第二轮完成；唯一待修正项（阿庇斯标注）已更正。