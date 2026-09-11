# AWAKE 产物留存审计 — 2026-09-06

## 结论

- 本次只读审计完成；没有删除、移动、压缩、同步或发布。
- 当前 C 盘可用空间：约 `55.263 GiB`。
- AWAKE 目录当前主要占用：`artifacts` 约 `14.44 GiB`，`tools` 约 `31.03 GiB`。
- `tools\worldbook-studio\artifacts` 约 `20.53 GiB`，其中日期归档约 `15.92 GiB`。
- `tools\persona-workbench\artifacts` 约 `9.58 GiB`。
- 本报告不改变 Worldbook Studio、Persona Workbench、UI Workstation 或 AWAKE 功能逻辑。

## Customer Delivery

- 共发现 `16` 个解压构建目录和 `16` 个 ZIP。
- `rollback-pointer.json` 当前指向：
  - `current`: `awake-customer-20260906-130127495-d1efabf7a27c-25c04fad`
  - `previous`: `awake-customer-20260906-091802884-181cac391605-3cfe6696`
- current/previous 的解压目录、ZIP 和 `.sha256` 均标记为保护项。
- 共发现 `1` 个回滚验证记录；在确认它已被汇总且不再是调查证据前保留。
- 其余 `14` 个构建未被当前指针保护，均只标记为 `cleanup_candidate_review`。
- 旧构建的解压目录约 `7.854 GiB`，旧 ZIP 约 `4.778 GiB`；合计约 `12.632 GiB` 为理论候选空间。
- 该数值不是可直接删除量；还需确认长期回退版本、验证引用、OneDrive 状态和是否有外部交付需求。

## Worldbook Studio Archive

- 共发现 `11` 个日期目录，总计约 `15.92 GiB`。
- 按当前默认策略，最近三个日期 `2026-09-04`、`2026-09-05`、`2026-09-06` 暂列 `policy_keep_recent`。
- 更早日期目录共 `8` 个，约 `9.868 GiB`，暂列 `cleanup_candidate_review`。
- 归档候选仍需逐目录区分最终结果、失败/争议证据、唯一原始素材、Manifest、哈希和脚本引用。
- 本次没有修改 Studio 归档实现，也没有删除任何日期目录。

## Other Areas

- `tools\worldbook-studio\artifacts\current-test*` 约 `4.61 GiB`，属于工具测试产物，当前仅作盘点。
- `tools\persona-workbench\artifacts` 约 `9.58 GiB`，包含多批旧 preview/release 产物，当前仅作盘点。
- `dist`、`ModuleData`、`src`、`obj` 和 `_build_out` 未发现与上述热点同量级的占用。
- 当前没有证据允许清理工具源码、权威运行时内容、唯一原始素材或游戏目录。

## Changed Files

- `tools\audit-artifacts.ps1`
- `docs\ARTIFACT-RETENTION-POLICY.md`
- `docs\artifact-retention\ARTIFACT-AUDIT-20260906.json`
- `docs\artifact-retention\ARTIFACT-AUDIT-20260906.md`

## Verification

```powershell
pwsh -NoProfile -File .\tools\audit-artifacts.ps1 `
  -IncludeHashes `
  -OutputPath .\docs\artifact-retention\ARTIFACT-AUDIT-20260906.json
```

验证结果：

- `schema_version=awake.artifact-retention-audit.v1`
- `mode=read_only_preview`
- `cleanup_performed=false`
- current/previous 均被识别并保护。
- `.zip`、`.zip.sha256`、回滚指针和回滚验证记录均已进入清单。

## 未完成与下一步

1. 现有 customer-delivery 契约明确要求旧构建保持不动；不能在没有独立审查的情况下把自动清理塞入现有打包器。
2. 需要用户确认一个长期回退版本后，才能制定明确的 customer 清理批次。
3. Worldbook Studio 和 Persona Workbench 的工具产物只能另立边界审计；本批次不修改工具。
4. 清理前必须进行逐项引用核验、哈希核验和 OneDrive 文件状态核验。
5. 真正清理后才可以记录“清理前后大小”和释放空间；当前没有清理后数据。
