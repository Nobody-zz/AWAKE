# AWAKE · AF Batch 2-5 落地方案

> 日期：2026-08-16
> 范围：命令台/收件箱/周报、记忆日结、UI 整理、守卫与性能探针

## Batch 2：命令台 + 事件收件箱 + 周报

- 命令台根菜单增加“事件收件箱”和“世界周报”。
- 收件箱展示本周 `WorldEventLedger` 记录。
- 周报复用 `NarrativeReportBuilder`。
- 命令台拦截时记录原因。

## Batch 3：记忆日结与重试

- 新增 `NpcMemoryOverviewBuilder`：按日压缩记忆。
- `NpcMemoryService.ConsolidateDailyAsync`：生成日结、幂等写入。
- 失败任务进入内存重试队列，最多 3 次。

## Batch 4：UI 整理

- 新增 AF UI 审计清单文档。
- 命令台、收件箱、周报统一文本弹窗，不引入新 prefab。

## Batch 5：守卫与性能探针

- `NpcDialogueReplyNormalizer`：清洗回复文本。
- `AwakePerfProbe`：记录对话生成与世界书查询耗时。

## 验收

- 构建 0 警告 / 0 错误。
- SdkSmoke PASS。
- 本地化 OK。
- dist/游戏哈希一致。
