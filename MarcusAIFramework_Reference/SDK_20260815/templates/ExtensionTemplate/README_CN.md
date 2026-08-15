# 你的扩展

这是一个独立编译的 MarcusAIFramework 扩展模板。

- 逻辑 ModId：`YourCompany.YourExtension`
- DLL：`YourCompany.YourExtension.dll`
- 支持 API：`v1.4.8`、`v1.3.15`
- 只通过公共 `MarcusAIFramework.Api` 注册 capability；不访问框架 internal、Campaign.Current 或 Companion 路径。

发布前请删除示例 capability 的占位内容，补齐权限用途、Schema、README_CN.md / README_EN.txt 与两种目标 API 的构建记录。
