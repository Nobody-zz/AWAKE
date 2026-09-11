# Worldbook Studio MVP ID Ledger 契约

`authoring/identity/id-ledger.jsonl` 是只追加的稳定 ID 登记和 tombstone 清单。每行一个已发布 ID，包含 `id`、`id_kind`、`first_revision`、`last_state`、`event_id` 和 `record_hash`。

- ID 一经登记不得重用；删除只写 `tombstoned`，不得物理移除历史记录。
- `redirect`、`supersedes`、`split_from`、`merged_into` 必须绑定 `event_type=migration` 的有效事件。
- 所有迁移目标必须与源 ID 类型一致；合并/拆分必须列出全部旧引用并能在 ledger 中追溯。
- ledger 使用 UTF-8、LF、无 BOM 的 JSON Lines；`record_hash` 为规范化记录的 SHA-256；文件按行追加，编译时验证重复、断链、重用和 hash 失配。
机器结构由 `id-ledger.v1.schema.json` 固定。新 ID 使用 `event_type=registration`；迁移使用 `event_type=migration`；tombstoned ID 只能转为历史状态，不得重新变为 active。状态迁移和 record_hash 校验必须在编译报告中逐条列出。