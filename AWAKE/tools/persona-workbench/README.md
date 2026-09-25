# Persona Workbench — 作者侧工作区

本 README 是 **Persona Workbench 角色卡制作者 / Agent** 的统一入口。终端用户使用说明见 `README-FreePreview.md` 与 `PersonaWorkbench-使用说明.md`。

## ⚠️ 必读：创作与取证准则（强制执行）

在创建或修改任何 `characters/*.persona.json` 之前，**必须先通读**：

> **`AUTHORING-GUIDELINES.zh-CN.md`** — W1–W9 与 M1–M9：包含身份/归属取证、补写标记、门禁、完成定义及表达质量观测。具体规则中有硬门、可选建模和观测项，按原文执行，不能把所有 M 项一概当作强制字段。

违反任何一条，该卡不得标记 `approved`。本准则来自 2026-09-11 创作/校核中 5 次真实性误判（含将官方英雄 `lord_1_9` 阿庇斯·瓦罗斯误判为"无游戏依据"）的复盘，是防重复犯错的前置约束，不是可跳过的建议。

> **制作规范候选稿（尚未生效）**：[`PERSONA-CARD-PRODUCTION-SPEC.zh-CN.md`](PERSONA-CARD-PRODUCTION-SPEC.zh-CN.md) 正在接受运行时链路与对抗性验证。它尚未替代本 README 指向的旧规范；候选验收和用户确认完成前，角色卡制作仍以 `AUTHORING-GUIDELINES.zh-CN.md` 及下方现行门禁为准。候选稿现将“有限范围可用”与“完整人格刻画”分开；本地模型曾出现错误放行和漏读，模型结果不计作独立评审。两位人类评审者使用哈希锁定的[审查包](qc/PERSONA-PRODUCTION-SPEC-REVIEW-PACK-20260925.md)。

---

## 目录导航

| 路径 | 内容 |
|---|---|
| `characters/` | 角色卡数据（`*.persona.json`，schema `persona-workbench.character.v1`） |
| `contracts/` | 契约 schema 与 authoring-handoff 信封定义 |
| `contracts/persona-workbench.character.v1.schema.json` | 卡契约（含 `origins` / 数值轴 min-max / `status` 枚举） |
| `tools/` | 门禁与稽查脚本（schema / 归属 / 文字 lint、角色名表重建） |
| 外部译名单一事实源 | `../..` 下 `docs/mappings/*-zh-en.tsv` |

## 制作/修改角色卡的固定动作

1. 读 `AUTHORING-GUIDELINES.zh-CN.md`（当前正文含 **W1–W9 + M1–M9**；不要仅凭旧版本记录里的规则清单判断哪些项目是硬门）。
2. 对目标角色先在 `heroes.xml` / `spkingdoms.xml` / `spclans.xml` 锁定 `heroId` / `kingdomId` / `clanId`。
3. 中文名走 `docs/mappings/*-zh-en.tsv` 查表。
4. 按 **W9 完成定义**跑七道门禁，全过才算完成（缺一不算，任一道红即仍为 `draft`）：

   | # | 门禁 | 入口 |
   |---|---|---|
   | 1 | 结构 | `tools/audit-character-schema.ps1` |
   | 2 | 归属 | `tools/audit-character-affiliations.ps1` |
   | 3 | 文字 | `tools/audit-character-text.ps1` |
   | 4 | 表达质量（第五门禁） | `tools/audit-character-enhancement.ps1` |
   | 5 | 编译 | `tools/compile-verify.ps1` |
   | 6 | 复读自检 | `../worldbook-runtime-sim/persona_scenario8_focused.ps1 -Cards <卡>` |
   | 7 | 运行时物化 | `tools/materialize-definitions.ps1` + `tools/audit-definitions.ps1` |

5. 一键复检：`tools/run-card-gates.ps1 -Cards <卡名>`（依次跑前四道并汇总）。
6. 质量基线：`tools/gen-persona-quality-baseline.py` → `docs/persona-quality-baseline-<date>.json`（当前基线：76 卡 / 过 2 / 败 74）。

## 数据来源

游戏在 `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\`；取证口径见准则附录（注意"heroes.xml 搜不到 ≠ 游戏内没有"）。
