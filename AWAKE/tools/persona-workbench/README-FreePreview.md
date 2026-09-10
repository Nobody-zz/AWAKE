# Persona Workbench Free Preview

这是独立的本机作者工具，不启动 Bannerlord，也不会自动调用 AI Provider。

## 启动

1. 删除旧版解压目录，将 ZIP 完整解压到一个新的空文件夹；不要覆盖或混合 r44 及更早版本的旧文件。本包已内置运行时，不需要另装 .NET 或 Node.js。
2. 双击 `PersonaWorkbench.Launcher.exe`。
3. 在可见启动器中点击绿色的“启动并打开 Persona Workbench”按钮。
4. 工作台打开后可以直接使用顶部“AI 批量制作”导入资料；需要 AI 时再手动设置本次会话 Key。
5. 需要彻底关闭后台服务时，在启动器中点击“停止后台服务”。

发布者运行 `package-free-preview.ps1` 后，会同时得到一个目录包、同名的 `.zip` 文件，以及 ZIP 同级的 `.zip.sha256` 校验文件。用户只需要下载并完整解压 `.zip`；不要把目录包、ZIP 或旧版本文件混合使用。

人物描述下方的“扩写方向与关键词（可选）”可指定扩写重心、重点关键词及需要弱化的主题；这些内容只控制本次 AI 扩写，不会写入 Persona 或 DSL。

如果要一次制作多条 Persona，请在顶部“AI 批量制作”中读取 `.txt` / `.md` 资料，检查本地分段后进入批量生成。页面会显示实时进度；每条结果都要人工检查，成功结果可以逐条载入编辑器或导出 JSON。

`launch-free-preview.vbs`、`stop-free-preview.vbs` 和 `start-free-preview.ps1` 只保留为兼容入口；普通用户使用桌面启动/停止快捷方式即可。

## 完整说明

首次使用请阅读 `PersonaWorkbench-使用说明.md`，其中包含 AI 设置、保存与批准、常见错误和隐私边界。

服务只监听 `127.0.0.1:51337`。Provider 生成、云端端点确认和保存动作都需要玩家主动点击；Key 不写入 Persona 文件或浏览器存储。

