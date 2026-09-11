# 词条关联方式总览（分类系统梳理）— 2026-09-12（v2，补时序/因果/跨子系统）

> 回答"多个词条之间怎么关联"。按七条轴 + 运行时层组织。
> 设计总不变式：**一切跨词条关联都是元数据/指针，不构成调取容器，也不构成权限边界**——
> 调取单位永远是单个文档（keywords→entry），权限边界永远在表达层（grants/denies）。

## 轴一：内容类型（"这条知识是什么类型"）

| 机制 | 层级 | 说明 |
|---|---|---|
| 主分类 `domain` | 一级 | 政治/经济/文化/战争/地理，每档必选一个；运行时按主 domain 调取 |
| 二级主题 `subdomain` | 二级 | 每域稳定 ID 主题（地理→聚落/河流），新建必填；仅组织/索引/AI 建议元数据，不成为权限条件 |
| 相关分类 `related_domains` | 跨域 | 最多 4 项跨域关联 |

## 轴二：描述对象（"说的是谁/哪里/哪场战争"）

| 机制 | 说明 |
|---|---|
| 实体锚点 `entity_ids` | 文档绑到**已注册实体**——不限地点：城镇、英雄、家族，同样可以是战争、商路、神祇（凡实体登记表收录者） |
| 对象簇（地点簇为其一） | 同对象文档的聚合关联层：只作导航/百科浏览/referral 提示，**不作调取容器、不作权限条件** |

> **推广**：place_cluster 实为"对象簇"在地点上的实例；同一机制天然支持战争簇（同一战争的多档）、商路簇、家族簇。
> **开放问题**：簇是否允许嵌套（地区→地点，v1 有"巴旦尼亚地理"这类区域级条目）——投影批次定，倾向"区域档与地点档靠锚点关联，不做嵌套容器"。

## 轴三：名称（"同一个东西的多个叫法"）

`aliases`（别名进编译 keywords，多语言）+ `redirects`（旧名重定向；ID 发布后不可重用，合并/拆分必须人工映射）。

## 轴四：谱系（"这条知识的版本来龙去脉"）

`supersedes`（取代，如 r3→r2 九条映射）/ `split_from`·`merged_into`（拆合血缘）/ `revision`（同档版本线 r2→r3）。
**注意区分**：谱系轴管的是"同一知识的版本更替"，不是历史时序——后者归轴六。

## 轴五：时间/时序（"同一对象在不同时期"）★ 本次补

| 机制 | 层 | 说明 |
|---|---|---|
| `era`（B2） | 作者侧 | current / historical / pre_war / during_war / post_war / long_term / unknown；**历史不得写成 current** |
| `valid_from` / `valid_until`（B8） | 作者侧 | 知识的有效时段（如某归属只在某时段成立） |
| `occurredAt` / `firstSeenDay`·`lastVerifiedDay`·`expiresAt` | 运行时 | 事件发生时刻 / 记忆获得与衰减时刻 |

> 关系形态：**沿革链**——帕拉汶德示例的"当前名称档 + 历史名称档 + 继承档"本质是同一对象按时间层拆开的三个档，靠 era/valid_from 排成先后，而非相互取代。时间轴与谱系轴正交：r2→r3 是版本更替（谱系），"曾属巴旦尼亚→现归斯特吉亚"是时序（时间）。

## 轴六：因果与事件（"因什么而起、波及到谁"）★ 本次补

| 机制 | 层 | 说明 |
|---|---|---|
| `event_id` / `causationId`（TypedKnowledgeEntry） | 运行时 | 知识条目溯源到引发它的事件/因果链 |
| `EventPropagation`：visibility / scope / eligibleIdentityIds / propagationWatermark | 运行时 | 事件知识按可见性、地域范围（本地/本国/跨国/秘密）、指定身份定向传播 |
| `sourceType` = player / event / event_window_projection / native_observation | 运行时 | 运行时知识的获得途径（玩家亲授/事件见证/旁听/本地观察）——来源轴在运行时侧的延伸 |

> 关系形态：**因果链与传播链**——"北方战争的起因"档与"三年歉收"档、"某城被劫"档之间是因果/波及关系；这关系是运行时事件系统生成的，不由作者手填。

## 轴七：来源与冲突（"从哪来、和谁打架"）

claim→origin 绑定（locator + quote_hash，引文粒度细于断言）；`conflict_group_id` 多视角冲突分组（冲突不抹平）；`legacy_origin_ids` 旧视角链；冲突裁定走主编（B6 口径）。

## 跨子系统关联（作者层 ↔ 人格层）★ 本次补

`docs\mappings\persona-entity` 实体映射登记表 + `sourcePackId`（如 `awake.calradia.chronicle`）：世界书实体与 Persona 定义经同一实体登记表挂钩——NPC 的性格（persona）与它知道的世界知识（worldbook）在同一实体坐标上会合。表达层的 grants/profile 映射正是消费这张表。

## 运行时层（编译产物 + 查询期）

| 机制 | 说明 |
|---|---|
| keywords→entryIds 索引 | 编译期生成；一关键词命中多档出 `WB-INDEX-AMBIGUOUS`（歧义是弱关联信号） |
| `referralIds` / `hitIds` | 查询决策中的转介指针——对象簇联想顺序的运行时落点 |
| `grants` / `denies` / `scope`（本地/本国/跨国/秘密）/ `min_detail` | 表达层权限四件套——**权限轴**：决定关联知识能否被同一身份同时看到，不是词条间关系本身 |

## 一张图

```
        [一 内容类型] domain─subdomain─related_domains        [七 来源] claim→origin/conflict_group
                                  ╲                                      ╱
[四 谱系] supersedes/split_from ── 文档 entry ── [二 对象] entity anchor ⇄ 对象簇 ⇄ persona 映射层
                                  ╱              ╲╲
        [三 名称] aliases/redirects              ╱╲ [五 时间] era/valid_from（沿革链）
                                                ╱  [六 因果] event_id/causationId（传播链）
                          编译期：keywords 索引（歧义告警）＋锚点折入
                          运行时：domain 调取 → grants/scope 过滤 → referralIds 联想
```

## 状态

- **已实现**：五域/二级主题/相关分类（taxonomy v1 + CAS）、aliases/redirects、keywords 索引+歧义告警、锚点折入 keywords、EventPropagation/TypedKnowledgeEntry 字段契约、persona-entity 映射表；
- **本批新增（候选元数据级）**：place_cluster、split_from、supersedes 映射、conflict_group 沿用；
- **待落地（投影批次）**：正式锚点绑定、grants/profile/scope 映射、对象簇→referralIds 联想接线、子引文细化、era/valid_from 的正式校验、对象簇是否嵌套的裁定。
