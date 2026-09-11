# Marcus-Awake 全量内置迁移计划

- task_id: MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824
- revision: 1
- plan_status: approved_for_autonomous_execution
- date: 2026-08-26
- primary_executor: controller
- user_authorization: 用户已授权自行设定目标并推进至全量迁移合并

## 1. 目标

把原 MarcusAIFramework 的核心能力迁移为 AWAKE 自有实现，最终玩家只启用 AWAKE：

- AWAKE 不依赖外部 MarcusAIFramework 模组、DLL、Companion、旧 pipe、旧存储根或旧协议。
- AWAKE 包含游戏侧 `MarcusAwakeFramework.dll`、共享 `MarcusAwakeTransport.dll` 和独立 Runtime Service。
- 游戏内高频玩家配置只保留在 AWAKE MCM：API Key、URL、模型拉取、联通性测试、启用/禁用与隐私开关。
- Runtime Service、数据库、Provider HTTP、诊断细节和开发日志属于后台/开发者边界，游戏内只显示简化状态与可行动错误。
- 继承 Marcus 的 AI Gateway、路由/Provider、流式任务、取消、SQLite/FTS5、RAG、凭据保护、事件/时间线、诊断、迁移与工具能力，但按 AWAKE 的权限、性能和内容治理重新落地。

## 2. 权威边界

- 游戏状态、存档状态、命令结算：AWAKE 游戏侧与 Bannerlord。
- AI 请求、Provider、流、SQLite/FTS5、RAG 索引、报告压缩：Runtime Service。
- 跨进程协议：`MarcusAwakeTransport` v2；旧 `MarcusAIFramework.Companion.v1` 永不接受。
- 公共游戏侧 API：`MarcusAwakeFramework.Api`；不得把密钥、HTTP、数据库句柄、TaleWorlds 实时对象暴露给扩展。
- 内容与世界书：AWAKE/内容包文件；不得把战役状态冒充为 ModuleData。
- AI 只能生成叙事/结构化意图；触发、权限、命令、数值和世界变更由代码结算。

## 3. 阶段与闭环

### P3B：真实 Runtime Service / IPC

入口：Framework 启动 Service → bootstrap/握手 → 私有 Named Pipe → typed frame → ACK/response。

结算：sequence/session/epoch/nonce/checksum/父进程/SID 验证；取消、断线、drain、父进程退出均安全收口。

可观察结果：health/echo/cancel、拒绝旧协议/错误身份/重放/越界帧；服务无孤儿进程。

最低证据：P3B-E1 构建/静态；P3B-E2 真实子进程与 Named Pipe harness。

### P3C：SQLite/FTS5、KV、Timeline、RAG

入口：Runtime Service storage/RAG handler。

结算：事务性 KV、事件/时间线账本、文档 upsert、collection/corpus fingerprint、访问范围过滤、FTS5 bounded search。

可观察结果：重复写幂等、旧 corpus 拒绝、权限/owner 隔离、确定性排序、取消/重启恢复。

最低证据：P3C-E1 后端构建和 focused tests；P3C-E2 service handler integration fixtures。

### P3D：Provider、凭据、路由、流式与 fallback

入口：MCM/配置 DTO → service provider registry → route gateway。

结算：密钥不进日志/命令行/公共 API；模型发现、联通测试、OpenAI-compatible/Anthropic/Ollama 适配、SSE 增量、deadline/cancel、429/5xx 分类、有限 fallback。

可观察结果：玩家能填写并验证 URL/key/model；Provider 不可用时返回 typed degraded，不阻塞游戏。

最低证据：P3D-E1 fake HTTP/SSE/secret-redaction tests；真实云端 Provider 仍标为未验证。

### P4：MCM 玩家配置与配置持久化

入口：AWAKE MCM。

结算：MCM 保存值优先，Config.json 兼容回退；API Key 可视输入、测试/拉取模型为显式按钮；请求仅在玩家手势触发。

可观察结果：无需 Companion 即可完成最短配置闭环；后台日志不直接暴露在游戏 UI。

最低证据：MCM 静态/离线 harness；Bannerlord E4 由用户运行后日志验证。

### P5：DevTools/SDK/调用方接线

入口：AWAKE `SubModule`/`AwakeRuntime`/Npc dialogue/world report routes。

结算：AWAKE caller 通过内置 host/runtime port 调用；现有 NPC 对话、知识、记忆、事件与命令路径保持可降级；无外部 Marcus 引用。

可观察结果：从 AWAKE 入口能完成 route call → structured result → command/persistence/overlay。

最低证据：AWAKE release build、API/static scans、offline caller fixtures；E4/E5 仍需用户运行游戏/存档。

## 4. 不做

- 不同时接受外部 Marcus 与 Marcus-Awake 两套运行时。
- 不迁移旧 Marcus 数据库、旧 Companion profile、旧 pipe 或旧凭据；仅提供明确的冷启动/可选受控导入边界。
- 不把 Provider HTTP、API Key、SQLite、Runtime Service 操作放进游戏 tick/MCM tick。
- 不引入本地 ONNX/embedding/rerank 推理，不把语义检索复制成第二套模型实现。
- 不启动 Bannerlord、不改启动器、不覆盖游戏目录，除非另有明确同步与运行授权。
- 不把离线 build/smoke 当成 E4/E5 游戏完成。

## 5. 总体验收

- 编译：Framework/Transport/Service/AWAKE Release 均 0 warnings / 0 errors。
- 契约：所有共享 JSON/协议/服务 fixture 可解析，正反例和重放/取消/断线通过。
- 接线：每个新服务都必须满足入口 → 调用 → 结算/持久化 → 可观察结果；只存在不调用按 P0 缺陷处理。
- 安全：旧 Marcus 引用、旧 pipe、密钥、真实路径和阻塞 I/O 静态门禁通过。
- 包：`Awake.dll`、`MarcusAwakeFramework.dll`、`MarcusAwakeTransport.dll`、Runtime Service 及依赖清单可复制部署，版本/哈希/manifest 一致。
- 证据：分别报告 E1/E2/E3/E4/E5；没有用户当前 BuildId 日志时不得宣称 E4/E5。

## 6. 当前状态

- P3A：当前 API 分层审计完成，E1/E2 离线证据通过；旧 P3A forbidden scan 保留为历史检查，不作为内置 Runtime 架构的现行总闸。
- P3B：核心传输与生命周期完成离线 E2；当前回归 `19/19`，包含旧协议/身份/重放/断线/父进程退出边界。
- P3C：SQLite/FTS5、KV、Timeline、RAG 与 durable receipt 完成离线 E2；当前回归 `7/7`，包含提交后崩溃恢复。
- P3D：Provider、凭据、路由、流式、取消与 fallback 完成离线 E2；Provider 主回归、P3D-A0/A1/A2 均通过。
- P4/P5：AWAKE MCM 静态契约 `47/47`、AWAKE caller 静态契约 `7/7`、生产 smoke `17/17` 通过；当前 `002` 候选已可进入授权同步与用户游戏验证。
