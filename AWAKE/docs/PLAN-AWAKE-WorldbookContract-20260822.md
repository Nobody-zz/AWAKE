# AWAKE Worldbook Contract v1 统一世界书方案

- 日期：2026-08-22
- 状态：方案草案，待独立审查后进入实现计划
- 适用对象：AWAKE mod、Worldbook Studio、世界书作者、离线测试工具
- 目标：让模组运行时、世界书编辑器和作者内容共同遵守同一套世界书契约

## 1. 目标与核心判断

当前方向从“Studio v2 独立候选工具”调整为“AWAKE 正式世界书契约的编辑器、编译器和测试平台”。

最终关系固定为：

```text
AWAKE Worldbook Contract v1
        ├── authoring YAML：作者和 Studio 编辑
        ├── compiled runtime JSON/index：AWAKE 运行时读取
        └── preview/evaluation：Studio、Worker、运行时共用语义
```

Studio 不再以 `incompatible_with_v1` 作为最终目标；现有 Studio MVP 作为安全编辑和验证基础，后续改为生成 AWAKE runtime loader 正式消费的候选包。

## 2. 不可改变的边界

### 2.1 Studio 和 Runtime 共同负责

- 政治、经济、文化、战争四类客观世界知识。
- 国家、势力、城镇、村庄、家族、战争、贸易、宗教和习俗等知识对象。
- assertion、expression、来源、时代、宇宙、状态、权限和 referral。
- profile、文化、国家、聚落、年龄、Steward、角色和知识范围对知识可见性的影响。
- rumor、summary、detail、secret 四级表达层。
- base 与 adult_optional 内容层的隔离。
- 稳定 ID、revision、redirect、merge、split、tombstone 和审计记录。

### 2.2 Studio 当前不直接编辑

- AWAKE runtime 的债务、对话历史、压缩记忆、声音映射和存档状态。
- NPC 即时关系、事件结算、个人经历等运行时状态。
- 游戏同步脚本和游戏目录文件。
- Worker 或 AI 建议产生的未批准正典。

这些内容可以在未来通过 runtime 增量知识层接入，但不进入基础世界书正文契约。

## 3. 正式数据分层

### 3.1 Contract 层

建立轻量、无 YAML/AI/编辑器依赖的共享契约程序集或等价契约目录，定义：

- 稳定 ID 规则。
- 文档、assertion、expression、source、profile、referral、redirect DTO。
- domain、status、content_tier、knowledge_layer、scope 枚举。
- manifest 和 runtime package 版本。
- 运行时错误码和 unknown 行为。
- permission evaluator 的输入/输出结构。

AWAKE Runtime 和 Worldbook Studio 都以这一层为语义权威；运行时不依赖 YAML 解析器。

### 3.2 Authoring 层

作者使用 YAML 为主、JSON 为辅：

```text
authoring/
  documents/**/*.yaml
  sources/*.{yaml,json}
  audit/events/*.jsonl
  identity/id-ledger.jsonl
  suggestions/*
```

Authoring 可以保存来源全文、quote、作者备注、审批事件和诊断字段；这些字段不全部进入运行时。

### 3.3 Compiled Runtime 层

Studio 编译生成游戏读取包：

```text
runtime/
  manifest.json
  documents/*.json
  index/keywords.json
  index/aliases.json
  index/entities.json
  index/profiles.json
  index/domains.json
  index/referrals.json
  reports/compile.json
  SHA256SUMS.txt
```

运行时只读取经过批准、分层、权限和 hash 校验的 runtime package，不读取原始 YAML、suggestions、作者诊断或来源全文。

## 4. 世界书知识模型

每个正式文档至少包含：

```text
knowledge/document
  id
  revision
  domain
  title
  status
  universe
  era
  content_tier
  subject/entity references
  assertions
  sources
  authority
  retrieval metadata
  lifecycle
```

每个 assertion 表达一个事实、状态、关系、传闻或解释；每个 expression 是面向特定身份和详细层级的可读表达。

```text
assertion
  id
  revision
  kind
  semantic content
  source refs or approved author record
  expressions[]

expression
  id
  revision
  layer: rumor|summary|detail|secret
  text
  grants[]
  denies[]
  fallback_referral_ids[]
```

同一事实可以有多个表达，但不得因为文风变化复制出多个互相独立的事实 ID。

## 5. Runtime 检索契约

运行时读取流程固定为：

```text
输入问题、关键词、实体或对话意图
        ↓
关键词/别名/实体/domain 索引候选
        ↓
宇宙、时代和生命周期过滤
        ↓
profile 继承链与上下文过滤
        ↓
文化、国家、聚落、角色、年龄、Steward、scope 判断
        ↓
deny 优先于 grant
        ↓
选择最高允许的 expression layer
        ↓
返回可见表达、rumor 或 unknown
        ↓
无权读取时返回合法 referral
```

运行时不得每次对话扫描完整世界书；必须使用编译期索引和稳定 ID。

### 5.1 Retrieval 元数据

新世界书统一使用结构化 retrieval 元数据：

- keywords。
- aliases。
- entity_ids。
- domain。
- priority。
- query intent 或 knowledge kind。
- runtime gate。
- fallback/referral hints。

现有运行时的 `KEYWORD`、`Priority`、`When` 只作为代码审查和运行时行为参考，不构成新世界书的内容规范，也不要求旧文本迁移兼容。新契约应根据实际读取需求重新定义检索字段。

### 5.2 Runtime 输出隔离

NPC 可见结果只能包含：

- knowledge_id 或运行时稳定引用。
- visible/rumor/unknown/redacted 状态。
- 可见文本。
- 允许的 layer。
- referral ID 或显示对象。

不得泄漏：

- source ID 和来源全文。
- author diagnostics。
- grant/deny 规则 ID。
- 审计解释链。
- Worker 建议。
- 被拒绝的正文。

## 6. 身份和权限契约

运行时向读取器提供稳定 identity snapshot：

```text
profile_id
age
steward
culture_id
kingdom_id
settlement_id
role
knowledge_scope
```

scope 顺序固定为：

```text
local < regional < national < faction < elite < private
```

权限结算固定为：

```text
匹配 deny
    > 匹配显式 grant
    > profile 继承权限
    > unknown
```

空 grant 和空 deny 不表示全体可见。

表达层只能降级，不能升级：

```text
secret → detail → summary → rumor → unknown
```

普通平民、酒馆老板、赎金经纪人、公证商人、头人、士兵和贵族的差异通过 profile、scope、context 和 expression layer 共同表达，不再复制多套互相漂移的世界书正文。

## 7. 旧世界书的处理方式

现有四版世界书与新的 AWAKE 世界书在世界观、编写调性和内容组织方式上不一致，不作为新规范的迁移目标：

```text
卡拉迪亚（炫压抑）
卡拉迪亚（黑暗且压抑）
卡拉迪亚（超绝炫压抑）
卡拉迪亚（血腥且压抑）
```

处理原则：

- 旧四版仅作为可选参考素材，不是新世界书权威来源。
- 不要求保留旧 ID、旧目录、旧文风或旧表达层。
- 不建立旧四版到新世界书的自动迁移链。
- 不把旧四版的成人化表达、人物口径和叙事结构带入新正典。
- 新内容直接按 AWAKE Worldbook Contract v1 和新世界观重新撰写。
- 如需参考旧资料，必须由作者重新判断、重新改写、重新登记来源和重新审批。

旧内容不会阻塞新世界书的 Contract v1 冻结，也不会成为 Studio 的兼容负担。

## 8. Studio 需要对齐的功能

Worldbook Studio 继续保留当前已完成的：

- Schema 校验。
- Source registry。
- Audit event 和 ID ledger。
- Profile/referral registry。
- Permission preview。
- Content graph。
- Adult confirmation token。
- 原子候选发布。
- CLI/Web 和 F01–F21 测试。

改造重点：

1. authoring Schema 与 Contract v1 共用稳定模型。
2. 编译输出从独立 v2 候选改为正式 runtime package。
3. 增加 retrieval/index 字段和编译索引。
4. preview 调用共享 permission evaluator。
5. 增加 runtime package schema 和 loader compatibility test。
6. 将作者诊断和 source 信息留在 reports，不进入 runtime。
7. 新增中文编辑器、身份知识矩阵和 NPC 模拟器，但 UI 不改变 Contract 语义。

## 9. AWAKE Runtime 需要新增的能力

建立独立 Runtime loader：

- 启动时读取 manifest。
- 校验 package version、文件 hash 和完成标记。
- 构建或读取 keywords、aliases、entities、profiles、domains 索引。
- 加载 runtime documents 和 expressions。
- 使用共享 permission evaluator 结算 NPC 可见内容。
- 对不存在、损坏、版本不兼容或权限不明内容 fail closed。
- 不在 dialogue tick 中执行网络、阻塞文件读取或完整世界书扫描。
- 对同一查询使用缓存和稳定失效策略。

运行时读取失败时：

```text
不崩溃
不把隐藏知识当公开知识
不自动 fallback 到未审查原始 YAML
返回 unknown 或禁用该知识节点
```

## 10. 作者并行生产流程

Contract v1 冻结后，作者可以并行生产：

1. 按四领域填写知识。
2. 选择或登记来源。
3. 写 assertion，不先复制四种文风正文。
4. 为不同身份编写 expression。
5. 配置 rumor/summary/detail/secret。
6. 配置普通平民、士兵、头人、酒馆老板、贵族等 profile 权限。
7. 为 unknown 配置 referral。
8. 使用 Studio preview 检查 NPC 视角。
9. 使用本地 Worker 做重复、冲突、现代措辞和知识越权候选筛查。
10. 经人工审查、approval 和 compile 后进入 runtime candidate。

Worker 只能生成 suggestions 和 diagnostics，不得写入正典、来源登记、审批事件或运行时包。

## 11. 实施批次

### Batch 1：Contract v1 冻结

交付：

- `AWAKE-WORLDBOOK-CONTRACT-V1.md`。
- authoring Schema v1 修订版。
- runtime package schema。
- manifest/index/loader compatibility contract。
- 新世界书 retrieval/gate 字段定义。
- 新世界观的领域、身份、文风和内容层编写规范。

验收：

- 字段、版本、权限、unknown、失败行为无歧义。
- Studio、Runtime、作者三方可根据文档独立实现同一条知识。

### Batch 2：共享 Contract 和 Runtime loader

交付：

- 轻量 Contract 程序集或稳定契约目录。
- AWAKE runtime loader。
- runtime package 校验。
- 索引加载和权限读取。
- 无包、旧包、损坏包、未知 profile 和权限失败测试。

验收：

- 运行时只读取编译包。
- 不扫描原始 YAML。
- 不读取作者诊断和来源全文。

### Batch 3：Studio 正式编译目标对齐

交付：

- Studio 生成 runtime package。
- keywords/aliases/entities/profile/domain 索引。
- Studio preview 与 Runtime evaluator 一致性测试。
- 保留独立 export 和原子发布安全边界。

验收：

- 同一输入在 Studio preview 与 Runtime test harness 的结果完全一致。
- CLI、Web、Runtime 的 manifest/content/permission hash 可追溯。

### Batch 4：新世界观基础和作者并行生产

交付：

- 新世界观的政治、经济、文化、战争基础框架。
- 新世界书的身份、阶层、年龄、职业和知识范围规范。
- 新世界书的文风、表达层和 NPC 口径规范。
- 新来源登记和正典审批记录。
- 第一批政治、经济、文化、战争正典。

验收：

- 新内容不依赖旧四版文件才能成立。
- 未裁定冲突不进入 canon。
- 每条正式知识都能通过来源、权限、时代和 runtime compile 检查。

### Batch 5：Studio 作者体验和本地 Worker 测试

交付：

- 中文文档编辑器。
- 身份知识矩阵。
- NPC 模拟器。
- 字段级诊断和版本差异。
- Worker-low 固定 fixture 对话内测试。

验收：

- 非技术作者可不直接编辑复杂内部字段完成一条合法知识。
- Worker 不具备任何写入和发布权限。
- Worker、Studio、Runtime 的失败分类独立记录。

### Batch 6：运行时增量知识层

单独立项：

- 玩家传授知识。
- 事件生成周报。
- NPC 知识缓存。
- 个人经历和关系记忆。
- 运行时增量覆盖基础世界书的规则。

这些内容不进入 Batch 1–5 的基础世界书 Contract，避免再次混淆静态知识和运行时状态。

## 12. 完成定义

AWAKE 世界书 Contract v1 完成必须同时满足：

- Contract、Authoring、Runtime 三层版本明确。
- Studio 和 Runtime 使用同一套权限语义。
- Runtime 只消费编译包，不读原始世界书。
- Studio 生成 Runtime 正式包，不再依赖独立不可消费的最终格式。
- 新世界观拥有独立的领域、身份、文风、内容层和冲突处理规则。
- 作者可以按契约持续生产内容。
- 本地 Worker 只能做只读诊断。
- 运行时缺包、损坏包、未知权限时 fail closed。
- 至少有一套 Studio preview 与 Runtime evaluator 的一致性测试。
- 游戏内 E4/E5 验证绑定具体 BuildId、package hash 和日志会话。

## 13. 非目标

- 不把所有旧运行时文件直接塞进 Studio。
- 不把旧四版作为新世界书的自动迁移目标或兼容规范。
- 不让 Runtime 依赖 YAML、来源全文或 AI Provider。
- 不让 AI/Worker 自动决定正典。
- 不在本方案内实现完整 NPC 学习和周报系统。
- 不在未完成 Contract v1 前直接批量改写冻结 AWAKE 候选。

