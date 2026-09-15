# C 盘产物清理候选清单 — 2026-09-11

> 面向 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE`（实测 **48.62 GiB**）。
> 本清单为只读盘点结果，**尚未删除任何文件**。执行前需用户逐条确认。
> 测量方式：`robocopy <dir> NULL /L /S /BYTES`（逐文件 stat，不做 PowerShell 的对象展开）。
> 承接 `ARTIFACT-AUDIT-20260906.md` 的分类结论。

## 0. 为什么是这些，不是别的

- 新工作区 `D:\AWAKE-Dev\AWAKE` 已与源工作区逐文件比对：**源码 0 缺失**（1347 个代码文件全部到位），
  且 41 个 csproj 的文件路径型引用 **0 个悬空**。
  → 因此本目录下的**源码、文档、framework 不属于清理对象**。
- 下面所有条目都是「跑一次脚本就能重新生成」的产物：
  - `WorldbookStudio` 测试/打包快照（`scripts\package.ps1`、`scripts\test.ps1` 产出）
  - `customer-delivery` 交付包（`tools\customer-delivery\package-customer.ps1` 产出）
  - Persona Workbench 预览包（`package-free-preview.ps1` 产出）
  - production-smoke 的 evidence（`worldbook-runtime-production-smoke` 产出）

## 1. 必须保留（不在清理范围）

| 保留项 | 大小 | 依据 |
|---|---:|---|
| `customer-delivery\awake-customer-20260906-130127495-d1efabf7a27c-25c04fad`（目录 + .zip + .sha256） | 925 MB | `rollback-pointer.json` 的 `current` |
| `customer-delivery\awake-customer-20260906-091802884-181cac391605-3cfe6696`（目录 + .zip + .sha256） | 925 MB | `rollback-pointer.json` 的 `previous` |
| `customer-delivery\rollback-pointer.json` | 405 B | 回滚指针本体 |
| `customer-delivery\awake-customer-20260904-212030215-a03dd63d1ea0-7b480068`（目录 + .zip + .sha256） | 923 MB | 被 `rollback-verification-awake-customer-20260904-212030215-*.json` 引用，**待确认后再定** |
| `tools\worldbook-studio\artifacts\archive\2026-09-09` | 3.37 GiB | 最近 3 天归档（沿用 9-06 政策的 `policy_keep_recent`） |
| `tools\worldbook-studio\artifacts\archive\2026-09-10` | 6.91 GiB | 同上 |
| `tools\worldbook-studio\artifacts\archive\2026-09-11` | 0.84 GiB | 同上 |
| 其余全部（`src\`、`framework\`、`docs\`、`ModuleData\`、`dist\`、`_build_out\` 等） | 约 1.00 GiB | 源码与权威内容 |

## 2. 可删候选

### 2.1 Worldbook Studio 历史归档（`tools\worldbook-studio\artifacts\archive\`，早期 11 个日期）

| 绝对路径 | 大小 |
|---|---:|
| `...\AWAKE\tools\worldbook-studio\artifacts\archive\2026-08-26` | 0.13 GiB |
| `...\AWAKE\tools\worldbook-studio\artifacts\archive\2026-08-27` | 1.68 GiB |
| `...\AWAKE\tools\worldbook-studio\artifacts\archive\2026-08-28` | 0.84 GiB |
| `...\AWAKE\tools\worldbook-studio\artifacts\archive\2026-08-29` | 0.00 GiB |
| `...\AWAKE\tools\worldbook-studio\artifacts\archive\2026-08-31` | 0.42 GiB |
| `...\AWAKE\tools\worldbook-studio\artifacts\archive\2026-09-01` | 0.71 GiB |
| `...\AWAKE\tools\worldbook-studio\artifacts\archive\2026-09-02` | 0.42 GiB |
| `...\AWAKE\tools\worldbook-studio\artifacts\archive\2026-09-03` | 5.67 GiB |
| `...\AWAKE\tools\worldbook-studio\artifacts\archive\2026-09-04` | 1.55 GiB |
| `...\AWAKE\tools\worldbook-studio\artifacts\archive\2026-09-05` | 2.40 GiB |
| `...\AWAKE\tools\worldbook-studio\artifacts\archive\2026-09-06` | 2.10 GiB |
| **小计** | **15.92 GiB** |

### 2.2 Worldbook Studio 测试快照（`artifacts\current-test*`，11 个）

| 绝对路径 | 大小 |
|---|---:|
| `...\worldbook-studio\artifacts\current-test` | 432.3 MB |
| `...\worldbook-studio\artifacts\current-test-20260906` | 430.8 MB |
| `...\worldbook-studio\artifacts\current-test-20260906-r2` | 430.8 MB |
| `...\worldbook-studio\artifacts\current-test-pending-20260829` | 429.6 MB |
| `...\worldbook-studio\artifacts\current-test-pending-20260829-r2` | 429.6 MB |
| `...\worldbook-studio\artifacts\current-test-pending-20260829-r3` | 429.3 MB |
| `...\worldbook-studio\artifacts\current-test-pending-20260829-r4` | 298.7 MB |
| `...\worldbook-studio\artifacts\current-test-pending-20260829-r5` | 429.3 MB |
| `...\worldbook-studio\artifacts\current-test-pending-20260829-r6` | 429.3 MB |
| `...\worldbook-studio\artifacts\current-test-pending-20260829-r7` | 429.3 MB |
| `...\worldbook-studio\artifacts\current-test-pending-20260829-r8` | 429.4 MB |
| **小计** | **4.49 GiB** |

> 注：`current-test` 是 `scripts\test.ps1` 的默认输出位置，删掉后下次跑测试会重建。

### 2.3 Worldbook Studio 发布 zip

| 绝对路径 | 大小 |
|---|---:|
| `...\worldbook-studio\artifacts\WorldbookStudio-win-x64.zip` | 130.6 MB |
| `...\worldbook-studio\artifacts\WorldbookStudio-win-x64.zip.sha256` | 66 B |

> 这是**文件**，不是 7.87 GB。早期用 `Get-ChildItem -Recurse` 测时会被 PowerShell 当成通配符匹配所有同名 zip，得出错误值。
> 可用 `scripts\package.ps1` 重建。

### 2.4 customer-delivery 非保护构建（13 个）

每个构建 = 解压目录（约 574 MB）+ `.zip`（约 349 MB）+ `.zip.sha256`。

| 构建 ID 后缀（前缀均为 `awake-customer-`） | 大小 |
|---|---:|
| `20260904-211717776-a03dd63d1ea0-1db6baa2` | 923 MB |
| `20260904-212420010-a03dd63d1ea0-0a0a75b9` | 923 MB |
| `20260904-215301323-a03dd63d1ea0-1964d6b9` | 923 MB |
| `20260904-220838153-a03dd63d1ea0-3096a175` | 923 MB |
| `20260904-221030410-a03dd63d1ea0-63dffee8` | 923 MB |
| `20260904-221053300-a03dd63d1ea0-767601bc` | 923 MB |
| `20260904-221533869-a03dd63d1ea0-625292cf` | 925 MB |
| `20260906-073550143-a03dd63d1ea0-3a6795f2` | 925 MB |
| `20260906-075041335-a03dd63d1ea0-18f2abd0` | 925 MB |
| `20260906-075552305-df4f37abfda1-58d5bb0f` | 925 MB |
| `20260906-080012697-285bcbb82ee1-2f1783d6` | 925 MB |
| `20260906-080847460-a03dd63d1ea0-7a0a0490` | 925 MB |
| `20260906-090525786-181cac391605-13b7a7f4` | 925 MB |
| **小计** | **11.73 GiB** |

### 2.5 Persona Workbench 残留

| 绝对路径 | 大小 |
|---|---:|
| `...\AWAKE\tools\persona-workbench\artifacts` | 1.11 GiB |
| `...\AWAKE\tools\persona-workbench\.publish-free-preview-6ec60e60d158490da63de5d7d91bfdca` | 99.1 MB |
| `...\AWAKE\tools\persona-workbench\.publish-free-preview-776e91af4eaa4438ab9503c7f7644112` | 99.1 MB |
| **小计** | **1.30 GiB** |

### 2.6 production-smoke evidence

| 绝对路径 | 大小 |
|---|---:|
| `...\AWAKE\tools\worldbook-runtime-production-smoke\artifacts` | 216.5 MB |

## 3. 合计

| 方案 | 回收 | C 盘可用（当前 35.10 GiB） |
|---|---:|---:|
| **保守档**：只删 2.1–2.6 | 约 **33.78 GiB** | 约 **68.9 GiB** |
| **激进档**：再删 `archive\2026-09-09`、`09-10`、`09-11` | 约 **44.90 GiB** | 约 **80.0 GiB** |

## 4. 执行前置检查（每条都要过）

1. 确认路径解析后仍在 `...\_houkai_merge\AWAKE\` 之下（防手滑删到别处）。
2. 确认待删目录**不是** reparse point（本清单已核验：`artifacts` 下无任何 junction/symlink；但仍建议执行前再查一次）。
3. 确认 `rollback-pointer.json` 的 `current_build_id` / `previous_build_id` 指向的构建**不在**待删列表内。
4. OneDrive 处于同步完成状态（避免删除与同步竞争）。
5. 删除后复查 `Get-PSDrive C` 的可用空间变化，与预期量级吻合再继续下一批。

## 5. 未纳入本清单但值得知道

- `...\_houkai_merge\AWAKE\ModuleData\Worldbook\`（**2.9 MB**：415 个 personality_background + 335 个 rules + event_data 3 + persona_definitions 2 + …）
  在本工作区**刻意剥离**，`D:\AWAKE-Dev` 内不存在。
  **删整个 `_houkai_merge\AWAKE` 之前必须先决定这份内容的归宿。**
- `docs\archive`（206）、`docs\evidence`（1597）、`docs\sync-reports`（33）、`docs\worldbook-migration`（265）共约 47 MB，
  既不在 `D:\AWAKE-Dev` 也不在 git 中；体积小，本清单不处理。

### 5.1 后续处置（2026-09-11 当日更新，用户决策）

- **v1 世界书本体：归宿已决（归档后删）**。归档 `D:\AWAKE-Archive\worldbook-v1_20260911.zip`
  （SHA-256 `9298EE54D9D2A960932E57949D94B4F2216F827706BF2ECB4D4E036CB76E9BB9`，759 文件 / 2,993,320 字节，`unzip -t` 完整性 OK），
  随后旧工作区 `ModuleData\Worldbook\` 整目录删除（`ModuleData` 其余 Knowledge/Languages/Rules 未动）。
  执行记录见 `CLEANUP-EXECUTION-20260911.md` 追加节。删整个 `_houkai_merge\AWAKE` 的前置条件已解除。
- **`docs\worldbook-migration`（265 文件）：已迁回** `D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\`，
  并按工作区边界规则拆分——132 个含编年史正文的产物移至 `D:\AWAKE-Archive\worldbook-migration-content\`，
  仓库保留 133 个契约/报告/红测文档；清单与出仓文件哈希见该目录 `CONTENT-ARTIFACTS-POINTER.md`。
  `WORLDBOOKSTUDIO-AUTHOR-HANDBOOK-v3-20260910.md` 引用随之解除悬空。
- 实测备注：2.9 MB 数值经逐文件核验准确（robocopy /BYTES = 2,993,320）；`du -s` 的约 4.8 MB 为块大小开销，不作为口径。
