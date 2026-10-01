# 角色卡标签与王国锚归一（2026-10-01）

> 范围：`AWAKE/tools/persona-workbench/characters/` 下的 355 张 `.persona.json` 与 355 个 `.origins.json` 侧车。
> 证据等级：本文所有读数均为【实测】（在本机跑出）。凡引用他人未提交脚本的结论，已单独标注。
> 相关门：[`AWAKE/tools/persona-card-gate.py`](../tools/persona-card-gate.py)（平行统计门，17 条判据）与
> [`AWAKE/tools/persona-workbench/tools/run-card-gates.ps1`](../tools/persona-workbench/tools/run-card-gates.ps1)（项目官方 6 道门禁链）。

---

## 零、一句话

把 275 张卡的**非法标签**、3 张卡的**非法 facet 键**、107 个侧车的**非法王国 id** 归一到权威词表／枚举。
官方门禁链从 **2/5 通过**变成 **4/5 通过**；剩下唯一红门是「279 张卡未纳入版本控制」。

---

## 一、改了什么

### 1. 标签归一 —— 278 张卡

| 类别 | 卡数 | 处理 |
| --- | --- | --- |
| `tags` 含未注册标签 | 275 | 按 §二 映射到 39 个封闭词表 |
| `facetStrengths` 键非法（含任何 `trigger.*`） | 278 | 目标在 32 个 facet 枚举内则改名，`trigger.*` 一律删除 |
| 其中只有 facet 键坏、`tags` 已干净 | 3 | 乌里克、亚恰娜、佐里卡（facet 里混进了 `trigger.family_safety`） |

工具：[`AWAKE/tools/persona-workbench/tools/normalize-card-tags.py`](../tools/persona-workbench/tools/normalize-card-tags.py)
（`--cards` 必需，另有 `--dry-run` / `--backup` / `--report` / `--emit-map`；facet 枚举**现读** schema 的
`properties.facetStrengths.propertyNames.enum`，不硬编码；只改 `tags` 与 `facetStrengths`，其余字段原样保留）。

### 2. 王国锚归一 —— 107 个侧车

| 原值 | 归一到 | 数量 |
| --- | --- | --- |
| `empire_north` | `empire` | 45 |
| `empire_south` | `empire_s` | 29 |
| `empire_west` | `empire_w` | 33 |

归一后侧车 `kingdomId` 只剩 8 个合法值，合计 355：
`aserai 45 / battania 37 / empire 49 / empire_s 42 / empire_w 39 / khuzait 43 / sturgia 48 / vlandia 52`。

**为什么是卡写错而不是枚举缺项**：sage `bannerlord_kingdoms` 只有 8 行，其中
`empire` 就是 Northern Empire（`{=NF627oiX}Northern Empire`）、`empire_s`=Southern、`empire_w`=Western。
`empire_north` / `empire_west` / `empire_south` 在游戏数据里根本不存在。
根因是**命名空间混淆**：侧车的 `clanId` 是 `clan_empire_north_1`，作者把家族的 `north` 抄进了王国 id。

### 3. 堤诺普斯（`lord_1_40`）按门重做

第一张完整走完「重写 → 过门」的卡（`id` 归一、`core` 103→220 字并补终止标点、`summary` 重写不再派生、
`publicDescription`/`privateDescription` 补完定长截断、四套 profile 键名换正、`facetStrengths` 去 trigger 键）。
另外补了一条 `realSelfBehaviors`「但凡有人拿军令当借口克扣伤兵口粮，我绝不答应」——
「但凡」是条件词、「绝不」是底线词，二者在 70 字窗口内共现，把官方 `4-enhancement` 门从 FAIL 拉成 PASS。

---

## 二、标签映射表（14 个未注册标签 → 13 个目标，丢 1 个）

| 未注册标签 | 卡数 | 归一到 | 备注 |
| --- | --- | --- | --- |
| `trait.ruthless` | 235 | `trait.deceitful` | 语义有损：狠辣 ⊃ 狡诈，词表无更近项 |
| `expression.cold` | 235 | `expression.understated` | |
| `behavior.calculating` | 235 | `behavior.keeps_leverage` | |
| `trigger.family_interest` | 235 | `trigger.family_safety` | |
| `trait.warm` | 31 | `trait.kind` | 与下行在 31 张那批撞车，需去重 |
| `trait.compassionate` | 31 | `trait.kind` | |
| `expression.soft` | 31 | `expression.warm` | |
| `trigger.civilian_safety` | 31 | `trigger.threat_to_home` | |
| `expression.reserved` | 5 | `expression.understated` | |
| `behavior.rule_bound` | 5 | **丢弃** | 语义已被同集合里的 `trait.traditional`（信奉旧制、礼法与祖宗规矩）覆盖 |
| `trigger.tradition` | 5 | `trigger.social_slight` | 最弱的一条映射 |
| `trait.generous` | 4 | `trait.kind` | |
| `behavior.loyal` | 4 | `trait.loyal` | 语义精确，跨了类别（behavior→trait） |
| `trigger.ally_interest` | 4 | `trigger.loyalty_or_betrayal` | |

映射后的原型集合都落在词表的一致区域（这是映射可信度的旁证）：

| 原集合 | 张数 | 映射后 | 最近的 bundle |
| --- | --- | --- | --- |
| 冷血算计 | 235 | `trait.pragmatic, trait.deceitful, expression.understated, behavior.keeps_leverage, trigger.family_safety` | `bundle.schemer` 的子集 |
| 温和护卫 | 31 | `trait.kind, expression.warm, behavior.protects_inner_circle, trigger.threat_to_home` | `bundle.warrior` 的亲属 |
| 守旧 | 5 | `trait.traditional, trait.cautious, expression.understated, trigger.social_slight` | `bundle.patriarch` 的亲属 |
| 忠义进取 | 4 | `trait.ambitious, trait.kind, trait.loyal, expression.direct, trigger.loyalty_or_betrayal` | — |

---

## 三、为什么是改卡而不是扩词表

1. skill `persona-authoring` 明说 39 个标签是**闭合**词表，「自造键必被 schema/crosswalk 拒」。
2. `compile-verify.ps1` 是**故意**把「未注册 tag」从「只记报告」改成「判红」的。
3. `AWAKE/docs/persona-contract/persona-workbench-to-awake.crosswalk.v1.json` 的
   `targetRegistry.sha256` **钉住了注册表哈希** —— 往注册表加标签会作废整份 crosswalk。
4. 运行时后果是硬的：`src/PersonaTagRegistry.cs:122-136` 里只要有一个未注册标签，
   `TryExpand` 就返回 false ⇒ `src/PersonaDslGenerator.cs:127` 判 `!expanded` ⇒
   `:131 result.UsedLegacyFallback = true` ⇒ **整张卡丢掉人格 DSL、退回 legacy 模板**。归一前有 275 张如此。

---

## 四、证据

| 检查 | 结果 |
| --- | --- |
| 空跑 | `changed=278 errors=0` |
| 应用后重跑空跑 | `changed=0` ⇒ **幂等** |
| 备份 | 278 张卡 → `%TEMP%\awake-card-backup-20261001\`；107 个侧车 → `%TEMP%\awake-origins-backup-20261001\` |
| 平行门（我那道） | `PERSONA_TAG_UNREGISTERED 275 → 0`；`violations 2269 → 1993`（−276）；`failed_cards` 仍 278（散文问题没动） |
| 官方 schema 门 | 表行 355、FAIL 行 **276 → 107**、PASS 行 79 → 248；**未注册 tag 明细 169 → 0** |
| 官方整链 `run-card-gates.ps1 -SkipCompile` | `1-schema PASS / 2-affiliations PASS / 3-text PASS / 4-enhancement PASS / 5-provenance FAIL`，`OVERALL: FAIL`，exit 1 |
| 独立复核（不依赖他人未提交脚本） | Python 直查 355 张：未注册标签 **0**、冲突对 **0**（注册表只有一对冲突声明 `expression.indirect ↔ expression.direct`） |

**必须标注的保留**：`audit-character-schema.ps1` 的标签注册表校验（+27 行，2026-09-29 新增）、
`run-card-gates.ps1`（+29/−7）、`materialize-definitions.ps1`（+10/−6）**都是另一会话未提交的工作区改动**。
所以上表「官方 schema 门」「官方整链」跑的是**工作区脚本**，不能声称「仓库已提交的门禁链通过」。
「355 张零未注册标签 / 零冲突对」是我用 Python 直接量的，结论不依赖那批改动。

---

## 五、一处必须纠正的读数（行尾）

先前我说「diff 只动了 `tags` 与 `facetStrengths` 的行」——**这句话不成立，作废**。

实测：归一脚本把 **262 张卡 + 107 个侧车从 CRLF 写成了 LF**（备份里 262 张是 CRLF、16 张是 LF；侧车 107 个全是 CRLF）。

但这**不是**仓库层面的改动：

```
core.autocrlf = true
git ls-files --eol -- AWAKE/tools/persona-workbench/characters
  → 152 个已跟踪文件全部 i/lf（106 个 w/lf、46 个 w/crlf）
```

索引里存的是 LF，工作树里的 CRLF 在 git 看来本来就不是改动。⇒ **不需要回改**。
教训记在这里：比较「迁移前后是否只动了预期字段」时，**必须用二进制模式读**，
文本模式会把 CRLF 归一掉、把行尾改动藏住 —— 我第一版就是这么被骗过去的。

---

## 六、坏卡的根因：散文是被机械切碎的，不是写坏的

对照 `...\Modules\AnimusForge\PlayerExports\卡拉迪亚编年史\personality_background\lord_6_8__额速儿.json`
（1705 B，只有 `Personality` / `Background` / `VoiceId` 三个字段）与卡
`characters/额速儿_Esur_khergit_khuzait.persona.json`：

| 卡里的字段 | 来源 |
| --- | --- |
| `summary`「额速儿是一位典型的草原猛将，性格里全是拧巴的矛盾。」 | `Personality` 的**第一句照抄** |
| `core`「我是额速儿，额速儿出生于库赛特汗国中显赫一时的库吉特家族…」 | `Background` **第三人称原文套「我是」再切断** |
| `identityFacts`「…母亲墨速宜坚、母亲那种为受。」 | 对 `Background` 做**定长切片**切出的残句（原文是「在母亲墨速宜**坚**韧且强硬的领导下」「他深受母亲那种为受压迫部族打抱不平的影响」） |
| `privateDescription`「作为族长墨速宜与岁仑之子」 | 同段原文 |

⇒ 262 张坏卡是一条**把外部散文切碎灌进字段**的流水线产物，不是「文笔差」。
这也解释了 247 张卡共用同一句样板、235 张卡共用同一套标签：模板填的是**家族原型**，不是人。

**这条素材线不能用来写卡**：`AWAKE/docs/mappings/persona-game/persona-game-mapping.v1.json:6` 明确写着
`"source_policy": "Read AF filenames only; do not read personality_background JSON content."`（2026-08-23 定）。
同一份文件的价值在别处 —— 它是一张 415 行的**身份锚表**（hero_id → clan_id / kingdom_id / culture_id /
home_settlement_id / clan_owner_hero_id / is_noble_clan）。

---

## 七、该用什么写卡：游戏自己的原生性格特质

`Modules/SandBox/ModuleData/lords.xml` 里 **398/399** 个领主带骑砍原生性格特质（每项 ±1）：

| 特质 | 有值的英雄 | 含义 |
| --- | --- | --- |
| `Valor` | 163 | +1 勇武 / −1 怯懦 |
| `Honor` | 161 | +1 重信 / −1 无信 |
| `Mercy` | 159 | +1 仁慈 / −1 残忍 |
| `Generosity` | 196 | +1 慷慨 / −1 吝啬 |
| `Calculating` | 187 | +1 算计 / −1 冲动 |

另有政治特质 `Egalitarian` 14 / `Oligarchic` 12 / `Authoritarian` 9。

**覆盖（实测）**：355 张卡的侧车 heroId **全部**能在 `lords.xml` 里解析到；
**354/355** 至少有一项性格特质（分布：1 项 64 张、2 项 137 张、3 项 109 张、4 项 34 张、5 项 10 张）。

**两条交叉验证**：

- 正例：堤诺普斯（`lord_1_40`）原生 `Mercy=1, Generosity=1, Calculating=-1, Egalitarian=1`，
  与我手写那张卡的「仁慈＋慷慨＋不算计」**独立吻合**。
- 反例：额速儿（`lord_6_8`）原生 `Valor=1, Generosity=1`（勇武慷慨），
  坏卡却给他 `trait.pragmatic, trait.deceitful, behavior.keeps_leverage`（冷血算计）——**与游戏自己的数据对着干**。

产物：[`AWAKE/docs/reference/persona-authoring-facts.v1.tsv`](reference/persona-authoring-facts.v1.tsv)（355 行）。
生成器：[`AWAKE/tools/persona-workbench/tools/build-authoring-facts.py`](../tools/persona-workbench/tools/build-authoring-facts.py)
（只读；连接侧车 heroId、`lords.xml` 原生特质、sage 的 father/mother/spouse、权威译名表、身份锚表；可复跑）。
一行一个角色，含 `hero_id / name_zh / name_en / culture / kingdom_id / clan_id / is_noble_clan /
clan_owner_hero_id / home_settlement_id / age / is_female / voice / father / mother / spouse /
personality_traits / political_traits`。

**但这条映射还不够格当门判据。** 用暂定的「原生特质 → 期望/矛盾标签」表跑全语料：

| | 有矛盾 | 至少命中一项原生信号 | 完全脱离原生信号 |
| --- | --- | --- | --- |
| 好卡 76 | 25（33%） | 70（92%） | 6（8%） |
| 坏卡 279 | 123（44%） | 192（69%） | 86（**31%**） |

好卡也有 33% 被判矛盾（拉盖娅、蒙楚格、卡拉多格都在内）⇒ 暂定映射**假阳性太高**，只能当软提示。
三项里只有「完全脱离原生信号」有区分度（8% vs 31%，3.9 倍）。
要升级成硬判据，必须先拿好卡当阳性对照把映射校准好 —— 这件事本轮**没做**。

---

## 八、还没做

- **278 张卡的散文重写**。本轮只做了机械可验证的部分；散文层一个字没动（`CORE_TOO_SHORT 278`、
  `CORE_UNTERMINATED 262`、`CROSS_CARD_DUPLICATE 262`、`SPEAKER_LABEL_PREFIX 262` 全部原样）。
- **279 张卡 + 279 个侧车纳入版本控制**（`5-provenance` 的唯一解）。官方门给的处置是
  「确认内容后 git add 纳入，或明确废弃并移出 characters/」—— 内容已确认**不达标**，
  所以这是「先纳管作基线、再重写」还是「先重写、再纳管」的顺序选择，**未定**。
- 39 张卡回填真身 id（清单在 [`AWAKE/docs/PERSONA-SAGE-AUDIT-20261001.md`](PERSONA-SAGE-AUDIT-20261001.md)；
  2 张同名歧义）；52 个无卡领主补卡（含 17 个无名 `lord_7_*`）。
- `AWAKE/tools/persona-awake-joint/` 的 6 处真 `& pwsh` 调用（`verify-contract.ps1:109`、
  `verify-native-prerequisite.ps1:95` 与 `:112`、`verify-e2-matrix.ps1:59` 与 `:84`、`verify-g3-a0-scope.ps1:24`）
  要改成宿主可移植解析。
- 355 张全部 `status: "draft"` ⇒ 按 skill `persona-authoring` §6，**没有一张能进运行时对话**
  （`PersonaDataLoader` 只放行 `approved`）。
- 8 个 definition 的 `realSelfBehaviors = {}`；73/76 物化过期；查士丁娜撞名；
  I2 `characterId` 76/76 违规。

---

## 九、复跑方式

```powershell
# 1. 标签归一（空跑 / 应用 / 报告）
py -3 AWAKE\tools\persona-workbench\tools\normalize-card-tags.py `
    --cards AWAKE\tools\persona-workbench\characters --dry-run `
    --report C:\temp\tagnorm.txt
py -3 AWAKE\tools\persona-workbench\tools\normalize-card-tags.py `
    --cards AWAKE\tools\persona-workbench\characters `
    --backup C:\temp\card-backup --report C:\temp\tagnorm-applied.txt

# 2. 角色事实表
py -3 AWAKE\tools\persona-workbench\tools\build-authoring-facts.py `
    --out AWAKE\docs\reference\persona-authoring-facts.v1.tsv

# 3. 平行门（17 条判据）
py -3 AWAKE\tools\persona-card-gate.py --cards AWAKE\tools\persona-workbench\characters --max-detail 0

# 4. 官方门禁链（工作区脚本）
powershell -NoProfile -ExecutionPolicy Bypass -File `
    AWAKE\tools\persona-workbench\tools\run-card-gates.ps1 -SkipCompile
```

---

## 十、本轮改了哪些文件

| 文件 | 状态 |
| --- | --- |
| `AWAKE/tools/persona-workbench/tools/normalize-card-tags.py` | 新增 |
| `AWAKE/tools/persona-workbench/tools/build-authoring-facts.py` | 新增 |
| `AWAKE/docs/reference/persona-authoring-facts.v1.tsv` | 新增（355 行） |
| `AWAKE/docs/PERSONA-TAG-AND-KINGDOM-NORMALIZATION-20261001.md` | 新增（本文） |
| `AWAKE/tools/persona-workbench/characters/*.persona.json` | 278 张改（标签/facet）；堤诺普斯 1 张重写 |
| `AWAKE/tools/persona-workbench/characters/*.origins.json` | 107 个改（`kingdomId`） |

**后两行没有进本次提交。** 那 279 张卡 + 279 个侧车在 git 里是未跟踪状态（`git ls-files --others`，
合计 558 个 `.json`）。官方 `5-provenance` 门把它们的处置定义成项目级二选一 ——
「确认内容后 git add 纳入，或明确废弃并移出 characters/」。而本轮已确认这些卡的内容**不达标**
（散文层 `CORE_TOO_SHORT 278` / `CROSS_CARD_DUPLICATE 262` 全部原样），
所以此时纳管与门的判据自相矛盾。**纳管还是废弃，等重写完成后再定。**
