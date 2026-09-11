# Worldbook Studio MVP 运行时映射契约

## 目标

MVP 只生成 `awake.worldbook.v2` 候选包和映射报告，不把候选包放入当前 AWAKE `ModuleData`。Studio 只保证输出路径硬阻断和正常自动探测不扫描 export；手工复制或显式传入 v2 manifest 属于 unsupported，不宣称旧 loader 会拒绝消费。marker 仅用于诊断。

## 字段映射

| Authoring JSON Pointer | 编译候选 v2 | 现有 v1 映射 | 状态 |
|---|---|---|---|
| `/revision` | `revision` | 无字段 | unsupported_for_v1 |
| `/id` | `id` | `WorldbookRule.Id` | mapped；缺失/重复硬失败 |
| `/title` | `display.title` | 无稳定字段 | report_only |
| `/summary` | `summary` | 无字段 | unsupported_for_v1 |
| `/domain` | `domain` | 无字段 | unsupported_for_v1 |
| `/universe` / `/era` | `timeline` | 无字段 | unsupported_for_v1 |
| `/status` | `canon_status` | 无字段 | unsupported_for_v1 |
| `/content_tier` | `content_tier` | 无字段 | unsupported_for_v1 |
| `/registry_bindings` | `registry_bindings` | 无字段 | unsupported_for_v1 |
| `/authority` | `authority` | 无字段 | unsupported_for_v1 |
| `/sources` | `provenance.sources` | 无字段 | unsupported_for_v1 |
| `/assertions[*]/id` / `/revision` | `assertions[].id` / `revision` | 无稳定字段 | unsupported_for_v1 |
| `/assertions[*]/text` | `assertions[].text` | `WorldbookRule.Content` 或变体正文 | lossy；必须保留报告 |
| `/assertions[*]/expressions[*]/id` / `/revision` | `expressions[].id` / `revision` | 无稳定字段 | unsupported_for_v1 |
| `/assertions[*]/expressions[*]/text` | `expressions[].text` | `WorldbookVariant.Content` | lossy；正文可映射 |
| `/assertions[*]/expressions[*]/layer` | `expressions[].layer` | 无可靠字段 | report_only |
| `/assertions[*]/expressions[*]/grants` | `access.grants` | `WorldbookWhen.Roles/Cultures/KingdomIds/SettlementIds/SkillMin` | lossy |
| `/assertions[*]/expressions[*]/denies` | `access.denies` | 无可靠否定字段 | unsupported_for_v1；不得静默丢弃 |
| `/assertions[*]/expressions[*]/fallback_referral_ids` | `expressions[].grants[].referral_ids` | `WorldKnowledgeRule.ReferralIds` | only public-askable registry targets; deny/disabled/unknown paths emit none |
| `/lifecycle` / `/aliases` / `/redirects` | `identity.*` | 无稳定字段 | unsupported_for_v1 |
| `/author_created` / audit event | `provenance.review` | 无字段 | unsupported_for_v1 |

## 编译规则

- 任一必填字段为 `unsupported_for_v1` 时，只能生成 v2 候选包和报告，不得生成 v1 兼容文件。
- `mapped`、`lossy`、`unsupported_for_v1` 是机器可校验枚举。
- `lossy` 必须列出丢失风险和源字段；`unsupported_for_v1` 必须列出阻断原因。
- 候选包写入独立目录并带 `awake.worldbook.v2.incompatible_with_v1=true` 标记，marker 只作为诊断信息。
- 文件名、数组下标和显示名称不能参与 ID 生成。

## 隔离验收

F15 必须覆盖：正常自动探测不发现 export；`--out` 位于 v1 探测树或其规范化等价路径时在写入前拒绝。手工复制或显式传入 manifest 属于 unsupported，不纳入“不消费”保证；若未来需要 fail-closed，必须另立运行时适配计划。
