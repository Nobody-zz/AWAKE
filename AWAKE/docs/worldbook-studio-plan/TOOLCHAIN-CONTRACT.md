# Worldbook Studio MVP 工具链契约

- 目标框架：`.NET 10.0.301`，由 `global.json` 锁定；当前 Persona Workbench Web 已核验为 `net10.0`，只复用本地 Web/launcher 约定，不复制业务契约。
- 依赖：使用 lockfile/锁定版本；允许的 NuGet 源和离线缓存策略必须固定；首次离线构建作为 CI/本地 smoke。
- 形态：本地 Web + 独立 CLI；默认 Windows 本地运行，不依赖 Bannerlord 进程。
- AI：Provider-agnostic 接口，MVP 可不配置；Provider 不参与确定性编译。
- 构建：必须提供 Studio 专属 build、test、package、release 检查入口。
- 发布清单：可执行文件、静态资源、Schema、机器注册表、语言包、fixtures、编译候选、报告和哈希清单。
- 候选包：写入独立 `export/WorldbookV2/`，不位于 v1 自动探测树或其规范化等价路径；`--out` 先做 realpath/祖先路径拒绝，再写入版本、`awake.worldbook.v2.incompatible_with_v1=true`、内容清单和 SHA-256。marker 只是诊断字段，不是隔离机制。
- MVP 不调用游戏同步脚本，不覆盖游戏目录，不修改当前冻结候选；手工复制或显式注入 v2 manifest 不在 Studio 的“不消费”保证内。
- 离线模式：默认拒绝所有出站网络、远程来源、联网更新检查和 Provider 请求；`--no-ai` 即使存在 Provider 配置也必须 fail closed。

## Windows 原子发布状态机

1. 在目标 `export/` 同卷创建唯一 sibling 临时目录。
2. 写入候选文件、报告、内容图、manifest 和 `SHA256SUMS.txt`，完成后 flush 文件与目录。
3. 最后写入 `complete.marker`；只有 marker 存在且全量 hash 校验通过的版本目录才是可发布候选。
4. 以同卷原子替换小型 `current.json` 发布指针，指针只包含候选目录名、manifest hash 和发布时间；不直接原子替换非空目录。
5. 保留上一份已发布指针和候选目录；启动/读取时忽略无 marker、hash 错误或指针不存在的临时目录。

F12 必须验证中断、磁盘满、文件占用、锁竞争和指针切换失败时，`current.json` 不改变，旧候选仍可读，临时目录可安全清理。