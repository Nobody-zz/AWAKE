# AWAKE 三工作站客户包

本包包含 UI Workstation、Worldbook Studio 和 Persona Workbench。三者均为本地工具，UI Workstation 只编排导航，不启动另外两个工作站。

启动入口：

- `ui-workstation/start-ui-workstation.ps1`
- `worldbook-studio/Awake.WorldbookStudio.Launcher.exe`
- `persona-workbench/PersonaWorkbench.Launcher.exe`

先分别启动 Worldbook Studio 和 Persona Workbench，再启动 UI Workstation。首次使用建议从 Worldbook Studio 创建草稿，使用 Persona Workbench 生成或交接内容后，在 Worldbook Studio 中审查、保存和导出。

本包只证明自动化与包级检查。游戏运行、真机用户流程和长时存档回归不在本包自动完成。

根级打包命令为 `tools/customer-delivery/package-customer.ps1`。它只消费已经通过自身 manifest/sidecar 校验的三个子包，并在全部检查成功后写入 sibling ZIP 与外置 rollback pointer。

验收边界：

- 包校验使用 `tools/customer-delivery/validate-customer-delivery.ps1`，必须同时提供客户目录、对应 ZIP 和 `rollback-pointer.json`。
- 本地 Worker 只允许 loopback Ollama；验收使用 `tools/customer-delivery/local-worker-package-acceptance.ps1`，生成内容始终先保存为 `needs_review`，不会自动进入正典。
- authority 导出是独立的 staging 流程；项目维护者可使用 `tools/customer-delivery/customer-authority-export-smoke.ps1` 验证注册、批准、CompileProof 和 staging 导出。staging 不等于已发布 canon。
- `E3` 及以下证据不代表用户已完成实际编辑，也不代表 Bannerlord 游戏内可用。
