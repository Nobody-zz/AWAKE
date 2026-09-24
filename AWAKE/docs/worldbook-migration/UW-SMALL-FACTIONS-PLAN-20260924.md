# 《小阵营》补料作业单（2026-09-24）

> **甲方裁定**：`甲。`（七变体**不拆散**，整条进《小阵营》一档）
> **甲方追问**：`开什么工，身份对应的代码搞清楚了吗？怎么做搞清楚了吗？`
> ⇒ 身份与机制已查明（`UW-RUMOR-MECHANICS-20260924.md`）；**本单是把料落到档上的具体作业**。

## 一、料：`rule_火焰余烬__火焰余烬.json`（B 类，6 变体，原文已逐条读过）

| 变体 | `When` | 真实说话人 | 原文关键句（连续子串，作引文用） |
|---|---|---|---|
| 0 | `Cultures:[empire]` | 帝国街坊 | 「北区卖陶罐的老瘸子就是他们的人」 |
| 1 | `Cultures:[empire]`+`Roles:[lord]` | 帝国领主 | 「多半是帝国的癣疥之疾，不是心腹之患」 |
| 2 | `Cultures:[empire]`+`KingdomIds:[embers_of_flame]` | **火焰余烬自己** | 「我们不是匪帮，我们是被遗忘者的工会」 |
| 3 | `Roles:[lord]` | 领主（不限文化） | 「那层千年教派的自我定位，是他们在这个灰产行当里唯一的信用担保」 |
| 4 | 空（**任何人**） | 路人普遍印象 | 「死了皇帝不埋，等着他回来」 |
| 5 | `Cultures:[empire]`+`KingdomIds:[hidden_hand]` | **秘密之手** | 「同业，但不是同行」 |

## 二、落法：**六变体 → 三条表达**（不是六条各占一层）

> 依据：`SelectExpression(entry, query, evaluation)` **按条目算，每档只送一条**
> ⇒ 同一档内多层互斥。**六变体要"不拆散"，就得按"说话人的层级"并成 3 条**，
> 让每个层级的人各听到**同一批人不同嘴里的同一件事**。

| 层 | 受众（能力上限） | 用哪个变体 | 口吻定位 |
|---|---|---|---|
| **rumor** | `villager` / `commoner`（local） | **变体 0** ＋ **变体 4** | 街面上看到的、听来的 |
| **summary** | `townsfolk`（regional） | **变体 2** ＋ **变体 5** | **局内人自述**（他们自己／同行） |
| **detail** | `notable` 及以上 | **变体 1** ＋ **变体 3** | 朝廷／外来者的分析口吻 |

### ★ 三条分层铁律（逐条守）

1. **每档只送一条** ⇒ **层与层受众必须互斥**（rumor 挂低能力、detail 挂高能力）。
2. **`rumor` 层只挂低能力身份**；`townsfolk` 若同时有 summary 层，会被**自动从 rumor 层摘掉**
   ⇒ `rumor` 层**不挂 townsfolk**。
3. **变体 2（他们自己）/ 变体 5（秘密之手）在系统里筛不出来**（无 `clan_ids`）
   ⇒ 靠**口吻**承担"谁在说"：用第一人称"我们"写，落到 **summary 层**（城里人听得到局内人的说法）。
   **不写 `kingdom_ids: embers_of_flame`（死条件）。**

## 三、grants 设计（照 `serfs` 样板的档位）

```
expr.small-factions-rumor   layer=rumor    → commoner(local) / villager(local)
expr.small-factions-summary layer=summary  → townsfolk(regional)
expr.small-factions-detail  layer=detail   → notable..noble（原样保留）
```

★ **是否加 `culture_ids`？** —— 《火焰余烬》六变体的 `When` 里**五条带 `Cultures:[empire]`**（只有变体 3/4 不限）
⇒ 这档的"主角"是**帝国地界的派系**，加 `culture_ids:[entity.culture.empire]` 是**贴料的**。
**但**：变体 3（领主不限文化）与变体 4（任何人）**不限文化** ⇒ 全档一刀切加 empire 会**把这两条也锁进帝国**，与料不符。

⇒ **决定：本档 `rumor` 层不加 `culture_ids`**（因为变体 4 是"任何人"的口吻，最该被所有人听到）；
**`summary` / `detail` 层维持现状（也不加）**，理由同上——**料本身允许跨文化流传**。
（§五-③ 的 culture 近似**留给真正只该帝国人听的内容**，本档不适用。）

## 四、验收（每步都给读数）

1. 双写一致（WS ＋ AO 两目录 sha256 相同）
2. 全链编译（`geo1-v27-uw-flame`），`validation total=… error=0`
3. 文本门禁 `--pure`：110 问可达／压制 0／不达 0
4. **分层实测**：平民听 rumor 层、城镇居民听 summary 层、要人以上听 detail 层
5. **新旧对质**：新句进、旧句退、别档句 0
