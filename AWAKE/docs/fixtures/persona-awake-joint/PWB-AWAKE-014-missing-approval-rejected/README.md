# PWB-AWAKE-014-missing-approval-rejected

该夹具使用 approved Workbench source、明确 selection 和已接受 observation evidence，但完全缺少双层 approval evidence。适配器必须在 promotion 阶段返回 `persona.approval_missing`，不得把 Workbench 的 `approved` 状态直接当成 AWAKE approved。
