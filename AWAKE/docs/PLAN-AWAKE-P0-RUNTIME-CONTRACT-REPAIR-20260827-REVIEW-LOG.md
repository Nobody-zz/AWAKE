# AWAKE P0 运行时契约与候选身份修复计划审查日志

**计划：** `PLAN-AWAKE-P0-RUNTIME-CONTRACT-REPAIR-20260827.md`  
**审查性质：** 独立只读反向审查  
**审查日期：** 2026-08-27  
**代码写入：** 未授权、未执行

## Round 1

### 审查范围

- 是否越过 B3 `user_signoff=false` 或 Marcus/Worldbook Studio 并行批次边界；
- 是否把 v2 产物写成既存事实；
- 是否遗漏构建、同步、存档和 Bannerlord 运行时安全门；
- 是否存在不必要的代码承诺。

### 发现

计划总体边界正确，但阶段 C 仍允许调整 Worldbook Studio 编译端输出，且没有明确列出 AWAKE 当前构建质量门、Storage/存档 schema 禁止修改和 Bannerlord 生命周期安全门。

### 处理

- 将 Worldbook Studio、编译端、公共 schema 和输出格式改动移出本批；如确有必要，另立计划并重新审查。
- 增加 `tools\\build.ps1`、0 warnings/0 errors、SdkSmoke、maf-lint、`BUILD_VERIFICATION.txt`、BuildId 和哈希记录要求。
- 明确本批不改 Storage、Save key、存档 schema 或迁移。
- 增加 `OnSubModuleLoad`、UI、Campaign、Tick 的生命周期和非阻塞安全门。

### 结论

`VERDICT: REVISE`

## Round 2

### 复核范围

- 本批是否只处理 AWAKE 运行时侧的核验、诊断和已确认读取路径；
- 是否明确不改 Studio、公共 schema、Storage、Save key 和游戏目录；
- 是否补齐构建和运行时安全质量门；
- v1/v2 是否仍保持取证后再选择，没有提前决定。

### 结论

- 未发现阻断冲突；
- 首期只读边界、并行批次边界和候选冻结边界均已明确；
- v1/v2 仍为证据驱动的待决选项；
- 计划没有把 v2 产物或代码修复写成已完成事实。

`VERDICT: APPROVED`

## 当前授权状态

```text
plan_status = proposed_for_user_signoff
review_status = APPROVED
user_signoff_required = true
implementation_authorized = false
game_directory_sync = forbidden_in_this_batch
frozen_candidate = unchanged
```

最终批准只说明计划边界通过审查，不代表已批准写代码、构建新候选或同步游戏目录。
