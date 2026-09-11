# Worldbook Studio 批 3（运行时检索质量 2 + 1 可选）执行结果（2026-09-10）

> 批次定义见 `WORLDBOOKSTUDIO-REDTEAM-REMEDIATION-PLAN-20260910.md` 第 3 节「批 3」。
> 前置：批 0 的 D1-3 决策（别名折进 `keywords`，不动 runtime 契约）、批 1、批 2 已交付。
> 硬边界（全程遵守）：未启动游戏、未同步游戏目录、未访问云端 Provider / 真实 API Key / 网络服务；
> 未修改真实源目录与五个世界书迁移候选；未生成新的 rewrite candidate；未把任何候选标为 approved/canon/compiled/published/runtime-ready。

## 1. 逐条结果

| 编号 | 问题 | 结论 | 主要改动 | 证据 |
|---|---|---|---|---|
| K1 | 运行包 `keywords` 只有「内部文档 id + 标题」，检索不到别名与可读地点名 | **已修** | `keyword` 来源改为「标题（多语言）+ `aliases`（多语言，含字符串数组）+ 实体锚点可读名（`display_name_zh`/`english_name`/`family_name_zh`/别名）」，内部文档 id 降级为**最后一个兜底关键词**；实体登记表缺失时安全退化为「标题 + 别名 + 内部 id」，不阻断编译 | 打包版 CLI 编译 `pravend-cluster`：`keywords` = `[标题, 帕拉汶德, Pravend, 巴拉维诺斯, Paravenos, doc.*]`，6 条断言全绿 |
| D1-2 | 一个关键词命中多条词条时，运行时会把同一说法指向多个词条且无任何提示 | **已修** | 编译期把「一个关键词命中 N 条词条」写成 `WB-INDEX-AMBIGUOUS` 告警（含命中数 + 命中条目 id 与其标题），上限 20 条逐条列出、溢出给汇总；**只告警不阻断** | `pravend-cluster` 实测产出 4 条（`帕拉汶德`/`Pravend`/`巴拉维诺斯`/`Paravenos` 各命中 3 条词条），`detail` 逐条列出命中条目 |
| D1-1 | 词条 id 是否要改成可读形式 | **不改（已给判断）** | 词条 id 是运行包与存档侧的稳定契约；可读性由 `keywords` 承担，改 id 属破坏性变更且无收益 | 见本文件第 2 节 |

## 2. 冻结的字段契约（后续批次必须按此对接）

```text
runtime.entries[].keywords = [
    标题各语言值,                 // title 对象里每个字符串值一个关键词
    aliases 各语言值,             // 支持 { "zh-CN": ["别名A","别名B"] } 与 ["别名A"] 两种写法
    实体锚点可读名,               // entity_ids → docs\mappings\persona-entity 实体登记表
                                 //   display_name_zh / english_name / family_name_zh / aliases[]
    <内部文档 id>                 // 永远排在最后，仅作兜底
]
  · 去重按 Ordinal；空白项丢弃；同一关键词不会重复出现
  · 实体登记表不可达时：退化为「标题 + 别名 + 内部 id」，不报错、不阻断
runtime.indexes.keywordToEntryIds  = 关键词 → 词条 id 数组（含冲突情况，不做消歧）
validation.diagnostics[] 新增 code = "WB-INDEX-AMBIGUOUS"（severity=warning，path=null，detail=命中条目）
```

D1-1 判断（备查）：词条 id `awake:entry:<domain>.<name>` 由 `StableEntry(sourceId)` 生成，是运行包索引、编译
settlement、下游模组取用的稳定键。把它改成可读串等于同时改运行包契约、编译产物哈希与调用方匹配逻辑，
收益仅是"日志里好看"，而可读检索已经由 `keywords` 覆盖。**结论：不改。**

## 3. 用户可感知的行为变化（重要）

1. **NPC 现在能用"巴拉维诺斯""Pravend""帕拉汶德"任一说法的别名检索到同一条知识**。此前运行包里这几个词根本不存在，玩家/NPC 说旧称时检索不到任何词条。
2. **同一聚落拆成多条词条时会看到"检索冲突"告警**，而不是运行时静默返回一堆不相关词条。本批实测 `pravend-cluster` 三篇文档共享同一聚落锚点，产出 4 条歧义告警——这是**预期行为**，暴露的是"同一聚落的地理/历史/传承是否该拆成三条词条"这个内容策略问题（见第 5 节 X11）。
3. **别名现在会被编织进检索词**，因此"补别名"从"只影响显示"变成"真正影响检索"，作者需要知道这一点（作者手册待补，登记批 5）。

## 4. 测试与打包证据

```text
scripts\test.ps1        TEST_EXIT=0
  · PASS: editor session harness (12/12)
  · PASS: editor safety harness (4/4)
  · PASS: draft DOM/state harness (5/5)
  · PASS: editor content static checks / core checks (11/11)
  · PASS: Worldbook Studio harness (117/117)      ← 批 2 为 113/113，本批 +4
  · PASS: Worldbook Studio BatchTests (23/23)
  · PASS: Authoring save HTTP smoke（含 loopback-only / game_directory_touched=false 声明）

scripts\package.ps1     PACKAGE_EXIT=0
  · TEST: PASS
  · CONTRACT: PASS
  · PASS: Worldbook Studio release check artifacts\current-test\WorldbookStudio

scripts\batch3-runtime-keywords-check.ps1（本批新增，可重复执行）
  改前基线（批 2 的打包版）：aliases_present=false, entity_anchor_english_name_present=false,
                            entity_anchor_alias_present=false   → passed=false
  改后（本批打包版）        ：全部 8 条断言 true，ambiguity_diagnostic_count=4 → passed=true
  证据：docs\evidence\WORLDBOOKSTUDIO-BATCH3-RUNTIME-KEYWORDS.json
```

新增回归测试 4 条（`tests\Awake.WorldbookStudio.Tests\Program.cs`）：
`K1 runtime keywords carry titles document aliases and entity anchor names`、
`K1 runtime keywords degrade safely without the entity mapping package`、
`D1-2 ambiguous keywords are reported without blocking compilation`、
`compile settlement public projection tolerates null diagnostic fields`。

## 5. 批 3 期间新发现（增量 backlog）

| 编号 | 发现 | 判断 | 去处 |
|---|---|---|---|
| **X8** | `CompileSettlementSupport.PublicNode` 遇到 JSON `null` 抛 `NullReferenceException`：只要校验报告里有一条 `path=null` 的诊断（例如本批新增的歧义告警），**编译已提交却返回不透明的 `WB-AUTHORITY-UNKNOWN-500`**，且因 operation 已 committed，重试走同一投影路径会**永久失败** | **P1，本批已修**（`PublicNode` 首行 null 保护）+ 回归测试 | 已闭环 |
| X9 | CLI 对意外异常只回 `WB-AUTHORITY-UNKNOWN-500`，无因由可查 | 中（支持性）：已补一行 stderr `[cli] <异常类型>: <消息>`（不含堆栈与绝对路径，避免与公开投影脱敏口径冲突）；结构化 `correlation_id` 仍属批 5 | 批 5 |
| X10 | `WB-INDEX-AMBIGUOUS` 的 `path=null`，诊断无法定位到具体文档/条目，只能读中文 detail | 低-中：应把 `path` 填成文档或条目标识 | 批 5/6 |
| X11 | 同一聚落拆成"地理/历史/传承"多条词条时**必然**共享锚点别名 → 必然产生歧义告警。这是内容策略问题（是否需要"主条目/别名归属"机制），不是代码缺陷 | 中（产品口径）：需要决定"别名归主条目"还是"接受多条目共享别名" | 待你定口径，登记为批 6 候选 |

## 6. 未验证项与边界声明

- **未验证**：游戏内实际检索效果（模组本体取用运行包）——按计划属交付后的独立验收，本批只证明"包里有"，不证明"游戏内能取到"。
- **未验证**：真实云端 Provider 链路（本批未访问任何真实 Provider / Key / 网络服务）。
- **仍存在**：本机 Worker 链路**不发系统提示词**（G6 的"后半段"）——批 2 的覆盖率/边界闸门对本地 Worker 目前**没有实际约束力**，与 X1（本机 Worker 无界面配置入口）合起来是"本机 AI 独当一面"的真正瓶颈，建议提到批 4 最前。
- 过程说明：本批由子代理执行，执行中途因本地路由余额不足（HTTP 402）中断，未及自检；代码已落盘，由主代理复核、补测试、重跑全量测试与打包后验收。
