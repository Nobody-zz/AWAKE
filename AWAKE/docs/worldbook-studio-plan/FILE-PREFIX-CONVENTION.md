# 档名前缀规范（worldbook authoring）

> 立：2026-09-24 · 起因：甲方要求「用一个前缀把暗面那批档拢到一起」
> 落点：`tools/worldbook-studio/workspace/full-geo1/authoring/*.yaml` 的**文件名**
> 性质：**人眼分类标签**。编译器不读文件名（只读档内 `domain`/`subdomain`），所以它不参与三哈希、改它不动上线包。

---

## 一、先分清两层：档名 ≠ 条目 id

同一件事在仓库里有**两个不同的名字**，混起来会改坏已发布的东西：

| 层 | 例子 | 谁在用 | 能不能改 |
|---|---|---|---|
| **档名** | `underworld-alleys.yaml` | 只有人（`ls` 一眼归类、文档里指档） | ✅ 随便改，改名即归类 |
| **条目 id** | `awake:entry:politics.town-alleys` | 游戏运行时、边表（1286 条）、`index.json`、`documents.json`、三哈希 | ❌ **已发布，不许动** |
| **文档 id** | `doc.politics.town-alleys` | 编译链主键（`_dark_chain_20260920.py` 的 `docid_of`）、`workspace-head.json` | ❌ 同上 |

⇒ **`slug` 在生成器里身兼三职**（文件名 / 文档 id 尾段 / 条目 id 尾段），改名时**只许改第一职**。
生成器侧的做法见 `docs/worldbook-migration/projection/authoring-out/_gen_dark_20260920.py` 里的 `FN_PREFIX` 映射表 —— 它把「文件名」从 `slug` 里拆出来单独映射，`slug` 本身不动。
⚠️ 那张表是**防退回装置**：不写它，任何人重跑生成器，镜像目录会重新长出旧文件名。

---

## 二、前缀取值规则

1. **一处前缀 = 一个内容类目。** 不许拿域当前缀 —— 实测全库 564 档里 `politics-` / `economy-` / `culture-` / `war-` **各 0 档**，因为域只有 5 个而类目有几十个。
2. **前缀后直接接内容词，不加分隔。** 例：`underworld-alleys`，不写 `underworld_alleys` 或 `uw-alleys`。
3. **前缀是全英文小写词。** 不许拼音，不许中英混写。
4. **一个类目只用一个前缀。** 同一批档不许一半 `alley-` 一半 `underworld-`（这就是 09-20 暗面批的原始毛病）。
5. **前缀不追求与 `domain` 对齐。** `underworld-smuggling` 的 `domain` 是 `economy`，档名照样归暗面 —— 前缀说「是什么货」，`domain` 说「进哪个抽屉」。

## 三、已成文的前缀谱系（截至 2026-09-24）

| 前缀 | 档数 | 内容 |
|---|---|---|
| `villages-` | 273 | 村庄 |
| `castles-` | 67 | 城堡 |
| `towns-` | 53 | 城镇 |
| `weapons-` | 39 | 武器与甲 |
| `goods-` | 39 | 货品 |
| `items-` | 22 | 物品与牲畜 |
| `military-` | 12 | 军事 |
| `troops-` | 10 | 兵种 |
| **`underworld-`** | **8** | **城镇地下秩序（巷子 / 帮派 / 犯罪 / 赎罪金 / 强盗 / 走私）** |
| `tales-` | 6 | 传说 |
| `territories-` | 4 | 领地掌故 |
| `mountains-` `lakes-` `throne-` `rivers-` | 各 2~4 | 地理与王座 |
| 其余单档 | 1 | `seas-` `deserts-` `mines-` `peninsulas-` `plateaus-` `clans-` `notables` `serfs` `small-factions` |

## 四、`underworld-` 归入范围（09-24 甲方裁定）

**归入**（8 档）：`underworld-alleys`（巷子）· `underworld-gang-leaders`（巷子头目）· `underworld-struggle`（巷子争夺）· `underworld-gangs`（帮派）· `underworld-crime-rating`（犯罪等级）· `underworld-blood-money`（赎罪金）· `underworld-bandits`（强盗）· `underworld-smuggling`（走私货）

**不归入**（3 档，当时同批但性质不同）：
- `notables`（要人）—— 城市社会结构，不专属暗面
- `serfs`（农奴）—— 乡下的依附关系，不是城里地下
- `small-factions`（小阵营）—— 跨域并集，含雇佣兵与宗教运动

⇒ 判据：**「是不是只在城镇后街 / 法外地带成立」**。是则入，否则留。

## 五、已知缺口（记账，未处置）

1. **分类表没有「法外／地下」这一格。** 这 8 档只能挤进 `politics/law`（7 档）与 `economy/trade`（1 档）。建议补 `politics/lawlessness` 或 `culture/underworld`——但分类表一改 Studio 包就过期（`scripts/package.ps1` 把整个 plan 目录拷进包 `schemas/`），要另走一次定向刷新。**本次未动。**
2. **`link-registry.v1.json` 与 `should-link.v3.json` 是手写/一次性生成的快照**，与 558 档当前状态不同步（前者 `source_entries` 记为 558，边 1286；后者 1394 边）。它们是**当时的三哈希签名产物**，不随档名走、也不应手改 —— 要更新得整条链重跑+重签。
