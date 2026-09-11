# PersonaWorkbench × AWAKE 联合目标证据矩阵

> 当前状态：`E2 / G3-S0 pending / G3-A blocked / G3-B blocked / G4 not_attempted`
>
> 这份矩阵是交接与验收索引，不替代 Contract-Lock、联合计划、G3 执行门禁或实际报告。任何“已完成”只表示对应行的证据已达到该行要求，不表示整条游戏内链路完成。

## 1. 目标到证据

| 目标要求 | 权威设计/实现来源 | 当前证据 | 状态 | 尚缺条件 |
|---|---|---|---|---|
| 共享核心：schema、canonical JSON、digest、revision、selection、crosswalk | `docs/PLAN-PersonaWorkbench-AWAKE-Joint-CONTRACT-LOCK-20260823.md`、`docs/persona-contract/*` | Contract verifier `pass/0`；正负 handoff fixtures | 已达 E2 | 需要两端真实实现互验后才能进入运行时发布 |
| Workbench 先行验证层与 AWAKE approval 分离 | Contract-Lock 状态机、Workbench source/selection fixtures | `PWB-AWAKE-001` `pass/0`；draft、stale、unknown、loss、schema skew、缺 approval 等负向夹具按预期 reject | 已达 E2 | 需要后续真实 Workbench export 与 AWAKE approve 入口 |
| Workbench → authoring-v2 → export-v1 → definition-v1 | `tools/persona-awake-joint/*`、`docs/fixtures/persona-awake-joint/*` | `PWB-AWAKE-001–009`、`014–017`；ledger `pass/0` | 已达 E2 | 不得把 fixture 结果当成生产 runtime 接线 |
| RuntimeBundle、ContextSnapshot、唯一 Persona facade | G3 execution gate、architecture matrix | runtime-static `reject/10`；当前未发现 `PersonaRuntimeProvider.BuildProjection`、`ContextSnapshot`、`RuntimeBundle` | 未完成 | G3-S0 readiness、Native exact-scope approval、唯一 active lease |
| Knowledge、Persona、Prompt 使用同一快照 | Architecture matrix §4、G3-A 验收 | 当前只有设计要求，无生产调用证据 | 未验证 | G3-A `PWB-AWAKE-010/011` 正向 evidence |
| 实时状态变化触发正确 invalidation | G3-A dynamic invalidation 规范、fingerprint 规则 | 当前 fingerprint/cache/Overlay 闭环静态检查失败 | 未完成 | ContextModes、bundle/digest、Overlay→Persona invalidation 实现与 focused test |
| reload 失败保留 last-known-good | Contract-Lock fallback/reload 规则、fixture `008` | `PWB-AWAKE-008` 离线夹具 `pass/0` | 仅适配器 E2 | 需要 runtime `RuntimeBundle` 原子切换和真实 caller 证据 |
| G3-S0 Storage readiness 与世界书兼容边界 | G3-S0 scope manifest、G3 执行门禁、`AwakeStorageContract`/`AwakeTerminalBehavior` | scope verifier 新增 detached authority、scope hash、三 schema 映射、Worldbook SyncData 双边界断言；当前 `blocked/20` | 门禁已修订，源码未实现 | 独立 `APPROVED`、active lease、G3-S0-001..006 focused runtime evidence |
| Persona continuity/override/recovery 存档 | `PersonaPersistenceModels.cs`、G3-B 计划 | DTO/model 存在；`PWB-AWAKE-012` 正确 `blocked/20` | 未完成 | G3-S0 后的 Storage owner、namespace、save/load/branch/restart 正向 evidence |
| 游戏内角色卡实际进入 prompt | G4 验收表、匹配 BuildId 日志 | 当前没有候选同步、游戏日志或 prompt 消费证据 | 未开始 | 新 BuildId、E3 同步、用户运行游戏并提供日志 |
| 长期存档/重启连续性 | G3-B/G4/E5 验收 | 当前没有读档、重启或 E5 证据 | 未开始 | G3-B 正向实现后再做 E5 |

## 2. 当前真实入口与缺口

- 生产 Persona 入口仍是 `_houkai_merge/AWAKE/src/NpcDialogueService.cs:1014-1019` 的 `WorldbookRuntime.Current` → `WorldbookService.BuildPersona`；
- `WorldbookRuntime.Reload()` 仍是先清空再加载，尚未满足 atomic last-known-good；
- `PersonaPersistenceModels.cs` 是模型雏形，不是已接线的 Storage owner；
- 当前 Native 报告为 `blocked/20`：checkpoint 是 B1 Knowledge 批次，`executionLease=none`，`storageReady=false`；
- `PWB-AWAKE-012` 的负向通过只证明“缺 lease 时不会误写”，不证明 persistence 能力存在。
- G3-S0 当前只完成门禁契约修订：detached approval/lease record、原始 scope hash、三个 typed schema 映射、Worldbook SyncData 分界和原子 readiness 验收已锁定；尚未写入 `AWAKE/src`。
- `verify-e2-matrix.ps1` 当前聚合 20 个 E2 fixture：`pass/0`、20/20 policy-matched，其中 `PWB-AWAKE-013` 明确记录 1 个 `protected_baseline_drift` warning；未修改冻结期望。

## 3. 执行顺序

```text
G3-S0 Storage readiness contract
  -> G3-A Native Persona runtime projection
  -> G3-B Persona continuity/override persistence
  -> G3-C Offline integrated contract
  -> G4 Game evidence
```

每批必须独立取得 exact-scope approval、唯一 active lease、disjoint write set 和对应证据；前一批 lease 关闭后才能进入下一批。`verify-g3-plan.ps1` 的 `pass/0` 只证明计划内部一致，不授予源码写权限。

## 4. 当前放行结论

当前允许继续：

- 联合契约、fixture、报告和门禁工具的隔离维护；
- G3-S0/G3-A/G3-B 的精确 scope 审计与审查准备；
- E2 离线回归和报告一致性验证。

当前禁止：

- 修改 `_houkai_merge/AWAKE/src`；
- 修改 `ModuleData`、`dist`、游戏目录、PlayerExports 或 frozen candidate；
- 编译、同步、启动 Bannerlord或宣称 E3–E5；
- 把 B1 Knowledge approval、旧 BuildId 或静态存在性当成 Persona runtime 完成证据。
