# 世界事实到正式周报：耦合收口契约

状态：`superseded_by_minimal_e2_slice`

> 封存提示：本文件不再是当前实现依据。当前唯一权威计划是 `PLAN-WORLD-FACT-REPORT-MINIMAL-E2-20260912.md`。

范围：只定义 AWAKE 模组本体的事实 Journal、查询层、正式周报和知识投影之间的耦合；不包含 Worldbook 正文、角色卡、Persona、对话或 Marcus 公共 API。

## 1. 数据所有权

| 层 | 拥有的数据 | 可以做什么 | 不可以做什么 |
| --- | --- | --- | --- |
| `WorldFactJournal` | 原始结构化事实 | 保存、校验、按事实 ID 读回 | 不生成叙事文本，不知道周报/角色 |
| `WorldFactQuery` | 查询结果，不持久化所有权 | 按策略筛选、排序、解释排除原因 | 不写 Journal，不修改事实 |
| 正式周报 | 某个已完成窗口的展示快照 | 保存窗口、来源 ID、策略版本、指纹 | 不反向修改事实 |
| 知识投影 | 可检索的动态知识条目 | 按 `WorldKnowledge` 结果建立投影 | 不从周报文本反推事实 |
| 人物记忆层 | 后续独立批次的候选/记忆 | 选择角色确实相关且可知的事实 | 不把候选自动写成角色记忆 |

唯一允许的数据方向：

```text
事实 Journal → WorldFactQuery → 周报 / 知识 / 人物候选 / 事件候选
```

## 2. 周报输入契约

正式周报只能接收由 `QueryWeeklyDynamicsAsync` 创建的 typed 输入。该输入必须固定携带：

- `Policy == WeeklyDynamics`；
- `Status == Success` 或 `Empty`；
- `UsedLegacyFallback == false`；
- `WindowStartDay`、`WindowEndDay`，且 `WindowEndDay - WindowStartDay == 6`；
- `JournalRevision` 与 `JournalDigest`，用于把报告绑定到本次完整读取结果；
- `Facts` 与 `SourceFactIds` 一一对应，`SourceFactIds` 去重且顺序稳定。

报告构建器不再接受独立的 `currentDay`、`WorldEventRecord` 列表或原始 Journal 读取结果。兼容 v1 的转换器可以读取旧记录，但不得作为正式 v2 生成器的输入。

正式周报的最低输入条件为：

```text
FormalWeeklyReportInput.Policy == WeeklyDynamics
FormalWeeklyReportInput.Status == Success 或 Empty
FormalWeeklyReportInput.UsedLegacyFallback == false
```

正式报告不得使用 legacy fallback。Journal 缺失时只能生成近期预览或继续展示既有 v1 报告，不能把旧 records 冒充为新的正式 v2 来源。

`WorldKnowledgeProjectionService` 同理只接受 `Policy == WorldKnowledge`；人物记忆和事件触发各自只能接受对应策略。

## 3. v2 报告来源字段规则

新 v2 报告的顶层字段固定为以下 11 个，不能增加或减少：

```text
schemaVersion, reportId, period, generatedBy, policyVersion,
contentFingerprint, sourceFactIds, sourceEventIds, sections,
visibility, extensions
```

其中：

```text
sourceFactIds: 参与生成报告的真实 WorldFact.factId，去重、按报告生成顺序排列
sourceEventIds: 可选的真实旧事件 ID；没有真实映射时必须是空数组
```

v2 每个非空 item 的字段固定为 `itemId`、`text`、`sourceFactIds`、`sourceEventIds`、`entryId`；其中 `sourceFactIds` 必须非空，且必须是顶层 `sourceFactIds` 的子集。`sourceEventIds` 可以为空，但其中的每个 ID 必须是顶层 `sourceEventIds` 的子集。

`sourceEventIds` 保留为兼容字段，语义收窄：

- 只有事实中存在真实、稳定的旧事件 ID 时才填写；
- 没有真实旧事件 ID 时可以为空；
- 不把 `factId` 截断、改名或伪造为事件 ID；
- v2 的内容指纹必须包含顶层和 item 级的 `sourceFactIds`、`sourceEventIds`。

这样旧系统仍可读取 `sourceEventIds`，新系统则通过 `sourceFactIds` 完成事实闭包。v1 报告保持原字段和原字节，不迁移、不重写；v1 validator 不被 v2 字段规则反向改变。

## 4. 窗口、ID 和指纹

- 完成窗口使用 `windowEndDay >= 7`、`windowStartDay = windowEndDay - 6`；报告 `reportId` 固定为 `awake:report:weekly-v2-{windowEndDay}`。
- v1 使用 `awake:report:weekly-{windowEndDay}`，与 v2 并存，互不覆盖；菜单优先显示同窗口的合法 v2，否则显示合法 v1。
- `period` 必须与上述闭区间对应，使用 UTC、Round-trip 格式；extensions 只允许 `awake:windowStartDay` 和 `awake:windowEndDay` 两个 JSON 整数。
- `policyVersion` 固定为 `awake.weekly-report.policy.v2`。
- `contentFingerprint` 是排除自身后的 canonical UTF-8 JSON 的大写 SHA-256；对象属性按 ordinal 排序，sections/items 保持生成顺序，所有来源和 identity 集合按 ordinal 排序。
- canonical 输入包含除 `contentFingerprint` 外的全部 10 个顶层字段，以及 item 级来源字段；schema、正例、反例和固定 hash fixture 必须使用同一 canonicalizer。
- 同一 `reportId` 且 fingerprint、窗口、来源闭包完全一致时幂等成功；任一不一致都返回稳定冲突并保留原快照。

## 5. 生成和持久化顺序

```text
QueryWeeklyDynamics
  ↓
创建并校验 FormalWeeklyReportInput
  ↓
按事实生成 v2 sections 和 item 级来源闭包
  ↓
写入 sourceFactIds、兼容 sourceEventIds、policyVersion、fingerprint
  ↓
v2 schema + canonical JSON + fingerprint 校验
  ↓
按 reportId 读取状态并幂等复用或 fail closed 冲突
```

报告是 materialized view，不是事实所有者。晚到事实不自动改写已经完成的报告；后续修订另行定义。

## 6. 兼容和失败语义

- v1：只读兼容，旧 `awake:report:weekly-{endDay}` 不变。
- v2：使用独立版本化 ID，不覆盖 v1。
- Journal `corrupt/unavailable`：正式报告返回不可用，不生成空报告。
- Journal `empty`：只有合法空窗口才允许生成合法空报告。
- Journal `missing`：正式报告不使用 legacy fallback；近期预览可明确标记兼容来源。
- Journal `empty`：仅在窗口合法且 `Facts`、`SourceFactIds` 均为空时生成合法空报告；空报告不包含 item。
- 同一 `reportId` 同一 canonical fingerprint：幂等成功。
- 同一 `reportId` 不同来源或 fingerprint：稳定冲突，保留旧快照。

## 7. 持久化和状态交接

本耦合切片使用现有 `WorldStateStore` 的报告状态 seam，不重新设计事实 Journal 存储。v2 状态必须至少保存：`reportId`、`schemaVersion`、窗口起止日、`status`、`contentFingerprint`、完整报告 payload、`sourceFactIds`、`sourceEventIds` 和最后一次错误码。

状态写入遵守以下规则：

1. 先读取同 `reportId` 的现有状态，再按 canonical fingerprint 比较；不能只比较 reportId。
2. 写入返回不确定时，重读同一 reportId；只有窗口、schema、来源闭包和 fingerprint 全部一致才确认已应用，否则返回 retryable/unavailable。
3. 已应用快照损坏时，只允许用同窗口、同版本且来源仍可验证的报告修复；原始事实不删除。
4. v1 状态只读兼容，不由 v2 写入覆盖或改名。

本切片的 handoff 状态固定为：

```text
WorldFactQuery E2 verified
→ coupling contract independently reviewed
→ user sign-off
→ v2 implementation allowed
```

事实查询层的 build/smoke 证据不能替代耦合契约审查；在本文件为 `revised_pending_independent_review` 或 `REVISE` 时，正式 v2 接线禁止实施。

## 8. 必须同步的现有契约

`PLAN-WEEKLY-DYNAMICS-20260912.md` 必须引用本文件作为唯一 v2 来源字段和指纹权威；其中不得再单独声明另一套 v2 顶层字段。`PLAN-WORLD-REPORT-FOUNDATION-20260912.md` 已标记为 superseded，仅保留追溯证据，不得作为实现依据；其旧的 v2 字段描述必须明确指向本文件，避免被机械化实施者误读为当前契约。

在该修订独立复审达到 `APPROVED` 且获得用户签收前，不实现正式 v2 生成器和持久化接线。

## 9. 当前实现对照

- 已符合：事实 Journal 独立保存；查询层只读；近期预览走查询；知识投影有查询接缝；人物候选拒绝无实体的 legacy 事实。
- 待实现：typed `FormalWeeklyReportInput`、正式周报消费 `QueryWeeklyDynamicsAsync`、v2 `sourceFactIds`/item 闭包、v2 schema/fixture、指纹和正式报告存档接线。
- 当前候选 BuildId：`awake-20260912-world-fact-query-004`；本契约尚未产生新的代码候选。
