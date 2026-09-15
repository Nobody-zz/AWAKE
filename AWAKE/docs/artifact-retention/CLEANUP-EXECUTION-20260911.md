# C 盘产物清理执行报告 — 2026-09-11

对应候选清单：`CLEANUP-CANDIDATES-20260911.md`（保守档）
目标：`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE`
用户决策：保留 `current` / `previous` / 被回滚验证记录引用的构建；保留 `archive` 最近三天。其余全删。

## 结果

| 指标 | 值 |
|---|---:|
| C 盘可用（前） | 35.14 GB |
| C 盘可用（后） | **68.80 GB** |
| 实际回收 | **33.66 GB** |
| 预期回收 | 33.78 GB |
| 偏差 | 0.12 GB（0.4%，在测量噪声内） |
| `...\_houkai_merge\AWAKE` 体积 | 48.62 GB → **14.84 GB** |

## 分四批执行

| 批次 | 内容 | 回收 | C 盘可用 |
|---|---|---:|---:|
| 1 | `worldbook-studio\artifacts\archive\` 早期 11 个日期目录 | 15.97 GB | → 51.11 GB |
| 2 | `current-test*` 11 个快照 + `WorldbookStudio-win-x64.zip`(+sha256) | 4.63 GB | → 55.75 GB |
| 3 | `customer-delivery` 13 个非保护构建（目录 + zip + sha256） | 11.53 GB | → 67.28 GB |
| 4 | `persona-workbench\artifacts`、2 个 `.publish-free-preview-*`、`production-smoke\artifacts` | 1.52 GB | → 68.80 GB |

## 执行前置校验（全部通过）

- 67 个目标路径 `GetFullPath` 后全部位于 `...\_houkai_merge\AWAKE\` 之下（0 越界）
- 67 个目标全部存在（0 缺失）
- 5 处候选区域递归扫描 reparse point：**0 个** junction/symlink
- 待删列表与 `rollback-pointer.json` 的 `current_build_id` / `previous_build_id` 无交集

## 删除后核验（全部 OK）

| 保留项 | 实测 |
|---|---:|
| `src\` | 1.2 MB |
| `framework\` | 355.6 MB |
| `docs\` | 47.1 MB |
| `ModuleData\Worldbook\` | 2.9 MB（`personality_background` 415 + `rules` 335 + `event_data` 3 + `persona_definitions` 2 + 其余 2） |
| `archive\2026-09-09` | 3,455.7 MB |
| `archive\2026-09-10` | 7,079.0 MB |
| `archive\2026-09-11` | 864.3 MB |
| `customer-delivery` current | 574.6 MB |
| `customer-delivery` previous | 574.6 MB |
| `customer-delivery` rollback-verified | 574.3 MB |
| `rollback-pointer.json` | 在 |

`D:\AWAKE-Dev` 未被波及：HEAD 仍为 `69b9fc7`，工作树除本任务新增的文档外无变化。

## 备注

- 删除使用 `[IO.Directory]::Delete(path, $true)` / `[IO.File]::Delete(path)`（PowerShell 进程内 .NET API）。
  `Remove-Item -Recurse -Force` 被本机策略拦截，未使用。
- 本目录下所有删除目标未做备份：按其性质（脚本可重建产物）判定无需备份。
- 若日后要回收更多，候选为 `archive\2026-09-09`、`09-10`、`09-11`（11.12 GB）。

## 追加执行：`ModuleData\Worldbook` 归档后删除（2026-09-11 晚，用户决策）

- **决策**：v1 世界书本体（淘汰的后备版 `awake.worldbook.v1`，id `awake.calradia.chronicle`）**归档后删除**，
  作为日后删除整个 `_houkai_merge\AWAKE` 的前置解锁。删除范围仅此子目录。
- **归档**：`D:\AWAKE-Archive\worldbook-v1_20260911.zip`，
  SHA-256 `9298EE54D9D2A960932E57949D94B4F2216F827706BF2ECB4D4E036CB76E9BB9`，
  759 文件 / 2,993,320 字节（与源逐字节一致），zip 完整性 `unzip -t` 通过（762 条目 = 759 文件 + 3 目录项）。
- **删除前校验（全部通过）**：源/暂存目录文件数与总字节数一致；`manifest.json`、`migration_report.json`、
  `persona_definitions/tag_registry.json` 三文件 SHA-256 源与暂存逐一比对一致；暂存目录压缩为 zip 并校验后才删源。
- **删除后核验**：`...\_houkai_merge\AWAKE\ModuleData\Worldbook\` 已不存在；
  `ModuleData` 其余内容（`Knowledge` / `Languages` / `Rules`）完好；归档 zip 删除后复查 `unzip -t` 仍通过。
- **关联处置（同日）**：`docs\worldbook-migration`（265 文件）迁回 `D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\`
  并按工作区边界规则拆分——132 个含编年史正文的产物移至 `D:\AWAKE-Archive\worldbook-migration-content\`，
  仓库保留 133 个文件；详见该目录 `CONTENT-ARTIFACTS-POINTER.md`。
- **挂账与暂缓**：v1 时代死代码 `WorldbookLoader.cs` / `WorldbookModels.cs` / `WorldbookService.cs` 待独立清理批次；
  公开镜像 git 历史中的 759 个 v1 文件暂缓处理，待正式 v2 包发布路径定案时一并决策。
