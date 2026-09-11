# Marcus-Awake Embedded Migration Phase 0.5 Checkpoint

- `task_id`: `MARCUS-AWAKE-EMBEDDED-P0.5-20260824`
- `batch_id`: `marcus-awake-config-data-inventory-20260824`
- `status`: `offline_verified`
- `execution_lease`: `completed`
- `scope`: 只读盘点 AWAKE MCM 配置落盘、Provider/凭据入口、Native SyncData、Framework Storage namespace/key/schema、Companion 实际部署与服务日志边界。
- `files_changed`:
  - `_houkai_merge/AWAKE/docs/MARCUS-AWAKE-P0.5-CONFIG-DATA-INVENTORY-20260824.md`
  - `_houkai_merge/AWAKE/docs/MARCUS-AWAKE-CAPABILITY-CARRYOVER-MATRIX-v1-20260824.md`
  - `_houkai_merge/AWAKE/docs/checkpoints/MARCUS-AWAKE-EMBEDDED-P0.5-20260824-checkpoint.md`
  - `_houkai_merge/AWAKE/docs/AWAKE-CURRENT.md`
- `verification`:
  - `E0`: 实测 MCM 全局配置路径为 `OneDrive\\文档\\Mount and Blade II Bannerlord\\Configs\\ModSettings\\Global\\AWAKE\\AWAKE.json`。
  - `E0`: 实测 AWAKE MCM JSON 与 MarcusAIFramework MCM JSON 的字段边界；未发现 API Key/URL/模型出现在两份 MCM JSON 中。
  - `E0`: 实测游戏目录仍存在外部 `MarcusAIFramework` 模块、`MarcusAIFramework.dll` 和 `.NET 8` Companion。
  - `E0`: 实测 `%LOCALAPPDATA%\\MarcusAIFramework` 存在 `credentials.dpapi`、`platform.db`、`campaigns`；未读取秘密内容。
  - `E0`: 追踪 AWAKE 三个 Native `SyncData` key、12 个 Storage namespace、主要 key/schema、SessionEnding drain 与 Worldbook v2/RAG 双路径。
  - `E0`: 读取框架/Companion/AWAKE 日志，确认实际存在启动、Named Pipe 连接/断开和服务退化记录。
  - `E1/E2`: 未构建、未运行游戏、未同步任何发布目录。
- `known_limitations`:
  - 当前游戏目录仍是旧外部框架拓扑，不能证明内置程序集或新 Service 已工作。
  - MCM 实际 JSON 含 `AiRuntimeStatus`，但源代码标记 `JsonIgnore`；具体序列化差异需在迁移后用契约测试锁定。
  - 旧 `platform.db`/`campaign.db` 的内部表和迁移序列未改动、未作为新 schema 权威；只确认文件存在与路径拓扑。
  - Companion 的真实 Provider 联机、完整配置事务和当前新协议尚未验证。
  - 未锁定旧 Marcus 数据库接管/迁移/隔离的最终方案。
- `next_action`: 以 Marcus 核心能力继承矩阵为中心锁定 P1.5 API/程序集/IPC/Storage 契约；在契约审查和用户签收前不得修改 `AWAKE.csproj`、`SubModule.xml` 或运行时代码。
- `last_error`: `none`

