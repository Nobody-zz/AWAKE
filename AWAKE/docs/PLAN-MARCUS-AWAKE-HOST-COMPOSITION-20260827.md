# Marcus-Awake Framework Host 组合与注册子批次

- `task_id`: `MARCUS-AWAKE-EMBEDDED-FULL-CAPABILITY-20260824`
- `batch_id`: `MARCUS-AWAKE-HOST-COMPOSITION-20260827`
- `parent_plan`: `docs/PLAN-MARCUS-AWAKE-FULL-MIGRATION-20260826.md`
- `status`: `implementation_authorized_by_parent_plan`
- `scope`: Framework Host 默认服务组合、扩展注册表、完整 Host 兼容投影
- `out_of_scope`: P3B Transport/RuntimeService 四个并行写集、Provider/Storage 后端实现、游戏目录、Bannerlord 启动

## 可验证目标

1. `FrameworkHostLocator.Register(IFrameworkExtension)` 能创建并发布唯一 Host。
2. 扩展的 capability、command、context provider 能通过统一注册面进入 Host。
3. `FrameworkHost` 同时提供新 Framework 生命周期面和现有 AWAKE 调用方使用的完整 Host 面。
4. 尚未接入的 Provider、Storage、RAG、模型、媒体和资产服务返回 typed unavailable，不阻止模组加载。
5. Framework Release 构建与既有核心测试保持通过。

## 约束

- 不访问 `Campaign.Current`，不在 tick 中执行阻塞 I/O。
- 不修改 P3B 四个并行写集文件。
- 不把 unavailable 实现伪装成 Provider、SQLite、RAG 或游戏内闭环完成。
- 保留现有构造函数和公开 API 兼容性。