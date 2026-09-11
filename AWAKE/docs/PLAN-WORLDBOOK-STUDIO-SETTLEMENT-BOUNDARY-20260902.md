# Worldbook Studio 结算幂等与边界硬化落地计划

- 批次：`WORLDBOOK-STUDIO-SETTLEMENT-BOUNDARY-20260902`
- 日期：2026-09-02
- 风险：`high-risk`
- 前置审计：`WORLDBOOK-STUDIO-ARCHITECTURE-DEBT-AUDIT-20260901`
- 目标：让客户编译在丢响应、重复提交、进程重启和非法输入场景下有确定结果；同时不再向 HTTP 客户端泄露 authority 内部路径/状态细节。

## 已确认事实

1. `AuthorityGateService.CompileApproved` 是客户编译唯一入口，但当前没有持久 compile operation；重复请求会重新校验、重新编译并可能再次替换 compiled 输出。
2. `ExportStaging` 已有 operation journal，因此 compile 与 export 的幂等/恢复语义不一致。
3. `SafeId` 只替换 `/`、`\\`、`..`，没有严格字符、长度、Windows 保留名或碰撞契约。
4. `AuthorityFailure` 当前将 `InvalidOperationException.Message` 原样写入响应；编辑/作者保存错误已有安全消息映射。
5. 当前批准批次 `WORLDBOOK-STUDIO-APPROVAL-COMPILE-CLOSURE-CONTRACT-20260901` 已批准，不重新打开；本批只增加其下游结算与安全边界，不改变显式 approval/CompileProof 目标。

## 设计决策

### 结算操作

采用“proof 派生、服务端拥有”的 compile operation：`customer.compile.<compile_proof_id>`。

- 同一 proof、同一 normalized output、同一 confirmation token：返回原 compiled artifact 与原 CompileResult，不重复编译。
- 同一 operation ID 但 output/token 改变：固定 `409`，不生成第二个 artifact。
- operation 在 `prepared`/`failed_recovery`：固定 `409`，返回“上次编译结果待确认/已 fail-closed”，不盲目重放。
- operation 记录 `request_digest`、proof hash、output、confirmation token hash、target、manifest hash、编译结果摘要和时间戳。
- 先落 prepared operation，再执行编译；成功后落 committed marker 与 operation。进程重启时，未有 committed marker 的 prepared compile 一律 fail-closed；有完整 marker 且目标 manifest hash 匹配时恢复 committed。
- 通过现有 operation 查询路由暴露状态；不新增第二条编译 authority path。

### 输入边界

- `RequireId` 统一要求 Unicode NFC 后长度 `1..128`，字符仅允许 ASCII `A-Z a-z 0-9 . _ -`；拒绝空格、控制字符、路径分隔符、冒号、通配符、保留名和规范化后碰撞。
- 所有进入 authority 文件名的 ID 先经同一验证；错误固定带 authority code，不把底层 `ArgumentException` 暴露给客户。
- `SafeId` 不再承担“清洗并继续”的语义；非法输入直接 fail-closed。

### HTTP 错误

- `AuthorityFailure` 仅返回稳定 `error`、安全 `message`、`side_effect=none` 和 server-generated `correlation_id`。
- 具体异常仅写本地日志/诊断，不进入 wire response；本批不引入外部遥测服务。
- 已有 authority error code 与状态码保持兼容；未知 authority 异常统一 `400` 或 `500` 的安全默认，不泄露路径。

## 唯一闭环

```text
customer compile request
→ derive server-owned compile operation
→ validate CompileProof
→ prepared operation persisted
→ CompileExact + WriteCompiled
→ committed marker/operation
→ compiled path + manifest/result observable
→ same request replay or deterministic conflict
```

## 验收标准

- 有效 proof 首次 customer compile 写出 compiled artifact、operation record 和 commit marker。
- 相同 proof/output/token 第二次调用不增加 compiled artifact，不改变 manifest hash，不重复写 operation journal，并返回原结果。
- 相同 proof 改 output 或 confirmation token 返回 `WB-AUTHORITY-OPERATION-409`，文件快照不变。
- prepared operation 无 marker 时初始化后进入 `failed_recovery`，后续调用返回确定性 `409`；完整 marker/manifest 匹配时恢复 committed。
- invalid/missing/非法 ID 不触发输出、journal、Provider 或目录越界副作用。
- authority HTTP 错误响应不包含 workspace root、绝对路径、临时路径、proof 内容或底层异常原文；含 correlation ID。
- 现有 AuthorityGate、customer closure、HTTP smoke、Studio harness 继续通过；旧路由仍 `410`。
- 不启动 Bannerlord、不写游戏目录、不调用真实 Provider、不读取真实 API Key。

## 明确不做

- 不在本批统一 Draft 与 Batch 领域模型。
- 不在本批重写切片算法、增加合并/拆分 UX 或实现真实浏览器 E2E。
- 不改变 PublishStaging 的发布指针语义。
- 不删除 `Commit`/`CommitArtifactOperation` 任何一个；统一提交器留到 characterization tests 之后。
- 不提版本、不同步游戏目录、不修改冻结候选。

## 文件范围

- Core：`AuthorityGate.cs`，必要时新增 compile operation contract/helper。
- Web：`Program.cs`，必要时新增本地 correlation/log helper。
- Tests：`AuthorityGate` 专项、HTTP/closure fixture；新增幂等、恢复、输入边界、错误不泄露断言。
- Docs：本计划、review-state、完成 checkpoint。

## 验证顺序

1. 独立只读审查本计划与相关符号。
2. 用户签收后，先写失败回归测试，再做最小实现。
3. 运行 AuthorityGate、HTTP/customer closure、完整 Studio harness、Release build。
4. 重新打包并验证源码/包内 web 资产、manifest、operation contract 一致。
5. 更新 checkpoint，随后再开 AI authoring domain/segmentation UX 批次。

## 当前门槛

- `plan_status`: `ready_for_review`
- `review_state`: `C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\docs\review-state\WORLDBOOK-STUDIO-SETTLEMENT-BOUNDARY-20260902.json`
- `user_signoff_required`: `true`
- `minimum_evidence`: compile replay/conflict, prepared recovery, invalid-ID zero side effect, safe HTTP error, existing regression suite, package/release check。
