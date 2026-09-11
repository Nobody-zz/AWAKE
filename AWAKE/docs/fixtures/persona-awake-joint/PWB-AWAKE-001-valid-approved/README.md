# PWB-AWAKE-001-valid-approved

这是 `source.document` 结构下的 approved Workbench 正例输入。当前离线 runner 已实际读取 `source.document`、验证 source digest、selection sidecar、双审批和 observation evidence；它是 E2 隔离迁移通过证据，不是 AWAKE runtime 或游戏内接线证明。

`registry.json` 是 isolated fixture registry：它以当前目标 registry 的 tag/bundle 快照为基础，并额外保留本夹具旧 Workbench tag 的显式 alias，便于当前离线 identity 检查；它不是生产 `ModuleData` 接入证明。`input.json.metadata.targetRegistrySha256` 固定记录目标 registry 摘要，`registrySha256` 记录本夹具 registry.json 的实际字节摘要，`metadata.crosswalkSha256` 记录 crosswalk 文件摘要，三者不得混用。

selection 必须由 `awake.persona.selection.v1` sidecar 明确提供；不得从显示名、文件名或文档 ID 推断。`source.json` 与 `registry.json` 是固定 UTF-8 bytes 参考，001 的 source/registry digest 必须与它们实际字节一致。
