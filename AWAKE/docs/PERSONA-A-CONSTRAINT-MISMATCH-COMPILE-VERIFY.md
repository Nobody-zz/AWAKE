# A 类约束脱节 · 真实编译验证报告

> 结论：46 张角色卡**全部无法通过真实编译**。失败 100% 发生在迁移链的最上游两道门（根白名单、19 键 registry 校验），从未触达 crosswalk 映射/emit 阶段。
> 上一轮的「crosswalk 缺项」是伪命题——因为上游 `PersonaValidator` 已把卡拒之门外，crosswalk 缺不缺行根本轮不到触发。
> 根因不是「该补的映射没补」，而是**作者侧 schema 与迁移侧编译器对同一批字段的约束方向相反**，且**编译器 19 键 registry 落后于运行时 8 键 registry**。

---

## 1. 验证方法（权威路径，非静态推断）

本报告数字全部来自对真实编译链的执行，而非正则/脚本猜测。临时验证程序（`C:\Users\26811\AppData\Local\Temp\pwb-compile-verify\`）通过 `dotnet run` 直接引用源码项目 `PersonaWorkbench.Core`，对 `characters\*.persona.json` 逐张调用运行时代码路径：

| 层 | 调用的真实代码 | 模拟的权威行为 |
|---|---|---|
| L1 加载 | `PersonaDocumentCodec.Deserialize(json, registry)` | `PersonaDocumentStore.Read` 的根白名单校验 |
| L2 校验 | `PersonaValidator.Validate(doc, PersonaTagRegistry.CreateDefault())` | 编译器 19 键校验（`Build` 第一步） |
| L3 迁移 | `PersonaAuthoringV2Adapter.Build(doc, "", "none")` | 完整 authoring-v2 迁移（含 crosswalk/emit） |

样本：`characters/` 下 46 个 `*.persona.json`。registry 三个事实源：编译器 `CreateDefault()` **19 键**、运行时 `tag_registry.json` **8 键**、作者侧 JSON schema **开放词汇**。

---

## 2. 结果总表（46 卡 → 0 成功）

| 失败类型 | 命中卡数 | 判断层 | 契约来源 |
|---|---|---|---|
| `persona.document_unknown_property`（根字段越界，实为 `origins`） | **46/46** | L1 加载 | schema 有 `origins`，编译器白名单无 |
| `tag.unregistered`（tags 数组越界键） | **46/46** | L2 校验 | 19 键 registry 外 53 个键 |
| `facet.unregistered`（facetStrengths 越界键） | **35/46** | L2 校验 | 19 键 registry 外 36 个键 |
| `axis.value_invalid`（轴值越界 ±2） | **36/46** | L2 校验 | schema `-3..3` vs 编译器 `-2..2` |
| `facet.strength_invalid`（facet 值越界 1..4） | **1/46** | L2 校验 | schema `0..5` vs 编译器 `1..4` |
| **Build 成功（`IsSuccess`）** | **0/46** | L3 迁移 | — |

`ok:` 反向计数（无该错误的卡）：轴值合规仅 10 张、facet 键合规仅 11 张、facet 值合规 45 张。

---

## 3. 三层约束脱节对照（根因本体）

同一批字段，三个真相源给出**方向相反**的约束，作者照着 schema 写、编译器照着 registry 拒：

| 字段 | 作者侧 schema（`.character.v1`） | 迁移侧编译器（`PersonaValidator`） | 运行时（`tag_registry.json`） |
|---|---|---|---|
| tag 键 | 开放 `string[]` | 封闭 **19 键** `tag.unregistered` | **8 键** |
| facet 键 | 开放 `additionalProperties` | 封闭 19 键 + 禁 trigger/boundary | （facet 无独立 registry） |
| facet 值 | `0..5` | `1..4` | — |
| 轴值 | `-3..3` | `-2..2` | — |
| `origins` | 显式声明 | **白名单外** `document_unknown_property` | — |

编译器 19 键 ≠ 运行时 8 键，**交集仅 2 个**：`trait.pragmatic`、`expression.measured`。

---

## 4. 越界键清单（拆分两类，卡数来自真实编译）

### 4a. 运行时漂移键（编译器 19 键不认，但运行时 8 键已注册）—— 系统性缺陷

作者在**大量、正确地**使用运行时语义，编译器却因 registry 落后而拒绝：

| 键 | 类别 | 命中卡数 |
|---|---|---|
| `boundary.no_modern_psychology` | boundary | **41** |
| `boundary.no_instant_submission` | boundary | **40** |
| `trigger.threat_or_leverage` | trigger | 28 |
| `expression.indirect` | expression | 20（facet 另 16） |
| `behavior.conditional_cooperation` | behavior | 11（facet 另 14） |
| `trait.status_conscious` | trait | 9（facet 另 7） |

### 4b. 纯自造键（三方都无）—— 作者自由发挥，47 个 tag / 36 个 facet

按前缀分布（括号内为命中卡数）：

- **behavior 9**：`acts_before_reasoning`(2)、`administers_fairly`(1)、`charges_first`(2)、`guards_home`(1)、`misrepresents`(1)、`retaliates`(1)、`seeks_power`(1)、`tests_commitment`(2)；另 `conditional_cooperation` 计入 4a
- **boundary 4**：`no_direct_confrontation`(1)、`no_grand_ambition`(1)、`no_intrigue`(1)、`no_servility`(1)
- **expression 12**：`blunt`(3)、`evasive`(2)、`fierce`(1)、`flamboyant`(2)、`frank`(2)、`high_energy`(1)、`informal`(1)、`ingratiating`(1)、`reserved`(3)、`stoic`(1)、`understated`(2)、`warm_to_allies`(1)
- **trait 13**：`caution`(4)、`caution_for_family`(1)、`courage`(1)、`deceitful`(2)、`direct`(1)、`hedonistic`(1)、`kind`(4)、`loyal`(2)、`pride`(2)、`private`(2)、`tradition`(2)、`wild`(1)、`will`(2)
- **trigger 10**：`being_put_on_pedestal`(1)、`clan_safety`(1)、`duty_or_responsibility`(1)、`family_safety`(8)、`fear_of_decline`(1)、`loyalty_or_betrayal`(1)、`reputation_challenge`(2)、`social_slight`(1)、`threat_to_home`(2)、`threat_to_house_honor`(1)

近名漂移（作者概念对、键名对不上 registry）突出三处：`trait.caution`↔`trait.cautious`、`trait.pride`↔`trait.proud`、`trait.tradition`↔`trait.traditional`。

---

## 5. 为什么「crosswalk 缺项」是伪命题

- `PersonaAuthoringV2Adapter.Build` 的**第一行有效逻辑就是 `PersonaValidator.Validate`**（`PersonaAuthoringV2.cs` L49-53），校验失败即 `return Failure`，**不进入** crosswalk 映射循环（L111 起）。
- 因此每张卡都死在 L1/L2，`TryReadSourceValue` / `emit` / `preserve_only` 从未执行。上一轮列的「53 tag + 36 facet 缺项」其实是被上游拦截的**越界输入**，不是 crosswalk 的漏洞。
- `ValidateCrosswalkClosure`（L165-194）只做单向校验：确保「契约封闭集 → crosswalk 有 row」。它对 registry+profile 的封闭契约是**全覆盖、可过**的，crosswalk 本身零缺项。

---

## 6. 修复方向（分层，待决策，不实施）

1. **定单一权威词汇源**：三套 registry（schema 开放 / 编译器 19 / 运行时 8）必须收敛。先回答「运行时 8 键是否就是最高权威」——若是，编译器 `CreateDefault()` 应扩到与之对齐，4a 的 6 键漂移即消失。
2. **`origins` 归属**：它是作者侧校验元数据、非运行时语义。二选一：(a) 从 `persona-workbench.character.v1.schema.json` 移除并旁挂为 sidecar 文件；(b) 在 `PersonaDocument` 增 `Origins` 并把 `origins` 加进 `AllowedProperties` 白名单。倾向 (a)。
3. **范围统一**：轴 `-3..3`、facet `0..5` 与 `-2..2`、`1..4` 对齐到同一套，schema 与 validator 同步改，避免「schema 放行、编译器拒绝」的相反裁决。
4. **4b 自造键处置**：逐键决定「并入权威词汇 / 删 / 翻译成最近权威键」，并修正三处近名漂移。
5. **回归门禁**：把本报告所用验证程序固化为 `tools/persona-workbench/tools/compile-verify.ps1`（或等价），纳入样板门禁链，使「schema 通过但编译器拒绝」今后在编辑期即暴露。

决策点：请先定 (1) 权威词汇源 与 (2) `origins` 去留 —— 这两项决定了 4a/4b 的处置方式和改动范围。