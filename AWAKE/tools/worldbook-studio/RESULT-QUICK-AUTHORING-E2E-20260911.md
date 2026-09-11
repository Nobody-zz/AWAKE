# Quick Authoring 端到端打通结果（2026-09-11）

本文件记录「简单提示词 + 参考资料 → 完整 authoring candidate → 建档 → 编辑器读回」这条
Quick Authoring 链路第一次在本机 Worker（Ollama qwen2.5）上跑通的实测结果，以及这一轮修掉的缺陷。

## 一、结论

- 本地真实模型（`qwen2.5:latest`）从 prepare 到 create-document **全链路跑通**，产出可读回的 `needs_review` 档案。
- 档案正文来自服务端注入的冻结事实：4 条 assertion 全部在编辑器模型里读回。
- 参考资料 6/6 关键信息点覆盖，4/4 引文可在原文逐字定位且 `quote_hash` 匹配。
- 全程未访问云端 Provider、未启动游戏、未同步游戏目录。

## 二、这一轮修掉的缺陷

| # | 缺陷 | 影响 | 修法 |
|---|---|---|---|
| 1 | `ApplyFrozenTopLevelGraph` 把临时数组里的 JsonNode 直接 Add 进结果数组 | 原生异常 `The node already has a parent.` 泄漏给用户，Pass B 必然 400 | 搬移前 `DeepClone()`，并注明 JsonNode 单父节点约束 |
| 2 | `ValidateStrictReviewStatuses` 要求 review 状态字段必须写全 | 默认值缺写把整轮生成判死（模型只写 `{"perspective": ...}` 空壳表达时） | 缺失按 `pending` 处理；写出非 `pending` 仍然阻断 |
| 3 | Pass B 没有专属的输出形状覆盖规则 | 模型照共用骨架写出 11 条 claim、11 个 span、11 条命题：一次生成 17 分钟、7900 输出 token，且抄出空壳 expressions | 新增 `QuickAuthoringPassBSchemaOverride`，并放在提示词最后（最高优先级）：候选只允许 7 个字段，语义图与事实留空由服务端注入 |
| 4 | Pass B 不注入候选事实 | 建档出来的档案**没有正文**（正文由 `candidate.facts` 生成 assertion） | 按 cluster 命题/claim/span 的来源并集从冻结事实里注入，取不到时退化为整包事实 |
| 5 | `ValidateDocumentMetadata` 把空列表写成 `related_domains: []` 再判非法 | UI 每次都传 `relatedDomains: []`，用户点「建档」必失败，且错误与他无关 | 与落盘模板口径统一：空列表等于「没有相关分类」 |

提示词版本从 `prompt.v10` 升到 `prompt.v11`。

## 三、实测数据（v12a，最佳一轮）

| 指标 | 数值 |
|---|---|
| 候选数 | 1 |
| 资料覆盖 | 6/6 |
| 引文定位 | 4/4 可定位、4/4 哈希一致 |
| Pass A | 261 秒 / 2437 输出 token |
| Pass B | 200 秒 / 1627 输出 token（修 prompt 前为 1061 秒 / 7896 token） |
| 档案 | `authoring/geography/entry-*.yaml`，status=`needs_review`，正文 4 条 |
| review-projection / review-decision | 200 / 200 |

## 四、如何复跑

```powershell
Set-Location 'C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-studio'
dotnet build Awake.WorldbookStudio.slnx --configuration Release
pwsh -NoProfile -File .\scripts\test.ps1        # 全量离线测试
pwsh -NoProfile -File .\scripts\package.ps1     # 打包 + TEST/CONTRACT 检查
pwsh -NoProfile -File .\_tmp\real-worker-pravend.ps1 -UseStudioInstructions 1 `
  -Mode quick_authoring -WorkerTimeoutSeconds 1800 -NumCtx 32768 -NumPredict 8192 `
  -EvidencePath '_tmp\quick-authoring-e2e-<ver>-evidence.json'
```

打包产物：`artifacts\current-test\WorldbookStudio`（含 `web\Awake.WorldbookStudio.Web.exe` 与 `schemas\`）。

`-TrackPhases` 会在子进程里轮询 `GET /api/ai/authoring/draft/attempts/{draftId}/{attemptId}`，
把真实阶段写进 evidence 的 `observed_phases`（该端点要求 `Origin` + `X-AWAKE-CSRF` + 会话 Cookie 三件套）。

## 六、第二条修复线（进度/取消、词条数量、正文改写）

在这一版之后又按用户反馈修了三批问题，均以真实 Worker 复跑验收：

| 批次 | 问题 | 修法 | 关键文件 |
|---|---|---|---|
| 1 | 生成长时间无反馈，也不能中断 | attempt 增加 `phase`；`single`/`pass_a`/`pass_b` 三处上报；UI 增加阶段标签 + 计时 + 「取消生成」，1.5s 轮询 attempt 状态，取消会 abort 请求并清 token | `AuthoringDraftStore.cs`、`QuickAuthoringOrchestrator.cs`、`studio-draft.js`、`studio-draft.css` |
| 2 | 生成几条词条由模型决定 | `AuthoringDraftIntent.CandidateMode`（`auto`/`single`），进入 request hash 与提示词；`single` 时服务端把模型给出的多个 cluster 合并成一条并留下可见 warning；UI 增加「词条数量」下拉 | `AuthoringDraftContracts.cs`、`AuthoringDraftPromptCatalog.cs`、`AuthoringDraftResponseNormalizer.cs` |
| 3 | 正文是逐句搬运，不是归纳 | 契约明确 `facts[].text` 可改写、`evidence.quote` 必须逐字；正文若新增年份/正式 ID 一律阻断；正文与原文逐字相同时给作者可见 warning；引用被模型截断并补句号时按尾部标点容错定位；模型指明来源句内部的引用吸附为服务端原文整句 | `AuthoringDraftPromptCatalog.cs`（`prompt.v12`）、`AuthoringDraftResponseNormalizer.cs`、`SourceEvidenceMatcher.cs` |

### 实测数据（v16，含阶段追踪）

| 指标 | 数值 |
|---|---|
| 阶段序列 | `phases=pass_a,pass_b status=succeeded` |
| attempt 终态 | `succeeded` / `pass_b` |
| 候选数 | 1（`candidate_mode=single`） |
| 资料覆盖 | 6/6 |
| 引文定位 | 4/4 可定位、4/4 哈希一致 |
| 正文改写 | 4 条中 2 条已归纳复述、2 条仍与原文逐字相同（触发「未做归纳改写」warning） |
| 档案 | status=`needs_review`，未自动进入 approved/canon/published |
| review-projection / review-decision | 200 / 200 |
| Ollama 调用 | 2 次；eval 4074 token / prompt 13748 token |

### 测试覆盖

- `Draft.Tests` 106/106，其中新增：attempt 阶段上报（含跨 attempt 不串台）、候选数量进入 request hash、`single` 模式提示词契约、`single` 合并 cluster、截断引用补句号仍可定位、正文可复述而引用保持逐字、复述新增年份必须阻断、逐字抄写必须告警。
- 前端 harness 新增 3 项（进度阶段 + 取消入口、取消 abort/清 token、词条数量随新建草稿重置），`draft DOM/state` 8/8。
- `scripts/test.ps1` 全量通过（含 Workstation 120/120、BatchTests 23/23、editor content 11/11、HTTP smoke）。

### 本轮新增仍需注意

- 「取消生成」是客户端 abort，服务端据此取消进行中的 Provider 请求（`CancellationToken` 已贯通到 provider），并把 attempt 落成可重试的失败态；但不会回滚已经产生 token 的模型算力。
- 逐字搬运的判断只比较正文与其自身证据引文，改写到「近似但不同」仍可能只是换词，仍需人工复核。

## 五、仍需注意

- 本次只验证了 4 句样本。更长的参考资料会考验 Pass A 的候选边界划分（历史上出现过一句话拆成多个候选的过度分割）。
- 多候选场景（同一资料拆成多份档案）在本轮没有实测；当前样本模型给的是单候选，等于走了最顺的路径。
- 浏览器层面的视觉与交互只由 DOM/state harness 覆盖，未做人工浏览器走查。
- 模型仍会多写被服务端覆盖的顶层 claims/target_spans（本轮 4 条），不影响结果，但仍是 token 浪费。
