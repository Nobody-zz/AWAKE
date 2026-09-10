# 本地 Worker 配置

## 这是什么

本地 Worker 是**外部前置依赖**，不是随包组件，也不伪装成内置模型：客户包内不含 Worker、不含模型文件，也不会自动下载或安装。参考默认 loopback 地址为 `http://127.0.0.1:11434`，协议为 `awake.worker.v1`。

## 需要准备什么

1. 一个已经在本机运行、能完成 `awake.worker.v1` 握手的 Worker 服务（只监听 loopback）。
2. 该服务的共享密钥。
3. **在启动 Worldbook Studio 之前**让程序知道这两件事（通过环境变量）：
   - `WORLD_BOOK_LOCAL_WORKER_URL`：Worker 的 loopback 地址；
   - `WORLD_BOOK_LOCAL_WORKER_SECRET_ENV`：存放密钥的那个环境变量的名字；该名字指向的变量里放密钥值。

当前版本的工作室界面**不提供**填写本机 Worker 地址的入口，因此这一步必须由提供 Worker 的一方在启动前完成，不能在界面里临时填写。

## 怎么确认配好了

配置至少需要 Worker endpoint、协议版本和模型 ID。启动 Worldbook Studio 后先执行握手，再执行分析；握手、分析、候选校验、`needs_review` 建档和 readback 应绑定同一 BuildId 与内容哈希。

## 常见问题

- 提示“当前 AI Provider 尚未配置或不可用”：多数是 Worker 没启动，或环境变量没有在启动前设置。
- 不想配置 Worker：可以改用云端 Provider，或先不使用 AI —— 编辑、保存、校验、预览、导出和“先做本地检查”都不依赖 Worker。
- 只想确认资料不出本机：核对地址是否为 loopback（`127.0.0.1` / `localhost` / `::1`），不要把离线资料发到外部地址。

## 边界

没有 Worker 时，工具应明确显示 `not_configured` 或 `needs_review`，不能静默把失败当成成功，也不能把临时测试 shim 称为客户 Worker。不要连接来历不明的本机服务或云端地址。
