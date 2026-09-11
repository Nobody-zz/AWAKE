# Worldbook Studio 用户级数据安全修复：审查日志

**计划**：`docs/PLAN-WorldbookStudio-UserBugFix-20260826.md`  
**任务编号**：`WORLDBOOK-STUDIO-USER-BUGFIX-20260826`  
**日志状态**：`NEEDS_REVIEW`  
**日期**：2026-08-26  

## 审查边界

本日志只记录计划审查，不代表代码审查、发布审查或游戏内验证。审查节点被授予只读权限，不能启动游戏、Provider、Worker、Launcher，不能修改源码、计划、checkpoint 或发布包。

## Round 1：独立只读挑战

- 节点：`review-plan-r1`
- 角色：独立 reviewer
- 读取范围：计划、主保存服务、工作区写入服务、Web 主编辑器路由和前端作者模式入口
- 结果：审查节点在限定时间内未返回正文；经中断后关闭
- verdict：无
- 结论：`needs_review`

## Round 2：收窄范围重试

- 节点：`review-plan-r2`
- 角色：独立 reviewer
- 读取范围：同一计划和保存相关最小方法集合；禁止递归扫描历史目录和生成物
- 结果：审查节点在限定时间内仍未返回正文；经中断后关闭
- verdict：无
- 结论：`needs_review`

## 当前判定

- 未获得 `VERDICT: APPROVED`，不能把本计划视为已通过的实现授权。
- 当前计划可以作为用户审签用的修复方案草案；它不授权修改代码、修改批量合同、同步游戏目录或修改冻结 AWAKE 候选。
- 未根据未返回的审查节点臆造 findings，也未将超时、关闭或工具状态解释为“审查通过”。

## 下一步

1. 用户确认第一阶段边界和三个开放决策。
2. 重新进行一次可返回正文的独立只读审查，要求明确 `VERDICT: APPROVED` 或 `VERDICT: REVISE`。
3. 若返回 `REVISE`，只吸收有源码证据的修订并追加记录；若返回 `APPROVED`，仍需用户签收后才能进入代码实现。

## Round 3：计划拆分后的独立只读挑战

- 节点：`review-plan-r3`
- 输入：计划摘要和已确认的保存/路由事实；无文件写入权限，无外部副作用权限
- verdict：`VERDICT: REVISE`
- 主要 findings：
  - 原第一阶段把异步竞态、CAS、dirty guard、高级模式校验、操作门禁和本地草稿合并过多；要求拆为 1A、1B、1C。
  - `editGeneration` 只能隔离前端响应，不能单独阻止旧请求写盘；请求必须绑定文档身份、模式、捕获代次和 CAS 基线。
  - 高级保存当前存在先写后读风险；写前解析/Schema 校验必须在服务端完成，并验证目标文件、临时文件和 revision 的失败后不变性。
  - 编译/导出/预览必须验证保存成功后的请求顺序和使用的 revision，不能只检查按钮是否被调用。
  - 需要补充保存已提交但客户端超时/响应丢失、保存期间再次保存、页面离开、直接 API 调用和草稿过期等验收场景。
- 采纳修订：
  - 当前实施批次收窄为 1A：单一编辑会话、读取响应隔离、作者模式保存协调器、保存状态提示。
  - 1A 明确“保存期间继续编辑允许、同一档案最多一个保存请求、重复点击显示 busy、完成后由用户再次保存”。
  - 1B 单独处理高级模式写前校验、危险操作门禁、编译/导出/预览顺序和页面离开。
  - 1C 单独处理本地临时草稿，并先冻结草稿格式、隔离键、大小和保留策略。
  - 将验收表拆成 A1–A7、B1–B6、C1–C4，并补入超时、操作顺序、API 直调和失败后不变性。
- 未采纳建议：数据库、全局状态框架、自动合并、多用户协作和批量重构；这些不直接解决当前主编辑器保存竞态，会扩大回归范围。
- 当前结论：`needs_review`；本轮 `REVISE` 已吸收，但还没有取得新的 `VERDICT: APPROVED`。

## Round 4：1A 实施闸门复核

- 节点：`review-plan-r4`
- 输入：拆分后的 1A 范围、作者模式保存语义、A1–A7 及补充 A8–A15 验收摘要
- verdict：`VERDICT: REVISE`
- 主要 findings：
  - dirty 必须明确定义为规范化可编辑模型与保存投影的比较；不能同时声称“dirty 只派生”又强制 generation 变化后保持 dirty。
  - path/mode/edit generation/hash/revision 仍不足以防止 A→B→A；必须使用每次重新打开都失效旧结果的不可复用 session token，以及每个操作唯一 request token。
  - 保存成功后必须把投影、sourceHash、revision 作为不可拆分基线更新；编辑代次变化时只保留当前输入，不得遗留旧 revision。
  - 所有响应可见状态，包括正文、诊断、读取错误、loading、dirty、revision 和 save status，都必须经过同一 token 闸门。
  - 必须明确长期 pending、用户中止、运输层失败和迟到响应语义；不能自动重试或误报成功。
- 采纳修订：
  - 1A 采用稳定规范化的可编辑字段比较 dirty，允许用户改回保存内容后恢复为未修改。
  - 增加不可复用 `sessionToken` 和唯一 `requestToken`，所有 UI 状态回写均受其保护；不把这些前端字段加入 API 合同。
  - 将成功基线锁定为 `{editorProjection, sourceHash, revision}`，编辑期间成功返回时也完整更新该元组。
  - 1A 不设置自动 HTTP 超时和自动重试；长期 pending 保持 busy，用户中止/运输失败进入“保存结果待确认”，通过回读核对。
  - 验收和测试补入 A8–A15：ABA、深复制 payload、基线原子更新、失败不部分更新、旧错误/成功状态隔离、跨档迟到保存、API 合同和结果待确认。
- 未采纳建议：第二次保存自动排队/合并、将前端 generation 字段放进 API、强制加入 reducer 作为验收条件；前者扩大状态语义，后两者分别改变合同或把实现细节误当行为要求。
- 当前结论：`needs_review`；修订已写入计划，需再次取得独立 `VERDICT: APPROVED` 后才能改代码。

## Round 5：前端可执行性与结果不确定性复核

- 节点：`review-plan-r5`（独立只读审查）
- 输入：1A 计划、`index.html` 当前作者模式入口、`Application.SaveEditorDocument`、`Workspace.SaveAuthoringIfUnchanged`
- verdict：`VERDICT: REVISE`
- 主要 findings：
  - 当前计划需要明确同档案锁的所有者、状态转换、`force` 行为和“检查保存结果”例外；仅有全局 `busy` 不能证明 A3。
  - 计划要求的 `sessionToken/requestToken` 尚未在当前实现中存在，必须约束正文、诊断、错误、loading、dirty、revision、save status 和 `finally` 的统一回写闸门。
  - dirty 规范化需要冻结为可执行函数和测试向量，不能继续使用未排序的 `JSON.stringify` 作为暗含规则。
  - 保存必须捕获完整深复制上下文；成功基线 `{editorProjection, sourceHash, revision}` 必须整体更新，附加回读不能使用变化后的 `state.currentPath`。
  - 必须区分目标替换前失败与目标替换后客户端结果未知；当前 1A 不设置自动超时，但需要显式中止后的 `pending-confirmation` 和回读生命周期。
  - “非法高级模式内容不覆盖有效文件”与 1A 延期到 1B 冲突，必须从 1A 不变量移出。
- 采纳修订：
  - 增加 1A 可执行状态与锁契约：`idle/saving/pending-confirmation/failed/conflict`，保存和待确认期间禁止切档/重开/切换模式，显式中止只进入 pending。
  - 明确保存结果检查、baseline 权威来源、前后替换失败分类及 `force=true` 的不可绕过范围。
  - 将高级模式安全写盘明确移出 1A，不再把 1A 证据包装成高级模式保护。
  - 增加 A1–A15 的 Setup/Action/Expected/Network-File assertions 测试卡和统一确定性要求。
- 未采纳建议：引入数据库、全局状态框架、自动保存队列、自动合并、多用户协作锁；这些不是本批次解决当前用户级丢数据问题的最小路径。
- 当前结论：`needs_review`；以上修订已写入计划，等待下一轮独立只读审查。

## Round 6：状态映射、dirty 向量与 pending 闭环复核

- 节点：`review-plan-r6`
- 输入：R5 修订后的 1A 计划、状态/锁契约、A1–A15 测试卡
- verdict：`VERDICT: REVISE`
- 主要 findings：
  - dirty 仍缺少可执行的字段清单、空值/缺省值等价规则、类型归一化和具体向量；不同实现可能得到不同结果。
  - `failed`、`conflict`、`pending-confirmation` 的失败点映射仍有“失败或待确认”等二选一表述，必须为每种证据指定唯一状态，并列出文件/baseline/重试结论。
  - pending 回读未明确完整三元组的判定；投影相同但 hash/revision 部分变化、迟到成功和检查失败的结果需要唯一归类。
  - conflict 的锁恢复、`force=true` 允许范围、被锁操作是否发请求及“页面离开进入 pending”与 1B 边界需要明确。
- 采纳修订：
  - 固定 `normalizeAuthorProjection` 的字段、归一化规则、排除字段和 D1–D6 测试向量。
  - 增加失败状态唯一映射表：400/422→failed，409→conflict，不确定错误→pending；补充前置失败和不确定失败的文件断言边界。
  - 固定 pending 回读四种结果：完整新三元组确认、完整旧三元组未提交、部分变化/无法归属冲突、GET 失败继续 pending；迟到旧响应不得回写。
  - 明确 `reloadFromDisk` 是 conflict 唯一解除入口；`force` 仅限启动/创建/显式冲突重读，saving/pending/conflict 不能绕过；页面离开不在 1A 转 pending。
  - 将 A6/A11/A12/A13/A15 改成唯一状态和请求/文件断言。
- 未采纳建议：引入自动合并、跨用户锁、数据库持久化和 1B 高级模式校验；均超出当前作者模式保存安全批次。
- 当前结论：`needs_review`；修订已写入计划，等待下一轮独立只读审查。

## Round 7：R6 修订后的授权复核

- 节点：`review-plan-r7`
- 输入：R6 修订后的 1A 计划、dirty 规范化函数契约、失败状态唯一映射、pending 回读判定和 A1–A15 测试卡
- verdict：`VERDICT: REVISE`
- 主要 findings：
  - pending 的“明确未提交”不能只看旧 hash/revision；必须同时比较保存开始时的旧 baseline projection，已确认提交则比较本次 POST 的 captured projection。
  - A1–A15 还没有直接覆盖 conflict 状态下的切档、重开、模式切换和 `force`，也没有覆盖 `reloadFromDisk` 的取消、成功、失败和迟到响应。
  - A6/A11 的异常仍需按 400/422、409、5xx/网络/Abort/解析失败/保存后回读失败拆成唯一状态和对应文件/baseline 断言。
- 采纳修订：
  - 保存上下文增加 `capturedBaselineProjection` 与 `capturedModelProjection`；pending 回读改为完整 path + projection + hash + revision 判定。
  - 增加 A13a/A13b/A13c，明确锁住被阻止请求、旧响应和 conflict 恢复路径；补充统一确定性要求。
  - 将 A6/A11 拆成明确前置失败、CAS 冲突和结果未知三个类别，并规定只在当前 CAS 权威路径上断言 409 文件不变。
- 当前结论：`needs_review`；以上修订已写入计划，等待最后一轮独立只读审查。

## Round 8：最终 1A 授权复核

- 节点：`review-plan-r8`
- 输入：R7 修订后的 1A 计划、双投影 pending 判定、conflict 锁恢复测试和 A1–A15 测试卡
- verdict：`VERDICT: APPROVED`
- 主要结论：
  - 1A 已限定为作者模式编辑会话与保存安全，不把高级模式、页面离开守卫或本地草稿的未完成能力混入本批。
  - dirty 已固定为 `normalizeAuthorProjection` 与 D1–D6 向量；保存上下文同时保存旧 baseline projection 和本次提交 projection。
  - pending 回读已固定为完整规范路径、projection、hash、revision 判定；400/422、409 和结果未知状态映射唯一；锁、force、reloadFromDisk 和 A1–A15 测试卡可执行。
- 实现硬约束：
  - 所有可见状态回写必须通过当前 session/request token、文档身份、模式、编辑代次和保存基线校验。
  - 保存请求必须使用深复制快照，成功时整体更新 `{editorProjection, sourceHash, revision}`；期间新编辑不得被响应覆盖。
  - `saving/pending-confirmation/conflict` 禁止保存、切档、重开、切模式及 force 绕过；pending 仅允许一次完整回读，不自动重试；`reloadFromDisk` 须由用户确认放弃当前表单后执行。
- 当前结论：`APPROVED_FOR_1A_IMPLEMENTATION`；计划已登记为可实现，用户已通过当前“做”请求签收。
