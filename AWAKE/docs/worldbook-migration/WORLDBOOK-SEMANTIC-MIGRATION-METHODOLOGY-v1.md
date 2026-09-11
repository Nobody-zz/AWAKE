# Worldbook 语义迁移方法论 v1

> **状态：`superseded_by_v1.1_red_team_repair`。** 本文保留为历史方法版本和问题来源；新的迁移批次必须使用 `WORLDBOOK-SEMANTIC-MIGRATION-METHODOLOGY-v1.1.md` 及其机器契约，不得把本文单独作为全库迁移执行标准。

> 适用范围：将旧式世界书、规则库、人物背景或多变体知识库，迁移到结构化的知识 authoring 系统。  
> 当前验证对象：AWAKE Worldbook Studio 迁移试点。  
> 方法状态：`pilot_validated / semantic_review_required`。  
> 本文是可复用工作方法，不是任何世界书的 `canon`、发布说明或 Runtime 兼容声明。

## 1. 核心结论

世界书迁移不是字段转换，而是**知识重建**：

```text
旧文件
  → 来源证据
  → 原子命题
  → 认识论/视角/时间分析
  → 知识主题聚类
  → 重新撰写
  → 目标格式投影
  → 人工审核
```

最重要的原则：

```text
源文件是证据，不是正文。
旧 Variant 不是新 Expression。
旧文件不是目标 Document。
Keywords 不是事实。
RAG 摘要不是最终正文。
When 不是自动权限。
TextMapping 不是正式 Entity ID。
结构校验通过不是语义正确。
```

## 2. 目标与非目标

### 目标

- 保护唯一权威来源不被覆盖；
- 让每个目标命题可以回溯到具体来源证据；
- 将事实、解释、传闻、关系、状态和未决信息分离；
- 允许一份旧文件拆成多个目标命题，也允许多个旧文件合并成一个主题；
- 重新撰写目标知识，而不是以旧文换序、删句或同义词替换伪装迁移；
- 在没有来源、权限、实体和时代证据时 fail closed；
- 让迁移结果可复核、可拒绝、可重做、可追踪。

### 非目标

- 不把旧库自动正典化；
- 不自动补写来源没有的事实；
- 不自动决定最终世界观、时代、权限或内容层；
- 不把显示名、文件名或关键词直接变成稳定 ID；
- 不用 AI/Worker 代替作者对冲突和语义的裁决；
- 不把 Authoring v1 文档存在误报成 Runtime 可消费；
- 不以一次试点结果宣称全库迁移完成。

## 3. 三层数据模型

迁移系统必须保持三层分离。

### 3.1 来源证据层

来源证据层记录原始输入，不改变原文：

- `source_root`
- 相对文件路径
- 原始文件 hash
- 规范化文件 hash
- JSON pointer 或等价定位
- 原始 quote
- 规范化 quote
- quote hash
- 旧 ID
- 旧字段名
- 旧 Variant/When/TextMapping 索引

来源证据层回答：

> 原文在哪里？原文到底写了什么？输入是否发生变化？

### 3.2 语义中间层

语义中间层是整个方法论的核心。它不受旧格式或最终格式限制。

每条原子命题至少记录：

```text
claim_id
epistemic_kind
subject
predicate
object
perspective
time_scope
polarity
confidence
conflict_group_id
source_bindings[]
legacy_origin_ids[]
canonicalization_state
review_state
rewrite_rationale
loss[]
unresolved[]
```

语义中间层回答：

> 这段文字在知识上声称什么？谁这样说？是在什么时间范围内？它是事实、判断、传闻、关系、状态还是未决内容？

### 3.3 目标投影层

目标投影层才生成 Studio 文档：

- document；
- assertion；
- expression；
- source reference；
- profile/entity/referral binding；
- grants/denies；
- lifecycle/redirect。

目标投影层回答：

> 经过语义裁决后，如何用目标系统表达这条知识？

不能跳过语义中间层直接从来源文件生成目标文档。

## 4. 标准流水线

### 阶段 A：锁定权威来源

1. 明确唯一 source root；
2. 处理外层压缩包或同名目录；
3. 排除历史导出、四档副本、dist、Runtime 目录和旧发布包；
4. 记录 source root 的绝对路径作为审计元数据；
5. 生成不可覆盖的父 snapshot；
6. 给每个文件保存相对路径、大小、原始 hash、解析状态。

验收：

- 源文件数量明确；
- JSON/XML/YAML 解析状态明确；
- 源文件可以重新 hash；
- 源 root 未被写入；
- 任何后续候选都携带 snapshot ID。

### 阶段 B：建立 source inventory

对每个文件记录：

- 文件类型；
- 旧 ID；
- 字段集合；
- Keywords；
- RAG；
- Variants；
- When 条件；
- TextMappings；
- 运行时/配置信号；
- 成人/敏感信号；
- 解析异常；
- 主题候选；
- 实体候选。

此阶段只做发现，不做最终语义判断。

### 阶段 C：原子化来源

将旧输入拆成来源原子：

```text
file_metadata
keyword
rag
semantic_prototype
variant
when
text_mapping
```

每个来源原子必须有且仅有一个 disposition：

```text
preserve
merge
split
rephrase
drop
unresolved
```

规则：

- `drop` 必须有 loss；
- `unresolved` 必须有 rationale；
- 一个来源原子可以去向多个 claim，但必须列出全部目标；
- 多个来源原子可以合并到一个 claim，但必须保留全部来源；
- 不能把整个文件作为唯一宽泛证据来替代段落级定位；
- `SemanticPrototypes` 必须显式记录去向，不能静默丢失。

### 阶段 D：提炼原子命题

从来源原文中提炼最小可判断命题。

推荐句式：

```text
主体 + 谓词 + 客体/范围
```

例：

```text
沙拉斯湾位于西大洋伸入卡拉迪亚西岸的半封闭海域。
南部群岛削弱外海涌浪。
帝国传统把南部群岛称为“摇篮之岛”。
先祖登陆说缺少考古确证。
```

不要把多个层次压成一个命题：

```text
沙拉斯湾既是地理要地、帝国摇篮、神秘群岛和战略港口。
```

这类句子无法判断每一部分的证据、视角和知识类型。

### 阶段 E：认识论分类

#### `source_fact`

来源明确声称的事实，但不等于已批准正典。

例如：

```text
某山脉位于某高原东缘。
某村庄产出毛皮。
```

#### `interpretation`

来源中的判断、因果解释、战略评价、政治立场或文化解释。

例如：

```text
帝国领主把某湖视为防务节点。
商人认为道路不便会增加运输成本。
```

#### `rumor`

来源明确以传闻、据说、传说、民间记忆或敌对叙事呈现的内容。

例如：

```text
船员传说某群岛在雾中会改变形状。
当地传说湖水曾被鲜血染红。
```

#### `relationship`

必须有明确的主体、关系谓词和客体。

例如：

```text
某河流汇入某湖。
某村庄的毛皮进入区域贸易。
某半岛处在两片海域之间。
```

#### `state`

必须有时间或状态边界。

例如：

```text
某半岛曾先后由三个势力控制。
```

不能把历史状态直接写成当前状态。

#### `unresolved`

来源存在提示，但不能安全裁决。

例如：

```text
当前归属无法仅凭旧世界书确定。
旧 settlement marker 没有正式 registry 目标。
```

### 阶段 F：保留视角

每条命题都要问：

1. 谁在说？
2. 这是观察、信念、评价还是事实？
3. 这个说法是否只适用于某文化、职位、技能或社会位置？
4. 是否有其他来源对同一命题提出相反解释？

禁止把：

```text
库赛特人认为这片石地不适合骑兵
```

改写成：

```text
这片地形客观上不适合骑兵
```

正确做法是保留：

```text
epistemic_kind = interpretation
perspective = khuzait
```

文化视角可以被重新总结，但不能被抹平成无视角事实。

### 阶段 G：时间与状态

对每条命题标记：

```text
current
historical
bounded
unknown
```

遇到以下词汇必须谨慎：

```text
曾经
后来
如今
现在
仍然
已灭亡
被占据
属于
```

若来源只支持“历史上曾经控制”，目标不得写成“当前归属”。

当前运行时状态只能由游戏状态或明确 Runtime 来源提供，不能由历史世界书自动推导。

### 阶段 H：冲突分析

冲突不是重复，也不是必须消除的噪声。

同一主题出现不同文化叙述时：

1. 识别共同事实；
2. 分离各方的因果解释；
3. 为冲突命题建立 `conflict_group_id`；
4. 保留各自视角；
5. 不通过润色制造虚假的统一叙述。

冲突类型至少包括：

- 身份冲突；
- 地理归属冲突；
- 历史顺序冲突；
- 当前状态冲突；
- 战争责任冲突；
- 文化解释冲突；
- 事实与传闻层级冲突。

### 阶段 I：知识聚类

聚类单位应是知识主题，而不是旧文件。

常见拆分：

```text
一个旧地理文件
  → 位置
  → 地貌
  → 资源
  → 道路/水路
  → 战略意义
  → 历史控制
  → 传说
```

常见合并：

```text
多个地点文件
  → 同一水系知识
多个文化视角文件
  → 同一实体的不同解释层
```

合并/拆分决定必须记录：

- 原文件路径；
- 原文件 hash；
- 原旧 ID；
- 原子来源；
- 目标主题候选；
- 处理理由；
- loss；
- 人工确认事项。

### 阶段 J：重新撰写

目标正文的写作顺序：

1. 先写共同且稳定的地理/人物/势力事实；
2. 再写可独立证明的关系；
3. 再写历史状态；
4. 再写带视角的解释；
5. 最后写传闻和证据限制；
6. 将迁移元数据放在审阅报告，不放进世界书正文。

重写不是：

- 旧文换序；
- 旧文删句；
- 旧文拼接；
- 旧文逐句同义词替换；
- 把 RAG 改名为 summary；
- 把旧 Variant 改名为 expression。

重写必须体现：

- 命题合并或拆分；
- 事实与解释分离；
- 视角保留；
- 时间边界保留；
- 冲突显式保留；
- 不确定性保留；
- 目标系统的知识层次变清晰。

### 阶段 K：目标格式投影

只有完成语义中间层后才进入 Authoring v1。

投影规则：

- assertion 表示已经整理过的知识主张；
- expression 表示具体知识表达，不是旧 Variant 的机械容器；
- 只有需要 profile/层级/视角差异时才生成多个 expression；
- 未确认权限时不生成 grants/denies；
- 未确认实体时不写正式 entity ID；
- 未确认 content tier 时不进入 active/base closure；
- 所有候选保持 `needs_review`。

## 5. 关系、状态、权限和实体的特殊规则

### 5.1 `When` 处理

旧 `When` 默认是：

```text
视角/受众/条件证据
```

不是：

```text
自动 Grant
自动 Deny
```

只有在以下条件全部满足时，才能生成权限候选：

- 有明确的 profile/entity registry ID；
- 条件的语义已被人工确认；
- profile、文化、国家、聚落、职位、技能的映射关系已登记；
- `mapping_basis` 指向具体来源原子和 registry；
- 不会因为映射而放大原始知识范围。

### 5.2 TextMapping 处理

旧动态绑定只能先记录为：

```text
binding_candidate
unresolved_entity
reference_only
```

不能直接把：

```text
__bound_settlement__
```

变成正式：

```text
entity.settlement.*
```

正式 ID 需要单独的 registry 和人工确认。

### 5.3 内容层处理

内容层不是敏感词扫描的结果。

敏感词扫描只能产生：

```text
adult_or_sensitive_review_signal
```

不能自动产生：

```text
adult_optional
```

内容层决定需要结合：

- 明确年龄；
- 内容性质；
- 来源登记；
- 基础层边界；
- fallback/redirect/index/cache 闭包。

### 5.4 Source registry 处理

每个正式 source ref 至少需要：

```text
source_id
source_version
source_content_hash
locator_root
relative_locator
normalized_quote
quote_hash
```

必须能验证：

- source version 存在；
- source hash 匹配；
- locator 不越界；
- quote 可以从来源重新取得；
- quote hash 可以重算；
- `license_status=permitted`；
- `use_status=active`；
- `valid_until` 未过期。

## 6. 可复用的中间产物

每一批建议固定产出：

```text
SOURCE-SNAPSHOT-<batch>.json
SOURCE-INVENTORY-<batch>.json
SOURCE-PARSE-REPORT-<batch>.json
SEMANTIC-WORKSHEETS-<batch>.json
KNOWLEDGE-CLUSTER-PLAN-<batch>.json
CONFLICT-REPORT-<batch>.json
MIGRATION-LOSS-REPORT-<batch>.json
REWRITE-CANDIDATE-<batch>.json
SEMANTIC-REWRITE-VALIDATION-<batch>.json
NEEDS-REVIEW-<batch>.json
```

### Semantic worksheet 最小结构

```json
{
  "source_unit_id": "legacy unit id",
  "relative_path": "knowledge/rules/example.json",
  "input_sha256": "sha256",
  "review_state": "needs_review",
  "claims": [
    {
      "claim_id": "authoring_provisional.claim.<24 hex>",
      "epistemic_kind": "source_fact",
      "subject": "实体",
      "predicate": "关系",
      "object": "范围或对象",
      "perspective": "neutral or named perspective",
      "time_scope": "current|historical|bounded|unknown",
      "polarity": "affirmed|negated|undetermined",
      "confidence": "high|medium|low|unknown",
      "source_bindings": [],
      "legacy_origin_ids": [],
      "rewrite_rationale": "",
      "loss": [],
      "unresolved": []
    }
  ],
  "legacy_origins": []
}
```

### Target coverage 最小结构

```text
target_span
  → claim_ids[]
  → source_origin_ids[]
  → operation
```

`operation`：

```text
preserve
merge
split
rephrase
drop
unresolved
```

## 7. 质量门

### Gate 0：来源完整性

- source root 已锁定；
- 文件清单完整；
- 源 hash 可复核；
- 源目录未修改。

### Gate 1：语义覆盖

- 每个 source unit 有工作表；
- 每个旧输入原子有 disposition；
- `drop`/`unresolved` 有 loss/rationale；
- claim 有具体 source binding；
- worksheet、cluster、candidate、validation 的 source unit 集合一致。

### Gate 2：语义正确性

- 事实没有被写成解释；
- 解释没有被写成事实；
- 传闻没有被写成事实；
- 历史状态没有被写成当前状态；
- 视角没有被抹平；
- 极性没有改变；
- 主体/谓词/客体没有无记录改变；
- 没有目标正文之外的新事实。

### Gate 3：重写质量

- 目标正文不是旧 Variant 的全文复制；
- 不是简单换序、删句、拼接或同义词替换；
- 目标句/段都有 claim coverage；
- 高文本重叠有明确例外；
- 低文本重叠仍通过命题保持检查；
- 人工审阅记录 `understood_rewrite`。

### Gate 4：目标格式

- Authoring v1 schema 可解析；
- ID 命名空间正确；
- source ref 完整；
- entity/profile 引用闭合；
- grants/denies 不会无证据增加；
- content tier、universe、era 未确认时保持阻断。

### Gate 5：发布边界

- `needs_review` 不得进入 canon；
- 未批准不得 compile；
- 未通过 package/hash 不得发布；
- 未有 Runtime 证据不得宣称可消费；
- 未有游戏验证不得宣称入口闭环。

## 8. 证据等级与完成声明

迁移状态必须和证据等级分开记录。推荐使用项目统一的 E0–E5 体系：

| 等级 | 能证明什么 | 不能证明什么 |
|---|---|---|
| E0 | 计划、范围、静态文件存在、待办和假设已记录 | 不能证明内容正确或可触发 |
| E1 | JSON/XML/schema 解析、字段和引用结构通过 | 不能证明语义正确、权限正确或 Runtime 可用 |
| E2 | 离线 validator、golden fixture、重复/冲突/覆盖检查通过 | 不能证明游戏内可达或真实 Provider 行为 |
| E3 | 候选包、清单、版本和 hash 在规定输出位置一致 | 不能证明用户已经审核，也不能证明 Runtime 消费成功 |
| E4 | 用户运行匹配 BuildId 的游戏入口，完成入口→调用→结算→可观察结果闭环 | 不能自动证明读档、长时稳定性或所有内容分支 |
| E5 | 匹配 BuildId 的存读档、二次进入、长时或回归验证 | 不能替代来源、语义和人工内容裁决 |

世界书迁移的最小完成声明应分开写：

```text
source_read
inventory_complete
semantic_claims_extracted
knowledge_clustered
rewrite_candidate_generated
structural_validation_passed
semantic_review_passed
human_reviewed
canon_approved
compiled
published
runtime_verified
```

禁止用以下替代关系：

```text
JSON 可解析       ≠ 语义正确
schema 通过       ≠ 内容批准
hash 一致         ≠ Runtime 可消费
candidate 存在    ≠ canon
离线 smoke 通过   ≠ 游戏内闭环
```

## 9. 失败模式与纠正

| 失败模式 | 表现 | 纠正 |
|---|---|---|
| 字段搬运 | Variant 数量等于 expression 数量 | 回到 claim 提炼 |
| RAG 冒充正文 | summary 只是 RAG 改名 | RAG 只作线索 |
| 视角扁平化 | “某人认为”变成客观事实 | 保留 perspective |
| 历史升级 | “曾控制”变成“当前属于” | 增加 time_scope |
| 传闻升级 | “据说”变成 fact | 标记 rumor |
| 编辑判断入正文 | 写入“应保留”“不能证明” | 移到 review metadata |
| 权限自动化 | When 直接变 grant | registry-backed mapping |
| 实体猜测 | 文件名变 entity ID | unresolved entity |
| 结构幻觉 | schema 通过就宣布完成 | 独立语义审核 |
| 批量扩张 | 5 个文件成功就扩全库 | 只扩展已覆盖能力边界 |
| 状态混淆 | needs_review 被称为完成 | 分离状态词汇 |

## 10. 推荐的批次策略

不要一开始处理整个世界书。

推荐：

```text
批次 0：全库只读 snapshot/inventory
批次 1：5 个代表文件语义阅读
批次 2：5 个文件重写候选
批次 3：独立语义审阅
批次 4：修订后再扩展至其余 25 个
批次 5：主题簇级迁移
批次 6：正式 registry/permission/entity 绑定
批次 7：离线编译与包检查
批次 8：用户运行游戏后的 Runtime 验证
```

每个批次都要有：

- 输入 snapshot；
- 明确写入边界；
- 独立验证；
- 明确未决项；
- 下一动作；
- 可回退或可废弃状态。

## 11. 当前 AWAKE 试点的经验结论

当前 30 文件试点已经证明：

- 757 文件的单一下载包可以建立只读来源快照；
- 30 个 source unit 可以进入统一的工作表结构；
- 5 个文件足以暴露地理事实、文化视角、传闻、历史状态和动态绑定的主要难点；
- 旧 Variant 直接映射到 expression 是错误路线；
- 目标文档可以重新撰写为 32 条候选 assertion；
- grants/denies 在证据不足时应保持为空；
- Studio schema 通过仍可能被 source registry gate 阻断；
- source hash、结构状态和语义状态必须分开报告。

当前试点仍未证明：

- 30 个文件全部完成语义理解；
- 5 个候选已经通过人工语义批准；
- source registry 已可进入正典；
- entity/profile ID 已闭合；
- legacy When 已完成 Runtime permission 映射；
- AWAKE Runtime 可安全消费这些候选；
- 游戏内入口已经闭环。

## 12. 可复用执行清单

### 开始前

- [ ] 权威 source root 已明确；
- [ ] 历史副本和四档副本已排除；
- [ ] 源目录为只读；
- [ ] 当前 batch 的范围和非目标已写出；
- [ ] snapshot ID 和 hash 已记录。

### 阅读阶段

- [ ] 所有文件字段已盘点；
- [ ] 所有 Variant/When/TextMapping 已定位；
- [ ] 所有关键词和 RAG 已登记；
- [ ] 所有异常、重复和冲突已标记；
- [ ] 每个来源原子都有 origin ID。

### 语义阶段

- [ ] 每个 claim 有 subject/predicate/object；
- [ ] epistemic kind 已判断；
- [ ] perspective 已判断；
- [ ] time scope 已判断；
- [ ] polarity 已判断；
- [ ] conflict group 已建立；
- [ ] source binding 具体到段落或 JSON pointer；
- [ ] claim 与来源原子双向可追溯；
- [ ] loss 和 unresolved 已显式记录。

### 重写阶段

- [ ] 目标正文重新组织；
- [ ] 没有直接复制旧 Variant；
- [ ] 没有把 RAG 直接当 summary；
- [ ] 没有把传闻变事实；
- [ ] 没有抹平文化视角；
- [ ] 没有把历史状态写成当前状态；
- [ ] 没有把迁移元数据写入世界书正文；
- [ ] 每个目标句/段有 coverage；
- [ ] 所有新增/删减有 rationale/loss。

### 投影阶段

- [ ] Authoring v1 schema 可解析；
- [ ] 所有状态仍为 needs_review；
- [ ] grants/denies 没有无证据增加；
- [ ] entity/profile 没有无证据创建；
- [ ] content tier 没有被误标为已确认；
- [ ] source registry 未闭合时保持 blocked；
- [ ] compile/export 尚未被误报为完成。

### 交付阶段

- [ ] 写入文件列表明确；
- [ ] 未写入文件列表明确；
- [ ] 源 hash 前后相同；
- [ ] 验证输出可解析；
- [ ] 失败和阻断原因有分类；
- [ ] 未验证项和下一动作明确；
- [ ] 没有把静态证据包装成游戏内证据。

## 13. 方法版本演进

### v1.0

重点建立：

- 来源隔离；
- claim 中间层；
- 认识论分类；
- 视角与时间；
- 逐项来源绑定；
- 重新撰写；
- needs_review 交付。

### v1.1 计划

在不扩大迁移范围的前提下，补充：

- source proposition inventory 的正式 Schema；
- legacy origin exactly-once validator；
- target-span coverage validator；
- snapshot golden fixture；
- semantic preservation 正反例；
- provisional ID 的正式拒绝规则；
- source registry 与 child/parent snapshot 的 join 规则。

只有这些验证器在小批次通过后，才应扩大至其余 25 个文件。

## 14. 一句话版本

```text
先锁来源，再拆原子；
先辨命题，再分视角；
先保时间和冲突，再做聚类；
先重新写知识，再投影格式；
先保 needs_review，再谈 canon；
先有证据，再宣布完成。
```
