# PWB-AWAKE-006-schema-skew-rejected

保留一个合法的 source.document，仅在 selection 放入 `unknownNestedField`，避免两个未知字段抢占首个错误并造成“同时报出”的虚假期望。schema validator 必须拒绝未知嵌套字段，而不是静默丢弃。该夹具不替代 009；`awake.worldbook.v1` runtime entry 的边界拒绝由 009 单独锁定。
