# AWAKE 原版状态数值与公式规范 v1

> 状态：数值方案草案；本文件先固定来源、公式、阈值和测试向量，不直接修改运行时代码。
>
> 编制日期：2026-08-23
>
> 适配规范：`docs/AWAKE-NativeState-Adapter-Spec-v1.md`
>
> 原版审计：`docs/mappings/bannerlord-native/关系-家族-家庭-外交原生映射表-v1.md`

## 1. 目标

本文件把“原版数值可以直接调用，也可以作为 AWAKE 数值变动依据，但必须由 AWAKE 自己的代码适配”的原则落成可执行的数值规则。

本文件解决四个问题：

1. 哪些值是 Bannerlord 原版权威值，AWAKE 只读或直接调用。
2. 哪些值是 AWAKE 的自定义状态，不能被原版值覆盖。
3. 原版值如何通过少量、确定性的整数函数转成 AWAKE 行为语义。
4. 玩家传授错误知识时，NPC 如何在不消耗额外 AI 算术的情况下接受、怀疑或拒绝。

设计要求：

- 代码计算，AI 表达；
- 先硬门槛，后简单评分；
- 权限决定“能知道什么”，关系决定“愿不愿意说/信”；
- 亲历和高确定性知识不能被一次对话强行覆盖；
- 关系数值不能被家庭或家族概念重复计算；
- 未经明确事件原因的原版关系变化，不自动改变 AWAKE 自定义信任。

## 2. 三类数值

### 2.1 原版权威值

这些数值由 Bannerlord 维护，AWAKE 不复制、不镜像、不自行持久化：

| 标识 | 原版含义 | 范围/形式 | AWAKE 处理 |
|---|---|---|---|
| `R_base` | 基础角色关系 | `-100..100`，默认 `0` | 需要原始关系语义时直接调用 |
| `R_effective` | 有效角色关系 | `-100..100` | 社会交往基线或兼容原版显示时直接调用 |
| `N_enemy` | 原版敌对 | `R_base < -25` | 作为原版敌对硬门控 |
| `N_friend` | 原版友好 | `R_base > 50` | 作为原版友好兼容判断 |
| `N_neutral` | 原版中立 | `-25..50` 边界包含 | 作为无敌友状态回退 |
| `ClanRelation` | 原版家族关系结果 | 运行时计算 | 家族政治用途直接调用 |
| `FactionStance` | 派系姿态 | 中立/战争/和平等 | 外交和战争事实直接调用 |
| `FamilyLinks` | 家庭拓扑 | 父母、配偶、子女等 | 作为关系语境，不当作数值 |

原版关系的具体实现事实：

- 关系默认值为 `0`；
- 关系上下限为 `-100..100`；
- 原版有效关系会加入 Honor `±2`、Valor `±1`、Mercy `±1` 的人格修正；
- 原版家族聚合使用普通领主 `0.1`、族长额外 `0.2`、族长配偶额外 `0.05`，双方同时为族长时组合权重乘 `20`；
- 族长更换时原版已有旧族长关系乘 `0.7` 的迁移逻辑。

以上数值属于原版实现，AWAKE 只读取结果，不重新实现这些公式。

### 2.2 AWAKE 自定义持久值

这些值表示 AWAKE 的认知和记忆，不等于原版好感：

| 标识 | 范围 | v1 语义 | 是否被原版值自动覆盖 |
|---|---:|---|---|
| `T_awake` / `trust` | `-100..100` | 当前关系目标对玩家说法和行为的长期可信度 | 否 |
| `L_awake` / `love` | `-100..100` | 私人情感、依恋或欲望轴 | 否；不参与客观知识可信度 |
| `H_awake` / `hostility` | `-100..100` | AWAKE 记录的主动敌意、怨恨和报复倾向 | 否；不等于原版敌对 |

当前存档结构仍以 `heroId` 为核心。本文件不修改存档键，也不假设当前已经支持完整 NPC-NPC 方向性关系。

### 2.3 AWAKE 临时派生值

这些值只在一次业务结算中生成，不进入存档：

- `S_social`：由原版有效关系映射出的社会合作修正；
- `T_modifier`：由 AWAKE 信任映射出的可信度修正；
- `H_penalty`：由 AWAKE 敌意映射出的合作惩罚；
- `B_score`：玩家说法的代码可信分；
- `D_score`：知识披露意愿分；
- `Q_score`：请求接受分。

派生值必须有明确的业务名称，禁止把所有用途重新合并为一个全局 `affinity`。

## 3. 原版值的直接调用规则

### 3.1 个人关系

| 业务问题 | 使用原版值 | 不能替代为 |
|---|---|---|
| 原版敌对门控 | `N_enemy` / `Hero.IsEnemy` | `H_awake` 或 `affinity` |
| 原版友好判断 | `N_friend` / `Hero.IsFriend` | `R_effective` 自行重算 |
| 社会距离基线 | `R_effective` | `R_base` 单独代替 |
| 原始关系事件 | `R_base` / `Hero.GetBaseHeroRelation` | 家族关系 |
| AWAKE 记忆可信度 | `T_awake` 加投影 | `R_base` 直接覆盖 |

### 3.2 家族、家庭和外交

AWAKE 不建立“家族好感”或“家庭好感”副本：

| 业务问题 | 权威读取 | 处理方式 |
|---|---|---|
| 两个角色的私人交往 | `Hero.GetRelation` | 使用有效个人关系 |
| 是否属于同一家族 | `Hero.Clan` | 只输出 `sameClan` |
| 家庭关系 | `Father`、`Mother`、`Spouse`、`Children` | 输出家庭拓扑标签 |
| 家族政治关系 | `Clan.GetRelationWithClan` | 直接调用原版家族结果 |
| 领主聚合家族关系 | `FactionManager.GetRelationBetweenClans` | 只用于政治/外交语境 |
| 战争/和平 | `StanceLink` / 派系状态 | 直接使用姿态和战争事实 |
| 族长更换 | `ChangeClanLeaderAction` | 重新读取，不重复迁移 |

`sameClan`、`sameFamily` 和“族长是亲属”不自动给 `T_awake` 加分。它们只能影响身份、权威、利益和知识规则，具体规则必须明确写在对应业务函数中。

## 4. AWAKE 基础投影函数

以下函数是 v1 的确定性基线。它们只处理整数和枚举，不访问 AI。

### 4.1 原版敌友状态

```text
NativeConflict(R_base):
    if R_base < -25: return enemy
    if R_base > 50:  return friend
    return neutral
```

边界必须保持原版含义：

- `R_base = -25` 是中立，不是敌对；
- `R_base = 50` 是中立，不是友好。

### 4.2 社会修正 `S_social`

`S_social` 不是原版关系的新版本，只是将有效关系用于 AWAKE 行为时的轻量修正：

```text
SocialModifier(R_effective):
    if R_effective <= -50: return -20
    if R_effective < 0:    return -10
    if R_effective <= 25:  return 0
    if R_effective <= 50:  return 10
    return 15
```

说明：

- `S_social` 只影响合作、披露意愿和非敌对交往；
- 它不能授予知识权限；
- 它不能改变 `N_enemy`、`N_friend`；
- 它不能写回原版关系；
- 它不进入 AWAKE 存档。

### 4.3 信任修正 `T_modifier`

```text
TrustModifier(T_awake):
    return clampTowardZero(T_awake / 5, -20, 20)
```

采用整数除法并向零截断：

| `T_awake` | `T_modifier` |
|---:|---:|
| `-100..-95` | `-20..-19` |
| `-50..-46` | `-10..-9` |
| `-5..4` | `-1..0` |
| `45..49` | `9` |
| `50..54` | `10` |
| `95..100` | `19..20` |

这样可以让 AWAKE 信任影响行为，但不会压过原版敌友事实或知识权限。

### 4.4 敌意惩罚 `H_penalty`

```text
HostilityPenalty(H_awake):
    if H_awake >= 70: return hard_block
    if H_awake >= 40: return -15
    return 0
```

负的 `H_awake` 不再额外产生正分；降低敌意应通过事件更新或其他正向状态表达，避免同一事件被双重奖励。

`H_awake >= 70` 的 `hard_block` 只阻止普通自愿合作、主动披露和普通请求，不阻止明确的威胁、求生、权力胁迫或战争事件路线。那些路线必须拥有自己的事件规则。

## 5. 玩家传授知识的可信度公式

### 5.1 先区分三个问题

代码必须分别处理：

1. **NPC 是否本来知道？** 由世界书权限、身份、记忆和亲历状态决定。
2. **NPC 能否说出？** 由知识权限、表达详细度和披露意愿决定。
3. **NPC 是否相信玩家新说法？** 由本节的可信度公式决定。

高关系只能影响第三个问题的一部分，不能绕过第一个问题。

### 5.2 证据枚举

AI 可以辅助把玩家说法归类，但只能输出有限枚举；数值由代码映射：

| 枚举 | 分值 | 说明 |
|---|---:|---|
| `none` | `0` | 没有可核验支持 |
| `specific` | `5` | 说法具体、连贯、可追问 |
| `corroborated` | `10` | 有旁证、见证人或多个一致来源 |
| `direct_proof` | `20` | 有直接证据或可立即验证的事实 |

AI 不得直接提交任意 `evidenceScore`；未知枚举按 `none` 处理。

### 5.3 说法一致性枚举

| 枚举 | 分值 | 说明 |
|---|---:|---|
| `coherent` | `5` | 与 NPC 已知背景不冲突，叙述连贯 |
| `unclear` | `0` | 信息不足，无法判断 |
| `inconsistent` | `-10` | 前后矛盾或与明显常识不符 |

### 5.4 利益偏向枚举

利益只做小幅偏向，不能把利益变成“自动相信”：

| 枚举 | 分值 |
|---|---:|
| `benefits_npc` | `5` |
| `neutral` | `0` |
| `costs_npc` | `-5` |

### 5.5 已知知识的冲突惩罚

只有玩家说法与 NPC 已有知识冲突时才应用：

| NPC 已知状态 | 冲突惩罚 | 是否允许一次对话直接覆盖 |
|---|---:|---|
| `unknown` | `0` | 不适用 |
| `rumor` | `5` | 可以重新评估 |
| `probable` | `15` | 可以动摇，但不自动确认 |
| `confirmed` | `30` | 只能进入怀疑/暂信，不能直接改成客观事实 |
| `witnessed` | `60` | 无直接证据时直接拒绝 |

`confirmed` 和 `witnessed` 是运行时知识状态，不修改世界书的客观事实字段。

### 5.6 可信分 `B_score`

```text
BeliefScore(input):
    score = 50
          + TrustModifier(T_awake)
          + SocialModifier(R_effective)
          + EvidenceBonus(input.evidence)
          + CoherenceBonus(input.coherence)
          + InterestBonus(input.interest)
          - ConflictPenalty(input.knownState, input.hasConflict)

    return clamp(score, 0, 100)
```

### 5.7 可信结果

对于没有高确定性亲历冲突的普通说法：

| `B_score` | 结果 | 记忆处理 |
|---:|---|---|
| `75..100` | `accept_unverified` | 可记为“已接受但未核实”，不得写入客观世界事实 |
| `55..74` | `tentative` | 记为暂信或待核实 |
| `30..54` | `skeptical` | 保留疑问，不主动传播 |
| `0..29` | `reject` | 不采纳；可以记录“玩家曾这样说过” |

### 5.8 高确定性知识的硬规则

```text
if knownState == witnessed and hasConflict:
    if evidence == direct_proof and T_awake >= 70:
        return tentative_without_overwrite
    return reject

if knownState == confirmed and hasConflict:
    if B_score >= 75:
        return tentative_without_overwrite
    if B_score >= 30:
        return skeptical
    return reject
```

这意味着：

- 高信任可以让 NPC 暂时动摇；
- 高信任不能凭一段话抹除亲历事实；
- `tentative_without_overwrite` 只能产生临时记忆或疑点；
- 只有后续事件、核验或周报系统才能把状态提升为更高确定性；
- AI 不得把“接受表达”解释成“客观世界事实已被改写”。

## 6. 知识披露公式

### 6.1 权限先行

世界书或内容包给出的身份权限是硬门槛：

```text
没有权限 → 不得因为关系高而获得该知识的详细内容
有权限     → 再计算是否愿意说、说到什么程度
```

关系不能提升 `maxDetail`，只能影响在权限允许范围内的披露意愿。

### 6.2 披露分 `D_score`

```text
DisclosureScore(input):
    if NativeConflict == enemy and route != coercion_or_war:
        return hard_block

    if HostilityPenalty(H_awake) == hard_block:
        return hard_block

    score = 50
          + TrustModifier(T_awake)
          + SocialModifier(R_effective)
          + AuthorityContextBonus(input.authorityContext)
          + RiskAdjustment(input.risk)
          + HostilityPenalty(H_awake)

    return clamp(score, 0, 100)
```

v1 中 `AuthorityContextBonus` 和 `RiskAdjustment` 是两个独立输入，只允许使用有限枚举，不能由 AI 自由填数字：

| 输入 | 枚举 | 分值 |
|---|---|---:|
| `authorityContext` | `ordinary_context` | `0` |
| `authorityContext` | `trusted_professional_context` | `5` |
| `authorityContext` | `public_or_safe_context` | `5` |
| `risk` | `ordinary_risk` | `0` |
| `risk` | `dangerous_or_observed` | `-10` |

一次结算最多各取一项：一个 `authorityContext` 加分和一个 `risk` 调整，不得把同一事实同时填入两列重复计算。

### 6.3 披露结果

| 条件 | 结果 |
|---|---|
| 无世界书权限 | `no_knowledge_or_referral` |
| 原版敌对且非特殊路线 | `refuse_or_mislead` |
| `D_score < 25` | `refuse_or_deflect` |
| `25..49` | 只能说摘要、模糊说法或转介 |
| `50..79` | 在世界书允许上限内正常表达 |
| `80..100` | 在世界书允许上限内表达得更完整 |

“更完整”不等于“获得权限”。例如普通平民的世界书权限只有传说摘要，即使 `D_score = 100`，也不能说出贵族圈子的详细历史档案。

### 6.4 转介规则

当 NPC 没有该领域知识但系统存在广知 NPC 时，代码生成转介结果：

```text
unknown_knowledge
→ referralTarget = 公证商人 / 赎金经纪人 / 酒馆老板 / 本国头人
```

转介对象由身份和地点注册表决定，不由 AI 临时创造。

## 7. 玩家请求公式

请求结算不能只看关系，也不能由 AI 自由决定。请求分类、风险和利益先由代码确定。

### 7.1 请求风险和利益

| 枚举 | 分值 |
|---|---:|
| `risk_none` | `0` |
| `risk_low` | `10` |
| `risk_medium` | `25` |
| `risk_high` | `40` |
| `benefit_against_npc` | `-5` |
| `benefit_neutral` | `0` |
| `benefit_for_npc` | `10` |

### 7.2 请求分 `Q_score`

```text
RequestScore(input):
    if NativeConflict == enemy and input.canBeTreatedAsOrdinaryRequest:
        return hostile_request_gate

    if HostilityPenalty(H_awake) == hard_block:
        return hostile_request_gate

    score = 50
          + TrustModifier(T_awake)
          + SocialModifier(R_effective)
          + BenefitBonus(input.benefit)
          - RiskPenalty(input.risk)

    return clamp(score, 0, 100)
```

### 7.3 请求结果

| `Q_score` | 结果 |
|---:|---|
| `70..100` | `accept` |
| `50..69` | `accept_with_condition` |
| `30..49` | `delay_or_negotiate` |
| `0..29` | `refuse` |
| 原版敌对/高 AWAKE 敌意 | `refuse`、`retaliate` 或进入专门事件路线 |

`L_awake` 不进入普通客观知识相信公式，也不进入所有请求的通用加分。只有明确的私人情感请求路线，才可以另行设计情感专用修正。

## 8. 原版关系变化与 AWAKE 状态变化

### 8.1 不按数字自动同步

以下规则固定为禁止：

```text
原版关系 +10 → 不自动等于 trust +10
原版关系 -20 → 不自动等于 hostility +20
族长好感变化 → 不自动传播给全家
家族关系变化 → 不自动改写个人 trust
```

原因是原版关系只说明游戏社会关系的变化，不说明 NPC 对某个事实、某次承诺或某个信息来源的认知原因。

### 8.2 允许成为 AWAKE 状态变动依据的情况

只有当原版变化带有可识别的事件原因时，才可以由 AWAKE 事件规则决定自定义状态变化：

| 事件来源 | 原版值作用 | AWAKE 处理 |
|---|---|---|
| AWAKE 已结算的帮助/背叛事件 | 作为结果校验 | 按该事件的专用 delta 更新记忆/信任 |
| 原版明确的关系事件且能识别原因 | 作为结果事实 | 调用对应 AWAKE 事件映射 |
| 只有关系数字变化、原因未知 | 只刷新快照 | 不改变 `trust/love/hostility` |
| 族长更换 | 原版已完成迁移 | 失效快照，不重复传播 |
| 战争/和平姿态变化 | 政治事实 | 更新战争语境，不直接改变私人信任 |

具体事件 delta 另列事件规则表；本文件不允许用“所有关系变化乘一个比例”代替事件原因。

### 8.3 原版写回

AWAKE 如果确实要改变原版关系，只能通过：

```text
AWAKE 事件结算
→ Preflight / 权限 / 幂等
→ ChangeRelationAction 等原版入口
→ 原版完成钳制、事件通知和存档
→ AWAKE 使快照失效
```

不得直接改写 `CharacterRelationManager` 内部存储。

## 9. 代码函数清单

实现时应优先做成纯函数或无副作用策略函数：

```text
NativeConflict GetNativeConflict(int baseRelation)
int SocialModifier(int effectiveRelation)
int TrustModifier(int awakeTrust)
HostilityEffect GetHostilityEffect(int awakeHostility)
int BeliefScore(BeliefInput input)
BeliefResolution ResolveBelief(BeliefInput input)
int DisclosureScore(DisclosureInput input)
DisclosureResolution ResolveDisclosure(DisclosureInput input)
int RequestScore(RequestInput input)
RequestResolution ResolveRequest(RequestInput input)
```

函数共同约束：

- 不访问 AI、不发网络请求、不写存档；
- 不接收任意浮点权重；
- 枚举之外的输入按安全缺省处理；
- 输入相同，输出必须稳定；
- 所有结果保留用于日志的原因标签；
- 数值计算集中在一处，不在提示词、UI 和事件 JSON 各写一套。

## 10. 测试向量

以下案例是实现前必须通过的最小测试集。

| 编号 | 输入 | 预期结果 |
|---|---|---|
| N-01 | `R_base=-30`、`R_effective=20` | 原版敌对；不能因有效关系为正而解除敌对门控 |
| N-02 | `R_base=-25` | 原版中立，不得误判为敌对 |
| N-03 | `R_base=50` | 原版中立，不得误判为友好 |
| N-04 | `R_base=60`、`R_effective=40` | 原版友好；`S_social=10`；不把关系写成 `trust=60` |
| N-05 | 同一家族但 `R_base=-40` | 仍是原版敌对；不得因 `sameClan` 自动加信任 |
| N-06 | 同家庭但无知识权限 | 不能因为亲属关系获得世界书详细知识 |
| N-07 | NPC 亲历事实、玩家说法冲突、无直接证据 | `reject` |
| N-08 | NPC 亲历事实、玩家信任 `100`、有直接证据 | 最多 `tentative_without_overwrite`，不得覆盖亲历事实 |
| N-09 | NPC 只有传闻、信任 `80`、具体且有旁证 | 可 `tentative` 或 `accept_unverified`，不得写入客观世界事实 |
| N-10 | 无世界书权限、信任 `100` | 仍为 `no_knowledge_or_referral`，关系不能授予权限 |
| N-11 | 原版敌对、信任 `100`、请求普通披露 | `refuse_or_mislead`，除非进入明确特殊路线 |
| N-12 | `H_awake=70`、普通合作请求 | 进入 `hard_block`，不调用 AI 争论数值 |
| N-13 | 原版关系数字变化但原因未知 | 只刷新快照，不改变 AWAKE 三轴 |
| N-14 | 族长更换 | 读取原版新族长结果，不额外复制旧族长关系 |
| N-15 | Campaign 或 Hero 不可用 | 返回 `unknown`，不伪造中立，不阻塞原版行为 |

## 11. 性能和 Token 规则

- 每次对话/知识结算只生成一份原版社会快照。
- 同一结算链复用快照，不反复调用 Hero、Clan 和派系 API。
- 先做 `AccessGate`、`NativeConflictGate` 和 `HostilityGate`，门控失败时不调用 AI。
- AI 接收枚举、标签和最终门控结果，默认不接收原始关系数字。
- AI 不计算 `B_score`、`D_score`、`Q_score`，也不计算关系 delta。
- 只有需要自然语言表达时才调用 AI；纯拒绝、转介和固定提示可以由代码直接生成。
- NPC 知识记忆只保存有长期价值的结果；一次未核实的玩家说法不能自动写入客观世界知识。

## 12. 版本和门禁

本文件完成后只达到 `E0` 设计证据，不代表公式已进入运行时。

进入代码前必须满足：

1. 对本文件执行项目要求的 grill 和独立只读审查。
2. 用户签收公式、阈值和非目标。
3. 先写纯函数测试，再实现运行时接线。
4. 不在本批改变原版关系存档结构。
5. 不在本批把 `trust/love/hostility` 迁移成 NPC-NPC 双向键。
6. 不在本批自动把 AI 输出写回世界书客观事实。

## 13. 明确结论

AWAKE 的数值策略确定为：

> **原版数值直接负责原版语义；AWAKE 只用少量确定性函数做用途投影；AWAKE 自定义数值负责认知和记忆；AI 不拥有算术和结算权。**

尤其是：

- 原版好感高，不代表 NPC 必然相信错误知识；
- 家族关系高，不代表家族每个成员都共享同一私人信任；
- 亲属关系不自动授予世界知识权限；
- 玩家可以让 NPC 动摇，但不能一次对话抹除亲历事实；
- 原版关系变化只有在原因明确时，才可作为 AWAKE 自定义状态变化的依据；
- 未知原因的原版变化只刷新 AWAKE 语境，不制造新的 AI 记忆。
