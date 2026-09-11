# Worldbook Studio AI 生成链路落地方案

> 日期：2026-09-06  
> 状态：设计与迁移审阅建议，不代表已实现  
> 范围：AI 世界书生成与语义审阅链路；不修改 Studio、AWAKE 运行时或游戏目录。

## 1. 目标

把当前“模型生成候选正文”的链路改成可验证的中间层流水线：

```text
源文件
→ SourceAtom
→ PropositionInventory
→ ClaimDraft
→ Conflict/Perspective Analysis
→ TargetSpanDraft
→ Candidate
→ Structural Validation
→ Semantic Review
```

目标不是让模型直接产出可发布世界书，而是让模型输出可以被人和机器共同审计的候选。

## 2. 建议的数据契约

### SourceAtom

```text
source_unit_id
source_snapshot_id
source_locator
origin_kind
raw_value_hash
normalized_quote
```

### Proposition

```text
proposition_id
subject
predicate
object
epistemic_kind
perspective
time_scope
polarity
source_origin_ids[]
confidence
unresolved[]
```

### TargetSpanDraft

```text
target_span_id
text
claim_ids[]
source_origin_ids[]
operation
review_state
```

`operation` 建议限定为：

```text
preserve | merge | split | rephrase | drop | unresolved
```

## 3. 生成链路的职责分离

| 阶段 | 可以做什么 | 不可以做什么 |
|---|---|---|
| Source parser | 读取、定位、规范化 | 不改源文件 |
| Proposition extractor | 拆命题 | 不写最终正文 |
| Claim normalizer | 归纳与分类 | 不补证据 |
| Conflict analyzer | 标记冲突、视角、时代 | 不替人裁决冲突 |
| Authoring model | 组织表达 | 不生成权限或正式 ID |
| Validator | 失败即阻断 | 不静默修补语义 |
| Human reviewer | keep/revise/drop/unresolved | 不把结构通过当成批准 |

## 4. 推荐的生成策略

### 4.1 先抽取，后撰写

不要使用单次 prompt：

```text
请把这个旧世界书改写成新世界书。
```

改为四次有界任务：

1. 只抽取命题；
2. 只做 claim 归一化；
3. 只分析冲突和视角；
4. 只根据已经审核的 claim 组织正文。

### 4.2 使用失败闭环

每一步输出都经过：

```text
parse
→ schema
→ source join
→ semantic checks
→ accept/reject
```

失败时只重试当前步骤，不重新生成整篇文档。

### 4.3 对高风险命题降级

以下命题默认进入 `unresolved` 或 `interpretation`：

- 当前归属；
- 精确战争顺序；
- 文化群体的统一意图；
- 传闻的真实性；
- 物理不可进入；
- 由地貌推导出的军事用途；
- 由道路困难推导出的运输成本。

## 5. 最小验证矩阵

### 正常样例

- 多个 Variant 汇聚为一个稳定事实；
- 同一事实有多个来源定位；
- 一个源段拆成多个独立 claim。

### 阻断样例

- 正文出现没有 claim 的新命题；
- claim 没有 source origin；
- target span 引用不存在的 claim；
- rumor 被标为 fact；
- historical state 被标为 current；
- source perspective 丢失；
- `When` 生成 grant/deny；
- TextMapping 生成正式 entity ID；
- unknown content tier 被默认标成 base。

## 6. 工程落地顺序

### M1：契约层

- 固定 Proposition、ClaimDraft、TargetSpanDraft schema；
- 增加 source snapshot/hash 绑定；
- 增加 `unresolved` 和 `operation`；
- 验证空 source、空 claim、空 span 的阻断行为。

### M2：生成层

- 将单次生成拆成四个独立 route；
- 每个 route 使用版本化 prompt；
- 记录 model、prompt revision、输入 hash、输出 hash；
- 禁止 route 直接写正式 authoring 目录。

### M3：审阅层

- 建立按 proposition 查看 source quote 的界面数据；
- 显示 perspective、epistemic_kind、time_scope 和 polarity；
- 显示正文命题与 claim 的覆盖关系；
- 支持 `keep/revise/drop/unresolved`，不提供默认 approve。

### M4：评测层

- 建立五候选作为回归 fixture；
- 加入传闻、视角、历史、实体和权限阻断 fixture；
- 记录覆盖率、扩张率、视角保持率和人工返工率；
- 对不同模型和 prompt revision 做可复现比较。

### M5：发布门

- 只有人工 review decision 为 accepted，才允许进入后续编译候选；
- accepted 仍必须经过 source registry、permission、entity 和 tier gate；
- 未闭合的 source 或 permission gate 永远不得通过 fallback 绕过。

## 7. 观测与成本控制

每个 AI route 至少记录：

- request id；
- model/version；
- prompt revision；
- source snapshot；
- input/output hash；
- latency；
- token usage；
- schema failure；
- semantic failure；
- retry count；
- final disposition。

不要记录未脱敏的秘密、Provider token 或完整私有源文本到公共日志。

成本优化优先级：

1. 先用确定性 parser 做字段和定位；
2. 只把必要的 source atom 发送给模型；
3. 对重复 Variant 使用缓存，但缓存必须绑定 source hash；
4. 只重跑失败的 proposition/claim/span；
5. 用小模型做格式和覆盖筛查，大模型只处理冲突与重写。

## 8. 当前范围外

- 不接入真实 Cloud Provider；
- 不访问 API Key、Token、Worker 或网络服务；
- 不修改 Worldbook Studio 实现；
- 不修改 AWAKE 模组本体；
- 不同步游戏目录；
- 不生成更多 rewrite candidate；
- 不将本方案视为已实现功能。

## 9. 验收标准

本方案未来落地时，至少需要证明：

- source atom → proposition → claim → target span 可追溯；
- 正文多命题能被阻断；
- rumor/history/perspective/time/polarity 测试通过；
- `When`、TextMapping、registry 不越权；
- prompt/model/schema/fixture 可版本化；
- 失败可局部重试；
- 没有人工 signoff 时不会产生 approved/canon/published/runtime-ready 状态。

