# AWAKE 世界知识独立闭环 checkpoint

- `task_id`: `AWAKE-KNOWLEDGE-INDEPENDENT-20260828`
- `plan_revision`: `10`
- `checkpoint_date`: `2026-08-28`
- `status`: `offline_verified`
- `evidence_ceiling`: `E2`
- `game_started`: `false`
- `game_directory_synced`: `false`
- `frozen_candidate_modified`: `false`

## 交接字段

- `files_changed`: `AWAKE/src/AwakeRuntime.cs`、`WorldStateStore.cs`、`ProbeExtension.cs`、`AwakeBackgroundTask.cs`、`WorldEventContracts.cs`、`AwakeEventBehavior.cs`；本批 smoke harness；本批计划、验收矩阵、代码债务审查和 checkpoint。
- `verification`: `WorldbookRuntimeSmoke` Release-r16 构建 `0/0`、exit `0`；`WorldbookRuntimeProductionSmoke` Release-r16 构建 `0/0`、`15/15 PASS`；冻结候选与游戏目录 DLL SHA-256 均保持 `F03477F4B3F26B7D800526E3A3260D1593085BCDBDAECC8AD35691DBFDE0A5A7`。
- `known_limitations`: 正式 `AWAKE\tools\build.ps1` 仍因 Marcus Framework 缺少 `netstandard, Version=2.0.0.0` 引用失败；未验证真实 Marcus Storage、Provider/Worker、Bannerlord、E3/E4/E5。环境中仍有历史 smoke 进程 PID `24648`（Release）、`12008`（Release-r9）、`13104`（Release-r9-4），本批未结束。
- `next_action`: 新建独立的真实 v2 世界知识内容迁移/包候选批次；先解决正式构建环境，再考虑匹配 BuildId 的同步与游戏验证。
- `last_error`: `AWAKE` 正式 Rebuild 在 `framework\MarcusAwakeFramework\src\RuntimeServiceClient.cs` 等位置报 `CS0012`，要求引用 `netstandard, Version=2.0.0.0`；本批未修改 Marcus。

## 本批已完成

1. 固定 v2 测试包已加入 `tools/worldbook-runtime-smoke/fixtures/fixed-v2/`，包含 `manifest.json`、`runtime.json` 和 `index.json`。
2. smoke 已改为实际执行：
   `ReadAndVerify → LoadVerified → WorldKnowledgeQueryService.Query`。
3. 固定包覆盖平民、士兵和贵族的详细度/权限分层，并验证三层包哈希。
4. 新增验收矩阵：`docs/AWAKE-KNOWLEDGE-ACCEPTANCE-MATRIX-20260828.md`。
5. 计划已补齐 `reportId` 唯一存储边界、`applied/retryable` 状态、事件持久化失败、day 7→21 逐周补报和三种崩溃恢复语义。
6. Revision 9 生命周期、失败闭锁和写入栅栏修复已完成，独立只读复审返回 `VERDICT: APPROVED`。
7. `KnowledgeService` 的旧指纹写入已完成边界审查：当前没有生产调用者，属于未接线旧 RAG 缓存，不纳入现行战役 Store 清单。
8. 限定范围代码债务审查已完成，报告为 `docs/evidence/AWAKE-KNOWLEDGE-INDEPENDENT-CODE-DEBT-20260828.md`。

## 离线证据

命令：

```text
dotnet build C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-runtime-smoke\WorldbookRuntimeSmoke.csproj -c Release-r16 --no-restore
```

结果：Release-r16 构建成功，`0` warnings，`0` errors。

命令：

```text
C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-runtime-smoke\bin\Release-r16\net10.0\WorldbookRuntimeSmoke.exe
```

结果：

```text
PASS: runtime loader/query/overlay/identity/registry/weekly-report/event-ledger smoke
exit=0
```

生产边界命令：

```text
dotnet build C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-runtime-production-smoke\WorldbookRuntimeProductionSmoke.csproj -c Release-r16 --no-restore
C:\Users\26811\OneDrive\文档\New project\_houkai_merge\AWAKE\tools\worldbook-runtime-production-smoke\artifacts\bin\Release-r16\Awake.WorldbookRuntimeProductionSmoke.exe
```

结果：构建 `0` warnings、`0` errors；`15/15 PASS`，exit `0`。覆盖真实 AWAKE 源代码的 `SubModule`/`ProbeExtension` 生命周期、Store 替换、直接写入收尾、失败闭锁和后台任务清理。

正式构建复核：

```text
AWAKE\tools\build.ps1 -BannerlordApi 1.3.15 -Configuration Release
```

结果：失败于 Marcus Framework 编译缺少 `netstandard, Version=2.0.0.0` 引用。该阻塞属于当前 Framework 项目/构建环境；本批未修改 Marcus、未复制旧 DLL、未将其包装为 AWAKE 正式 Release 成功。

固定 fixture 文件 SHA-256：

| 文件 | SHA-256 |
|---|---|
| `tools/worldbook-runtime-smoke/fixtures/fixed-v2/manifest.json` | `CE07127F645F519BB24885CC3B5714454061369438DDB4A1D5395AA77F218C15` |
| `tools/worldbook-runtime-smoke/fixtures/fixed-v2/runtime.json` | `1BA1F9AD9AB037D404ADC4629C3565927A4DC381AF93F27604EBE08C092D5F7E` |
| `tools/worldbook-runtime-smoke/fixtures/fixed-v2/index.json` | `A27CDE30669BFA80E5DCFEBFF0BF9245AFF23662A164EEC03B63F223FD91C7D8` |

## 当前未完成

- 真实世界观 v2 内容包尚未生成或同步；当前 `ModuleData`、`dist` 和游戏目录未改动。
- `WorldbookRuntime` 的真实内容包目录初始化、`NpcDialogueService` 完整 AI 请求出口和真实 Marcus Storage 尚未在本批验证。
- 未进行真实 Provider/Worker、游戏目录同步、Bannerlord 启动、E3/E4/E5 或跨进程存档验证。

## 写集边界

本批已修改或验证：

- `AWAKE/src` 中本批世界知识/生命周期生产代码；
- `AWAKE/tools/worldbook-runtime-smoke/` 与 `AWAKE/tools/worldbook-runtime-production-smoke/` 下测试 harness；
- 本批计划、验收矩阵、代码债务审查和 checkpoint。

本批当前仍未修改：

- `AWAKE.csproj`、`SubModule.xml`、`ModuleData`、`dist`；
- Marcus `framework`；
- Bannerlord 游戏目录、冻结候选和发布同步目标。

Marcus P3D-A0 已在 `docs/checkpoints/MARCUS-AWAKE-P3D-A0-20260827-checkpoint.md` 明确释放本批 AWAKE 世界知识相关路径。本批离线生产源代码闭环已完成，但不得用 E2 结果替代真实 Marcus Storage、匹配 BuildId 的同步、游戏内 E3/E4 或存档 E5 证据。
