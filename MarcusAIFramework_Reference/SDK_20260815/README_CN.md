# Marcus AI Framework SDK Preview

版本：`0.1.0-preview.1`；公共 API：`0.1`；协议：`1.0`；目标 Bannerlord API：`v1.4.8`、`v1.3.15`。

此目录是扩展开发者的编译期 SDK，不是可安装 Mod。扩展只引用 `ref/MarcusAIFramework.dll`，发布时不得把该 DLL、TaleWorlds DLL、四前置 DLL、密钥、数据库或资产真实路径打进扩展包。

## 快速开始

1. 复制 `templates/ExtensionTemplate`，替换 `YourCompany.YourExtension`、显示文本和版本。
2. 按目标 API 先构建框架，再把 `FrameworkReferenceRoot` 指向本 SDK 根目录。
3. 在 `ExtensionManifest` 中声明精确 Bannerlord API 白名单、权限和 capability URI。
4. 使用 `fake-host/FakeHost.cs` 与 `test-kit/MafAssertions.cs` 做无 TaleWorlds 对象的纯契约测试。
5. 运行 `analyzers/maf-lint.ps1 -ExtensionRoot <path>`；Preview linter 只发出警告，不自动改文件。
6. 分别在 v1.4.8 与 v1.3.15 的真实引用上构建；FakeHost 不能替代游戏内 smoke test。

## 目录契约

- `manifest.json`：把 DLL、XML 文档、Schema 和 SDK/API/协议版本绑定在一起。
- `schemas/`：扩展清单与 capability 描述 JSON Schema。
- `templates/`：一个逻辑 ModId、一个 DLL、同模块中英双语的最小扩展模板。
- `fake-host/`：虚拟时钟、权限矩阵、事件记录、流和 capability 结果的确定性测试替身。
- `test-kit/`：错误分类、所有者、correlation ID、流结束和 permission denial 断言。
- `analyzers/`：面向常见边界违规的 warning 级静态 linter。

SDK 只保证公共 DTO 与治理契约的编译期形状。它不证明 TaleWorlds Adapter、Gauntlet 布局、Provider 网络协议或实机生命周期正确。
