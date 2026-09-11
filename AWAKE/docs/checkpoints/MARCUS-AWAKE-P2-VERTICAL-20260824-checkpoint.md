# Marcus-Awake P2 Framework Core Vertical checkpoint

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-P2-VERTICAL-20260824`
- `status`: `offline_verified`

## files_changed

- 预审查阶段修正了 P1.5 文档状态/批准说明并固定其 hash；P2 增量写集已于 Round 8 独立审查通过并完成实现。
- P2 已实现并验证的写集：
  - `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/FrameworkBootstrap.cs`
  - `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/TestAccess.cs`
  - `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/FrameworkIdentity.cs`
  - `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/HostApi.cs`
  - `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/RequestContext.cs`
  - `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/SessionApi.cs`
  - `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/src/IpcAndDiagnosticsApi.cs`
  - `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/tests/FrameworkCoreVerticalSmoke.cs`
  - `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/tests/Program.cs`
  - `_houkai_merge/AWAKE/framework/MarcusAwakeFramework/tests/MarcusAwakeFramework.Tests.csproj`
  - `_houkai_merge/AWAKE/docs/evidence/MARCUS-AWAKE-FRAMEWORK-CORE-VERTICAL-SMOKE-E1-20260824.json`
  - `_houkai_merge/AWAKE/docs/AWAKE-CURRENT.md`（仅在 P2 完成后更新当前状态）

## verification

- P1 Framework Core DLL 与测试 EXE 哈希已按 P1 checkpoint/evidence 重新核对；P2 API major 已统一为 `2.0`。
- P2 当前写集已通过独立增量只读审查 `VERDICT: APPROVED` 并完成实现；Core build 为 `0 warnings / 0 errors`，P2 测试工程为 `0 warnings / 0 errors`，纵向 fixture 为 `FCORE-SUMMARY PASS 20`，既有 Core 合同测试为 `PASS ALL: 8 Framework Core contract tests`。
- generation fence、线程安全状态机、pending-operation drain barrier、typed diagnostics、Bootstrap 补偿回滚、并发/迟到结果矩阵均有离线断言；E1 evidence 已按固定文件名生成并通过 JSON/源文件哈希校验。
- P1.5 契约锚点已统一为 `P1.5_CONTRACT_LOCKED`；实现前重算 SHA-256 为 `DB45A9C4C130BC727394B61AFD9836508045F83B66BA7F23C0BF3686D596D619`，与锁定值匹配。

## known_limitations

- 当前 AWAKE 仍引用外部 `MarcusAIFramework`，P2 不改变这一事实；P2 只完成独立内置 Framework Core 离线闭环。
- P2 只覆盖 Framework Core 离线纵向链路；真实 Bannerlord SubModule、Runtime Service、IPC、Provider、MCM、E4/E5 属于后续阶段。
- 第一轮独立只读审查返回 `VERDICT: REVISE`；已修订 generation、diagnostics、fixture 命名、测试编译入口、回滚和证据写集。后续审查传输曾遇到上游 `502 Bad Gateway`（上游 `503` 重试耗尽），没有把失败当作批准；Round 8 已对新增 `FrameworkIdentity.cs` 写集完成独立复审并批准。随后补齐 20-case fixture 并通过离线验证。

## next_action

- 建立 P3 Runtime Service / Provider / IPC / SQLite-RAG 的独立计划与审查边界；在 P3 获批前不修改 AWAKE 主工程、SubModule 或游戏目录。

## last_error

- 第一轮审查发现：RequestContext 缺少 generation、Diagnostics 固定 receipt generation 为 0、测试 fixture 未被 csproj 编译、Bootstrap 访问边界未锁定、写集漏列 AWAKE-CURRENT、模拟生命周期命名可能误导。已纳入上一版修订。
- 第二轮审查未得到模型 verdict：代理子任务返回 `502 Bad Gateway` / upstream `503` after retries exhausted；本轮独立 CLI 入口也未能启动审查并被本地执行策略拦截；未修改代码、未产生批准结论。
- 最新完整审查发现：Probe 必须返回 typed failure；RequestContext 必须绑定 generation；SessionCoordinator/Lease 必须线程安全；drain 必须有 pending barrier；Bootstrap 必须有可验证补偿回滚；Host/Locator/Session 顺序必须唯一；fixture 必须验证迟到结果；evidence 必须分层并固定生成时机。已纳入 P2 计划并由 fixture/evidence 验证。
- 新一轮审查补充：P1.5 契约必须绑定不可变 hash；operation handle 必须防双重释放并定义完成竞态；锁边界和取消回调必须明确；P2 的 public breaking change 需升级 API major、列出迁移差异；P1.5 规定的 E1 文件名、错误码和 `DrainTaskId` 必须与计划统一。已纳入当前 P2 计划并通过 Round 8 增量复审。
