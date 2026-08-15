# Marcus AI Framework - Route Declaration Contract

Status: normative. Applies to every extension that issues `IAiGateway` requests.

## Rule

- Every Route ID an extension passes to `IAiGateway` MUST be declared in the extension manifest via `routeIds`.
- An undeclared route is a contract violation and a release blocker. The host rejects manifests that declare invalid, duplicate, or unpermissioned routes.
- Route IDs are namespaced as `<ExtensionId>.route.<name>` (lowercase; stable once published). Renames require the same migration/back-compat discipline as any other stable ID.
- Each declared route MUST also request the matching `ai.route.invoke:<route>` permission in the manifest.
- Players never type Route IDs by hand. The framework auto-provisions `RouteProfile`s from declared routes and surfaces them in the in-game AI Setup console ([Sync routes]).

## Manifest example

```csharp
public ExtensionManifest Manifest { get; } = new ExtensionManifest(
    Owner, "YOUR_EXTENSION_NAME", "0.1.0", 0, 1, new[] { "1.4.8", "1.3.15" },
    new[] { "ai.route.invoke:YourCompany.YourExtension.route.dialogue" },
    requiredCapabilities: null,
    optionalCapabilities: null,
    routeIds: new[] { "YourCompany.YourExtension.route.dialogue" });
```

## Reference

- `ExtensionManifest.RouteIds` - `src/MarcusAIFramework/src/MarcusAIFramework/Api/Extensions.cs`
- Validation - `Core/ExtensionRegistry.cs` (`extension.route_invalid`, `extension.route_duplicate`, `extension.route_permission_missing`)
- JSON Schema - `sdk/schemas/extension-manifest.schema.json` (`routeIds`)
- Template - `sdk/templates/ExtensionTemplate/src/TemplateExtension.cs`

---

# 马库斯 AI 框架 - 路由声明契约

状态：强制规范。适用于所有通过 `IAiGateway` 发起请求的扩展。

## 规则

- 扩展在 AI 请求中使用的每一个 Route ID 都必须在 manifest 的 `routeIds` 中声明。
- 未声明的路由属于契约违规，是发布阻断项。Host 会拒绝声明无效、重复或缺少对应权限路由的 manifest。
- Route ID 按 `<ExtensionId>.route.<name>` 命名（小写；发布后保持稳定）。改名需要与其它稳定 ID 相同的迁移/兼容纪律。
- 每个声明的路由还必须同时在 manifest 中请求对应的 `ai.route.invoke:<route>` 权限。
- 玩家永远不需要手动填写 Route ID。框架从声明自动补建 `RouteProfile`，并在游戏内 AI 设置台（［同步路由］）中展示。

## 清单示例

```csharp
public ExtensionManifest Manifest { get; } = new ExtensionManifest(
    Owner, "YOUR_EXTENSION_NAME", "0.1.0", 0, 1, new[] { "1.4.8", "1.3.15" },
    new[] { "ai.route.invoke:YourCompany.YourExtension.route.dialogue" },
    requiredCapabilities: null,
    optionalCapabilities: null,
    routeIds: new[] { "YourCompany.YourExtension.route.dialogue" });
```

## 参考

- `ExtensionManifest.RouteIds` - `src/MarcusAIFramework/src/MarcusAIFramework/Api/Extensions.cs`
- 校验 - `Core/ExtensionRegistry.cs`（`extension.route_invalid`、`extension.route_duplicate`、`extension.route_permission_missing`）
- JSON Schema - `sdk/schemas/extension-manifest.schema.json`（`routeIds`）
- 模板 - `sdk/templates/ExtensionTemplate/src/TemplateExtension.cs`
