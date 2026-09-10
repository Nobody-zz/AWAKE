# Persona Workbench contract closure 修复计划

- 批次：`PERSONA-WORKBENCH-CONTRACT-CLOSURE-20260831`
- 状态：`needs_review`（最终收敛后等待独立终审；review-state：`docs/review-state/PERSONA-WORKBENCH-CONTRACT-CLOSURE-20260831.json`）
- 风险：`high-risk`
- 边界：不改 AWAKE Runtime BuildId；不触碰既有发布包、游戏目录或真实 Provider 凭据。

## 目标

1. 分离 Workbench local approval 与 AWAKE runtime approval。
2. 增加 `persona-workbench.approval-receipt.v1` 与 `persona-workbench.authoring-handoff.v1`，handoff 固定 `awakeApproval=not_requested`。
3. crosswalk、source vocabulary、tag registry 严格 fail-closed；`preserve_only` 只能进入 legacy data/report。
4. manifest 覆盖 authoring schema、crosswalk、canonicalization 和 tag registry；建立唯一 final evidence pointer。
5. K1A 失败证据保留为 `superseded`，不得与 PASS 混称最终证据。

## Round 1 修订

- 新增并实现 `persona-workbench.approval-receipt.v1` 与 `persona-workbench.authoring-handoff.v1` schema；receipt 必须绑定 source/authoring/crosswalk/registry revision、content hash、签发者、签发时间、expiry、canonical proof hash 和 evidence id。
- Workbench 的 `approved` 只能表示 local approval；handoff 固定 `awakeApproval=not_requested`，不得由 Workbench 写入或伪造 AWAKE runtime approval。
- crosswalk 的 source vocabulary、rows、代码默认词表和 tag registry 做精确闭包比对；未知值、缺行、多行、digest 漂移、实际 `unmapped/reject` 均 fail-closed，`preserve_only` 只能进入 legacy report。
- source/package manifest 必须覆盖实际参与编译或 handoff 的 schema、crosswalk、canonicalization、tag registry 和代码；任一输入变化使旧 receipt/handoff/package 失效。
- 建立唯一 final evidence pointer，显式记录 `PASS/FAIL/IN_DOUBT`、superseded 关系、BuildId、source/package/evidence hash；pointer 不能指向失败或未绑定证据。
- handoff/preview 路由必须绑定有效 session/authorization；未授权、过期、重放或旧 fence 请求只返回拒绝，不写入 AWAKE 或游戏目录。

## 验证

- 未批准 handoff、未知 vocabulary/tag、过期 receipt 和 manifest 缺项均拒绝。
- 新 package 使用新的 Persona Workbench BuildId；既有 AWAKE Runtime 与历史包不变。
- fake provider 覆盖重复提交、迟到响应、lease 过期和 evidence pointer 冲突。

## Round 1 Evidence Gate

- fixture 必须覆盖未批准 handoff、修改后旧 receipt、未知 tag/vocabulary、registry 漂移、preserve_only、重复 handoff、session 失效、迟到响应和 pointer 冲突。
- 旧 K1A 失败证据必须保留且被机器标记为 `superseded`；只有唯一 final pointer 指向的完整 BuildId/source/package/evidence 链才能称为当前结果。
- 本批不触碰 AWAKE Runtime、旧发布包、游戏目录或真实 Provider；Workbench package 仍需新的 `PWB-*` BuildId。

## Round 2 收敛项

- schema 固定落在 `contracts/persona-workbench.approval-receipt.v1.schema.json` 与 `contracts/persona-workbench.authoring-handoff.v1.schema.json`；模型、签发、验证和 Web route 必须分别可定位，不允许只在计划中声明。
- receipt/handoff 验证固定入口为 `tools/validate-authoring-handoff.ps1`，必须校验 source/authoring/crosswalk/registry hash、revision、issuer、expiry、canonical proof、session、fence、replay 和 `awakeApproval=not_requested`。
- source/package manifest 固定由 `tools/write-source-manifest.ps1` 收集 `src/**`、`docs/persona-contract/**`、authoring schema、crosswalk、canonicalization 和 `ModuleData/Worldbook/persona_definitions/tag_registry.json`；`tests/verify-source-package-binding.ps1` 必须对每项输入做 hash 闭包断言。
- crosswalk 行列闭包 fixture 固定落在 `tests/PersonaCrosswalkClosureTests.ps1`，包含未知/缺失/重复 vocabulary、tag registry digest drift、`preserve_only` 与实际 `unmapped/reject`；warning 不能产生 handoff。
- 唯一 final evidence pointer 固定为 `artifacts/CURRENT-evidence.json`，schema 必须允许 `PASS/FAIL/IN_DOUBT` 与 `superseded_by`；`tools/finalize-ai-link-evidence.ps1` 只能在完整链通过后原子更新，旧 K1A 记录标记 `superseded`。
- preview/handoff route 必须经过现有 session manager 的授权检查；未授权、过期、重放和旧 fence 请求只返回拒绝，不写入 AWAKE 或游戏目录。

## Round 3 收敛项

- 明确实现定位：receipt/handoff 必须各有 schema、C# model、issuer、validator、Web route 和测试入口；`approved` 状态不得直接序列化为 handoff，必须经过 receipt 校验后生成唯一 handoff，且 `awakeApproval` 固定为 `not_requested`。
- `tools/validate-authoring-handoff.ps1` 必须以 fail-closed 结果码校验 schema version、source/authoring/crosswalk/registry revision 与 hash、issuer、expiry、canonical proof、session authorization、fence、replay、handoff idempotency 和 `awakeApproval`；warning 不得生成 handoff。
- `tests/PersonaCrosswalkClosureTests.ps1` 必须逐项断言未知/缺失/重复 vocabulary、未知 tag、registry digest drift、实际 `unmapped/reject` 和 `preserve_only`；`preserve_only` 只能生成 legacy report，不得进入 approved handoff。
- `tools/write-source-manifest.ps1` 与 `tests/verify-source-package-binding.ps1` 必须把实际嵌入或消费的 schema、crosswalk、canonicalization、tag registry 和代码纳入同一 hash 闭包；任一输入变化必须使旧 receipt/handoff/package 失效。
- `artifacts/CURRENT-evidence.json` 必须只有一个当前指针；`tools/finalize-ai-link-evidence.ps1` 只能原子写入完整链通过的 `PASS`，并把旧 K1A 记录显式标为 `superseded`，记录 `superseded_by`、BuildId、source/package/evidence hash；`FAIL` 或 `IN_DOUBT` 不得成为当前指针。
- preview/handoff HTTP fixture 必须直接调用路由并覆盖 session 未授权、过期、重放、旧 fence、重复 handoff、迟到响应和 pointer conflict；每项拒绝都断言不写入 AWAKE、游戏目录或真实 Provider。

## 最终收敛项（Round 3 前）

- 权威输入根固定为 `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE`；`tools/write-source-manifest.ps1` 必须显式接收并规范化多个 input root，收集 `tools/persona-workbench/src/**`、`docs/persona-contract/**`、`tools/persona-workbench/contracts/**`、对应 crosswalk/canonicalization、`ModuleData/Worldbook/persona_definitions/tag_registry.json` 以及实际编译代码；不得因单一 `SourceRoot` 限制遗漏任何契约输入。
- receipt/handoff 的 issuer 固定为服务端生成的、与有效 session 绑定的 issuer record；fence 为该 session 的单调递增整数，服务端持久化已消费的 `receiptId`/`handoffId`。合法相同请求重试返回同一 handoff（幂等），相同 id 携带不同内容、旧 fence 或已消费的不同请求返回 replay/conflict 拒绝。
- C# `PersonaAuthoringV2` validator 是运行时唯一判定权威；`tools/validate-authoring-handoff.ps1` 只调用或复现同一契约并作为离线证据入口，不能形成第二套放宽规则；任何 vocabulary/tag warning 只要会影响闭包就令 handoff 失败。
- 旧 K1A evidence 文件保持字节不可变；新增 sidecar supersession record 记录旧 evidence hash、`superseded_by`、新 BuildId/source/package/evidence hash，`CURRENT-evidence.json` 只指向新 sidecar 的完整链；不得原地改写旧 schema 或旧文件 hash。
- `CURRENT-evidence.json` 更新必须携带 `expectedCurrentHash` 与 session fence，在持久化锁/CAS 下完成；冲突时保留新 evidence sidecar、旧 current pointer 不变并返回 conflict，禁止半写入或指向 `FAIL`/`IN_DOUBT`。
- HTTP fixture 固定使用现有 Web 测试项目扩展的 in-process TestHost；注入 fake clock、fake provider、isolated workspace、session manager、route factory 和 delayed-response executor，禁止启动真实 Workbench、读取真实凭据或写入 AWAKE/游戏目录。

## 实施门槛

先创建高风险 review-state，完成独立审查并再次取得用户签收；本计划签收前不写 Workbench contract 或发布代码。
