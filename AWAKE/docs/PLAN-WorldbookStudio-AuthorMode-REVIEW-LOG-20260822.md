# Plan Review Log: Worldbook Studio 作者模式

Act 1（设计与范围锁定）已完成，用户于 2026-08-22 确认采用“五步引导式作者模式”，并要求继续推进。MAX_ROUNDS=5；第三轮后为降低事务复杂度，移除本批来源转换，改为来源型对象作者模式只读。

## Round 1 — Codex

VERDICT: REVISE

主要问题与修订方向：

1. 编辑快照遗漏 `universe`、`registry_bindings`、`authority` 等 v1 必需字段；已加入完整快照和只读默认值。
2. v1 在文档、断言、表达层均要求 `sources` / `author_created` 二选一；已增加来源型、原创型和显式转草稿规则。
3. `canon` 原创内容必须有 approved audit event；已禁止普通下拉直接批准，并保留 `review_event_id` 门禁。
4. `expressions`、权限和 referral 实际位于 assertion/expression 层；已改为递归映射。
5. 新 ID 不得由可变中文正文生成；改为首次生成后持久化的稳定 ID。
6. 文档、断言、表达 revision 需分别维护；已写入逐层 revision 规则。
7. v1 禁止未知字段；已改为未知字段 fail closed，合法但未覆盖字段原样保留。
8. 权限条件不只有身份/scope/detail；已加入年龄、技能、性别、族长及未支持实体条件的保留规则。
9. 作者模式不能重写并丢失高级权限条件；改为按稳定 ID 合并并显示未覆盖字段。
10. 作者/高级模式双向刷新未定义；已加入单一服务端快照和成功保存后重新投影。
11. 新保存 API 必须 CAS；已加入 source hash、revision、registry hash 和 409 冲突语义。
12. 新路由必须复用 authoring 路径/reparse 安全策略；已加入计划和测试。
13. 保存失败不能留下坏文件；已要求临时文件、解析/schema 校验通过后原子替换。
14. 诊断不能只靠数组索引；已改为 assertion/expression/rule 稳定 ID 定位。
15. catalog 版本/hash 必须闭环；已加入快照绑定和失配阻止保存。
16. 默认值必须 fail closed；已锁定 `needs_review`、`base`、最小权限和成人/正典显式确认。
17. 未覆盖字段需可见；已加入只读摘要和高级入口。
18. 前端需定义空态、删除确认、离开保护、键盘和恢复；已加入作者模式交互要求。
19. 异步响应可能覆盖新档案；已加入 request generation/AbortController 和 path/revision 检查。
20. Core 测试不能证明 DOM 流程；已要求真实浏览器闭环。
21. round-trip 需覆盖来源/原创、状态、生命周期、条件和 JSON/YAML；已加入 fixture 矩阵。
22. 旧 API 响应契约需回归；已加入旧路由结构/状态码/副作用检查。
23. 作者保存必须使旧 AI buffer 失效；已加入 CAS smoke 和 apply 拒绝测试。
24. 原计划范围过大；已拆为 Phase A 只读、Phase B CAS 保存、Phase C 默认入口切换。

## Round 2 — Codex

VERDICT: REVISE

大部分第一轮问题已解决，但仍需补齐：

1. 新增卡片必须在客户端生成不可变 `clientKey`，保存失败重试复用，避免重复 ID。
2. 诊断稳定 ID 必须有显式解析算法；无法解析时不能伪造卡片目标。
3. 统一先做完整 v1 schema 校验，再做对象层级作者字段白名单检查。
4. 来源转换要按对象边界、同一临时事务和 audit ledger 记录执行。
5. revision 只由规范化语义变化触发，不能因 YAML 排版变化递增。
6. 权限条件需明确 editable/readonly-summary/advanced-only 和显式 dirty 合并。
7. 有效但缺少中文显示名是 warning，未知 ID 才阻止保存。
8. 浏览器闭环需要固定 Browser/Playwright 路径、`data-testid`、截图/控制台/网络证据。
9. API/CAS smoke 需要独立脚本、临时 workspace、随机端口、健康检查、清理和失败证据。
10. 高级模式保存也必须刷新服务端快照并使旧作者/AI buffer 失效。

### Claude response

上述十项已写入设计与实施计划：客户端 key、诊断解析、schema 顺序、事务审计、语义 hash、权限 dirty 标记、warning/error 区分、Browser 证据、`editor-api-smoke.ps1` 和高级保存快照失效均已列为硬性步骤，提交第三轮复审。

## Round 3 — Codex

VERDICT: REVISE

剩余问题：来源转换会涉及文档文件与带 `sequence/previous_event_hash/event_hash` 的 JSONL 审计链，不能简单声称与文档原子替换属于同一事务。若继续保留来源转换，必须新增 pending journal、提交标记、单写锁和启动恢复协议。

### Claude response

采用更简单且更安全的范围修订：本批不做来源转换。来源型断言/表达在作者模式只读，修改请求 fail closed 并保留原缓冲区；高级模式仍可维护，后续另立“来源脱钩与审计事务”批次。这样本批只需要单文件临时写入、完整 schema 校验和 CAS，不新增跨文件事务系统。

## Round 4 — Codex

VERDICT: REVISE

发现计划中仍残留“显式转换标记”“来源转换”标题和“来源转换失败”测试语义，与已锁定的来源只读范围冲突。

### Claude response

已删除保存请求中的转换标记；将任务改为“来源只读门禁”；浏览器验证改为“尝试编辑来源型对象时被拒绝且缓冲区保留”。

## Round 5 — Codex

VERDICT: APPROVED

最终确认：来源型 assertion/expression 在作者模式只读，保存 API 不携带转换标记；其余 authoring v1 字段保留、递归映射、稳定 ID/revision、权限条件、registry hash、CAS、原子写入、模式刷新、诊断降级、API smoke、浏览器门禁和分阶段启用规则均可按当前 Studio 结构实施。
