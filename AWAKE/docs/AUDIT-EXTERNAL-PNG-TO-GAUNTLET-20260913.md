# 外部 PNG → Gauntlet · 可行性验证

日期：2026-09-13
范围：只读核查。未写任何功能代码。
问题：运行时 AI 出的 png，怎么显示进 Gauntlet？（不通则整个功能的形态要重来）

**结论：通。** 不需要 `SpriteParts → wEditor Import → tpac` 那条构建期管线；运行时把 png 交给一个自定义 `TextureProvider` 即可。07 稿左槽（212×360 全身像）可按原样实现。

以下每条判定都出自出货客户端的一手代码，不引社区样本。

---

## 一、判定依据

### 1. 官方自己就是这么干的

`Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.GauntletUI.dll`

`OnlineImageTextureProvider`：下载字节 → 存成 `.png` → `Texture.CreateTextureFromPath(PlatformFilePath)` → 包成 `EngineTexture` → `new Texture(ITexture)` → 在 `OnGetTextureForRender` 返回。

文件类型（png）、落盘位置（`PlatformDirectoryPath`）、转纹理的方式，与我们要做的事完全一致。这是最强证据：**"外部字节 → 运行时纹理 → Gauntlet 控件"官方已经跑通并出货了。**

### 2. 模块程序集定义 provider 是官方常态

同一个模块程序集里定义了 13 个 `TextureProvider` 子类，出货正常运行：

```
BannerImageTextureProvider        BannerTableauTextureProvider
BrightnessDemoTextureProvider     CharacterImageTextureProvider
CharacterTableauTextureProvider   CraftingPieceImageTextureProvider
ImageIdentifierTextureProvider    ItemImageTextureProvider
ItemTableauTextureProvider        OnlineImageTextureProvider
PlayerAvatarImageTextureProvider  SaveLoadHeroTableauTextureProvider
SceneTextureProvider
```

⇒ "模块 DLL 里定义的 provider 在出货版能被解析到"不是推测，是既有事实。

### 3. 注册是自动的，按类型简单名

`TaleWorlds.GauntletUI.TextureProviderFactory`：

```csharp
public static TextureProvider CreateInstance(string textureProviderName)
{
    if (_textureProvidertypes.TryGetValue(textureProviderName, out var value)) { ... }
}

public static void RefreshProviderTypes()
{
    _textureProvidertypes.Clear();
    foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        foreach (Type t in a.GetTypesSafe())
            if (typeof(TextureProvider).IsAssignableFrom(t) && !t.IsAbstract)
                _textureProvidertypes.Add(t.Name, t);
}
```

扫的是 **AppDomain 全部已加载程序集**（无程序集过滤），键是 `type.Name`（**简单名**）。我们只要让类名唯一即可；给类起个带前缀的名字（如 `AwakePortraitTextureProvider`）以免撞名。

### 4. 刷新时机对我们有利（关键）

`TaleWorlds.MountAndBlade.Module.LoadSubModules` 是**两段式**：

```csharp
// 第一段：遍历所有模块的所有 submodule —— 逐个 AssemblyLoader.LoadFrom(dll)
foreach (ModuleInfo module2 in modules)
    foreach (SubModuleInfo subModule in module2.SubModules) { ... AssemblyLoader.LoadFrom(text); ... }

// 第二段：整个循环结束后，才逐个调 OnSubModuleLoad
if (loadNewModules) { ...; OnNewModuleLoaded(); }
else { InitializeSubModuleBases(); }        // → foreach (_subModuleBases.Values) value.OnSubModuleLoad();
```

而 Native 的 `GauntletUISubModule.OnSubModuleLoad()` 里：

```csharp
private void RefreshResources(bool initialLoad)
{
    ...
    WidgetInfo.Refresh();          // ← 内部会调 TextureProviderFactory.RefreshProviderTypes()
    UIResourceManager.Refresh();   // ← 重建 ResourceDepot / WidgetFactory
    ...
}
```

⇒ **所有模块的 DLL 都已加载完之后，注册表才被扫描一次。** AWAKE.dll 此时必定在 AppDomain 里。
⇒ **AWAKE 侧不需要任何额外注册动作。**

另有两条保底路径（都不需要我们动手）：

- `GauntletUISubModule.OnNewModuleLoad()` → `_areResourcesDirty = true` → 下一个 `OnApplicationTick` 再走一次 `RefreshResources(false)`。热加载新模块时用。
- `WidgetInfo.Refresh()` 是 `public static`，我们自己也能调（第三方框架 `Bannerlord.UIExtenderEx` 就是反射调它来注入自己的 widget 类型的）。

---

## 二、完整链路

```
AI 出图（bytes / base64）
  │
  ├─ FileHelper.SaveFile(
  │      new PlatformFilePath(
  │          new PlatformDirectoryPath(PlatformFileType.Application, "<AWAKE 目录>"),
  │          name + ".png"),
  │      bytes)                                   ← 判 SaveResult，别静默成功
  │
  ├─ TaleWorlds.Engine.Texture.CreateTextureFromPath(path)      ← 官方一手用例
  │
  ├─ new TaleWorlds.Engine.GauntletUI.EngineTexture(engineTex)  // : ITexture
  │
  ├─ new TaleWorlds.TwoDimension.Texture(engineTexture)          // public ctor
  │
  ├─ TextureProvider.OnGetTextureForRender(...) 返回它
  │
  └─ TextureWidget.OnRender → SimpleMaterial.Texture → drawContext.Draw   （官方代码，无需改）
```

`TextureWidget.OnRender` 的消费方式（不必重写）：

```csharp
Texture = TextureProvider.GetTextureForRender(twoDimensionContext);
if (texture != null && texture.IsValid) {
    SimpleMaterial simpleMaterial = drawContext.CreateSimpleMaterial();
    simpleMaterial.Texture = Texture;
    simpleMaterial.NinePatchParameters = SpriteNinePatchParameters.Empty;
    ...
    drawContext.Draw(simpleMaterial, in drawObject);
}
```

注意 `NinePatchParameters = SpriteNinePatchParameters.Empty` —— **运行时纹理不走九宫格**。07 稿的槽位是带切角的矩形，切角得用 brush/装饰层做，不能指望纹理自己缩。

---

## 三、Prefab 侧

- widget 标签名 = C# 类**简单名**（`WidgetFactory.Initialize` → `_builtinTypes[type.Name]`）
- 形如 `<AwakePortraitWidget TextureProviderName="AwakePortraitTextureProvider" ImageId="..." />`
- 参数经 `TextureWidget.SetTextureProviderProperty(name, value)` **反射进 provider 的同名属性**；照抄 `ImageIdentifierWidget.ImageId` 的 setter 写法即可（含 `IsReleased` 先释放后赋值的成对调用）
- widget 类要被 `WidgetInfo.CollectWidgetTypes()` 收录，条件是**所在程序集引用 `TaleWorlds.GauntletUI`** —— BannerlordApi 满足
- `TextureWidget` 的构造要求 `(UIContext)` 单参构造（`CreateBuiltinWidget` 用 `GetConstructor(new[]{ typeof(UIContext) })`）
- AWAKE 的 `GUI/` 目录已被 `UIResourceManager.RefreshResourceDepot` 纳入（判据 `Directory.Exists(module.FolderPath + "/GUI/")`）
- brush 名全局唯一，继续 `Awake.` 前缀

---

## 四、实现时必须处理的坑

1. **两个 `Texture` 同名**。`TaleWorlds.Engine.Texture`（引擎对象）与 `TaleWorlds.TwoDimension.Texture`（控件吃这个）在同一个文件里同时 using 会歧义。必须显式别名。
   附带：ilspy 反编译出的 `OnlineImageTextureProvider` 源码在这个点上**不可编译**（`Texture` 裸用），不能照抄文本，要照抄语义。

2. **换图必须先释放旧纹理**。provider 实例与 widget 实例一一对应，生命周期由 `TextureWidget.OnDisconnectedFromRoot` → `OnClearTextureProvider()` → `provider.Clear(true)` 控制。我们的 provider 要在 `Clear` 里 `ReleaseImmediately()`，否则每换一次图漏一张 GPU 纹理。官方的 `ImageIdentifierTextureProvider.IsReleased` 属性就是为这个用途存在的。

3. **基类 `Clear(bool)` 只清 get-method 缓存，不释放纹理**（`ResourceTextureProvider` 同样不释放）。释放责任在我们。

4. **无纹理时返回 `null`，控件不画**。`TextureWidget.LoadingIconWidget` 专为此设：`OnTextureUpdated` 里按 `Texture.IsValid` 切换它的可见性。**正好对应 07 稿的 `busy` 态**——生成中就让它返回 null + 挂 loading 图标。

5. **provider 是懒创建的**：`TextureWidget.UpdateTextureWidget()` 里 `TextureProvider == null && TextureProviderName != ""` 时才 `CreateInstance`。而 `SetTextureProviderProperty` 内部会把 `Texture = null` ⇒ 改属性会触发重新取纹理。所以**先设属性、后取纹理**，顺序反了会白跑一帧。

6. **不能在 LateTick 阶段发起**。官方的 image identifier 路线有 `ScreenManager.IsLateTickInProgress` 断言。我们的 provider 若也走异步回调，注意回调落点。

7. **首次落图到显示之间有一帧以上延迟**（落盘 → 建纹理 → 下一帧 OnRender）。UI 状态机要能吃这个延迟，别做成"点完立刻可见"。

---

## 五、对功能形态的影响

- **形态不用重来。** 07 稿左槽按原样实现。
- **不用** `SpriteParts/` + wEditor `Import` + `AssetPackages/*.tpac`。那套只解决"构建期固定贴图"，解决不了"每次出图都不同"。
- **不用** `ImageIdentifier` 路线（要先造 `ImageIdentifier` 子类 + VM 绑定 + `TextureProviderName` 绑定）。除非将来想复用官方的角色/旗帜/物品渲染，否则 `TextureProvider` 直连更短。**推荐直连。**
- 现有 `GUI/AWAKESpriteData.xml` + `AwakeBrushes.xml` 继续管**静态**部分（面板底、切角、按钮三态、边框），运行时 png 只负责左槽那一个动态块。两者互不干扰。

### 未验证项（诚实标注）

- `Texture.CreateFromMemory(byte[])` 是否接受 **PNG 编码**字节（还是只吃原始像素）——没找到一手用例。**别赌**，走 `CreateTextureFromPath`。
- `Texture.CreateFromByteArray(data, w, h)` 的通道序（RGBA/BGRA）与行序未验证。
- `Texture.LoadTextureFromPath(fileName, folder)` 的 `folder` 语义未验证。
- 落盘目录用 `PlatformFileType.Application` 还是 `.User` 未验证差异（官方 `OnlineImageTextureProvider` 用的是 `Application`）。

---

## 六、复核用的坐标

反编译产物（未入库，在 `.gitignore` 覆盖的 `AWAKE/workspace/ilspy-probe/` 下）：

| 内容 | 位置 |
|---|---|
| `TextureProviderFactory` / `WidgetInfo` / `TextureWidget` / `TextureProvider` | `gauntletui/TaleWorlds.GauntletUI/` |
| `EngineTexture` / `UIResourceManager` / `GauntletLayer` | `engine-gauntletui/` |
| `WidgetFactory`（widget 类型解析 + assemblyOrder） | `prefabsystem/` |
| 13 个官方 TextureProvider + `GauntletUISubModule` | `mb-gauntletui/` |
| `Module.LoadSubModules`（两段式加载） | `mb/TaleWorlds.MountAndBlade/Module.cs` |
| `OnlineImageTextureProvider`（活样板） | `mb-gauntletui/.../TextureProviders/` |
| `PlatformFilePath/DirectoryPath/FileHelper/PlatformFileType` | `lib/` |

复现手法（本机已装 ilspycmd，注意它不认 MSYS 的 `/d/` 路径，要传 `D:\...`）：

```
ilspycmd -p -o 'D:\AWAKE-Dev\AWAKE\workspace\ilspy-probe\<名>' '<dll>'
```

查"某符号在哪些程序集里被引用"用 `grep -a -r -l --include='*.dll' '<符号>' <目录>`。
**不要**用 `tr -c '[:print:]' '\n' | grep -c '^符号$'` —— 该方法本机实测会漏（阳性对照 `GauntletMovie` 给出 0 命中，而它确实存在于该 dll）。
