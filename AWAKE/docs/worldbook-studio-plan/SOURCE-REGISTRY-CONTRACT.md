# Worldbook Studio MVP 来源登记契约

## 来源登记是唯一来源元数据入口

`authoring/sources/*.yaml` 保存来源登记；正典 `source_ref` 必须绑定已登记的 `(source_id, source_version, source_content_hash)`。来源正文保持只读，登记不会自动正典化。机器结构由 `source.registry.v1.schema.json` 固定。

每条登记至少包含：`source_id`、`source_version`、`source_nature`、`universe`、`era`、`locator_root`、`source_content_hash`、`content_tier`、`license_status`、`use_status`、`valid_until`、`imported_at` 和规范化规则版本。

## 哈希与定位

- 文本统一按 UTF-8、LF 换行、去除 BOM 后计算 `source_content_hash`。
- `locator` 必须是相对于 `locator_root` 的稳定路径/段落定位；绝对路径、远程 URL 和越界定位拒绝。
- `quote` 使用同一规范化规则重算 `quote_hash`；引用文本变化、定位越界或 hash 失配阻止编译。
- `license_status` 必须为 `permitted`，`use_status` 必须为 `active`，且 `valid_until` 未过期，来源才可进入导出闭包；`restricted` 只允许报告，不允许纯净导出。

## 版本不可变

同一 `source_id` 的不同版本必须有不同 `source_version` 和内容 hash；正典显式绑定的旧版本不得静默解析到新版本。来源登记 Schema、登记文件 hash 与正典 source_ref 失配时阻止编译。