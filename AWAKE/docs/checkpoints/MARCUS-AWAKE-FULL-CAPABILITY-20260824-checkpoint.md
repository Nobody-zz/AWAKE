# Marcus-Awake 全功能继承 checkpoint

- 日期：2026-08-24
- 任务：`MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- 状态：`offline_verified`

## 已完成

- 已读取 Marcus 参考设计大纲 00–18、实现状态、SDK/AuthorSource 和当前 AWAKE 交接资料。
- 已建立 `MARCUS-AWAKE-FULL-CAPABILITY-INVENTORY-v1-20260824.md`，覆盖 F-001–F-066。
- 已将 Host、Capability、GameData、RAG、Gateway、六类 Provider、流式/取消、Structured Output、Events、Storage、Timeline、Command、IPC、凭据、媒体/CAS、MCM、DevTools、SDK、观测、双版本、迁移和四个参考扩展全部纳入考虑。
- 已将继承矩阵升级为 v2，并把“功能不进入 Framework Core”与“功能不考虑”明确区分。
- 已更新 `PLAN-MARCUS-AWAKE-EMBEDDED-20260824.md`，新增全功能清单阶段和 API/Protocol/Storage/Failure contract lock。
- 已建立并修订 `MARCUS-AWAKE-P1.5-CONTRACT-LOCK-DRAFT-20260824.md`，补齐 Bootstrap 信任根、canonical Pipe ACL、全局 sequence/epoch/fence、IdempotencyScope/SettlementReceipt、CredentialBroker、FieldClassificationRegistry/EgressBroker、Native Save anchor 权威矩阵、Save barrier、MigrationManifest、Service 生命周期和 F-063–F-066 唯一 deferred 门。
- 已在继承矩阵新增 F-001–F-066 覆盖索引，并修正“27 个源码依赖文件 + 2 个项目入口文件”的迁移验收口径。
- 已新增 `MARCUS-AWAKE-CAPABILITY-OWNERSHIP-MAP-v1-20260824.md`，逐项绑定 F-001–F-066 的 owner、artifact、fixture、phase 和状态；覆盖检查为 66/66。
- 已明确 Runtime Service 独占 SQLite/FTS5、Embedding、Rerank 和物理 RAG DB；Framework Core 只提供 API/IPC/权限/预算；AWAKE 负责世界书语义与知识权限。
- 已固定 `FrameworkCoreVerticalSmokeFixture`、Framework Core Migration Baseline、AWAKE Gameplay Baseline、Full Capability Completion 三种验收口径。
- 已将 F-064 关系只读投影、F-066 代码事实观察定义为 `partial`，并为 DeferredCapabilityStatus/DeferredResult 增加 partial 子能力字段。
- 已写死三层权限顺序和 P0–P7 阶段退出门；fixture、artifact、owner 或 evidence 缺失时禁止转阶段。

## 当前权威边界

- `Awake.dll`：AWAKE 玩法、世界书、NPC、关系、事件、MCM 和存档入口。
- `MarcusAwakeFramework.dll`：通用游戏内框架契约和 IPC client。
- Marcus-Awake AI Runtime Service：Provider、Gateway、Prompt、Structured Output、RAG、Storage、Timeline、CAS、Media 和 IPC server。
- Marcus-Awake DevTools：作者维护、诊断、测试和脱敏导出。
- Marcus-Awake SDK：第三方开发资源，不进入玩家包。
- 四个 Marcus 参考扩展：全部功能作为 AWAKE 适配验收场景，不原样并入 Framework Core。

## 当前阻塞

- 第六、七轮本机 P1.5 独立只读审查仍为 `VERDICT: REVISE`；第九轮确认 6 项字段级冲突，第十轮确认 4 项跨文档一致性缺口；最终复审已确认全部闭合并返回 `VERDICT: APPROVED`。
- 第八轮三路独立只读复审因上游 `429 Too Many Requests` 未返回正文或 verdict；用户于 2026-08-24 发送“批准”后恢复复审，最终复审返回 `VERDICT: APPROVED`。用户批准信号按本批次签收记录。
- 尚未修改 `AWAKE.csproj`、`SubModule.xml` 或运行时代码。
- 尚未构建、同步、启动 Bannerlord 或改变冻结候选。

## 下一步唯一建议

P1.5 已取得 `VERDICT: APPROVED` 且完成用户签收；P1 Framework Core 独立写集已完成离线验证。当前不启动游戏、不同步游戏目录、不覆盖冻结候选。

## 第八轮限流后的恢复记录

- 复审输入保持不变：P1.5 契约草案、能力归属表、迁移计划、CURRENT 和本 checkpoint。
- 允许的下一步只有一次独立只读复审；不得并行重复提交，也不得绕过 verdict 直接建立代码写集。
