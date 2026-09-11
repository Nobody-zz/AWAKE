# Persona Workbench 清理预演清单 — 2026-09-06

## 预演状态

- 状态：`read_only_preview`
- 删除执行：`false`
- 预演根目录：`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\persona-workbench\artifacts`
- 当前未发现 `PersonaWorkbench` 或 `PersonaWorkbench.Web` 运行进程。
- 精确名称引用扫描：`0` 个候选命中；前缀匹配结果不计入引用证据。

## 数量与空间

- 全部项目：`108`（`101` 个目录、`7` 个 ZIP）
- 清理候选：`97` 个，约 `8.607 GiB`
- 暂时保留：`11` 个，约 `0.975 GiB`

| 分类 | 数量 | 大小 | 当前处理 |
|---|---:|---:|---|
| `DELETE_OLD_PREVIEW` | 96 | 约 `8.507 GiB` | 用户确认后删除 |
| `DELETE_OLD_ROLLBACK` | 1 | 约 `0.100 GiB` | 用户确认后删除 |
| `KEEP_CURRENT_DELIVERY` | 2 | 约 `0.137 GiB` | 保留目录与对应 ZIP |
| `KEEP_LATEST_SMOKE` | 1 | 约 `0.097 GiB` | 短期保留 |
| `KEEP_AUDIT_REFERENCE` | 1 | 约 `0.097 GiB` | 暂保留，不是回退版本 |
| `KEEP_METADATA_ONLY` | 6 | 约 `0.604 GiB` | 提取报告/哈希后再删完整目录 |

注：ZIP/目录大小按当前文件系统盘点，四舍五入后可能存在小数误差。

## 明确清理候选

### 目录

- `desktop-old-versions-20260820`
- 全部 `PersonaWorkbench-FreePreview-r*` 历史 revision 目录
- 全部 `PersonaWorkbench-FreePreview-final-*` 历史目录
- `PersonaWorkbench-FreePreview-rollback-20260830`
- 其他已被预演 JSON 标记为 `DELETE_OLD_PREVIEW` 的目录

### ZIP

- `PersonaWorkbench-FreePreview-r28-20260820.zip`
- `PersonaWorkbench-FreePreview-r30-20260820.zip`
- `PersonaWorkbench-FreePreview-r33-20260820.zip`
- `PersonaWorkbench-FreePreview-r44-20260821.zip`
- `PersonaWorkbench-FreePreview-r45-20260821.zip`

完整的逐路径、字节数、文件数和分类记录见：

`C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\artifact-retention\PWB-CLEANUP-PREFLIGHT-20260906.json`

## 暂时保留

- `customer-pwb-delivery-20260904-0002`
- `customer-pwb-delivery-20260904-0002.zip`
- `customer-pwb-smoke-20260904-0001`
- `customer-pwb-smoke-20260904-0001.zip`
- `PersonaWorkbench-FreePreview-k1a-20260830`
- `code-debt`、`evidence`、`quality` 相关小型证据目录，待报告/哈希提取后再决定是否删除完整目录。

`PersonaWorkbench-FreePreview-k1a-20260830` 仅是历史审计参考，不是长期回退版本；待当前交付证据完成归档后可再清理。

## 删除前门禁

1. 用户确认本清单中的 `DELETE_*` 集合。
2. 再次检查 Persona Workbench 进程、文件占用和 OneDrive 同步状态。
3. 核验保留项目的 ZIP、`.sha256`、Manifest 和最新 smoke/交付证据。
4. 对每个绝对路径执行目录边界校验，禁止使用模糊通配符跨出 `artifacts` 根目录。
5. 先使用 `WhatIf` 预演，再执行精确路径删除。
6. 删除后重新运行审计，记录释放空间、保留项和 current 交付验证。

## 当前禁止

- 不把候选包上传到 Git。
- 不执行 `git commit` 或 `git push`。
- 不修改 Persona Workbench 工具、源码、契约或生成流程。
- 不删除 `src`、`contracts`、`tests`、`tools` 或文档。
- 不启动 Bannerlord、不同步游戏目录、不访问真实 Provider。
