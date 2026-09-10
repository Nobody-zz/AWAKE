# Worldbook Studio authority gate 修复计划

- 批次：`WORLDBOOK-STUDIO-AUTHORITY-GATE-P0-20260831`
- 状态：`needs_review`（最终收敛后等待独立终审；review-state：`docs/review-state/WORLDBOOK-STUDIO-AUTHORITY-GATE-P0-20260831.json`）
- 风险：`high-risk`
- 边界：R14 与既有 authoring 文档保持只读历史，不自动迁移、删除或发布；不修改 AWAKE Runtime 和游戏目录。

## 实施顺序

1. 建立 durable `authoring-v1` sidecar：DocumentRevision、SelectionSnapshot、ApprovalProof、CompileProof、OperationJournal。
2. 旧 R14 mutation、compile、export route 在副作用前返回 `410`；不写盘、不调用 Provider。
3. compile/export 仅消费 immutable approved proof，重新核验 revision、content hash 和精确输入集合，禁止递归扫描 `authoring`。
4. staging artifact 与 release pointer 分离；P0 只生成 staging，发布指针切换另设明确 publish operation。
5. P1 durability（lease、pause、unknown result）仍作为后继；但 workspace head、commit marker、proof 提交和 operation journal 的崩溃/重启恢复属于本 P0，必须在同一提交闭环内可恢复，不得延期。

## Round 1 修订

- P0 不再把 authoring-v1 durability 作为仅有契约文字的后续项；必须先落地可恢复的 `DocumentRevisionRecord`、`SelectionSnapshot`、`ApprovalProof`、`CompileProof`、`OperationJournal`、workspace head/commit marker，并让真实 Web/CLI 入口读取它们。
- `/api/ai/batch/*`、`/api/compile`、`/api/export` 和所有 R14 mutation 路由必须在副作用前统一返回 `410`、`side_effect=none`；不写盘、不调用 Provider、不写 journal。
- compile 只接受 server-issued immutable `CompileProof`，重新核对每个 document ref、revision、content hash、引用闭包和 proof hash；禁止递归扫描 `authoring` 目录兜底。
- export 只生成 staging artifact；更新 `current.json` 必须由独立、持久化的 publish operation 完成，且 publish proof 与 staging manifest 一一绑定。
- `authoring-wire-contract.v1.json`、route registry 和 package/release-check 必须从 `draft` 进入实现绑定；R14 文件保留为只读历史，不再作为发布权威。

## 验证

- 未批准、已修改、引用不完整或伪造 proof 均拒绝且不产生输出。
- 旧 R14 路由均为 `410`，既有 current pointer 与历史包字节不变。
- fake executor 覆盖未提交、无响应、重启恢复、重复请求和 pointer 冲突。

## Round 1 Evidence Gate

- 每个旧路由都要有真实 HTTP/CLI fixture，断言状态码 `410`、零文件变化、零 Provider 调用和零 operation journal 变化。
- compile/export fixture 必须覆盖未批准、proof 伪造、revision/hash 变化、缺引用、重复 operation、staging 成功但 pointer 不变及 publish 冲突。
- 现有 R14/authoring/current pointer 字节哈希须在测试前后相同；任何迁移必须显式导入为 `needs_review` manual draft，不得自动升级。

## Round 2 收敛项

- 退役路由矩阵固定记录在 `contracts/authoring-v1-legacy-route-matrix.json`，逐项包含 HTTP method、route、CLI command、旧 R14 contract、side effect、预期 `410`；至少覆盖 `/api/ai/batch/*`、`/api/compile`、`/api/export`、document/draft save、AI apply/reject 及对应 CLI mutation。
- 保留路由也必须列明 method/route、是否写 durable authoring-v1、所需 session/lease/proof 和预期状态码；未列入矩阵的 mutation route 默认 fail-closed。
- 可执行 fixture 固定落在 `tests/AuthorityGateHttpTests.cs`、`tests/AuthorityGateRecoveryTests.cs`、`tests/AuthorityGateCompileProofTests.cs`，由 `scripts/test.ps1 -Suite AuthorityGate` 调用；fake executor 必须可注入提交成功、无响应、重启、重复 operation 和 pointer conflict。
- package/release-check 必须强制执行上述 suite、验证三份 authoring-v1 contract 状态不是 draft，并报告 `authority_gate_passed`；单独静态 JSON 解析不得计 P0。

## Round 3 收敛项

- `contracts/authoring-v1-legacy-route-matrix.json` 必须是冻结、可枚举的完整矩阵；每行包含 `method`、`route`、`cli_command`、`legacy_contract`、`mutation_class`、`side_effect`、`expected_status`，并覆盖 `Program.cs`、`AuthoringDraftEndpoints.cs`、`BatchEndpoints.cs` 及 CLI 中发现的每个旧 mutation；`/api/ai/batch/*` 必须展开为每个具体 method+route，不能只保留 wildcard；矩阵外的 mutation 默认返回 `410` 和 `side_effect=none`。
- `tests/AuthorityGateHttpTests.cs` 必须纳入现有测试项目或由新建的专用 test project 以 `ProjectReference` 承载，并按矩阵逐行调用真实 HTTP/CLI 入口，断言 `410`、文件快照不变、Provider 调用数为零、operation journal 不新增；保留路由必须断言 session/lease/proof 要求和 durable record 变化。
- `tests/AuthorityGateRecoveryTests.cs` 明确 fake executor 注入点及崩溃阶段：workspace head、commit marker、journal、approval proof、compile proof 的每个提交点都必须能在重启后恢复或 fail-closed，且不得生成半成品 pointer。
- `tests/AuthorityGateCompileProofTests.cs` 必须逐项验证 document ref、revision、content hash、引用闭包、proof hash、重复 operation、staging 成功而 pointer 不变和 publish conflict；不得通过递归扫描 authoring 目录兜底。
- `scripts/test.ps1 -Suite AuthorityGate` 与 `release-check.ps1` 必须实际执行上述三组测试并输出 machine-readable `authority_gate_passed`；仅存在文件、编译通过或 JSON 可解析不得计通过。
- `scripts/test.ps1` 必须实际声明并解析 `-Suite AuthorityGate`，调用承载上述 fixture 的 test project；`release-check.ps1` 必须显式传入该 suite 并拒绝缺失 `authority_gate_passed` 的报告。
- 三份 `authoring-v1` contract、legacy route matrix 和 action catalog 在 package/release-check 前必须从 `draft` 转为可执行绑定状态；release gate 必须逐份检查状态、schema hash 和 route matrix hash。
- `CompileProof` 必须在计划中绑定到真实 Web `/api/compile`、`/api/export` 和 CLI `compile`/`export` 参数与服务调用；fixture 不得只测试孤立 validator。

## 实施门槛

先创建高风险 review-state，完成最多三轮独立审查并再次取得用户签收；签收前不修改 Studio contract/loader/package 代码。
