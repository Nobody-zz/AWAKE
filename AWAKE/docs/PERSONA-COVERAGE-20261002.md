# 角色卡覆盖度实测（2026-10-02）

> 口径：**本机已安装的游戏数据**，不是第三方索引快照。
> 证据等级：E2（离线测量，可复跑）。

## 一、为什么要重测口径

角色卡专线一直带着「覆盖率拉满」这个目标，而工作区里同时存在两个互相矛盾的读数：

| 来源 | 英雄总数 | 缺卡 | 覆盖率 |
| --- | --- | --- | --- |
| BannerlordSage 索引（`bannerlord.db`） | 397 | 52 | 87.2% |
| 本机 `lords.xml`（v1.4.8） | 390 | **35** | **91.0%** |

两者都不是笔误：**Sage 库是 v1.3.15 时期建的，本机游戏是 v1.4.8**，版本不同、英雄集合不同。
按 `awake-recon` 的纪律「文档说的不算，跑出来的才算」，本文件一律以**本机游戏数据**为准，
并给出可复跑的测量工具 `AWAKE/tools/persona-card-coverage.py`。

## 二、读数（实测）

```
PERSONA_COVERAGE lords=390 cards=355 covered=355 missing=35
                 orphans=0 duplicates=0 coverage=91.0%
                 adults_missing=1 minors_missing=34 nameless_missing=1
                 anchor_ok=355 anchor_bad=0
```

- `lords.xml` 里 `lord_*` 形态的 id 共 **390** 个。
- `characters/` 下 `.origins.json` 侧车共 **355** 个，覆盖 **355** 个 lord id。
- **孤儿卡 0**、**重复 heroId 0** —— 355 个侧车与 355 张卡一一对应，没有一张卡指向不存在的英雄。
- **W4 身份锚 355/355 通过**：每张卡的 `sourceDescription` 都引用了自己的 heroId。
- 缺 **35** 个：**成年（≥18 岁）1 个，未成年 34 个**；其中 **1 个没有官方译名**。

## 三、缺的 35 个是谁（全表）

数据来源：`lords.xml`（年龄/性别/文化/嗓音/原生特质）+ `SandBox/ModuleData/heroes.xml`（家族/父母）+
`docs/mappings/character-names-zh-en.tsv`（官方中文名）。

| heroId | name_en | name_zh | 性别 | 年龄 | 文化 | 家族 | 父 | 母 | 嗓音 | 有官方名 | 原生人格特质 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| lord_1_13 | Arthon | — | 男 | 36 | empire | clan_empire_south_1 | — | — | earnest | **否** | — |
| lord_1_30_2 | Callinia | 卡尔利尼娅 | 女 | 3 | empire | clan_empire_south_4 | lord_1_30 | lord_1_30_1 | softspoken | 是 | — |
| lord_1_30_3 | Synesios | 叙涅修斯 | 男 | 2 | empire | clan_empire_south_4 | lord_1_30 | lord_1_30_1 | softspoken | 是 | — |
| lord_1_38 | Helea | 赫勒娅 | 女 | 17 | empire | clan_empire_south_2 | lord_1_15 | lord_1_16 | softspoken | 是 | — |
| lord_1_422 | Maurentios | 毛伦提俄斯 | 男 | 17 | empire | clan_empire_north_2 | lord_1_3 | lord_1_4 | softspoken | 是 | — |
| lord_1_47_2 | Casyrea | 卡绪雷娅 | 女 | 14 | empire | clan_empire_south_1 | lord_1_47 | lord_1_47_1 | softspoken | 是 | — |
| lord_1_47_3 | Colambea | 科拉谟柏娅 | 女 | 17 | empire | clan_empire_south_1 | lord_1_47 | lord_1_47_1 | softspoken | 是 | — |
| lord_1_48_2 | Anea | 阿涅娅 | 女 | 9 | empire | clan_empire_south_2 | lord_1_48 | lord_1_48_1 | curt | 是 | — |
| lord_1_48_3 | Nonesos | 诺涅索斯 | 女 | 11 | empire | clan_empire_south_2 | lord_1_48 | lord_1_48_1 | curt | 是 | — |
| lord_1_49_2 | Gordiana | 革耳狄安娜 | 女 | 17 | empire | clan_empire_south_4 | lord_1_49 | lord_1_49_1 | earnest | 是 | — |
| lord_1_52_2 | Megarita | 墨伽里塔 | 女 | 16 | empire | clan_empire_west_6 | lord_1_52 | lord_1_52_1 | earnest | 是 | — |
| lord_1_56_2 | Rustica | 卢斯提卡 | 女 | 17 | empire | clan_empire_south_4 | lord_1_56 | lord_1_56_1 | softspoken | 是 | — |
| lord_1_57_2 | Jephalia | 耶法利娅 | 女 | 2 | empire | clan_empire_west_5 | lord_1_57 | lord_1_57_1 | curt | 是 | — |
| lord_1_63_2 | Comatasa | 科马塔萨 | 女 | 12 | empire | clan_empire_south_5 | lord_1_63 | lord_1_63_1 | curt | 是 | — |
| lord_1_63_3 | Elidilea | 厄利狄勒娅 | 女 | 9 | empire | clan_empire_south_5 | lord_1_63 | lord_1_63_1 | curt | 是 | — |
| lord_1_69_2 | Dorathila | 多拉提拉 | 女 | 7 | empire | clan_empire_south_7 | lord_1_69 | lord_1_69_1 | softspoken | 是 | — |
| lord_2_10 | Valla | 瓦拉 | 女 | 17 | sturgia | clan_sturgia_1 | lord_2_1 | lord_2_2 | ironic | 是 | — |
| lord_2_13_3 | Luda | 卢达 | 女 | 17 | sturgia | clan_sturgia_1 | lord_2_13 | — | curt | 是 | — |
| lord_2_13_4 | Teta | 捷塔 | 女 | 15 | sturgia | clan_sturgia_1 | lord_2_13 | — | curt | 是 | — |
| lord_2_14_3 | Vizhduna | 维日杜娜 | 女 | 16 | sturgia | clan_sturgia_4 | lord_2_14 | lord_2_14_1 | earnest | 是 | — |
| lord_2_15_3 | Velina | 维琳娜 | 女 | 17 | sturgia | clan_sturgia_7 | lord_2_15 | — | softspoken | 是 | — |
| lord_3_13_2 | Razana | 拉扎纳 | 女 | 17 | aserai | clan_aserai_1 | lord_3_13 | lord_3_13_1 | curt | 是 | — |
| lord_3_15_2 | Bushila | 布希拉 | 女 | 5 | aserai | clan_aserai_4 | lord_3_15 | lord_3_15_1 | softspoken | 是 | — |
| lord_3_17_2 | Sanit | 萨妮特 | 女 | 6 | aserai | clan_aserai_5 | lord_3_17 | lord_3_17_1 | curt | 是 | — |
| lord_3_18_4 | Jalfar | 贾勒法 | 男 | 7 | aserai | clan_aserai_6 | lord_3_18 | — | ironic | 是 | — |
| lord_3_20_2 | Azina | 阿齐娜 | 女 | 16 | aserai | clan_aserai_3 | lord_3_20 | lord_3_20_1 | earnest | 是 | — |
| lord_3_22_3 | Zanuwa | 扎努瓦 | 女 | 17 | aserai | clan_aserai_8 | lord_3_22 | lord_3_22_1 | softspoken | 是 | — |
| lord_3_22_4 | Hajara | 哈贾拉 | 女 | 13 | aserai | clan_aserai_8 | lord_3_22 | lord_3_22_1 | softspoken | 是 | — |
| lord_4_24_4 | Irmgard | 伊姆加尔 | 女 | 17 | vlandia | clan_vlandia_8 | lord_4_24 | lord_4_24_1 | softspoken | 是 | — |
| lord_5_13_1 | Beasag | 贝阿莎格 | 女 | 17 | battania | clan_battania_1 | lord_5_13 | — | earnest | 是 | — |
| lord_5_15_3 | Diarbhain | 迪亚布海因 | 女 | 14 | battania | clan_battania_7 | lord_5_15 | — | ironic | 是 | — |
| lord_5_16_2 | Gawen | 加汶 | 女 | 14 | battania | clan_battania_5 | lord_5_16 | lord_5_16_1 | curt | 是 | — |
| lord_5_1_1 | Merag | 梅拉格 | 女 | 12 | battania | clan_battania_1 | lord_5_1 | — | ironic | 是 | — |
| lord_6_17_2 | Yesum | 也苏木 | 女 | 15 | khuzait | clan_khuzait_5 | lord_6_17 | lord_6_17_1 | softspoken | 是 | — |
| lord_NE8_c2 | Lucala | 卢卡拉 | 女 | 16 | empire | clan_empire_north_8 | lord_NE8_l | lord_NE8_s | softspoken | 是 | — |

年龄分布：`2:2  3:1  5:1  6:1  7:2  9:2  11:1  12:2  13:1  14:3  15:2  16:4  17:12  36:1`
性别分布：**女 33 / 男 2**（唯一的成年男性 Arthon、以及 7 岁的贾勒法）。

## 四、判定：这 35 个为什么不补

### 4.1 三十四个未成年（2–17 岁）

- 这 34 个在 `lords.xml` 里除了 `id` / 父母 / 年龄 / 装备 / `voice` 之外**一无所有**：
  `Traits` 全部为空 `{}`，没有一句官方生平、没有一次登场记录。
- 人格卡的第一人称叙事（`core` / `selfClaimRules` / `selfClaimExamples`）需要**可核验的性格材料**
  才能立住；对一个 2 岁的卡尔利尼娅、3 岁的叙涅修斯，任何「他在意什么、他绝不做什么」都是凭空创作。
  这与作者规范 W1（归属前置）与 W3（译名查表，表内无则标待核）所要求的可核验前提直接冲突。
- 结论：**不给幼童写第一人称人格卡**。缺口是诚实的，编出来的卡是假的。
- 例外说明：其中 **12 个已 17 岁、4 个 16 岁**，一两年内成年。如果将来要补，
  **应当从这批开始**（他们的 `voice` 与部分 `Traits` 已经存在），并且等游戏数据或官方汉化给出更多材料。

### 4.2 唯一的成年人 `lord_1_13`（Arthon，36 岁）

它是 35 个里**唯一一个成年、且唯一一个没有官方译名**的：

- `lords.xml` 里写的是裸字符串 `name="Arthon"`，**没有 `{=key}` 本地化键**；
- 在全部游戏 `Languages/*.xml` 里搜 `Arthon`，**零命中**（任何语言都没有）；
- BannerlordSage 库里**没有这一行** ⇒ 它是 v1.4.8 新增的英雄；
- 除 `lords.xml` 自身外，**没有任何文件引用它**（`ROT_Content` 与 `heroes.xml` 里也没有）。

其他信息：`clan_empire_south_1`（Pethros 家），配偶 `Hero.lord_1_14`（拉盖娅），
`Valor=1 Generosity=1 Calculating=-1`，`occupation="Lord"`。
`lords.xml` 里它的注释是 `<!-- Clan 7 consort. Charismatic, energetic but easily offended`（注释未闭合）。

按 W3「表内无则标待核，**禁止自造音译**」，**不给它编中文名、也不写卡**。
待官方或权威汉化给出译名后再补 —— 那时它会是 35 个里最该补的一个。

## 五、结论

- **可对话成年领主覆盖 = 355 / 356 = 99.7%。**
- 「覆盖率拉满」这个目标的**可达上限就是 99.7%**；要变成 100%，前置条件是
  先有一条 `Arthon` 的权威译名，而不是再写一张卡。
- 先前那个「还差 52 个领主」的说法，口径是 v1.3.15 的 Sage 快照，**应当作废**。

## 六、复现

```powershell
py -3 D:\AWAKE-Dev\AWAKE\tools\persona-card-coverage.py `
  --report D:\AWAKE-Dev\.tmp\coverage-report.txt
```

默认路径（可用参数覆盖）：

- `--lords`  `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\lords.xml`
- `--heroes` 默认取 `--lords` 同目录的 `heroes.xml`
- `--cards`  `D:\AWAKE-Dev\AWAKE\tools\persona-workbench\characters`
- `--names`  `D:\AWAKE-Dev\AWAKE\docs\mappings\character-names-zh-en.tsv`
- `--adult-age` 默认 18

stdout 只有一行 ASCII 汇总；完整中文表写入 `--report` 指定的 UTF-8 文件。
退出码：0 成功 / 2 路径缺失。

## 七、已知局限

1. **家族与父母来自 `heroes.xml`**，不是 `lords.xml`：多数领主的 `lords.xml` 块里没有
   `<Hero ... faction=...>` 元素（只有手写块如 `lord_1_13` 才有），儿童与部分领主的家族
   只在 `SandBox/ModuleData/heroes.xml` 里声明。工具两个源都读，取先命中者。
2. **成年阈值 18 是硬编码约定**，不是从游戏数据里读出来的；`lords.xml` 没有「成年」字段。
3. 本工具**只做覆盖度与锚定的测量，不判卡的内容质量**。内容质量由
   `AWAKE/tools/persona-card-gate.py`（19 条判据）与官方 6 道门禁链负责。
