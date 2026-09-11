# Plan: Worldbook Studio 用户级数据安全修复

**任务编号**：`WORLDBOOK-STUDIO-USER-BUGFIX-20260826`  
**计划状态**：`COMPLETED_OFFLINE_VERIFIED`  
**编写日期**：2026-08-26  
**主负责人**：主 Codex；实现前由独立只读审查代理复核  
**当前最高基线证据**：R14 Studio 离线包，E2  

## 1. 目标

修复 Worldbook Studio 中“代码路径能够运行，但普通内容编辑者在快速操作、失败、重启、文件同步或重复点击时可能丢数据、状态错乱、误以为已经完成”的用户级问题。

本计划采用分阶段方式推进。**当前实施批次只处理 1A：主编辑器编辑会话与保存安全**，先建立可靠的“打开 → 编辑 → 保存 → 回读”闭环；高级模式写前校验与危险操作门禁列为 1B，本地临时草稿列为 1C。AI 批量工作台、参考资料建档、Provider、OneDrive 边界和 Launcher 生命周期继续独立排期，不在当前批次混改。

## 2. 已确认事实

以下事实来自本轮只读审查和当前工作区文件，不是推测：

- 当前 R14 批量作者包状态为 `release_ready`，Core/Web、Batch、Draft、Launcher 离线测试和发布检查已有通过记录；当前包不等于真实云端 Provider、Worker、Bannerlord 或游戏目录验证。
- `WorldbookApplicationService.SaveEditorDocument` 已有作者模式的源 hash/revision 检查、Schema 检查、原子保存和保存后重读路径。
- `WorkspaceService.SaveAuthoringIfUnchanged` 已有基于文件 hash 的 CAS 保存；普通 `SaveAuthoring` 是临时文件写入后替换目标文件的原子路径，但没有统一的编辑会话语义。
- 前端当前通过 `state.dirty`、保存基线字符串和 `beforeunload` 判断未保存状态；新建、切档、模式切换、刷新、保存、编译、导出、预览和 AI 操作由多个异步事件入口分别处理。
- 当前最大风险不是“按钮或服务完全不存在”，而是旧响应、重复点击、外部改文件、保存失败、切档和关闭窗口之间缺少统一的状态保护与用户可恢复路径。
- 现有内部字段、revision、hash、注册表信息继续由程序负责；普通编辑者不应被迫理解 YAML、CAS 或内部 ID。

## 3. 用户约束与产品边界

- 默认使用者是零基础或低技术经验的世界书内容编辑者，不是开发者。
- 默认界面继续使用中文模块化表单；高级 YAML/JSON 模式保留，但不作为普通编辑入口。
- AI 只生成候选和建议，任何内容进入正典前都必须经过人工确认；第一阶段不修改 AI 生成规则。
- 云端 AI Provider 和本机 Worker 都保留，但本轮不改 Provider 协议和 Worker 部署方式。
- 不改变 AWAKE 冻结候选、游戏运行时、世界书 schema、旧 CLI 命令和既有批量合同，除非后续独立计划明确批准。
- 不启动 Bannerlord，不同步游戏目录，不结束现有进程，不把离线测试包装成游戏内验证。
- 工作区不是 Git 仓库，回滚使用本次精确文件备份和变更前后 hash，不依赖 Git 回滚。

## 4. 当前实施批次 1A 的最小闭环

```text
用户进入档案
  → 读取档案和编辑基线（路径、hash、revision）
  → 表单编辑（编辑代次递增）
  → 用户保存
  → 服务端重新检查 hash/revision、解析并校验候选、原子写入
  → 返回新的档案投影和新的保存基线
  → 前端只在响应仍属于当前档案且编辑代次未被新输入取代时更新界面
  → 用户看到明确的“已保存 / 已保存旧版本但仍有新修改 / 保存失败可恢复”状态
```

1A 的核心不变量：

1. 未保存的内存内容不能被切档、刷新、模式切换或旧异步响应静默覆盖。
2. 保存失败时，内存编辑内容仍保留，不能被错误响应替换成旧文件内容。
3. 外部文件发生变化时，保存必须拒绝覆盖，并给出重新读取或保留当前表单的选择；另存副本不属于 1A 能力，不在本批次宣称已提供。
4. 1A 不改变高级模式写盘语义；高级模式的非法内容不得被本批次误称为已保护，统一列入 1B。
5. 保存成功后，编辑器的保存基线、revision、hash、列表状态和当前档案必须一致。
6. 1A 不改变编译、导出、预览门禁；这些操作在 1B 之前继续作为未修复边界，不得在交付说明中宣称已保护。
7. 快速切换档案时，旧请求返回的数据不能写入新档案的界面。
8. 同一档案同一时间最多一个保存请求；保存期间再次点击保存必须显示忙碌状态，不并发写盘。
9. `dirty` 只由规范化后的当前作者模型与已保存投影比较得出；用户改动后又改回已保存内容时，dirty 必须恢复为 false，不能仅因 edit generation 变化而强行保持 dirty。
10. 每次打开、重新打开同一档案或切换模式都生成不可复用的 `sessionToken`；每个异步操作再生成唯一 `requestToken`。所有成功、失败、诊断、loading、dirty 和 save status 回写都必须通过 token 闸门。
11. 保存请求必须绑定文档路径、模式、捕获的编辑代次、保存快照和当时的 hash/revision；任何不匹配响应只能被丢弃或记录为过期。
12. 保存成功响应的基线是不可拆分的 `{editorProjection, sourceHash, revision}` 元组；无论当前是否有新输入，都先更新该元组，只有编辑代次未变化时才替换当前模型。
13. 1A 不设置会改变保存语义的自动 HTTP 超时，也不自动重试；请求长期 pending 时保持“正在保存”并允许继续输入。运输层明确失败、响应解析失败、用户中止或保存后回读失败时进入“保存结果待确认”，直到回读确认或用户明确采用磁盘版本。
14. 保存处于 `saving` 或 `pending-confirmation` 时，对当前文档的切档、重新打开、切换模式和再次保存全部被写入锁阻止；只能等待或执行“检查保存结果”。这样 session token 失效不会留下同一档案的旧保存与新保存并行。
15. 文档标识以服务端返回的规范相对路径为唯一 `documentKey`；前端不接受路径别名，比较时不自行折叠不同路径。模式不属于 documentKey，但属于 session token 和保存锁条件。

### 4.1 1A 可执行状态与锁契约

1A 只有一个浏览器编辑会话；它不是跨进程或多用户文件锁。服务端继续用 source hash/revision CAS 防止外部覆盖，前端锁只负责阻止本页把旧异步操作和新操作叠在一起。

**会话字段**：

- `sessionToken`：每次打开、重新打开、切换档案或切换作者/高级模式时生成；旧 token 永不复用。
- `requestToken`：每次读取、保存、保存结果检查和用户中止都生成；同一请求的成功、失败和 `finally` 只能使用自己的 token 回写。
- `documentKey`：只接受服务端返回的规范相对路径；打开请求完成前不能把用户传入的别名写成当前 key。
- `editGeneration`：每次作者表单实际写入可编辑字段时递增；保存开始时捕获 `capturedGeneration`。
- `savedBaseline`：不可拆分的 `{editorProjection, sourceHash, revision}`；其中 `editorProjection` 必须来自服务端保存后重读返回的 `editorDocument.model`，不能来自保存前本地模型或另一个未绑定请求的 GET。
- 保存上下文还必须捕获 `capturedBaselineProjection`（保存开始前的旧 baseline projection）和 `capturedModelProjection`（本次 POST 的深复制模型 projection）；两者不能混用。

**作者模式状态**：`idle`、`saving`、`pending-confirmation`、`failed`、`conflict`。`dirty` 始终由规范化当前作者模型与 `savedBaseline.editorProjection` 比较派生，不作为可独立写入的状态。状态不是保存结果本身：`idle` 只表示没有进行中的保存操作，是否 dirty 仍单独由比较结果决定。

**dirty 规范化函数**：实现名固定为 `normalizeAuthorProjection(model)`，输入为前端作者模型，输出只包含下面列出的字段；再用递归对象键排序后的 JSON 字符串比较。文本字段保留原始空格，不做隐式 trim；枚举、ID 和数字按下列规则归一化。

```text
{
  title: string(model.title ?? ""),
  domain: enum(model.domain ?? ""),
  subdomain: blankToNull(model.subdomain),
  relatedDomains: stringArray(model.relatedDomains),
  status: enum(model.status ?? ""),
  contentTier: enum(model.contentTier ?? "base"),
  summary: string(model.summary ?? ""),
  era: {
    preset: enum(model.era.preset ?? model.era.key ?? "unknown"),
    certainty: enum(model.era.certainty ?? "unknown"),
    startYear: nullableInteger(model.era.startYear),
    endYear: nullableInteger(model.era.endYear)
  },
  assertions: array(model.assertions).map(assertion => ({
    kind: enum(assertion.kind ?? "fact"),
    text: string(assertion.text ?? ""),
    expressions: array(assertion.expressions).map(expression => ({
      layer: enum(expression.layer ?? "summary"),
      text: string(expression.text ?? ""),
      grants: normalizeRules(expression.grants),
      denies: normalizeRules(expression.denies),
      fallbackReferrals: array(expression.fallbackReferrals).map(referral => id(referral))
    }))
  }))
}
```

其中 `array(null/missing)` 为 `[]`，`blankToNull(null/missing/"")` 为 `null`，`nullableInteger(null/missing/"")` 为 `null`；`stringArray` 丢弃空字符串但保留其他元素顺序；`normalizeRules` 每项只保留 `profileId/profile_id`、`scope`、`minDetail/min_detail` 和 `conditions`，条件只允许现有 `RuleConditionKeys`，缺少条件键即不输出；`id(referral)` 支持字符串或对象的 `id/referralId`。以下字段永远排除：`documentId`、`id`、`revision`、`clientKey`、`key`、`sourceMode`、`editable`、`sourceCount`、`profileLabel`、`profileValid`、`label`、`valid`、`publiclyAskable`、`referralIds`、`advanced` 及任何诊断/UI 临时字段。数组顺序有意义，不能排序或去重。

**锁规则**：

- `idle`：允许编辑、保存、打开其他档案、重新打开当前档案和切换模式；dirty 守卫仍按 1B 之前的现有行为保留，不在 1A 宣称页面离开安全。
- `saving`：允许继续编辑；禁止再次保存、打开任何档案、重新打开当前档案和切换模式。按钮显示“正在保存”；不排队、不合并第二次保存。
- `pending-confirmation`：允许继续编辑；禁止保存、打开档案、重新打开和切换模式；唯一例外是“检查保存结果”，且同一时间最多一个检查 GET。检查结果成功后回到 `idle` 或 `failed/conflict`，GET 失败继续保持 pending。
- `failed`：允许继续编辑和再次保存；不自动重试，不自动替换表单或基线。只有服务端明确返回保存前失败的 400/422 才进入此状态。
- `conflict`：只在明确 CAS 冲突，或待确认回读发现路径/投影/版本进展不一致时进入；保留当前表单，禁止保存、切档、重开和切换模式。用户只能通过明确的“重新读取磁盘版本”操作丢弃当前表单后回到 `idle`，1A 不提供自动合并或另存副本入口；重新读取前必须由用户确认“放弃当前修改”。
- `force=true` 只允许用于启动时首次打开或服务端刚创建后的指定档案，并且只能跳过 dirty 确认；`saving`、`pending-confirmation`、`conflict` 状态下始终不能绕过锁。刷新、普通切档和用户点击列表不能传入 force；`reloadFromDisk` 在 conflict 状态是唯一例外，但仍需用户确认放弃当前表单。

**保存结果检查**：检查请求读取当前规范路径的 `/api/editor-document`，并执行唯一判定：

- **已确认提交**：`path === documentKey`、规范化 `editorDocument.model === capturedModelProjection`、`editorDocument.revision > capturedRevision` 且 `editorDocument.sourceHash !== capturedSourceHash`。此时采纳返回的完整 `editorDocument` 为新 baseline；若当前 `editGeneration` 已变化，只更新 baseline，不覆盖当前表单。
- **明确未提交**：`path === documentKey`、规范化 `editorDocument.model === capturedBaselineProjection`、`editorDocument.revision === capturedRevision`、`editorDocument.sourceHash === capturedSourceHash`。此时回到 `failed`，保留旧 baseline，允许用户再次保存。不能只比较 hash/revision，也不能把本次提交 projection 误当成旧 baseline projection。
- **冲突/无法归属**：路径不一致，或投影与保存快照不一致，或 hash/revision 只部分变化。此时进入 `conflict`，保留当前表单，不自动采纳磁盘内容。
- **仍未知**：GET 失败、响应无法解析或响应字段不足。此时保持 `pending-confirmation`，不自动重试。

回读只接受完整三元组和规范路径，不能仅凭投影相同就确认；绝不自动再次 POST 保存。

**失败分类**：

- 目标替换前明确失败（请求校验 400、Schema/taxonomy 422）：磁盘字节、文件 hash、revision 必须不变；保留当前模型和旧 baseline，状态唯一为 `failed`。
- 明确 CAS 冲突（409）：磁盘字节、文件 hash、revision 必须不变；保留当前模型和旧 baseline，状态唯一为 `conflict`。
- 临时文件写入、目标替换、服务端 5xx 或其他无法证明是否已提交的错误：状态唯一为 `pending-confirmation`，不能声称文件不变。
- 目标替换后客户端没有确定响应（Abort、网络断开、响应解析失败、保存服务端回读失败）：状态唯一为 `pending-confirmation`；保留当前模型和旧 baseline，等待用户检查结果。
- 页面关闭或刷新不在 1A 内把状态转换为 pending；1C 才处理页面离开和本地草稿恢复。

**失败状态唯一映射**：

| 证据 | 唯一状态 | 文件和 baseline 结论 | 用户可做的事 |
| --- | --- | --- | --- |
| POST 返回 400，且错误属于请求不完整/字段无效 | `failed` | 目标文件字节、hash、revision 和旧 baseline 不变 | 修正表单后再次保存 |
| POST 返回 422，且错误属于作者模式 Schema/taxonomy 校验 | `failed` | 目标文件字节、hash、revision 和旧 baseline 不变 | 按诊断修正后再次保存 |
| POST 返回 409 `WB-CAS-409` | `conflict` | 当前实现的 CAS 检查发生在目标替换前；A1 必须断言文件字节、hash、revision 和旧 baseline 不变 | 明确重新读取磁盘版本，或取消本次编辑 |
| POST 返回 5xx、网络断开、Abort、响应解析失败、服务端保存后回读失败 | `pending-confirmation` | 不能假定写入成功或失败；当前表单和旧 baseline 保留 | 等待或点击“检查保存结果”，不自动重试 |
| pending 检查 GET 明确返回旧完整三元组 | `failed` | 磁盘明确仍为旧 baseline | 修正或再次保存 |
| pending 检查 GET 返回完整匹配的新版本 | `idle` | 采纳服务端 `editorDocument` 的 projection/hash/revision 整体作为新 baseline | 继续编辑或再次保存 |
| pending 检查 GET 路径、投影、hash、revision 任一部分无法归属/不一致 | `conflict` | 不采纳磁盘内容，不更新旧 baseline | 明确重新读取磁盘版本，或取消本次编辑 |
| pending 检查 GET 失败或字段不足 | `pending-confirmation` | 当前表单和旧 baseline 保持 | 稍后再次检查，不发送 POST |

409 的文件不变断言仅针对当前 `SaveEditorDocument → SaveAuthoringIfUnchanged` 权威路径：Core 在调用写入层前检查 revision，Workspace 在目标替换前检查 hash。若未来路由改变写入顺序，必须另立计划，不能沿用 1A 证据。

**dirty 固定测试向量**：

| 向量 | 当前模型相对 baseline 的变化 | 期望 |
| --- | --- | --- |
| D1 | 只改变对象键顺序、加入 `label/profileLabel/valid/clientKey/key/revision/sourceMode` | `dirty=false` |
| D2 | 缺少 `relatedDomains`、`fallbackReferrals`、`conditions`，对比显式空数组/空对象 | `dirty=false` |
| D3 | `subdomain` 从缺失改为 `null` 或空字符串；年月从缺失改为 `null` 或空字符串 | `dirty=false` |
| D4 | 事实正文、NPC 表达正文、分类、时期枚举、权限身份/范围/详细度任一改变 | `dirty=true` |
| D5 | 两条事实或两条表达只交换顺序 | `dirty=true` |
| D6 | 规则条件数组元素顺序改变，或新增/删除一个非空条件 | `dirty=true` |

D1–D3 是 UI 投影噪声和缺省值归一化；D4–D6 是作者实际编辑内容。文本本身（包括前后空格）不自动改写，因此只改文本空格也应为 `dirty=true`。

## 5. 架构选择

### 5.1 候选方案

| 方案 | 做法 | 优点 | 风险/放弃项 |
| --- | --- | --- | --- |
| A：只锁 UI | 保存期间禁用输入和切档 | 实现简单，容易理解 | 仍挡不住刷新、旧响应、外部文件修改；编辑者等待时无法继续整理内容 |
| B：只强化服务端 CAS | 所有保存都带 hash/revision，前端保持现状 | 能避免覆盖外部文件 | 不能阻止前端旧响应覆盖当前表单；用户仍可能看到错误状态 |
| C：编辑代次 + 服务端 CAS + 统一操作守卫 | 前端保护当前会话，后端保护文件版本，危险操作统一经过守卫 | 同时覆盖丢数据、串档、外部修改和失败恢复 | 需要整理少量现有前端入口和保存 DTO；测试面比 A/B 大 |

### 5.2 选定方案

选择 **C**，但拆成三个可单独验收的批次；当前只实施 1A，不引入全局状态管理框架、Mediator、Repository 层或新的持久化数据库。

- **前端保护（1A）**：为当前档案保存 `path`、`mode`、`documentKey`、`sourceHash`、`revision`、规范化 `savedProjection`、`editGeneration`、不可复用的 `sessionToken`、唯一 `requestToken`、保存操作 token 和进行中的保存状态。`dirty` 由稳定规范化后的当前可编辑模型与 `savedProjection` 比较得出；所有异步响应提交前检查 session/request token、文档 key、模式、捕获代次和保存基线。
- **规范化规则（1A）**：dirty 比较只取作者模式可编辑字段，排除 `editable`、`sourceMode`、`advanced`、诊断和其他 UI 临时字段；对象键按稳定顺序序列化，数组保持编辑器顺序，空值/缺省值按现有投影默认规则归一化。这样用户改动后又改回同一份可保存内容时，dirty 才能可靠恢复为 false。
- **保存保护（1A）**：作者模式继续走现有 `SaveEditorDocument`；同一 `documentKey` 只允许一个保存操作，保存期间继续编辑不会被响应覆盖。成功响应中服务端保存后重读得到的 `editorDocument + sourceHash + revision` 作为一个完整基线提交；若运输层失败、响应丢失或保存后回读失败，操作保持 `pending-confirmation`，不自动重试，直到通过 GET 回读核对。高级模式的写前解析/Schema 校验、CAS 请求合同列入 1B，不能由 1A 的作者模式证据替代。
- **危险操作守卫（1B）**：切档、新建、刷新、模式切换、编译、导出、预览和关闭前统一处理 dirty 状态；1A 只保留当前档案的读取与保存一致性。
- **恢复保护（1C）**：在浏览器本地保留当前档案的临时草稿快照，用于浏览器崩溃或 Studio 重启后的恢复提示；不自动写入正典，不替代磁盘保存，不发送给 AI。
- **用户反馈**：所有失败提示采用“发生了什么 + 当前内容是否还在 + 下一步怎么做”的格式，技术错误码放入折叠区域。

### 5.3 明确不选的方向

- 不把所有编辑器、批量工作台和 AI 草稿重写成同一个状态机。
- 不在 1A/1B/1C 引入数据库、自动合并算法或多用户协作锁。
- 不默认自动覆盖外部文件，不用“强制保存”绕过 CAS。
- 不把 localStorage 草稿直接当成发布内容，不在恢复时自动标记 `approved` 或 `canon`。
- 不顺手重构历史复杂代码；每行变更都必须能对应本计划的验收项。

## 6. 实施批次拆分

### 6.0 共同执行规则

本计划不把所有风险一次性塞进一个实现批次。每个子批次都必须有自己的失败样本、代码范围、验收项和 checkpoint；前一批未通过，不进入后一批。

- 当前只申请实施 **1A：编辑会话与作者模式保存安全**。
- **1B：高级模式写前校验与危险操作门禁** 另行冻结路由合同和架构细节后实施。
- **1C：本地临时草稿与恢复** 等 1A/1B 稳定后再实施，不把草稿恢复当作当前批次已具备能力。
- 任何涉及批量合同、AWAKE runtime、游戏目录、冻结候选或真实 Provider 的变更立即停止并另立计划。

### 1A-0：基线、复现样本与合同冻结

**目的**：在改生产代码前，把 1A 要解决的竞态和失败恢复变成可重复样本。

**工作项**：

- 固定当前 R14 包、工作室源码关键文件 hash 和现有测试结果。
- 锁定当前作者模式保存接口的请求、响应、错误码、文件 hash 和 revision 行为；不修改批量合同。
- 建立最小测试入口：
  - 档案 A 的读取响应晚于档案 B；
  - 保存期间继续编辑；
  - 保存期间重复点击；
  - 作者模式 CAS 冲突；
  - 保存失败后保留内存模型。
- 记录哪些是当前已观察到的风险，哪些是新增的保护性回归，不把测试样本写成真实事故。

**拟涉及**：`tools/worldbook-studio/tests/`、临时 fixture 或现有测试入口。  
**不涉及**：正式世界书、游戏目录、冻结候选。

### 1A-1：单一编辑会话与请求隔离

**目的**：让前端只有一个状态源，知道“现在正在编辑哪个档案、哪一次请求、哪一版内容”。

**工作项**：

- 建立唯一的编辑会话对象，集中保存 `path`、`mode`、`sourceHash`、`revision`、规范化保存投影、`editGeneration`、不可复用的 `sessionToken`、唯一 `requestToken`、当前保存状态、读取错误和操作诊断；`dirty` 只能由规范化当前模型与保存投影派生。
- 每次用户编辑递增 `editGeneration`；每次打开、重新打开同一档案或切换模式都使旧 `sessionToken` 失效；每个读取或保存请求捕获 `sessionToken + requestToken + documentPath + mode + editGeneration + sourceHash + revision`。
- 旧响应如果不匹配当前 session/request token、文档、模式或保存基线，只能作为过期结果记录，不能回写正文、标题、诊断、读取错误、loading、dirty、revision 或 save status。
- `openDocument` 只提交最后一次有效读取；档案 A 的迟到响应不能覆盖档案 B。
- 不在 1A 引入自动保存队列：同一档案同一时间最多一个保存请求；保存期间继续输入是允许的，重复点击保存显示“正在保存，请等待完成后再次保存”。
- 保存失败、取消或 CAS 冲突时不替换当前内存模型；dirty 仍按规范化当前模型与原保存基线比较，不因失败或编辑代次被强行改写。
- 保存请求不设置自动 deadline；请求长期 pending 时只显示 busy，不清除 dirty，不自动重试。只有用户点击“停止等待/检查保存结果”时才可中止客户端等待并进入 `pending-confirmation`；页面离开、刷新和浏览器关闭不在 1A 内转换状态，任何迟到的旧结果只能按旧 token 处理。

**拟涉及**：`src/Awake.WorldbookStudio.Web/wwwroot/index.html`、`src/Awake.WorldbookStudio.Web/wwwroot/studio-authoring-ux.js`，必要时只新增同目录小型会话辅助文件。  
**验收重点**：旧响应不能串档，新输入不能被保存响应覆盖，双击不会产生第二次有效保存，状态只有一个权威来源。

### 1A-2：作者模式保存协调器

**目的**：先稳定普通编辑者最常用的作者模式保存链路，不把高级模式和危险操作门禁混入本批。

**工作项**：

- 保存开始前深复制当前作者模式模型，并绑定当前文档身份、模式、编辑代次和 CAS 基线。
- 服务端成功返回后，把 `editorDocument` 中已有的 `model`、`sourceHash`、`revision` 作为一个完整保存基线元组提交；
  - 如果编辑代次未变化，使用返回投影更新当前模型和基线；
  - 如果期间有新编辑，只更新基线元组，保留当前内存模型，再按规范化比较重新计算 dirty；若当前模型已改回新基线，dirty 必须为 false，否则显示“此前版本已保存，当前仍有新修改”。
- 服务端 400/422 前置失败或 409 CAS 冲突时，不更新基线元组，不替换当前模型；分别进入唯一的 `failed` 或 `conflict`。5xx、网络失败、Abort、响应解析失败和保存后回读失败一律进入 `pending-confirmation`，不得自动重试。
- 1A 的“保存结果待确认”只表示客户端没有拿到确定响应，不表示成功或失败；回读按 4.1 的完整路径、投影、hash、revision 判定表处理，匹配才允许用户采纳新的基线，否则保留本地模型并进入明确的 failed/conflict/pending 状态。
- 保存按钮、保存中的状态和失败状态必须由同一协调器驱动，不能由 `index.html` 和 `studio-authoring-ux.js` 各自清除 dirty。
- `sessionToken`、`requestToken`、`editGeneration` 只用于前端回写闸门，不加入 `/api/save-editor-document` 请求合同；保持现有成功响应字段和作者模式错误映射。
- `reloadFromDisk` 是唯一的 conflict 解除入口：用户必须明确确认放弃当前表单后，才用新的 session token 读取当前规范路径；未经确认不清空模型、不发起新档案请求。

**拟涉及**：作者模式前端入口、`WorldbookApplicationService.SaveEditorDocument` 的必要最小适配，以及 Core/Web 测试。  
**验收重点**：输入期间保存不覆盖新输入；保存失败不清空内容；成功回读后的 hash/revision/投影一致。

### 1B：高级模式安全保存与危险操作门禁（后续独立批次）

**目的**：在 1A 稳定后，修复高级模式写盘风险，并保护所有会离开当前编辑上下文的操作。

**工作项**：

- 为高级保存锁定 `documentPath + mode + sourceHash + expectedRevision` 请求合同；服务端在任何目标替换前解析 YAML/JSON 并完成对应 Schema/taxonomy 校验。
- 目标文件、临时文件和 revision 的失败后不变性必须有文件 hash、目录残留和 revision 断言。
- 先对 `/api/save-authoring` 的现有调用方、成功响应、错误码和兼容输入做 characterization；若需要扩展 DTO 或增加新路由，先补充架构 RFC，不修改已冻结批量合同。
- 统一保护切档、新建、刷新、作者/高级模式切换、编译、导出、预览和页面关闭；操作只在捕获的快照保存成功并返回匹配 revision 后继续。
- 保存失败、CAS 冲突、超时或保存后回读失败时，不发起后续操作；保留内存文本并显示重试、重新读取或取消。

**拟涉及**：`src/Awake.WorldbookStudio.Core/Application.cs`、`Workspace.cs`、`src/Awake.WorldbookStudio.Web/Program.cs`、前端入口和 Core/Web/浏览器测试。  
**进入条件**：1A 的单一会话源、保存响应隔离和作者模式回归全部通过。  
**验收重点**：非法内容、旧 revision、外部修改、文件锁定和操作顺序都不能覆盖有效文件或误用旧版本。

### 1C：本地临时草稿与恢复（后续独立批次）

**目的**：在 1A/1B 稳定后，为浏览器崩溃、Web 重启或误关窗口提供可选择的内容恢复。

**工作项**：

- 先单独冻结草稿格式、工作区隔离键、大小限制、保留时长和损坏处理；不把浏览器存储直接视为发布内容。
- 在用户编辑时以节流方式保存临时草稿：路径、源 hash、revision、模式、编辑内容和时间；不保存 API key，不发送网络。
- 重新打开同一档案时比较磁盘 hash：
  - 磁盘未变且草稿较新，提示恢复；
  - 磁盘已变，提示查看草稿或重新读取，不自动覆盖；
  - 草稿损坏时删除该条草稿并给出说明，不影响正式档案。
- 明确页面关闭、刷新、保存超时、显式丢弃、恢复后再次保存和跨工作区切换的状态转换。
- 保存成功后清理对应草稿；未得到用户确认前不能写入正典或改变审核状态。

**拟涉及**：前端恢复模块、浏览器测试和用户指南。  
**进入条件**：1A/1B 的保存与冲突状态已稳定。  
**验收重点**：草稿可恢复、可显式丢弃、不会跨工作区串线，也不会自动发布。

### 1A-3：作者模式保存状态与用户提示

**目的**：让编辑者能判断作者模式保存是否正在进行、是否成功，以及当前文字是否仍然安全。

**工作项**：

- 顶部状态至少区分：`未修改`、`有未保存修改`、`正在保存`、`已保存`、`已保存旧版本，当前仍有新修改`、`保存冲突`。
- 失败提示遵循三段式：
  - 发生了什么；
  - 当前内容是否仍在；
  - 现在可以做什么。
- 错误码、路径、hash、revision 放在“技术信息”折叠区；普通编辑者默认不需要阅读。
- 保存中的按钮状态和成功/失败提示由 1A 保存协调器驱动；不再依赖多个脚本分别清除 dirty。
- 保存失败或冲突后保留当前作者模式表单，按状态提示修正重试、检查结果或确认放弃后重新读取；不显示“完成”类误导信息。

**拟涉及**：`index.html`、作者模式辅助脚本和必要的提示测试。  
**验收重点**：编辑者能分辨保存中、保存成功、保存旧版本后仍有新修改和冲突状态。

### 1A-4：聚焦验证、回归、打包和债务审查

**目的**：只在 1A 的作者模式闭环通过后生成新的离线候选包，并证明没有破坏既有 Studio 功能。

**工作项**：

- 先跑 1A 的 Core 保存/CAS 测试，再跑 Web/HTTP 状态测试和作者模式浏览器交互测试。
- 运行现有 Studio harness、Draft/Batch 回归、Launcher 回归和 release-check；发现无关失败时分类记录，不修改无关代码。
- 生成新的内部候选包并检查 manifest、文件数量、源码/包 hash；不覆盖 R14 基线包。
- 对本批实际改动文件执行 `post-change-code-debt-audit`，统计重复逻辑、多重状态权威、未接线代码和高频 I/O 风险。
- 更新 checkpoint，明确 1A 编译、离线测试、打包、Launcher、Provider/Worker、游戏内验证分别达到的证据等级；不把 1B/1C 或游戏内能力提前宣称完成。

**1A 交付边界**：1A 只交付作者模式读取/保存会话安全。高级模式写前校验、编译/导出/预览 dirty guard、本地草稿和页面关闭恢复仍标记为未完成，直到 1B/1C 各自通过验收。

## 7. 分批验收契约

### 7.1 当前批次 1A：编辑会话与作者模式保存

| 编号 | 用户操作/触发 | 可观察结果 | 必须保持的不变量 | 失败时行为 | 最小证据 |
| --- | --- | --- | --- | --- | --- |
| A1 | 打开档案 A，再快速打开档案 B | 最终界面只显示 B；A 的迟到响应不改变 B | 当前路径、标题、内容和诊断属于同一档案 | 读取失败显示明确提示，不清空当前有效模型 | 延迟读取集成测试 + 浏览器双切档 |
| A2 | 保存期间继续输入 | 保存返回后新输入仍存在，状态继续显示未保存 | 保存请求只提交捕获快照，旧响应不得覆盖新代次 | 显示“此前版本已保存，当前仍有新修改” | 延迟保存测试 + 浏览器慢响应 |
| A3 | 保存期间再次点击保存 | 不产生并发第二次写入；按钮显示忙碌，完成后可再次保存 | 同一档案最多一个 in-flight save | 不丢输入，不重复推进 revision | 请求计数、revision 和 UI 状态断言 |
| A4 | 作者模式保存成功后继续查看当前档案 | 服务器返回投影、hash、revision 与界面保存基线一致 | 保存成功才更新基线；编辑代次未变化才替换投影 | 回读失败保留当前模型并显示失败 | Core/Web round-trip 测试 |
| A5 | 作者模式 CAS 基线已变化后保存 | 保存被拒且磁盘文件没有被覆盖 | 请求绑定文档身份、source hash 和 revision | 保留当前表单，提示重新读取或取消 | CAS 文件 hash/revision 测试 |
| A6 | 作者模式保存返回明确 400/422 或无法判断是否写入的异常 | 400/422 唯一进入 `failed`；5xx/网络/Abort/解析失败唯一进入 `pending-confirmation`；当前内存内容仍可见 | 明确前置失败才断言文件不变；不确定失败不得假定文件不变；不更新旧 baseline | `failed` 可修正后重试；pending 只能检查结果，不自动重试 | 异常注入测试 + UI 状态和文件断言 |
| A7 | 任意作者模式保存请求返回旧响应 | 旧响应不改变当前文档、模式、正文、诊断、dirty 或 revision | 单一编辑会话是唯一状态源 | 过期响应只记录诊断，不弹出误导成功提示 | 前端状态单元测试 |
| A8 | 档案 A → B → A 后，旧 A 读写响应返回 | 旧 A 响应不能改变新 A 会话 | 每次重新打开都使用不可复用 session token | 过期响应被丢弃 | ABA 顺序测试 |
| A9 | 保存开始后修改表单 | 服务端收到的是保存开始时的深复制快照 | 后续输入不能污染已提交 payload | 保留新输入并按最新基线重新计算 dirty | 延迟保存 payload 测试 |
| A10 | 保存期间编辑后成功返回 | 当前表单不被覆盖；基线元组的投影、hash、revision 一起更新 | 下一次保存使用新 revision | 显示“此前版本已保存，当前仍有新修改”或规范化比较后的未修改 | 基线原子更新测试 |
| A11 | 保存期间编辑后返回明确 400/422、409 或不确定失败 | 前两类分别进入 `failed`/`conflict`，不确定失败进入 `pending-confirmation`；当前模型可观察 | 只有 400/422/当前 CAS 路径可断言文件不变；所有分支都不得部分更新 baseline | failed 可修正重试；conflict 先明确重读；pending 只能检查结果 | 失败注入 + 状态、完整 baseline 和文件断言 |
| A12 | 当前读取请求被用户中止或运输失败，旧读取随后成功 | 当前请求保持失败或 pending；旧成功不能回退覆盖最新错误状态 | 所有 UI 状态回写都通过 request token | 保留最新会话状态；不自动重试读取 | 乱序成功/失败/Abort 测试 |
| A13 | 当前 A 保存中尝试切换到 B；A 的保存响应随后返回 | B 不进入；当前 A 的正文、模式、诊断、dirty、revision、错误和 save status 不变 | saving/pending 锁在请求发出前阻止 B GET；迟到响应仍受 session token 闸门 | 提示“正在保存/检查保存结果”，用户不能用 force 绕过 | 请求计数 + 跨档锁测试 |
| A14 | 直接调用作者保存 API | 请求体、成功字段和既有错误映射保持一致 | 前端控制字段不进入 API 合同 | API 返回明确错误，不产生隐式重解释 | Web 合同测试 |
| A15 | 保存 POST 长期 pending；用户点击“停止等待/检查保存结果”；旧 POST 后续迟到成功、失败或冲突 | 进入 `pending-confirmation`；不自动 POST；完整回读判定为已确认提交、明确未提交、冲突或仍未知 | 确认前当前模型和旧 baseline 不变；匹配时整体采纳服务端 editorDocument | 只允许新 request token 的检查 GET；GET 失败继续 pending | 模拟 pending、Abort、迟到响应和四种回读结果 |

### 7.1.1 A1–A15 可执行测试卡

所有 1A 测试使用临时工作区和两个最小有效档案 A/B；网络层使用可控制的 fake `fetch`，每个请求返回一个可手动完成、失败或保持 pending 的 deferred promise。每个测试开始时记录 A/B 文件字节、hash、revision，结束时同时检查页面状态、请求次数和文件状态。除特别说明外，`force=true` 不得绕过 `saving` 或 `pending-confirmation` 锁。

| 编号 | Setup | Action | Expected / assertions | Network / file assertions |
| --- | --- | --- | --- | --- |
| A1 | 会话为空；准备 A、B 两个有效 editor-document 响应 | 打开 A，尚未完成时打开 B，先完成 A 再完成 B | 最终 `documentKey/path/title/model/diagnostics` 全属于 B；A 响应不改任何可见状态 | 两次 GET；A 的完成结果被 token 闸门丢弃；A/B 文件不变 |
| A2 | 当前为 A，模型已保存；保存 POST 延迟返回 | 开始保存，然后输入新内容，完成 POST | 新内容仍在；状态为“已保存旧版本，当前仍有新修改”；不覆盖当前模型 | POST body 是保存开始时的深复制快照，不包含后续输入；文件只写一次 |
| A3 | 当前为 A，模型 dirty；保存 POST 延迟返回 | 连续点击两次保存 | 只有一个 POST；第二次点击只显示 busy；完成后允许用户再次保存 | POST 计数为 1；revision 只推进一次；无自动排队 |
| A4 | 当前为 A；准备服务端成功返回的 editor-document | 保存并完成服务端回读 | `savedBaseline` 的 projection/hash/revision 与响应整体一致；无新编辑时当前模型可采用响应模型 | 不再依赖实时路径追加 GET；返回 projection 来自保存后重读 |
| A5 | 记录 A 的 hash/revision，然后外部修改 A | 用旧基线保存 | 保存冲突；当前表单保留；不显示已保存 | POST 被 CAS 拒绝；A 的外部修改字节/hash/revision 保持不变 |
| A6a | 当前 A dirty；fake 保存返回 400/422 前置错误 | 保存 | 当前模型、原 baseline 和 dirty 保留；唯一状态为 `failed`，不显示成功 | 目标文件字节/hash/revision 和临时文件状态不变；无自动重试 |
| A6b | 当前 A dirty；fake 保存返回 5xx、网络断开、Abort、响应解析失败或保存后回读失败 | 保存 | 当前模型、原 baseline 和 dirty 保留；唯一状态为 `pending-confirmation`，不显示成功 | 不假定文件成功或失败；无自动 POST 重试；只允许检查结果 |
| A7 | 当前 A；构造一个已过期的保存或读取响应 | 先开始新操作，再完成旧响应 | 旧响应不能改变正文、诊断、读取错误、loading、dirty、revision 或 save status | 旧 request token 只记录过期结果，不产生 toast“已保存” |
| A8 | 完成 A → B → A 三次打开；保留第一次 A 的延迟响应 | 完成最后一次 A 后再完成第一次 A 响应 | 最终界面属于第二次 A 会话；两个 A 会话的 token 不可互用 | 旧 A 响应被 session token 拒绝；A 文件不被前端重复写入 |
| A9 | 当前 A；保存入口接收可观察的 request body | 保存开始后立即修改表单 | request body 与保存开始快照深度相等；后续输入只存在当前内存模型 | 修改嵌套事实/表达对象后，已发出的 body 不变；请求只读取一次快照 |
| A10 | 当前 A dirty；保存期间新增输入；服务端返回成功 projection | 完成保存 | baseline 元组一次性变为返回 projection/hash/revision；当前新输入不被替换；dirty 由规范化比较决定 | 下一次保存使用返回的新 revision；不允许残留旧 revision/hash |
| A11a | 当前 A dirty；保存期间新增输入；fake 返回 400/422 | 完成失败 | 当前模型、baseline 元组和 dirty 全部保持；唯一状态为 `failed` | 文件字节/hash/revision 与失败前一致；保存 body 仍是旧快照；无成功 toast |
| A11b | 当前 A dirty；保存期间新增输入；fake 返回 409 | 完成失败 | 当前模型和旧 baseline 保持；唯一状态为 `conflict`，禁止保存/切档/重开/切换模式 | 当前 CAS 路径下文件字节/hash/revision 与失败前一致；无成功 toast |
| A11c | 当前 A dirty；保存期间新增输入；fake 返回 5xx/网络/Abort/解析失败 | 完成失败 | 当前模型和旧 baseline 保持；唯一状态为 `pending-confirmation` | 不部分更新 baseline；不自动 POST；只允许检查结果 |
| A12 | 连续发起两次读取；第二次为当前请求并失败，第一次稍后成功 | 先完成第二次失败，再完成第一次成功 | 最新失败状态保持；旧成功不恢复正文、诊断、loading 或错误 | 旧 request token 被拒绝；当前 session 不被回退 |
| A13a | 当前 A 处于 `saving` 或 `pending-confirmation`；准备 B | 点击 B、重新打开 A、切换作者/高级模式，并尝试传入 `force=true` | 所有操作在发请求前被锁阻止；当前 A 的正文、模式、诊断、dirty、revision、错误和 save status 不变 | B/A GET 和模式切换副作用请求数为 0；只显示当前锁提示 |
| A13b | 当前 A 保存响应迟到；测试 harness 注入旧 A 响应 | 迟到响应到达 | 旧响应不改变当前状态；若强制解除锁后仍需重新打开，必须生成新 session token | 旧 response 只记为过期；不能用 force 绕过 |
| A13c | 当前 A 为 `conflict`；准备磁盘新版本 D | 用户取消 reload、确认 reload 后让 GET 成功、让 GET 失败、再让旧 conflict 响应迟到 | 取消保持 conflict；成功 reload 采纳 D 的完整 baseline 并回到 idle；失败保持 conflict；旧响应不回写 | 未确认时无 GET；确认后只有一个新 session GET；迟到响应被丢弃 |
| A14 | 直接构造 `/api/save-editor-document` 请求 | 分别发送缺字段、旧 hash/revision、有效请求，并尝试加入前端 token 字段 | 错误码/成功字段保持既有合同；前端 token 不进入持久化语义 | 有效请求返回 `ok + editorDocument`；无效请求不替换文件；不改变已冻结批量合同 |
| A15 | 保存 POST 长期 pending；用户点击“停止等待/检查保存结果”；旧 POST 后续迟到成功、失败或冲突 | 进入 `pending-confirmation`；不自动 POST；按双投影和完整 path/hash/revision 判定为已确认提交、明确未提交、冲突或仍未知 | 确认前当前模型和旧 baseline 不变；匹配时整体采纳服务端 editorDocument | 只允许新 request token 的检查 GET；GET 失败继续 pending；旧 POST 不得解锁或覆盖 |

**确定性要求**：A2/A9/A10/A11 必须在测试中比较嵌套模型的深层内容，不能只比较对象引用；A1/A7/A8/A12/A13 必须断言所有可见状态而不只是正文；A3/A15 必须断言 POST 数量和是否发生自动重试；A5/A6/A11/A14/A15 必须断言文件字节、hash、revision 和临时文件残留。A13c 必须覆盖 conflict 下的切档、重开、切换模式、force 和 reloadFromDisk 的取消/成功/失败/迟到响应。若浏览器测试无法直接读取页面内状态，必须通过公开的测试 hook 或 DOM 状态面板观察，不得用 sleep 代替请求完成信号。

### 7.2 后续批次 1B：高级模式与危险操作

| 编号 | 用户操作/触发 | 可观察结果 | 必须保持的不变量 | 失败时行为 | 最小证据 |
| --- | --- | --- | --- | --- | --- |
| B1 | 高级模式输入非法 YAML/JSON/Schema 后保存 | 保存被拒，磁盘仍是原有效文件，临时文件被清理 | 任何目标替换前完成解析和校验 | 定位错误并保留编辑文本 | Core 文件 hash/临时文件测试 |
| B2 | 外部修改后高级保存 | 出现冲突，不覆盖外部内容 | hash/revision CAS 生效 | 可重新读取、保留本地或取消 | API 合同 + 文件对比测试 |
| B3 | 编译/导出/预览时有未保存修改 | 先保存；只有保存返回匹配 revision 后才发起操作 | 操作使用的版本可观察 | 保存失败、冲突或超时则操作请求为零 | 请求顺序、revision 和结果测试 |
| B4 | 切档、新建、刷新、模式切换或页面关闭时 dirty | 显示保存、放弃、取消 | 取消保持原文档和内存内容 | 保存失败不继续离开 | 浏览器生命周期测试 |
| B5 | 保存已提交但客户端超时或响应丢失 | 重试不会盲目覆盖；状态可通过回读/CAS 判断 | 内存、磁盘和基线最终可辨认 | 显示“结果待确认”，提供回读或另存 | 模拟丢响应集成测试 |
| B6 | 直接调用保存/操作 API | 请求、响应、错误码和写入语义保持合同 | 前端守卫不是唯一安全层 | 直接 API 也不能绕过写前校验和 CAS | Web 合同测试 |

### 7.3 后续批次 1C：临时草稿恢复

| 编号 | 用户操作/触发 | 可观察结果 | 必须保持的不变量 | 失败时行为 | 最小证据 |
| --- | --- | --- | --- | --- | --- |
| C1 | 浏览器/Web 重启后存在未保存草稿 | 打开档案时明确提示恢复或放弃 | 草稿按工作区和路径隔离 | 损坏草稿不影响正式档案 | 浏览器存储恢复测试 |
| C2 | 磁盘已变化但本地仍有草稿 | 不自动覆盖磁盘；用户可查看、放弃或重新读取 | 草稿基线可比较 | 显示冲突原因 | hash/基线测试 |
| C3 | 恢复草稿后再次保存 | 仍走正常 CAS、校验和人工保存流程 | 恢复不改变审核状态 | 保存失败保留恢复内容 | 端到端恢复测试 |
| C4 | 显式放弃草稿 | 只删除临时草稿，不影响正式档案 | 不误删 authoring 文件 | 显示已放弃 | 浏览器交互测试 |

## 8. 测试层次

### 单元测试

- 1A 编辑代次、请求代次和文档身份的提交/拒绝规则；
- 1A 保存中的 busy 状态、重复点击和保存失败分支；
- 1A 不可复用 session/request token 的提交/拒绝规则，覆盖 A→B→A；
- 1A 规范化作者模型 dirty 比较，覆盖改动后改回基线；
- 1A 旧响应不回写当前会话的正文、诊断、loading、错误和保存状态；
- 1A hash/revision CAS 判断和 `{editorProjection, sourceHash, revision}` 原子基线更新；
- 1A 长期 pending、运输层失败、用户中止和迟到响应的状态转换；
- 1A 用户状态文案映射。

1B/1C 的单元测试在各自计划冻结后追加，不提前把未实施功能算入 1A 证据。

### 集成测试

- 新建 → 编辑 → 作者模式保存 → 重读；
- 保存期间新输入 → 保存快照与最新内存代次分离；
- 保存期间重复点击 → 服务端只收到一个有效写入；
- 作者模式外部修改 → CAS 冲突，内存模型不变；
- 切档并发读取 → 旧响应隔离；
- 保存异常 → 内存模型、dirty 和保存基线不变；
- A→B→A → 旧读写响应不改变新会话；
- 保存期间编辑 → 服务端接收保存开始时的深复制快照，成功后基线元组完整更新；
- 保存期间编辑后失败/CAS 冲突 → 基线元组不部分更新；
- 最新读取失败或用户中止 → 旧成功响应不能恢复旧 UI 状态；
- 保存 A 时尝试切换 B → B 请求被锁阻止，A 的迟到保存响应不改变当前状态；
- 作者保存 API 请求、成功字段、错误码和前端控制字段合同回归；
- pending/中止/迟到成功 → 按完整回读判定为已确认提交、明确未提交、冲突或仍未知，不自动重试；明确 CAS 冲突直接进入 `conflict`。

1B 追加高级保存、操作顺序和 API 直接调用测试；1C 追加草稿恢复测试。

### 浏览器实测

- 1A 真实慢响应下连续输入和双击作者模式保存；
- 1A 960、760 像素宽度下保存状态和错误卡片不遮挡编辑区；
- 1A 切档读取响应乱序时当前档案不串线；
- 1B 执行页面关闭、刷新、切档和编译/导出/预览门禁；
- 1C 执行 Web 重启后的草稿恢复。

### 当前 1A 不执行

- 高级模式写前解析/Schema 校验和危险操作门禁（1B）；
- 本地草稿与页面关闭恢复（1C）；
- 真实 Bannerlord 入口、存档和长时回归；
- 真实云端 Provider 和本机 Worker 的性能或可靠性结论；
- 游戏目录、`dist`、PlayerExports 或冻结 AWAKE 候选同步；
- AI 批量长任务、参考资料建档和 Launcher 强制清理的行为修复。

## 9. 后续阶段路线

1A 通过后，再按独立计划推进，不把下列事项提前塞入当前实现批次：

1. **1B：高级模式与危险操作**——写前解析/Schema 校验、CAS 合同、dirty guard、编译/导出/预览顺序和页面离开保护。
2. **1C：本地临时草稿**——草稿格式、过期基线、恢复、显式丢弃和跨工作区隔离。
3. **后续 AI 会话与 Provider 可靠性**——会话恢复、测试连接、Provider/模型绑定、旧建议失效和人工确认提示。
4. **后续参考资料建档恢复性**——草稿 dirty 状态、证据绑定、采纳替换保护、创建日志和恢复。
5. **后续 AI 批量工作台状态机**——扫描失效、零事实、暂停/取消/重试、attempt/fence、批量恢复和逐项终态。
6. **后续文件、OneDrive、Launcher 和全局 UI 精进**——分别独立冻结，不在 1A/1B/1C 混改。

## 10. 交付门槛与停止条件

### 开始实现前

- `requirements-and-acceptance` 契约已写入本计划；
- `software-architecture-rfc` 边界选择已记录；
- 独立只读审查返回明确 `VERDICT: APPROVED`；
- 用户签收本计划；
- 建立本批精确文件备份和变更前 hash；
- 确认第一阶段没有与其他实现租约重叠。

### 实现过程中

- 每个子任务只有一个文件所有者；
- 发现需要改批量合同、AWAKE runtime、游戏目录或冻结候选时立即停止并另立计划；
- 遇到外部 Provider 429、取消、超时或结果不确定时记录 checkpoint，不自动重放；
- 不以“类存在”“JSON 可解析”或“编译通过”代替入口到可观察结果的验收。

### 1A 完成条件

- A1–A15 验收项均有对应证据；
- Core/Web/浏览器聚焦测试通过，既有回归没有被弱化；
- Release build 保持 `0 warnings / 0 errors`；
- 发布检查通过，R14 基线未被覆盖；
- 变更文件债务审计完成，确认无新增未接线路径或多重权威；
- 交付说明分开列出编译、测试、打包、Launcher、Provider/Worker、游戏内验证和未验证风险，并明确 1B/1C 尚未完成；
- 未达到游戏内证据时，最高证据只声明为 E2，不声明发布正典或运行时完成。

## 11. 审查与签收状态

```text
plan_status: COMPLETED_OFFLINE_VERIFIED
review_status: APPROVED
user_signoff_required: satisfied_by_current_user_request
primary_executor: root Codex
minimum_evidence: E2 offline behavior + package/release-check
code_change_authorized: true
game_sync_authorized: false
open_decisions:
  - 高级保存是否可以在保持现有路由名不变的情况下扩展请求字段（1B 未决，不阻塞 1A）
  - 本地草稿默认保留时长和单档案大小上限（1C 未决，不阻塞 1A）
  - 浏览器确认框是否使用原生 confirm 还是现有 Studio 对话框（1B/1C 未决，不阻塞 1A）
```

本文件已吸收独立审查的 `APPROVED` 结论，1A 已完成实现、回归、打包和离线审计；当前最高证据等级为 E2。1B/1C、游戏目录和真实运行时验证仍未完成，仍需独立计划与证据。
