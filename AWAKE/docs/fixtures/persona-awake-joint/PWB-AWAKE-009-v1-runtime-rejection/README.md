# PWB-AWAKE-009-v1-runtime-rejection

输入现在使用 runner 实际检查的 `runtimeEntry.schemaVersion=awake.worldbook.v1`，只验证 v1 runtime entry 的稳定拒绝；不自动做 v1→v2 runtime migration，也不把 `runtimeManifest`/package-v1 的别名误当作同一条门。
