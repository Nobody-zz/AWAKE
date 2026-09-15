# 卡拉多格人物档样本 — 首个编译成功的人物锚定档（2026-09-12）

> 目的：拿一档真东西，把《总纲》第八节那几条待裁定项变成可判定的事实。
> 结论：**编译跑通了（Studio 史上第一次），多视角分层也真的生效了。**

---

## 一、做了什么

| 项 | 内容 |
|---|---|
| 档 | `doc.politics.caladog-accession`（卡拉多格的即位），`workspace/authoring/caladog.yaml` |
| 锚点 | `entity.hero.lord_5_1`（卡拉多格）+ `entity.clan.clan_battania_1`（芬·格鲁芬多克） |
| 来源 | **游戏官方数据**（BannerlordSage v1.3.15），`source_nature: game_snapshot`，逐条带 locator + quote + quote_hash |
| 规模 | 6 条断言 / 18 条表达 / 6 个身份 |
| 来源文件 | `sources/game-battania-caladog.txt`、`sources/source-bannerlord-game-battania-caladog.yaml` |

走完全流程：schema 校验 → Studio `validate` → 权限门（register→select→approve→proof）→ **`compile`** → 丢进模拟器验证。

---

## 二、四个实测结论

### ✅ 1. 写人物/家族档确实绕开 C16（从推断变成事实）

`entity.hero.*` / `entity.clan.*` 通过了 `CanonicalEntityRef`，编译成功，产物 `workspace/compiled/customer/`（runtime.json / index.json / manifest.json 等 11 个文件齐全）。
**这是 Studio 第一次真正跑通编译**——之前 `compiled/` 一直是空的。

### ✅ 2. 多视角分层真的生效（五个身份五种说法）

编译产物丢进模拟器，按各身份真实能力查询「卡拉多格」：

| 身份 | 拿到的说法 |
|---|---|
| 平民 | 听说如今坐在巴旦尼亚王位上的叫卡拉多格，不是正经生来的王。 |
| 商人 | 巴旦尼亚现在的王叫卡拉多格，是先王收养的。 |
| 士兵 | 巴旦尼亚的至高王是卡拉多格，出身军旅，为埃里尔所收养。 |
| 头人 | 埃里尔死得不明不白。这事儿在底下传了好些年，宫里始终没个交代。 |
| 贵族 | 卡拉多格，巴旦尼亚至高王，芬·格鲁芬多克一族，先王埃里尔的养子。埃里尔一去，他便接了位。 |

**同一件事，五种口吻，没有一条是复制。** 这正是《总纲》要的东西，现在有实物了。

### ⚠️ 3. 授权必须贴合身份能力上限，否则该身份永远拿不到

`src/WorldbookIdentityCapabilityRules.cs` 里每个身份的**范围与详细度上限是写死的**：

| 身份 | 范围上限 | 详细度上限 |
|---|---|---|
| 平民 / 村民 | local | **rumor** |
| 市民 | regional | summary |
| 乡绅 | regional | detail |
| 商人 / 酒馆老板 / 赎金经纪人 | faction | detail |
| 士兵 / 头人 | national | detail |
| 贵族 | elite | 按年龄·管理等级 |

我第一版给平民配了 `regional` + `summary` → **全员 blocked**。改成 `local` + `rumor` 才通。
> **推论**：现有 12 档给平民配的是 `regional` + `summary`，按这张表，**平民在真实游戏里一条都拿不到**。这条需要复核。

### ⚠️ 4. `deny` 是整条词条的核弹，慎用

`WorldKnowledgeQueryService.cs:52` 先做 `HasMatchingDeny(entry, ...)`，**命中就 `continue` 掉整个词条**——不是只挡那一条表达。

而且实测：加了 3 条 deny（目标是平民/村民/巴旦尼亚贵族）后，**连没被 deny 的士兵、商人、头人也一起被挡**，全员 blocked。去掉 deny 立刻恢复正常。
→ deny 的实际波及范围比直觉大。**本档暂不设 deny**，等这条查清再补。

---

## 三、顺带查清的两件事

1. **`culture_ids` / `kingdom_ids` 不打断编译**：编译器的严格白名单只用于 `entity_ids`；条件字段走宽松的 `MapConditionId` 路径。登记表里 `entity.culture.*` / `entity.kingdom.*` 是 **0 条**，但 ID 可从游戏数据直接取（本次取到 `entity.kingdom.battania`、`entity.culture.battania`，编译后正确变成 `awake:culture:battania`）。
2. **条件一旦设置，运行时必须提供对应的 KingdomId / SettlementId 才匹配**。本档未设，留待后续专项验证。

---

## 四、官方译名（已从游戏数据核实，勿凭印象）

卡拉多格（Caladog）· 芬·格鲁芬多克（fen Gruffendoc）· 巴旦尼亚（Battania）· 马鲁纳斯（town_B1，家族主城）。
八位统治者的完整译名表已写进技能 `bannerlord-game-data-access`。

---

## 五、下一步

1. **先复核第 3 条推论**——现有 12 档的平民授权是否全部超限（成本低、影响面大）
2. 查清 deny 的波及范围（第 4 条），再决定 deny 怎么写
3. 条件维度（kingdom/settlement）专项验证
4. 之后才是扩量

---

**状态**：未提交。`caladog.yaml` 及其来源为新建文件；`workspace/compiled/customer/` 为首次编译产物。
