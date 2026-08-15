# AWAKE 内容包公开 API 契约（v1 草案）

> 日期：2026-08-15
> 范围：内容包通过 AWAKE 注册接口接入运行时；本文件锁定签名、owner 校验、权限、加载顺序与版本。
> 状态：草案，Phase B 实现前冻结；内容包 manifest 声明所需 `AwakeApiVersion`，不匹配则拒绝加载。

## 1. 版本与能力

- `AwakeApiVersion = 1`
- 内容包 manifest 新增 `AwakeApiVersion=1`；AWAKE 加载前校验，不匹配即拒绝。
- 每个接口方法都带 owner 校验：调用方 `ExtensionId` 必须等于注册时的 owner，否则 `permission.owner_mismatch`。

## 2. 接口签名

```csharp
namespace Awake;

public enum AwakeContentTier
{
    Pure = 0,
    Standard = 1,
    Intense = 2
}

public interface IPersonaDialogueShell
{
    bool Open(string personaId, string heroId, string openingHint);
    void Close();
}

public interface IMenuEntryRegistry
{
    bool Register(string gameMenuId, string optionId, string localizedTextId,
        Func<bool> condition, Func<MenuCallbackArgs, bool> consequence);
    bool Unregister(string gameMenuId, string optionId);
}

public interface ICommandAdapterRegistry
{
    bool Register(string commandId, CommandRiskTier riskTier,
        ICommandAdapter adapter, string owner);
}

public interface IStateSchemaRegistry
{
    bool RegisterNamespace(string namespaceId, string schemaId,
        Func<JObject, string> validator, Func<JObject, WorldStateCommand, string> applier);
}

public interface ILifecycleHook
{
    void OnCampaignStart();
    void OnReset();
    void OnOverlayClose();
    void OnFinalDrain();
}

public interface IContentPolicyGate
{
    AwakeContentTier EffectiveTier { get; }
    bool Allows(AwakeContentTier itemTier);
}

public interface IKnowledgeCorpusRegistry
{
    bool RegisterCorpus(string corpusId, string jsonPath, string fingerprint,
        AwakeContentTier tier);
}

public interface IRouteRegistry
{
    bool RegisterRoute(string routeId, string taskKind, bool allowCloud,
        string[] candidateModelIds);
}
```

## 3. 注册规则

- 内容包注册任何能力前，先通过 `IRouteRegistry`/`ICommandAdapterRegistry` 声明 owner；owner 必须等于内容包 `ExtensionId`。
- 注册顺序：能力探测 → owner 绑定 → ContentPolicy → 菜单/命令/存储 → 生命周期。
- 同一 owner 重复注册同一 ID：幂等或返回 `already_registered`，不允许静默覆盖。
- 运行时 API 版本不匹配：加载失败并记日志，不部分注册。

## 4. 加载顺序

1. MarcusAIFramework
2. AWAKE（初始化 API 版本与注册表）
3. 内容包（声明 `AwakeApiVersion=1`）

## 5. 权限集成

- 内容包注册的命令仍走 `PermissionGate` 玩家触发 `EnsureAsync`，不依赖自动授权绕过同意。
- 内容包菜单/对话打开走 AWAKE 主线程 marshal（`AwakeUiDispatcher`）。

## 6. 待冻结项

- `MenuCallbackArgs` 依赖 Bannerlord 类型，具体参数在 Phase B 实现时按游戏 API 冻结。
- `WorldStateCommand` 结构与内容包 schema 注册的精确载荷在 Phase C 冻结。
