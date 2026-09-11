# AWAKE Git 归档分类与历史产物清理计划 — 2026-09-06

## 当前结论

- 目标 Git 仓库：`C:\Users\26811\OneDrive\文档\New project\AWAKE-Repo`
- Remote：`https://github.com/Nobody-zz/AWAKE.git`
- 当前远端分支：`main`
- 本地仓库有未提交修改；本计划不覆盖、重置或提交这些修改。
- 本次只完成分类和规划；没有删除、移动、提交或推送。

## Git 分类

### A 类：允许进入 Git

- AWAKE 当前源码、`AWAKE.csproj`、`SubModule.xml`、运行时所需 `ModuleData` 和 `GUI`。
- 可复现的构建、审计、验证脚本。
- 小型 Schema、契约、README、计划、审计报告和必要的构建验证记录。
- 小型 JSON/YAML/文本证据，前提是没有密钥、个人路径、真实 Provider 响应或大体积生成内容。
- `docs\artifact-retention\` 下的留存策略、审计摘要和分类清单。

### B 类：只保留哈希/Manifest，不能进入普通 Git

- customer build 的完整 ZIP、解压目录及大型测试包。
- Worldbook Studio、Persona Workbench 的完整发布包、解压包和批量输出。
- 大型日志、数据库、缓存、录屏、临时导出和重复验证目录。
- 对 B 类产物只在 Git 保存 `build_id`、来源 Manifest、SHA-256、大小、生成时间、保留理由和外部归档位置。

### C 类：外部 Release/归档

- 明确需要用户下载的最新 customer ZIP。
- 明确需要长期保留且已验证的单个回退包。
- 外部归档必须有对应 `.sha256`、Manifest 和 Git 中的索引记录；不把解压目录长期放进 Git。

### D 类：清理候选

- 无 current/previous 指针保护、无调查引用、无唯一证据价值的历史构建。
- 重复的解压目录、同一 ZIP 的多份展开副本、失败后残留的完整工作目录。
- 已被新版本替代且没有独立回归价值的 Persona Workbench preview。

## Persona Workbench 当前分类

`tools\persona-workbench\artifacts` 当前发现 `101` 个目录，合计约 `9.408 GiB`：

| 分类 | 数量 | 大小 | 处理建议 |
|---|---:|---:|---|
| `KEEP_CURRENT_DELIVERY` | 1 | 0.10 GiB | 保留本地；ZIP 作为外部交付候选 |
| `KEEP_LATEST_SMOKE` | 1 | 0.10 GiB | 短期保留，直到交付证据归档 |
| `KEEP_AUDIT_REFERENCE` | 1 | 0.10 GiB | 暂保留，供 K1A 审计引用；不得当作当前回退 |
| `KEEP_METADATA_ONLY` | 6 | 0.60 GiB | 保留小型报告/哈希，完整目录进入删除复核 |
| `DELETE_OLD_PREVIEW` | 91 | 8.42 GiB | 建议删除；不作为回退版本 |
| `DELETE_OLD_ROLLBACK` | 1 | 0.10 GiB | 建议删除；名称带 rollback 不能替代当前有效证据 |

### Persona Workbench 暂定保护集合

- `customer-pwb-delivery-20260904-0002`
- `customer-pwb-smoke-20260904-0001`
- `PersonaWorkbench-FreePreview-k1a-20260830`：仅作为短期审计参考，不是长期回退。
- `PersonaWorkbench-FreePreview-rollback-20260830` 不列入保护集合。

### Persona Workbench 清理原则

- `r1–r75`、`final-r*`、`desktop-old-versions-*` 等历史试作包不上传 Git，也不保留为回退。
- 旧 preview 若只对应已有报告，先确保报告/哈希已进入 Git 或清单，再删除完整目录。
- `code-debt`、`evidence`、`quality` 目录优先提取小型报告和哈希；不把完整发布目录当作永久证据。
- 删除前必须确认没有当前运行进程、脚本路径、审计报告或外部交付引用。

## AWAKE customer-delivery 分类

- `current` 与 `previous` 解压目录、ZIP、`.sha256` 和 `rollback-pointer.json`：保护。
- 另选长期回退版本之前，不自动猜测或删除“长期回退”。
- 其他历史构建只保留索引、Manifest、SHA-256 和明确调查证据。
- 完整 customer ZIP 不进普通 Git；最新交付物进入 C 类，待确认后上传 Release 或其他外部归档。
- 解压目录不是长期归档源；对应 ZIP、Manifest、sidecar 和回滚验证完成后，解压目录进入 D 类。

## 不上传清单

- `artifacts\`、Worldbook Studio/Persona Workbench 完整生成目录。
- `bin\`、`obj\`、`dist\`、`_build_out\`、缓存和临时目录。
- 真实 API Key、Token、Provider 响应、用户目录绝对路径和运行日志。
- Downloads 原始世界书和任何未授权的源内容副本。
- 游戏目录、当前四档权威运行时目录的外部副本。

## 执行顺序

1. 冻结当前 `AWAKE-Repo` 未提交状态，先导出 `git status` 和 diff 清单；不重置、不覆盖。
2. 生成 Git 归档 Manifest，列出 A/B/C/D 类、来源、大小、SHA-256 和保护理由。
3. 对 PWB D 类执行引用和进程检查，提取需要保留的小型证据。
4. 用户确认 D 类清理集合后，再做一次精确路径校验和 `WhatIf` 预演。
5. 先清理确认过的旧 PWB preview/rollback，再重新统计磁盘空间。
6. 仅同步 A 类到 `AWAKE-Repo`；同步脚本不得把 B/C/D 类产物带入仓库。
7. 在用户明确授权后，才执行 Git commit/push；提交前运行 secret scan、文件大小检查和构建/静态验证。
8. 上传 C 类大型包时，单独记录 Release/外部归档 URL、文件哈希和可恢复性证据。

## 当前禁止动作

- 不执行 `git reset`、`git clean`、`git commit`、`git push`。
- 不运行现有 `sync_awake_repo.ps1` 覆盖 dirty 的 `AWAKE-Repo`。
- 不删除 Persona Workbench 或其他历史产物。
- 不修改 Worldbook Studio、Persona Workbench、UI Workstation 或 AWAKE 功能逻辑。
- 不启动 Bannerlord、不同步游戏目录、不访问真实 Provider。

## 依据

- `docs\artifact-retention\ARTIFACT-AUDIT-20260906.json`
- `docs\artifact-retention\ARTIFACT-AUDIT-20260906.md`
- `tools\persona-workbench\package-free-preview.ps1`
- `tools\persona-workbench\README-FreePreview.md`
- `C:\Users\26811\OneDrive\文档\New project\sync_awake_repo.ps1`
- `C:\Users\26811\OneDrive\文档\New project\AWAKE-Repo\.gitignore`
