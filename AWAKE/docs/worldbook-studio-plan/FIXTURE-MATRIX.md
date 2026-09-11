# Worldbook Studio MVP Fixture 验收矩阵

| ID | 样本 | 预期 | 级别 |
|---|---|---|---|
| F01 | 最小正式正典 + 有效来源 | 编译成功、哈希稳定 | pass |
| F02 | 缺失 SourceContentHash/QuoteHash | 阻止编译，`WB-SOURCE-001` | error |
| F03 | `warband_future` 引用 `awake_current` 未声明迁移 | 阻止编译，`WB-TIME-001` | error |
| F04 | 同一表达层 grant 与 deny 冲突 | deny 胜出，预览为 unknown/降级 | pass |
| F05 | 未知 profile_id 或继承成环 | 阻止编译，`WB-PROFILE-001` | error |
| F06 | alias/redirect 单向迁移 | 成功；循环、类型错误或旧 ID 重用阻止编译 | error |
| F07 | 普通村民/贵族预览 | 输出不同，解释链完整 | pass |
| F08 | NPC 预览拒绝秘密 | NPC DTO 不含拒绝正文；作者诊断可见原因 | pass |
| F09 | adult_optional 通过来源/redirect/索引间接进入 clean 导出 | 阻止导出，`WB-TIER-001` | error |
| F10 | AI 未配置或 suggestions 被正典引用 | 无网络；建议不进入编译；间接引用阻止编译 | pass/error |
| F11 | warning 未确认后源文件变化 | 确认失效，阻止编译 | error |
| F12 | `fixture-F12-atomic-pointer.json`：版本目录写入中断/磁盘满/文件占用/锁竞争/指针切换失败 | `current.json` 不切换，旧候选仍可读，临时目录可清理；报告固定 fault_point/error_code/指针 hash 字段 | error |
| F13 | 重排数组/改文件名/改显示语言 | ID、关键词和编译哈希不变 | pass |
| F14 | 中文/英文界面切换 | 本地化字段按回退顺序显示，内部 ID 不变 | pass |
| F15 | `fixture-F15-v1-boundary.json`：Studio 输出到 v2 候选目录并检查 v1 探测树 | 正常自动探测不扫描 `export/WorldbookV2`；输出落入 v1 探测树前即拒绝；手工复制/显式注入属于 unsupported；报告固定四个边界字段 | pass/error |
| F16 | `author_created` 未批准或 sources/author_created 同时缺失 | Schema/编译阻止 | error |


| F18 | fixture-valid-minimal.yaml | Schema 通过；语义校验在登记来源/注册表 hash 正确时编译成功 | pass |
| F19 | fixture-valid-accepted-variant.yaml | accepted_variant 来源样本通过；不要求 canon approval | pass |
| F20 | fixture-invalid-source-version.yaml | source_version 缺失，Schema 阻止，WB-SOURCE-001 | error |
| F21 | fixture-invalid-canon-unapproved.yaml | canon assertion/expression 未批准，阻止，WB-CANON-001 | error |
| F22 | fixture-valid-authoring-minimal.yaml | 无来源、带 author_created 的作者草稿；可用于作者模式 AI 候选应用 Smoke | pass |
## 固定 CLI 契约

命令名：`worldbook-studio`。

```text
worldbook-studio validate --workspace <path> --offline --no-ai
worldbook-studio compile --workspace <path> --out <path> --offline --no-ai
worldbook-studio preview --workspace <path> --fixture F07 --profile profile.villager --offline --no-ai
worldbook-studio export --compiled <path> --out <path> --content-tier base --offline --no-ai
```

退出码：`0` 成功；`2` 校验错误；`3` 路径/配置错误；`4` 编译或导出失败。

固定输出：`reports/validation.json`、`reports/mapping.json`、`reports/preview-F07.json`、`compiled/manifest.json`、`compiled/SHA256SUMS.txt`。测试 harness 必须拦截网络并断言无 Provider 请求。
| F17-A | `status=canon` 且 assertion 为原创但未 `approved` | Schema/语义校验阻止，`WB-CANON-001` | error |
| F17-B | `status=canon` 且 expression 为原创但未 `approved` | Schema/语义校验阻止，`WB-CANON-001` | error |
| F17-C | `status=canon` 且原创项缺失 `review_event_id` | Schema/语义校验阻止，`WB-CANON-001` | error |
| F17-D | `review_event_id` 形状正确但审计事件不存在或对象/hash 不匹配 | 语义校验阻止，`WB-CANON-001` | error |
| F17-E | 原创项 `approved` 且匹配有效 approval 事件 | 编译成功，报告保留审计链 | pass |
