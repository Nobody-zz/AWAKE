# 世界书 authoring 格式规范（一页）

时间：2026-09-12　整理：阿砚
**这份不是新规范。** 是把散在四处、互相没串起来的既有口径收拢成一页，并补上今天实测出来的三条新结论。
每条都标了出处，可回溯。

---

## 零、先答："引文到底有什么用？"

因为引文**进不了游戏**（实测：编译后的 `runtime.json` 里 `sources`/`quote` **0 命中**），它常被误解成没用。它的用处不在运行时，在**生产阶段**：

| 用处 | 怎么起作用 |
|---|---|
| **可回溯** | 复核者顺着 `locator` + `quote` 能直接回到官方原文，不用重查游戏数据 |
| **防篡改** | 两个哈希：`quote_hash` 保证引文没被改，`source_content_hash` 保证来源文件没被换 |
| **防越界** | 第六节边境"官方文本外的人物关系"——**有引文才证明这条知识在官方文本里**，不是 AI 编的 |
| **防以偏概全** | 规则 12：引文粒度必须细于断言，不许"整段引用冒充局部支撑" |

**一句话**：引文是**"这本书不是编的"的凭证**，不是"游戏能跑"的数据。
类比：建筑图纸上的审图签字——住进房子用不到它，但没它你不知道这房是不是按规范盖的。

**推论（今日新结论）**：既然引文的全部价值在"证明知识有出处"，那它**只需要写在能支撑知识的最小粒度上**——也就是**断言级**。
表达只是断言的换说法，不引入新事实，**不需要自己的引文**。

> 出处：`WORLDBOOK-CONTENT-AND-FORMAT-GAP-20260912.md` 原话"来源引文做得很扎实…是最费功夫也最值钱的部分"；规则 12 见 `tools-r3/WRITING-RULES.md`。

---

## 一、结构只有三层

```
文档 doc.*（一篇）
 ├─ 元信息：这是什么、属于哪一域、什么时代
 └─ 断言 assertion × N
     ├─ 内核 text   ← 中立客观陈述，一条只说一件事
     ├─ sources     ← 这条知识的依据 ★引文写到这一层为止
     └─ 表达 expression × M
         ├─ text    ← 某个身份会怎么把这条知识说出来
         ├─ layer   ← 说到多细（unknown→rumor→summary→detail→secret）
         └─ grants  ← 谁能听到（profile/scope/min_detail + 条件）
```

**`assertion` = 一条知识；`expression` = 这条知识的一种说法。** 两者不是一回事，别混。

---

## 二、字段清单（哪些必填、哪些该填、哪些别碰）

### 文档级

| | 字段 | 说明 |
|---|---|---|
| **必填 13** | `schema_version` / `revision` / `id` / `title` / `status` / `domain` / `universe` / `era` / `content_tier` / `summary` / `registry_bindings` / `assertions` / `authority` | `id` 正则：`^doc\.(politics\|economy\|culture\|war\|geography)\.[a-z0-9]+(?:[._-][a-z0-9]+)*$` —— **只能小写**；`assertions` 至少 1 条 |
| **强烈建议填** | `aliases` / `entity_ids` / `subdomain` | 见第三节：**这两个决定"能不能被问到"** |
| **该填但没人填** | `related_domains` / `lifecycle` | 12 档 0/12。`lifecycle` 是拆档血缘，**拆了档必须记** |
| **别碰** | `redirects` / `author_created` | 前者只在合并/改名时用；后者与 `sources` **互斥** |
| **可选** | `sources` | 声明本文引了哪些来源。被 `ValidateTimeline` 读（查宇宙一致性） |

> 出处：`WORLDBOOK-MECHANISM-AND-STUDIO-20260912.md` 第三节（**该表写"可选 7 项"但列了 8 个，是个笔误，顺手记一下**）。

### 枚举值（背下来，写错就报错）

- `status`：`canon` / `accepted_variant` / `rumor` / `reference_only` / `future` / `needs_review` / `rejected`
- `domain`：`politics` / `economy` / `culture` / `war` / `geography`
- `universe`：`awake_current` / `bannerlord_1084` / `warband_future` / `ck3_mod` / `unknown`
- `era.certainty`：`exact` / `bounded` / `approximate` / `unknown`
- `content_tier`：`base` / `adult_optional`
- 断言 `kind`：`fact` / `state` / `relation` / `interpretation` / `rumor`
- 表达 `layer`：`unknown` / `rumor` / `summary` / `detail` / `secret`
- `grants.scope`：`local` / `regional` / `national` / `faction` / `elite` / `private`
- `grants.min_detail`：`rumor` / `summary` / `detail` / `secret`

### 断言级

必填：`id` / `revision` / `kind` / `text` / `expressions`
可选：`sources`（**该填**）、`author_created`（与 sources 互斥）

### 表达级

必填：`id` / `revision` / `layer` / `text` / `grants` / `denies`
可选：`sources`（**别填，见零节**）、`fallback_referral_ids`

### 引文（`source_ref`）必填 5 项

`source_id` / `source_version` / `source_content_hash` / `locator` / `quote_hash`，外加 `quote`（**不填 quote 就没法核 hash，等于白填**）

---

## 三、检索：决定"玩家问得到问不到"的两件事

**编译器生成检索词的规则**（`RuntimePackageCompiler.cs:104-112`，K1）：

```
检索词 = 标题 + aliases + 实体锚点可读名 + 文档 id（兜底）
```

**正文一个字都不进检索词。** 实测：
`doc.politics.caladog-accession` 的检索词只有 4 个 —— `['卡拉多格的即位','卡拉多格','芬·格鲁芬多克','doc.politics.caladog-accession']`。
玩家问"埃里尔"（正文出现 18 次）→ 5 个身份**全部 not_found**。

**所以想让一个名字能被问到，只有三条路：**

| 路 | 什么时候用 |
|---|---|
| ① **进标题**（拆成独立档） | 它是一个独立主题 —— ⚠ 这也顺带满足 B4"一主题一档" |
| ② **进 `entity_ids`** | 它是**游戏锚定实体**（`hero`/`clan`/`settlement`），名字自动进检索词 |
| ③ **进 `aliases`** | 它是俗称/别称，或者**它不是游戏实体**（如埃里尔只活在别人的描述文字里） |

**② 优先。** 因为只有 ② 顺带把知识挂到了游戏真对象上。③ 是兜底，① 是下策（除非本来就该拆）。

> 出处：K1 规则见 `RuntimePackageCompiler.cs`；"可检索性由 title/aliases 承担"见 `PLAN-WORLDBOOK-AUTHORING-PROJECTION-20260912.md:91`；
> "没有 aliases = 换个叫法问就找不到"见 `WORLDBOOK-CONTENT-AND-FORMAT-GAP-20260912.md:112-116`。**数字是今天实测的，规则是旧档早有的。**

---

## 四、最小范例（照这个抄）

```yaml
schema_version: awake.worldbook.authoring.v1
revision: 1
id: doc.politics.caladog-accession
title:
  zh-CN: 卡拉多格的即位
status: needs_review
domain: politics
subdomain: succession
universe: awake_current
era: {key: current, certainty: bounded}
content_tier: base
entity_ids:                        # ② 优先：游戏锚定实体，名字自动进检索词
- entity.hero.lord_5_1
- entity.clan.clan_battania_1
aliases:                           # ③ 兜底：俗称、旧称、非实体名
  zh-CN: [卡拉多格继位, 至高王卡拉多格]
summary:
  zh-CN: 巴旦尼亚至高王卡拉多格的来路与即位。
registry_bindings:
  profile_registry_version: 1.0.0
  profile_registry_hash: 309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5
  referral_registry_version: 1.0.0
  referral_registry_hash: 6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD
sources:                           # 文档级：本文用到的来源，登记一次
- source_id: source.bannerlord.game.battania-caladog
  source_version: sagecache-20260912
  source_content_hash: a7c1b41c428e404a3a8d699b99d6036fd8cb6087eb10dbcc19fe6085b5ebbcee
  locator: game-battania-caladog.txt#/hero/lord_5_1/text
  quote_hash: 777c8ee48209c8bf9f888d06fc3532390aca4f5370a604514b793bc26599e88f
  quote: 巴旦尼亚的现任至高王是卡拉多格
authority: {owner: awake_canon, conflict_policy: canon_wins}
assertions:
- id: assertion.caladog-1
  revision: 1
  kind: fact
  text:
    zh-CN: 巴旦尼亚现任至高王是卡拉多格。
  sources:                         # ★ 断言级：引文写到这一层为止
  - source_id: source.bannerlord.game.battania-caladog
    source_version: sagecache-20260912
    source_content_hash: a7c1b41c428e404a3a8d699b99d6036fd8cb6087eb10dbcc19fe6085b5ebbcee
    locator: game-battania-caladog.txt#/hero/lord_5_1/text
    quote_hash: 777c8ee48209c8bf9f888d06fc3532390aca4f5370a604514b793bc26599e88f
    quote: 巴旦尼亚的现任至高王是卡拉多格
  expressions:
  - id: expr.caladog-1-commoner
    revision: 1
    layer: rumor
    text:
      zh-CN: 听说如今坐在巴旦尼亚王位上的叫卡拉多格，不是正经生来的王。
    grants:
    - profile_id: profile.commoner
      scope: local
      min_detail: rumor
    denies: []
```

**注意**：表达段**没有 `sources`**。照这份写，一篇 6 断言 18 表达 ≈ **357 行**（原写法 465 行）。

---

## 五、红线（踩了就报错或白干）

| # | 红线 | 后果 |
|---|---|---|
| 1 | **多写一个字段** | 顶层 `additionalProperties:false` → 直接报错 |
| 2 | **`id` 里有大写** | 正则不通过 |
| 3 | **`sources` 与 `author_created` 同时写** | 互斥，报错（文档/断言/表达三级都一样） |
| 4 | **`denies` 漏写** | schema 必填，哪怕填 `[]`（**这是契约问题，GAP 文档第 5 条已记**） |
| 5 | **表达级也抄一遍引文** | 不报错，但纯浪费（见零节） |
| 6 | **grant 的 scope/min_detail 越过身份上限** | 该身份**永远 blocked**，静默失败 —— 上限见下表 |
| 7 | **改登记表数据文件** | 四个 hash 全变 → **全部 12 档 `WB-REGISTRY-001` 一起失效**（那两份被 golden 钉死，动不得） |
| 8 | **用了 `lifecycle` 血缘字段却没补 `event_id`+`revision`** | 报错 |
| 9 | **内核照抄引文** | ⚠️ **规则 6 要求不许照抄，但校验器没实现这道门**（grep 零命中）→ **没人拦你，全靠自觉** |
| 10 | **一条断言写了两件事** | 违反规则 4（"因为…所以…后来…"就拆条）。同样**无门可拦** |

### 身份能力上限（写 grant 的天花板，超了永远 blocked）

| 身份 | scope 上限 | detail 上限 |
|---|---|---|
| 平民 / 村民 | `local` | `rumor` |
| 市民 | `regional` | `summary` |
| 乡绅 | `regional` | `detail` |
| 商人 / 酒馆老板 / 赎金经纪人 | `faction` | `detail` |
| 士兵 / 头人 | `national` | `detail` |
| 贵族 | `elite` | `detail` |

> 出处：`src/WorldbookIdentityCapabilityRules.cs`。**这张表是实测出来的**（给平民配 `regional`+`summary` → 全员 blocked，改 `local`+`rumor` 才通）。

### 另外两条容易踩反的

- **`scope` 是"接触门槛"（保密层级），不是地理距离。** `local=1 < regional=2 < … < private=6`，`local` 是最低门槛、谁都够得着。想做地理梯度得用 `conditions.settlement_ids`。
- **`requestedDetail` 是上限，不是"我想要多细"。** 表达是 `summary` 时请求 `rumor`，反而被拒。

---

## 六、一句话记住

> **文档管一篇，断言管一条知识（带引文），表达管一种说法（带权限）。**
> **引文写到断言为止，检索词靠标题+锚点+别名，权限别越身份上限。**

---

## 七、这份规范里，哪些是新的、哪些是旧的

**旧档早有的**（我只做了收拢，没发明）：三层结构、字段清单与枚举、"可检索性由 title/aliases 承担"、来源优先级、规则 4/6/12、身份能力上限的出处。

**今天实测新增的**：
1. **引文不进运行时**（`runtime.json` 0 命中）+ 引文的用处是"生产凭证"不是"运行数据"
2. **引文只写到断言级**，表达级与其逐字重复，可删（`expression.sources` schema 选填，编译器不读）
3. **检索词只有三条路**（标题 / `entity_ids` / `aliases`），正文不进 —— 并给了实测数字
4. **规则 6 的"10-gram 门"没实现**（⚠ 待核实）；规则 4 也没有门

**我还没把握的**：`AuthoringEditorModel.cs:524` 那个 `sourceCount` 到底有没有人消费（**疑似死字段，待核**）。

**已查清（原标"待确认"，现已结掉）**：删了表达级引文，**编辑器不会补回来**。两个理由：

1. `AuthoringEditorModel.MergeExpressions`（`:217`）**从原始表达 clone 起步**（`Clone(original)`），sources 原样带过，只覆写 id/layer/text/grants/denies —— 它既不删也不加。
2. 更彻底的是：**带 `sources` 的档在作者模式里根本改不了。** `EnsureSourceObjectsUnchanged`（`:441-463`）规定，只要文档是 source 型且内容有变，就直接抛 `WB-EDITOR-SOURCE-403`「来源型档案不能在作者模式修改，请进入高级模式维护」。

> ⚠️ **这条是意外收获，值得单列**：**我们的档全是 source 型 → 在作者模式（Web 编辑器）里是只读的，必须走「高级保存」。** 这解释了为什么改我们的档感觉别扭。

---

**状态**：口径汇编，未提交。待 Max 过目。
