# 裁定：知识条目的说法可以绑定一个 clan（2026-09-24）

> **甲方口径，一字不改**：
> 「**我就是要知识条目的某一个说法绑定一个 clan，你绕来绕去干嘛**」
>
> 落点按项目约定：`docs/DECISION-<日期>-<题目>.md`，原话一字不改，只挂指针。

---

## 一 需求本身（一句话）

**一条知识条目的某个说法（assertion／expression），可以只送给「属于某个家族」的人。**
不是"挑一家人试点"，不是"先做成可预览的绑定数据"，也不是让甲方在若干方案里选——
**就是这件事，一次就通。**

## 二 与设计稿的冲突（必须说破）

`docs/superpowers/specs/2026-08-24-worldbook-entity-catalog-design.md:31` 明写：

> 「**B1 不实现 `hero_ids`/`clan_ids` 运行时权限条件**；作者选择结果先作为可预览的实体绑定数据，
> 运行时契约另立后续批次。」

本次**直接实现了 clan 的运行时条件**，越过了这条批次边界。

**处置**：甲方已于 2026-09-24 明确裁定要这个能力 ⇒ **设计稿 B1 那句中关于 `clan_ids` 的部分
视为被本次裁定覆盖**。

**★ 2026-09-25 补（甲方 `改掉，补`）**：设计稿 `:31` **已就地改掉**（改为"本条已作废、clan_ids
与 hero_ids 均已接上运行时条件"，其余各条仍然有效）；`hero_ids` **已跟进实现**，见 §七。
⇒ **本设计稿的分批边界整体不再成立**，两句都已落地。

## 二之二 `hero_ids`（2026-09-25 补）

同一件事的**更窄一格**：一条说法只送给**某一个人**（不是一族、不是一类人）。

**与 `clan_ids` 的分工**：那个问「他是不是这家的**人**」（同族都给），这个问「他是不是**他本人**」。

**落地形态**：

```yaml
grants:
  - profile_id: profile.noble
    scope: elite
    min_detail: detail
    hero_ids: ["entity.hero.lord_1_14"]   # ★ 值必须是 entity.hero.<hero_code> 三段
```

**编译器归一**：`entity.hero.lord_1_14` → `awake:hero:lord_1_14`。
**判定语义**：与 clan 完全同口径 —— 查询侧没带本人 ⇒ **一律不命中**；
「不知道他是谁」与「他不是这个人」**同解**。权重给 **35/条**（比 clan 的 30 高，因为它更窄）。

⚠️ **命名坑（踩过）**：`WorldbookQuery` 里**本来就有一个 `HeroId`** —— 那属于**作废的 `.Current` 链路**
（`WorldbookWhen.HeroIds` / `WorldbookService.cs:383`，其承载者 `WorldbookRuntime._current` 全仓
**从未被赋过真值**、外部调用方 0 个）。新加的走现行 `.Knowledge` 链路，**取名 `PersonHeroId` 避撞**。
⇒ 加字段前先 grep 一遍同名，别撞进作废链路。

## 三 落地形态

**作者怎么写**（`awake.worldbook.authoring.v1`）：

```yaml
grants:
  - profile_id: profile.noble
    scope: elite
    min_detail: detail
    clan_ids: ["entity.clan.clan_empire_south_1"]   # ★ 值必须是 entity.clan.<code> 三段
```

**编译器归一**：`entity.clan.clan_empire_south_1` → 运行包里成 `awake:clan:clan_empire_south_1`。

**判定语义**（`WorldbookIdentityEvaluator`）：
- 条件里写了 `clan_ids` 而查询侧没带 clan ⇒ **一律不命中**（不求近似、不回退到王国）。
- 「不知道他是谁家的人」与「他不属于这一家」**在这里同解**——都不给。

**与 `is_clan_leader` 的分工**：那个问「他是不是族长」，这个问「**他是哪家的人**」。
两者正交，可以同时写。

## 四 证据（零替身，可复跑）

探针：`tools/_uw_clan_gate_probe_20260924.py`（两轮全链：写档 → register → select → approve → proof →
compile → probe 矩阵）。验台 `tools/worldbook-runtime-sim`（把 `src/*.cs` 整编进自己的程序集，真件）。

| 判据 | 结果 |
|---|---|
| ① 同 clan 查 → 必须拿到（阳性对照） | PASS |
| ② 别的 clan 查 → 必须拿不到 | PASS |
| ③ 无 clan 查 → 必须拿不到 | PASS |
| ④ 空转护栏（三行都出现过） | PASS |
| ⑤ 编译真落下 `clan_ids` | PASS |
| ⑥ 变异：条件换 OTHER 后用 TARGET 查 → 必须翻成 0 | PASS |
| ⑦ 变异：条件换 OTHER 后用 OTHER 查 → 必须翻成 >0 | PASS |
| ⑧ 变异：无 clan 查仍为 0 | PASS |
| ⑨ 变异轮也真落了 `clan_ids` | PASS |

**⑥⑦ 的作用**：光有 ①，②说不准是"查询恒返空"造成的假绿。把**条件侧**也做一次对照
（同一份档、同一套 query，只把条件的 clan 换掉），读数**跟着 clan 真翻转**，才证明这道闸在测。

### ★ `hero_ids` 同批验证（2026-09-25，探针 `_uw_hero_gate_probe_20260925.py`）

| 判据 | 结果 |
|---|---|
| ① 本人查 → 必须拿到（阳性对照） | PASS |
| ② **同家族的另一个人**（伊拉 `lord_1_37`）查 → 必须拿不到 | PASS |
| ③ 无本人查 → 必须拿不到 | PASS |
| ④ 空转护栏 | PASS |
| ⑤ 编译真落下 `hero_ids` | PASS |
| ⑥⑦⑧ 变异轮（条件换成伊拉）→ `[1,0,0]` 翻成 `[0,1,0]` | PASS |

★ **②的取值是这份探针的要害**：阴性取了**同一个珀特洛斯家里的妹妹伊拉**、而不是随便一个外人。
若取外人，连 `clan_ids` 都能挡住他 —— 那测出来的是 clan 不是 hero。取同族人，才真正证明这道闸
分得出「一族人」与「这一个人」。

## 五 改了哪六处（+ 一处编辑侧白名单）

| # | 部位 | 文件 |
|---|---|---|
| ① | 授权 schema | `docs/worldbook-studio-plan/awake.worldbook.authoring.v1.schema.json`（`knowledge_rule.clan_ids`／`hero_ids`，值域 `^entity\.(clan|hero)\.[a-z0-9]+(?:[._-][a-z0-9]+)*$`）|
| ② | 条件模型 | `src/WorldKnowledgeModels.cs`（`WorldKnowledgeCondition.ClanIds`／`HeroIds`）；`src/WorldbookModels.cs`（`WorldbookQuery.ClanId`／**`PersonHeroId`**）|
| ③ | 编译器 | `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/RuntimePackageCompiler.cs`（`CopyStringArray(..., "clan_ids"/"hero_ids")` ＋ `MapConditionId("clan_ids")=>"clan"`／`("hero_ids")=>"hero"`）|
| ④ | 加载器 | `src/WorldKnowledgeLoader.cs:169`（`AddStrings(condition.ClanIds, ...)`／`AddStrings(condition.HeroIds, ...)`）|
| ⑤ | 判定器 | `src/WorldbookIdentityEvaluator.cs`（`ClanIds`／`HeroIds` 同款短路；权重 clan 30／hero **35**）|
| ⑥ | 查询侧 | `src/NpcDialogueService.cs`（`_heroClanId = hero.Clan?.StringId` ／ `_heroPersonId = hero.StringId`）|
| ⑦ | **编辑侧白名单** | `tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthoringEditorModel.cs:485`（`RuleConditionKeys`）——**不加，编辑器里填了留不住** |

## 六 没做的 / 待定

- **`hero_ids` 已跟进**（见 §二之二）；设计稿 `:31` 已改。
- 现有 558 档**没有一档**用到 `clan_ids`／`hero_ids`（它们刚刚才存在）；火焰余烬／秘密之手那批料
  是否改挂 `clan_ids`（替换原先的 `culture_ids` 近似），**未动，待甲方**。
- 家族归属的**值域**（哪些 clan code 可用）来自 `entity-registry.v1.json`（82 个 clan，
  带 `member_entity_ids`；415 个 hero 带 `family_entity_id`／`family_name_zh`）——
  **这份目录一字未动**，`clan_ids`／`hero_ids` 只是新增了消费方。
  ★ 已核对：`hero_code`／`clan_code` 与 `entity_id` **尾段 100% 一致**（415＋82 全对），
  且与游戏侧 `bannerlord_heroes.heroId`（`lord_1_1` 这种）**同形**，无前缀无大小写差。
