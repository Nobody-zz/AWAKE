# PWB-AWAKE-017-selection-schema-rejected

该夹具故意省略 selection sidecar 的 `schemaVersion`，其他字段保持可解析。适配器必须把它拒绝为缺少必需字段，不能把无版本的普通对象当成 `awake.persona.selection.v1`。
