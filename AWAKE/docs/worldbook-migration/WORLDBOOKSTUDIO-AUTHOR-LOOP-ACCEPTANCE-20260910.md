# Worldbook Studio 作者闭环验收报告（2026-09-10）

> **已并入唯一权威文档**：`WORLDBOOKSTUDIO-AUTHOR-HANDBOOK-v3-20260910.md`（E 部分，2026-09-10）。
> 本文保留为验收证据与历史记录；结论以 v3 手册为准。

> 目的：以“作者视角”验证 AI 生成的世界知识能否真正走完「生成 → 建档 → 作者编辑 → 保存 → 校验」，并记录摩擦点。  
> 环境：打包版 `artifacts/current-test/WorldbookStudio`（dev 模式本地回环）+ 本地 Ollama worker；不使用云端 Provider。

## 1. 验收链路与结果

| 步骤 | 命令/入口 | 结果 | 耗时 |
|---|---|---|---|
| AI 生成待审候选 | `_tmp\real-worker-pravend.ps1`（prepare→generate→create-document） | 候选带 era/锚点，建档 `status=needs_review` | ~20s |
| 作者打开 AI 档案 | `GET /api/editor-document` | `era=historical`、`subdomain=territories`、断言 2 条 | 0.2s |
| 作者读取原文 | `GET /api/document` | revision=1 起 | 0.02s |
| 作者编辑并保存 | `POST /api/authoring/save-authoring`（CAS：sourceHash+revision） | revision 3→4，编辑保留，era 不变 | 0.11s |
| 作者保存后校验 | `POST /api/validate`（带当前 hash/revision） | `Valid=true`、诊断 0 | 0.16s |
| 编译/导出 staging | `scripts\a1-authority-smoke.ps1` | PASS（register→selection→approve→compile-proof→compile→export） | ~3s |

证据文件：
- `docs/evidence/WORLDBOOKSTUDIO-AUTHOR-LOOP-ACCEPTANCE.json`（4 步全 PASS + 耗时）
- `_tmp/pravend-real-worker-evidence.json`（生成/建档/引文审计/锚点）
- `scripts\author-loop-acceptance.ps1`（可重复执行；幂等：certainty 在 unknown/bounded 间切换）

## 2. 作者体验观察（摩擦点）

1. ~~**摘要仍是占位**~~（已修，2026-09-10）：worker 现在为每个 section 生成 `summary` 并写入候选 metadata；实测建档文档的摘要已是 AI 内容（如“巴拉维诺斯是卡拉狄乌斯大帝建立的重要殖民地，取代沙拉斯成为卡拉德人的首都。”），不再是“待补充”。
2. ~~**二级主题有时为空**~~（已修，2026-09-10）：worker 要求每个 section 给出 subdomain，并带有按领域的确定性兜底（politics→kingdoms、geography→settlements、war→war_history、economy→trade、culture→customs）；实测 4 个候选均有 subdomain。
3. **校验接口要求版本信息**：`/api/validate` 必须带当前 `sourceHash`+`revision`，否则返回 `WB-OP-VERSION-400`。契约正确，但外部脚本/自动化容易踩坑（本报告脚本已修正）。
4. **工作区路径冗长**：临时工作区名（`awake-pravend-<guid>`）不便人工识别，调试时需从证据 JSON 读取。
5. **模型偶尔照抄原句**：worker 已加确定性兜底——当事实文本与引文完全相同时改为“官方记载：…”的引用式复述，保证事实与引文分离（验收项 `text-is-restatement-not-copy` 恢复 19/19）。

## 3. 结论

- 作者闭环可用：生成 → 建档 → 编辑 → 保存（CAS 生效）→ 校验全绿；编译/导出由权威链 smoke 覆盖通过。
- 首轮发现的两个内容补全短板（摘要占位、二级主题为空）已在本批修复并复验；当前期望对照 **19/19 PASS**。
- 下一步：B（第二个聚落复跑，验证可复制性）→ C（runtime 消费锚点）→ v3 权威文档合并。
