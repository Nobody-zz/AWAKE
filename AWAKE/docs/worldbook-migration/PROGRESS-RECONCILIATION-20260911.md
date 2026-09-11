# 世界书迁移线进度对账报告 — 2026-09-11

> 目的：旧工作区 `docs\worldbook-migration\`（265 文件）于 2026-09-11 迁回后，对账其记载的实际进度
> 与新工作区文档（认为停在 CONTRACT-REPAIR round 0）的差异。本文为只读对账结论，不含任何代码/内容改动。

## 一、结论一句话

**语义迁移（内容翻译）实际推进到了 R3 准备阶段：修订输入已备好、但被两道闸门明确拦住（target span 未闭合、全部人工决策未做），candidate 生成未被允许。** 新工作区文档记载的 "round 0 刚立项" 已过时；CONTRACT-REPAIR 计划书要的部分交付物（15 份 schema、工作表、重写候选）实际已存在，但 **validator 脚本不存在**。

## 二、语义迁移线时间线（R1→R3）

| 阶段 | 日期 | 做了什么 | 结论 |
|---|---|---|---|
| R1 | 0903-0904 | 30 条目语义工作表 + 5 个代表文件重写候选；独立审查 + 红测 | 审查发现命题/claim/span 三向不闭合等结构性问题 → 打回修订 |
| R2 | 0906 | `SEMANTIC-REVISION-BRIEF-R2`：为 5 个候选各列出最小修订动作（拆 claim、绑定 origin/locator、视角/时间范围不扩大）；`DECISION-PACK-R2` + `ACTION-QUEUE-R2`（23 个动作） | 修订任务包形成，状态 `needs_review`，明确"本轮不生成新 rewrite candidate" |
| R3 | 0908 | `SEMANTIC-REVISION-INPUT-R3`、`CLAIM-ORIGIN-REVISION-DRAFT-R3`（22 个新 claim，全带 origin+binding）、`TARGET-SPAN-PLAN-R3`、`WORKSHEET-R3-REVISION-DRAFT`、`EVIDENCE-PACK-R3` | **`R3-PREPARATION-VALIDATION` 给出权威结论**：`target_spans_closed=false`、`candidate_generation_allowed=false`、`all_decisions_unresolved=true` —— R3 停在这里，等人工 |

### R3 卡点（来自 R3-PREPARATION-VALIDATION checks 字段，一手证据）

- 23 个修订动作中 22 个新 claim 已建立且全带 origin/binding（这部分机器侧已就绪）；
- 但所有 target span 标记 `missing_until_candidate_r3`（要在生成候选时才落位）；
- 所有动作 `decision=unresolved` —— **每一条都要人工裁决 keep/revise/drop**；
- 因此校验明确判 `candidate_generation_allowed=false`：不允许生成候选。

## 三、CONTRACT-REPAIR 交付清单对照

计划书（`PLAN-WORLDBOOK-SEMANTIC-CONTRACT-REPAIR-20260903.md`）要交付 vs 实际：

| 交付物 | 计划 | 实际状态 |
|---|---|---|
| 语义迁移 schema（4 份新 schema） | 待补 | **已有 15 份 schema** 在 `worldbook-migration\`（含 semantic-worksheet / provisional-id / snapshot-manifest v1+v1.1 / rewrite-review / origin / conflict-group / when-decision 等） |
| validator `validate_semantic_batch_20260903.py` | 待写 | **不存在**（仓库与归档区均未找到任何 .py / validator 脚本）——且本机无 Python 3 |
| 30 份工作表 | 待做 | **R1 已有 30 条目工作表**（`SEMANTIC-WORKSHEETS-DOWNLOAD-20260903`，归档区）；R3 修订稿覆盖 5 候选（22 新 claim） |
| 5 份重写候选 | 待做 | **R1 已有 5 份**（`semantic-rewrite-batch-download-20260903`，归档区），但为被打回的 r2 版，待 R3 修订后重生成 |
| 阻断 fixture + 验证报告 | 待做 | 无独立 fixture；R3-PREPARATION-VALIDATION 部分承担了校验角色 |

审查流程状态：CONTRACT-REPAIR 的 review-state 仍是 `active / round 0 / user_signoff=false` —— 流程上它还是当前有效计划，但其"从零补契约"的前提已经过时。

## 四、Studio 配套线（0906-0910，新工作区基本没记录）

1. **红测 21 轮（0906-0907）+ C01-C18 修复**：AI 管线红队审查，缺陷 18 条有证据矩阵跟踪，后续并入 0910 修复计划。
2. **0910 红测修复批次**（来源：四轮红队 49 条发现）：
   - 批 0（6 条口径决策）✅ 完成；
   - 批 1（热修 14 条：首屏阻塞/错误可见性/默认值/FTUE）✅ 完成；
   - 批 2（生成正确性 11 条：覆盖率校验/候选边界/冲突误判/token 膨胀）✅ 完成，冻结字段契约；
   - 批 3（检索质量：别名进 keywords、歧义告警）✅ 完成；
   - **批 4 进行中**：X1（界面配置本机 Worker）+ G6 后半（本地链路发系统提示词）已完成，**N1/N2/B4 未开始**。
3. **作者闭环验收（0910）通过**：生成→建档→编辑→保存→校验→编译导出全链路 4 步 PASS（本地 Ollama，pravend 夹具）。结论并入手册 v3。
4. **0911 衔接**：新工作区的 Quick Authoring 上下文预算修复 + E2E 首通（`PLAN-QUICK-AUTHORING-CONTEXT-BUDGET-20260911` / `RESULT-...-E2E-20260911`）是这条线的直接延续——即 Studio 侧在迁移动作之后没有断档。

## 五、剩余工作清单（从现在到"能投影 Authoring v1"）

1. **人工裁决 23 个 R3 动作**（keep/revise/drop）——这是内容翻译线的唯一硬卡点，机器侧已就绪；
2. 落位 5 候选的 target span（在候选重生成时完成，依赖第 1 步）；
3. 重生成 R3 修订候选（不覆盖 r2），跑三向闭合校验（正文命题/claim/origin/span）；
4. 补 validator（或按本机无 Python 3 的现实改用 PowerShell/C# 实现）；
5. 独立审查 + 用户签收后，才允许投影 Authoring v1 → 编译 v2 运行包；
6. Studio 批 4 剩余 N1/N2/B4 收尾。

## 六、对 CONTRACT-REPAIR 计划的处置建议

CONTRACT-REPAIR 应**改写为续行计划**（基于 R3 现状），而非按"从零补契约"的原文执行；改写需按高风险批次流程过审 + 用户签收。本报告即为改写的输入。
