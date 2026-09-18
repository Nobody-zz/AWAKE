# 世界书分类体系 v2（题材树＋类型轴）· 定稿过目

> 2026-09-14。本文取代 `WORLDBOOK-TAXONOMY-REDESIGN-20260914.md`（B 方案稿，作废留痕）。
> 经两轮红队测试与 Max 裁决，方向定为**路 X：两级正式分类（题材树）＋一级非正式类型轴（前缀）**。

## 一、模型定稿

- **子域（domain → subdomain）＝题材**：这条知识*关于什么*。住在 `knowledge-taxonomy.v1.json`，CAS 门控——改它＝重建包。**正式层。**
- **前缀（档名 token）＝文档类型**：这条词条*是什么东西*。住在档名规范词表——改它＝改档名，不动包。**非正式层。**
- **前缀不是 tags**：tags 多值、自由书写、软标注；前缀单值、封闭词表、嵌在 doc_id 里（`doc.<domain>.<prefix>-<slug>`），三样全反。
- 关系：**三级筛选、两轴数据**。名录按 类别→子域→前缀 逐层收窄（观感是三级）；但前缀跨域复用（person 挂政治/经济/文化皆可），不是任何一格的专属下级。
- 项目里真正的多值标注位是 `related_domains`（0 档在用），与本结构无关，需要时另行启用。

### 已确认的硬事实
- 运行时检索**不消费** taxonomy（`WorldKnowledgeQueryService.cs` 0 处引用 domain/subdomain）。taxonomy 服务三处：Studio 作者归档／编译产物携带／名录筛选。

## 二、taxonomy v2（正式层，43 格）

**以原设计 43 子域为底，仅 3 处改动**——原设计的第二层本来就是题材格，红队证实其干净；此前"拆 settlements／terrain／rivers"的 48 子域版回滚。

| 域 | 子域（题材格） | 改动 |
|---|---|---|
| politics (8) | throne 王位 · kingdoms 王国 · territories 领地 · offices 官职 · law 法律 · diplomacy 外交 · clans 家族 · succession 继承 | 无 |
| economy (10) | land_production 土地 · food 粮食 · trade 贸易 · taxation 税赋 · currency 货币 · workshops 工坊 · debt 债务 · trade_routes 商路 · items 器物 · goods 物产 | 无 |
| culture (8) | **faith 信仰** · customs 习俗 · language 语言 · identity 身份 · marriage 婚姻 · clothing 服饰 · festivals 节庆 · arts 艺术 | **faith 保留不删**（措辞微调，见下）；撤掉 48 版自造的 tale 格 |
| war (8) | war_history 战史 · **military 军制** · troops 兵种 · weapons 武器 · tactics 战术 · fortifications 要塞 · logistics 军需 · prisoners 俘虏 | military_system → **military**（Max 已裁） |
| geography (9) | terrain 地貌 · climate 气候 · directions 方位 · **waters 水域** · roads 道路 · settlements 聚落 · natural_boundaries 自然边界 · resources 资源 · sea_routes 航路 | rivers → **waters**（Max 已裁；措辞扩为「河流、湖泊、海湾、海域、渡口和水域通行」） |

**措辞微调两处**（均已在 48 版里写好，直接沿用）：
- `waters.help`：「河流、湖泊、海湾、海域、渡口和水域通行。」conflict_hints 增一条：「水域本体归这里；海上航行路线归航路（sea_routes）。」
- `faith.help`：「神祇、教义、仪式、宗教组织与民间怪谈。」（补"民间怪谈"四字，收编怪谈类词条；详见 §五）

版本：`taxonomy_version` 1.0.0 → **1.1.0**（内容有改，结构未变）。

## 三、前缀类型词表（非正式层，住档名规范）

**已用 21 个**（按 2026-09-14 实测登记，含本轮恢复 8 档原名后）：

| 类 | 前缀 | 数量 | 题材落点 |
|---|---|---|---|
| 聚落 | castle / village / town | 67 / 273 / 53 | geography.settlements |
| 地貌 | mountain / plateau / peninsula / desert | 4 / 1 / 1 / 1 | geography.terrain |
| 水域 | lake / river / bay / sea | 3 / 2 / 1 / 1 | geography.waters |
| 矿场 | mine | 1 | geography.resources |
| 器物 | items / goods | 15 / 6 | economy.items / economy.goods |
| 领地 | territories | 4 | politics.territories |
| 王权 | throne | 2 | politics.throne |
| 氏族 | clans | 1 | politics.clans |
| 传说(文体) | tale | 6 | culture.faith/customs/arts/identity（按内容分挂，§五） |
| 军制 | military | 3 | war.military |
| 兵种 | troops | 2 | war.troops |
| 武器 | weapons | 1 | war.weapons |

**预留（新增只改档名规范，不动包）**：person 人物 · org 组织（行会/骑士团/教会）· battle 战役 · site 地标场所（图书馆/竞技场/圣地/港口）· edict 敕令 · chronicle 编年史 · law 法条 · 及地貌/水域开放种（forest / island / strait / bridge / ford …）。

**登记规则**：新词条建档时前缀必须从本表取；要加新前缀，先登记进档名规范词表（`WORLDBOOK-FILENAME-NORMALIZATION-REPORT-20260913.md` §一），再建档。**这就是「下次做新词条时规范好分类标准」的执行点。**

## 四、448 档落点与改名账

**改名仅 8 档**（全部恢复 48 版之前的原名）：

| 现档名（48 版） | 恢复为 | 数量 |
|---|---|---|
| waters-lakonis / -lake-* | lake-* | 3 |
| waters-（河 2 档） | river-* | 2 |
| waters-（湾/海 2 档） | bay-* / sea-* | 2 |
| resources-lycaron | mine-lycaron | 1 |

**改 subdomain 字段 406 档**（档名不动）：

| 档群 | subdomain：48 版 → v2 | 数量 |
|---|---|---|
| castle-* / village-* / town-* | castle/village/town → **settlements** | 393 |
| mountain-* / plateau-* / peninsula-* / desert-* | 各自 → **terrain** | 7 |
| tale-* | tale → **faith/customs/arts/identity**（§五） | 6 |

**不动 42 档**（字段不变，其中 8 档仅恢复档名）：items 15 / goods 6 / territories 4 / clans 1 / throne 2 / troops 2 / weapons 1 / military 3 ＝ 34 档字段与档名均不动（military 格在 48 版已是新名）；waters 7 档与 mine 1 档的 subdomain 已是 waters/resources，只恢复档名。

**用户四条纠错全部吸收**：湖→waters ✓ 矿场→resources ✓ 毛皮→goods ✓ 湾/海→waters ✓。

## 五、tale 6 档分挂方案

| 档 | 题材判定 | 落点 |
|---|---|---|
| tale-charas-origin（先祖登陆传说＋船人怪谈） | 起源神话与怪谈，涉神异 | culture.**faith** |
| tale-dawn-taboo（黑魔法信仰与禁入边界） | 明写「信仰叙述」 | culture.**faith** |
| tale-lakonis-lake（湖色变红的两种来由） | 地方风物说法，不涉神系 | culture.**customs** |
| tale-lycaron-rock（秃鹫传言，本地与异邦版本） | 地方口传 | culture.**customs** |
| tale-husn-fulq（集市说书「鬼点子」） | 说书＝故事表演 | culture.**arts** |
| tale-kachar-three（三族对半岛易主的各自讲法） | 民族归属叙事 | culture.**identity** |

⇒ faith 实得 2 档（+措辞收编怪谈），customs 2、arts 1、identity 1。**faith 不是没内容，是内容一直被错标成「文体」。**

## 六、红队发现对照（逐条交代）

| 红队发现 | v2 处置 |
|---|---|
| 裂缝一：制度类无处安放 | 原设计本有 law／succession／taxation／currency 等制度题材格，保留即治 ✓ |
| 裂缝二：人物缺席 | 类型轴治愈：person 前缀＋按事迹题材挂格（拉盖娅→politics.throne），不开人物格 ✓ |
| 裂缝三：扩展成本放反 | 开放维度全在前缀层（forest/island/strait 加前缀不动包）；题材层封闭 43 格 ✓ |
| 破洞四：同物多面 | 类型×题材正交分档（weapon-crossbow→war.weapons／item-crossbow_c→economy.items 两档分离）＋conflict_hints ✓ |
| 破洞五：faith 删急了 | faith 保留，措辞收编怪谈，实得 2 档 ✓ |
| 破洞六：自造格没定义 | v2 全部沿用原设计措辞，仅 waters/faith 两处微调 ✓ |
| 破洞七：地标/组织无格 | 类型轴治愈：site/org 前缀＋按题材挂格（圣地→culture.faith、行会→economy.trade） ✓ |
| 组织格缺失（二轮） | 同上，org 前缀解决 ✓ |
| 打地鼠（二轮总发现） | 题材层回归原设计题材格，实体类型全归前缀轴，不再互塞 ✓ |

## 七、待 Max 拍板

1. **faith 恢复**：您之前说「没什么相关内容」想删——分挂后它有 2 档实内容＋将来圣地/教会词条。**确认保留？**
2. **tale-kachar-three 挂 identity**（三族讲法＝民族归属叙事），认可？
3. **前缀单复数混用**（castle 单数 vs items/clans 复数）：**建议不动**（统一要再改几百档名，收益不值）。认可？
4. 本稿确认后：回滚 448 档至 v2 落点 → 重写 taxonomy → validate → 重登记重编译 → 重建包 → 刷新名录/登记表。整套执行。
