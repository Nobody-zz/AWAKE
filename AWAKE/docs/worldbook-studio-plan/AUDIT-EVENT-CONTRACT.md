# Worldbook Studio MVP 审计事件契约

## 目的

`review_event_id` 是对不可变审计事件的引用，不是只验证字符串形状的标签。事件登记文件只读参与校验，不属于正典正文，也不能被 AI 建议写入。

## 事件文件与机器结构

事件位于 `authoring/audit/events/*.jsonl`，只允许追加，不允许就地修改或删除。机器结构由 `audit-event.v1.schema.json` 固定。每条记录包含 `event_id`、`stream_id`、`sequence`、`event_type`、`object_kind`、`object_id`、`object_revision`、`object_hash`、`author_id`、`created_at`、`decision`、`previous_event_hash` 和 `event_hash`。

对象规范化统一使用 UTF-8、LF、无 BOM、JSON 对象键按字典序、数组保持源语义顺序、Unicode 不做隐式翻译；规范化对象（去除派生 hash 字段）使用 SHA-256 计算 `object_hash`。YAML 先用安全 Core Schema 解析：禁止重复键、锚点、标签和对象构造；注释丢弃，缺省字段保持缺省，显式 null 保留为 null；引号标量保持字符串，未加引号的布尔/整数按固定 JSON 类型解析，不自动解析日期；数组保持顺序。对象的 `revision` 由源文件显式携带，必须为正整数。

## 事件类型与决策矩阵

| event_type | 允许 decision | 用途 |
|---|---|---|
| `registration` | `approved` | 新 ID 或首次登记 |
| `review` | `reviewed`、`rejected` | 普通审阅，不授予正典批准 |
| `approval` | `approved` | 支持 `review_status=approved` |
| `migration` | `approved` | redirect、lifecycle 和 ledger 状态迁移 |

`event_type=approval`、`decision=approved` 是原创正典 assertion/expression 的唯一有效批准事件。

## 对象映射与 revision

- document/assertion/expression：`object_id` 等于对象稳定 ID；revision 必须从 1 开始连续递增，每次修改恰为前一 revision + 1。
- redirect：`object_id` 等于 `redirect_id`，revision 使用 redirect 自身 revision。
- lifecycle：`object_id` 等于宿主 document ID，revision 使用 lifecycle 自身 revision。
- identity ledger：`object_id` 等于 ledger 记录 ID，revision 使用 ledger record revision。
- entity/profile/referral/source/event/author：registration 事件只允许在对象首次登记时使用，object_revision 为该对象登记 revision。任何已登记对象的普通 revision 变更至少绑定一个同 object_id/revision 的 `review` 事件；canon 原创 assertion/expression 还必须绑定 `approval`。

## 精确 hash 输入

- `object_hash`：对对象的 canonical JSON（去除对象内任何派生 hash 字段）计算 SHA-256。
- `event_hash`：对完整事件 canonical JSON（只去除 `event_hash` 本身，保留 `previous_event_hash`）计算 SHA-256。
- ledger `record_hash`：对完整 ledger record canonical JSON（只去除 `record_hash` 本身）计算 SHA-256。
- canonical JSON 使用 UTF-8、LF、无 BOM、对象键按 Unicode 字典序、数组保持语义顺序、无尾随空格和无末尾换行；数字使用 JSON 最短表示。

## 链与校验

每个 stream 使用严格递增 `sequence`；首条 `previous_event_hash` 为 null，后续必须指向同 stream 前一 sequence。编译器按文件名排序后验证全局 stream 唯一性、sequence 连续性、事件 hash 链和 append-only ledger。F17-D 报告必须包含 event_id、stream_id、sequence、object_kind/id、expected_hash、actual_hash。语义校验器还必须拒绝同一稳定 ID 的 revision 回退、跳号、重用或缺少对应 migration/approval 事件。