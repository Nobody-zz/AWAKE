# AWAKE 产物留存策略

## 范围

本策略只管理构建、交付、验证和临时归档产物，不改变 AWAKE 运行时逻辑、世界书正文、事件规则、Persona Workbench、Worldbook Studio、UI Workstation 或游戏目录。

审计入口：

```powershell
pwsh -File .\tools\audit-artifacts.ps1
pwsh -File .\tools\audit-artifacts.ps1 -IncludeHashes -OutputPath .\docs\artifact-retention\ARTIFACT-AUDIT-YYYYMMDD.json
```

默认行为是只读预览。当前没有自动删除、移动或压缩行为。

## 保护边界

- `artifacts\customer-delivery\rollback-pointer.json` 始终保留。
- `rollback-pointer.json` 的 `current_build_id` 和 `previous_build_id` 对应的解压目录、ZIP、`.sha256` 和验证记录始终保留。
- 未被指针保护的 customer build 只能标记为 `cleanup_candidate_review`，不能仅凭日期删除。
- 当前四档运行时世界书、源码、唯一原始素材、Manifest、ContentHash 和必要验证证据不属于自动清理范围。
- 任何清理前必须确认没有被回滚脚本、验证脚本、发布流程或当前任务引用。

## 默认留存窗口

### customer-delivery

1. 永久保护 `current`、`previous`。
2. 另选一个经过验证的长期回退版本；在该版本被明确登记前，不自动猜测或删除。
3. 其余成功历史构建默认进入审阅候选。
4. 交付 ZIP 是长期交付源；解压目录属于可审阅的展开副本。只有在对应 ZIP、Manifest、`.sha256` 和回滚验证均确认后，才可单独清理解压目录。
5. 失败构建只保留仍在调查中的批次及最小复现证据。

### Worldbook Studio archive

1. 默认保留最近三个日期目录作为短期回溯窗口。
2. 超过窗口的目录先审计：识别唯一最终结果、失败/争议证据、Manifest、哈希和脚本引用。
3. 完全重复且无引用的中间导出可在确认后压缩或清理。
4. 不删除权威运行时世界书，也不删除未经确认的唯一原始素材。
5. 本策略不修改 Worldbook Studio 现有归档实现；后续若要改变其写入流程，必须另立独立审查批次。

## 操作顺序

1. 运行只读审计并保存报告。
2. 对每个 cleanup candidate 记录保留/清理理由和引用检查结果。
3. 先对 ZIP、Manifest、ContentHash、`.sha256` 做验证。
4. 优先清理已确认的临时解压目录和重复中间导出，不碰源文件与权威运行时目录。
5. 清理后再次运行审计，记录释放空间和 current/previous 回滚验证。

## 当前审计限制

- 审计脚本不会自动选择长期回退版本。
- `-IncludeHashes` 只计算文件 SHA-256，不代表语义或运行时验证。
- 当前报告不代表已经执行清理，也不代表任何候选可删除。
