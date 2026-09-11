# Worldbook Studio Quick Authoring 门面与渐进迁移架构方案

> 日期：2026-09-09  
> 状态：`implemented / offline_validated / final_audit_passed`  
> 适用范围：Worldbook Studio AI 生成链路与用户工作流  
> 不适用范围：AWAKE 模组本体、Semantic Migration 候选、真实 Provider、游戏目录

## 1. 决策摘要

Worldbook Studio 不重写现有 Core，也不把现有复杂架构简单藏起来。采用渐进迁移方案：

```text
现有安全内核
→ Quick Authoring 用户门面
→ QuickAuthoringOrchestrator 内部编排
→ 现有 Source Snapshot / Origin / Provider / CAS / Review / DocumentBuilder
```

普通用户只看到“提出目标 → 提供资料 → 查看草稿 → 确认进入编辑器”；命题、证据、来源、覆盖率和候选生命周期继续作为后台审计结构存在，并按需投影到高级详情。Quick Facade 只能提供单一入口和用户语言投影，不得复制 CandidateSet、Review 或 DocumentBuilder 的状态权威。

### 1.1 当前落地状态

- Quick Facade、`QuickAuthoringOrchestrator`、Pass A/B semantic packet、编辑后重验和 ContentPolicy 基础门控已接入现有链路。
- Pass A 将直接来源命题冻结为 `provisional` packet；Pass B 只能复用该 packet 的 facts、propositions、claims、target spans 和来源身份。
- retry/recovery 会复用冻结 packet；Provider fingerprint、prompt revision、normalization revision、source hash 和 packet hash 均参与身份校验。
- `adult_optional` 必须在生成前确认；成人语境出现时，资料还必须明确相关角色已成年，否则阻断。
- 当前验证使用本地假 Worker 和离线 harness；尚未使用真实 Worker、Cloud Provider、游戏或发布目录。
- 最终离线验收已通过：Worldbook Studio `113/113`、Batch `23/23`、Draft `75/75`、Workstation `12/12`、前端回归全通过，HTTP draft/save/batch smoke 和 public contract smoke 全通过。

## 2. 三种模式

### 2.1 手工编辑

用户直接填写标题、摘要、正文、身份表达和分类，不调用 AI 语义生成链路。

### 2.2 Quick Authoring

面向普通作者，从一份参考资料和简单目标生成待审核草稿。用户不需要理解 facts、claims、target spans 或 CandidateSet。

```text
简单目标 + 参考资料
→ 后台来源绑定与风险检查
→ 用户可读待确认草稿
→ 人工确认
→ needs_review 作者文档
```

Quick Authoring 不是 Semantic Migration，不自动完成多来源综合、复杂因果和高保真旧世界书迁移。

### 2.3 Semantic Migration

继续使用独立迁移工作流：

```text
source atom
→ proposition inventory
→ claim normalization
→ perspective / time / epistemic analysis
→ target span
→ human semantic review
```

## 3. 用户工作流

Quick Authoring 对外只暴露：

1. 内容类型：人物、地点、事件、势力、制度/文化或一般世界知识；
2. 创作目标：用户用自然语言说明想整理什么；
3. 参考资料：粘贴或导入；
4. 内容范围：基础内容或成人拓展；
5. 可选要求：一句话说明重点或禁忌。

高级约束可以展开，但不能要求用户理解内部字段名。

生成结果默认显示：

```text
AI 待确认草稿
正文
需要你确认的问题
来源详情
进入世界书编辑器
```

内部字段保持完整，但默认不平铺：

```text
proposition / claim / target span / source origin / coverage / candidate set
```

隐藏的是结构复杂度，不是证据和问题。每个用户可见的事实、摘要和表达都必须提供“查看来源”；全部问题必须可展开查看，硬阻断不得被折叠后隐藏。

`adult_optional` 不是普通内容范围选项。选择它之前必须通过 18+ 硬校验和 ContentPolicy gate；未通过或年龄不明确时按不满足处理。

## 4. 后台结构

```text
QuickAuthoringRequest
→ SourceSnapshot
→ SourceOriginCatalog
→ ProvisionalSemanticPacket
→ Semantic Gate
→ AuthoringProjection
→ Projection Gate
→ Review Candidate
→ AuthoringDraftDocumentBuilder
→ needs_review document
```

### 4.1 现有内核继续保留

- Source snapshot、source hash 和 CAS；
- 服务端 source origin catalog；
- Provider、consent、retry/recovery；
- CandidateSet 生命周期；
- review-only / pending / needs_review；
- evidence、quote、locator、quote hash；
- AuthoringDraftDocumentBuilder；
- 最终 accepted projection fingerprint。

SourceOriginCatalog 的分句结果只是可定位证据范围，不是已经完成语义原子化的结论。每个 origin 必须同时绑定原始 source snapshot、原始 offset、规范化文本和 quote hash；切分算法版本必须进入 request/packet 身份。

### 4.2 新增门面与编排

新增的主要职责是：

- 将简单用户输入转换为内部 intent；
- 调用现有来源、Provider 和审核组件；
- 将复杂内部结果投影为用户可读草稿；
- 将用户可执行的问题投影为有限操作；
- 保持旧路径可读取、可恢复、可回退。

`QuickAuthoringOrchestrator` 只做调用编排和投影，不拥有第二份候选状态、审核状态或持久化事实。所有状态仍由现有 Draft Store、CandidateSet、Review 和 DocumentBuilder 结算。

Legacy staged 只能作为旧结果读取、恢复和明确的兼容入口，不能绕过新的 source、consent、review 和 create-document Gate。

## 5. 生成策略

第一版采用保守双阶段，但不把第一阶段称为“已验证事实”。

### Pass A：直接来源分析

只提取 `direct_source` 命题，并标注：

- 事实、传闻、解释、推测、未知；
- 当前、历史、未来、无时间、未知；
- 肯定、否定、争议、未知；
- 视角和来源 origin；
- 需要人工确认的限定条件。

Pass A 结果为 `provisional`，服务端只验证结构和来源绑定，不替人确认语义真值。

Pass B 必须携带并校验不可变的 `source_content_hash`、`semantic_packet_hash`、`prompt_revision`、`normalization_revision` 和 `provider_fingerprint`。来源或语义包发生变化时，旧 Pass B 结果不得复用。

用户编辑任何 AI 文本后，必须标记为 `author_modified` 并重新执行适用的来源、约束和语义检查；检查未完成时不能显示为“证据已确认”。

### Pass B：作者投影

只能消费冻结的 semantic packet，生成：

- 标题；
- 摘要；
- facts；
- expressions；
- target text；
- 用户可读 warnings。

Pass B 不得新增 proposition、claim、origin、正式实体 ID、人物、年份、战争、关系或因果。

### 暂不自动处理

- multi-source synthesis；
- 复杂隐含因果；
- 跨段关系推断；
- 未编译的自然语言约束；
- 未注册 profile ID；
- 无法证明语义等价的自由改写。

这些内容进入 `unresolved` 或 `reanalysis_request`。

如果 Quick Authoring 因资料不足或不支持的推导而无法继续，必须提供可见的手工编辑回退，不得让用户误以为软件无响应。

## 6. 迁移策略

采用 Strangler Facade，而不是一次性重写。

### 阶段一：用户门面

已保留当前后端合同，并新增用户化 Quick Authoring 请求和结果投影。复杂审计信息只做折叠详情。

### 阶段二：内部编排

`QuickAuthoringOrchestrator` 已统一调用：

```text
source origin
→ provider
→ normalizer
→ gate
→ candidate
→ document builder
```

Quick UI 不再直接依赖旧的单次 complete 逻辑；旧 staged 仍由同一安全入口兼容。

### 阶段三：接入 Pass A / Pass B

用户仍然点击一次“开始整理”，当前内部已改为：

```text
Pass A
→ Gate A
→ Pass B
→ Gate B
```

### 阶段四：收敛旧路径

剩余收敛工作：

- legacy staged 继续保留；
- 旧 Quick complete 标记为兼容路径；
- 清理未使用的历史 Prompt 常量；
- 删除不再需要的静默默认修复；
- 保留旧 Draft 读取和恢复能力。

## 7. 安全与状态不变量

- 用户输入和来源正文永远是不可信数据；
- 模型不能创造 source origin ID 或正式 entity ID；
- Gate 处理的最终文本必须等于 DocumentBuilder 写入的文本；
- 用户修改后的文本必须重新标记状态；
- retry 默认复用冻结 packet；
- source、prompt、model、normalization 变化必须产生新 hash；
- 未人工采纳内容不得进入作者文档；
- 任何 AI 结果不得自动进入 approved、canon、compiled、published 或 runtime-ready。

## 8. 兼容性边界

- 不删除现有 CandidateSet、Draft Store、Provider、CAS 和 recovery；
- 新增内部 packet 时必须保留旧 Draft v1 读取；
- 严格 Provider 解析与 Legacy Adapter 分离；
- Quick 新路径不修改 Semantic Migration 候选；
- Batch facts-only 继续单独统计，不伪装成语义迁移；
- 不修改 AWAKE 模组本体、ModuleData、dist 或冻结产物。

## 9. 验收条件

### 用户验收

- 用户不阅读开发文档即可找到 Quick Authoring；
- 只填写基础输入即可开始；
- 用户理解结果是待确认草稿，不是正典；
- 用户优先看到真正需要处理的问题；
- 高级证据结构可以查看但不阻塞普通操作；
- 进入编辑器不会被误解为发布。

### 工程验收

- 新门面最终可达：入口 → 编排 → Provider → Gate → 草稿 → 作者文档；
- Gate 与 DocumentBuilder 使用同一份最终 authoring projection；
- origin、claim、target 和 evidence 可回溯；
- retry、CAS、recovery 和 Draft resume 保持安全；
- 旧 staged 路径继续可用；
- 不访问真实 Provider，不启动游戏，不同步目录。

### 质量验收

- direct-source 命题的 unsupported rate 可测；
- rumor→fact、historical→current、polarity drift 为零；
- source origin 无效引用为零；
- 用户修改后的旧证据复用为零；
- candidate 之间诊断不串台；
- 新旧路径用本地 Worker 做对照测试；
- 所有结果继续保持 review-only / pending / needs_review。

## 10. 明确不做

- 不重写整个 Worldbook Studio；
- 不把复杂内部结构删除；
- 不把 Semantic Migration 合并进 Quick Authoring；
- 不为了减少提示而取消来源证据；
- 不用 UI 隐藏来掩盖未解决问题；
- 不以一次成功生成证明模型没有幻觉；
- 不在未完成本地对照评测前宣称最终交付。
- 不把 source origin 的句子边界当作最终语义边界；
- 不让 Quick Facade 形成第二套 CandidateSet 或审核状态；
- 不让 Pass B 在缺少命题时自由补写，必须回到局部 reanalysis。
- 不隐藏来源、硬阻断或全部问题；
- 不让 adult_optional 绕过 18+ 和 ContentPolicy gate；
- 不让 legacy staged 绕过新的 Quick 安全边界。
