# AI 生图（NPC 全身像）· 加法方案

**日期**：2026-09-13
**线**：模组主体（代码）线 —— 但本方案跨 4 条线，见 §10
**上游**：
- `AWAKE/docs/AUDIT-EXTERNAL-PNG-TO-GAUNTLET-20260913.md`（显示侧，已验通）
- `AWAKE/docs/UI-ART-ASSET-INTERFACE-20260913.md` §4.3（用图方向已登记：说话人头像 / 半身立绘，调性＝暖调烛光）
- `AWAKE/docs/ui-design/npc-dialogue-layout-07.html`（界面稿，未提交）

---

## 判断

**不用发明任何东西，也不用新拉一条管线。** 模组里已经有一条完整的「命中转」通道：游戏 → Runtime Service → Provider → 回帧；生图只是在这条通道上加一个货物品类。显示侧上一轮已经验通。

所以「怎么加」＝**在既有的四个环上各拧一颗螺丝**。

---

## 0. 已定约束（Max 已拍）

| 项 | 定 |
|---|---|
| 出图位置 | **游戏内 · 自建中转**（模组只调自己的服务，key 留服务端） |
| prompt 来源 | **默认用已有描述或模板，用户也可以自己改** |
| 显示侧 | 已验通，照 `AUDIT-EXTERNAL-PNG-TO-GAUNTLET-20260913.md` 的链路做 |
| 界面 | 照 `docs/ui-design/npc-dialogue-layout-07.html` |

---

## 1. 现状事实（三条，决定加法的形状）

**① `AWAKE/src` 零网络代码。**
全目录 grep `HttpClient` / `WebClient` / `System.Net` / `IHttpDriver` / `HttpHelper` —— **零命中**。模组进程从来没有出过网。

**② 出网口已经存在，而且在另一个进程里。**
`framework/MarcusAwakeRuntimeService/` 是独立进程（`MarcusAwakeRuntimeService.exe`），命名管道收帧 → `ProviderWireAdapter` 分派 → `ProviderRouter` → `ProviderAdapterBase` → provider HTTP。
**并且 API Key 已经存在那边**：MCM 输入 → `AwakeProviderConfiguration.SaveCredentialAsync` → `UpsertCredentialAsync(ProviderCredentialRequest{ secret })` → Runtime 侧保护存储（`CredentialReference = "awake.provider.default"`）。

> ⇒ 「自建中转」不需要新建什么东西——**Runtime Service 就是那个中转**，key 从来就不在模组里。这条约束天然满足。

**③ 框架层的「生成 → 资产化」接缝已经备齐，但从没被接上。**
`Compat/IMediaService.cs` 有 `GenerateImageAsync(ImageGenerationRequest, RequestContext, CancellationToken)`；
`ImageGenerationRequest` 已带 `Prompt / NegativePrompt / Width / Height / Seed / Provenance / RetentionClass / CloudExportClassification / DeadlineUtc / IdempotencyKey / PinModel`；
`GeneratedAssetResult(AssetHandle, ResolvedModel)` / `AssetHandle`（11 个只读属性，构造时校验 assetId、contentHash、byteLength≥1、ownerExtensionId）/ `AssetContent.GetContentCopy() → byte[]`。

**但**：`HostApi.cs:81` 是 `media = new UnavailableMediaService();` —— 硬编码，连 override 都不给；`FrameworkServiceOverrides` 只有 5 个槽（Permissions / Prompts / Storage / Rag / GameData），**没有 Media、没有 Assets**。

> ⇒ 接缝是死桩。要接，先给它开个槽。

---

## 2. 两条路，差别只在「图片从哪出去」

| | **A · 复用 Runtime Provider 链** | **B · 模组内直连中转** |
|---|---|---|
| 第一张图 | 慢：`MarcusAwakeTransport`（协议常量/契约）+ `RuntimeServiceHost` 分派 + `ProviderWireAdapter` + `ProviderRouter/AdapterFactory` + 新 adapter + `ProviderProtocolContract` + P3 测试夹具 | 快：模组 1 个工程 |
| 凭据 | 复用既有保护存储、限流、时钟、端点策略、错误分类、`RuntimeProviderOutcomeLedger` | 模组侧新增一处 token（或复用同地址） |
| 触碰面 | framework **5 个工程** + 模组 | framework **1 行 + 1 槽** + 模组 |
| 主要风险 | **帧体积**：图片得走文件或分帧，`MaximumFrameBytes` 与严格 envelope 校验会卡（待验） | **模组第一次有出网口**（TLS / 线程） |
| 形态定错时的代价 | 5 个工程返工 | 换一个类 |

### 推荐：先 B 打通形态，端口形状照 A 写

理由：**形态没定就上协议锁，定错一次是五个工程返工。** 而 B 的接口只要写成 A 的形状（一个 `IMediaService` 实现 + 一个端口），将来搬进 Runtime 就是**替换实现、不动调用方**。

框架面要动的东西，总共就 1 槽 1 行：

```csharp
// FrameworkServiceOverrides.cs：加一个属性
public IMediaService Media { get; set; }

// HostApi.cs:81：从硬编码改成可覆盖
media = overrides?.Media ?? new UnavailableMediaService();
```

> `IAssetService` 也同样是死桩（`HostApi.cs:84`），但**这一步不必开槽**：`AwakeMediaService` 自己就是那个存储侧，落盘后按幂等键就能定位文件。等哪天真要统一资产面，再开第二个槽。

---

## 3. 要动的地方（逐层）

| # | 层 | 文件 | 动作 | 线 |
|---|---|---|---|---|
| 1 | 契约 | `docs/CONTRACT-IMAGE-RELAY-20260913.md`（新） | 写中转请求/响应 | 我 |
| 2 | 框架 | `framework/MarcusAwakeFramework/src/ServiceOverrides.cs` | +1 属性 | 框架层（报备） |
| 3 | 框架 | `framework/MarcusAwakeFramework/src/HostApi.cs` | +1 行 | 框架层（报备） |
| 4 | 模组 | `src/AwakePortraitCache.cs`（新） | 幂等键 + 目录 + 命中判定 | 我 |
| 5 | 模组 | `src/AwakePortraitTextureProvider.cs`（新） | `TextureProvider`，零注册动作 | 我 |
| 6 | 模组 | `src/AwakeMediaService.cs`（新） | `IMediaService` 真实现 | 我 |
| 7 | 模组 | `src/AiTaskConstants.cs` | `RouteImagePortrait = "AWAKE.route.image.portrait"`，进 `AllRouteIds` + `NewRouteIds` | 我 |
| 8 | 模组 | `src/AwakeHostComposition.cs` | overrides 里挂 `Media` | 我 |
| 9 | 模组 | `src/NpcDialogueVM.cs` | +4 个 bool + `ExecuteGeneratePortrait()` | 我 |
| 10 | UI | `GUI/Prefabs/NpcDialogue.xml` | 三列改版 + 画位状态层 + `TextureWidget` | **UI 线（混编文件，动前报备）** |
| 11 | 美术 | 面板 / 切角 / 按钮三态 | 烛光调性 | 美术线 |

**第 7 条是白拿的**：`PermissionCatalog.BuildAll()` 会遍历 `AiTaskConstants.NewRouteIds` 自动生成权限项，`AwakeProviderConfiguration.ApplyProfilesAsync` 会遍历 `AllRouteIds` 自动下发 profile。**加一个 route 常量，权限页和配置下发自动跟上**，不用另写。

---

## 4. 出图端点：url + key 两栏，谁都能填

**判断：不要为 Player2 写任何东西。它只是"一个能填的 url + 一把 key"里的一行。**

### 4.1 配置形态 —— **独立一组** url + key，做法与 AI 链路那套同源

**它必须是独立一组，不能复用 AI 链路那几个控件。** 这不是洁癖，是分层硬约束：

```
模组进程（net472）  ──只能引用──▶  MarcusAwakeFramework（net472，嵌在模组里）
凭据存储 + 出网实现 ──在另一个进程──▶  MarcusAwakeProvider / MarcusAwakeRuntimeService（net8）
```

⇒ **模组进程读不到 AI 链路那把 Key**，`IProviderRuntimePort` 也只有 `Upsert*`，**没有任何回读接口**。
⇒ 生图既然要在模组侧自己出网（§2 的 B 路），就必须有自己的地址 + 自己的钥匙。
⇒ 共用一栏只会造出"**地址跟着走了、钥匙拿不到**"这种半吊子。

**做法（控件类型、归一化、保护存储纪律）与 AI 链路那套**同源**，只是配置项是新的：**

| MCM 里现有的（AI 链路） | 生图这一组（新） | 说明 |
|---|---|---|
| 服务地址 `ProviderBaseUrl` | **出图服务地址** `PortraitImageBaseUrl` | 同一个归一化函数：粘完整接口地址也接受，自动剥成 API root |
| 这是云端服务 `ProviderIsCloud` | **出图走云端服务** `PortraitImageIsCloud` | 云端才带凭据，本地不带 —— 正是实测的 Player2 双形态 |
| 输入或替换 API Key | **输入或替换出图 API Key** | 同一种弹框、同一条纪律（只写本机保护存储，不进 MCM / 存档 / 日志） |
| —— | **出图接口形状** `PortraitImageShape` | 新增：请求长什么样（§4.2） |
| 模型名称 / 拉取模型 / 测试连接 | **测试出图** `TestImageGeneration` | 生图侧不拉模型列表，改为真打一发（§4.5） |

**两种填法（实测过的是第一种）：**

| 填什么 | 效果 |
|---|---|
| 地址 = `http://127.0.0.1:4315/v1`，走云端 = **关**，key 留空 | 走玩家本机 Player2 App（免认证，花玩家自己的 joules）—— **实测打通** |
| 地址 = `https://api.player2.game/v1`，走云端 = **开**，key = dashboard 生成的 P2 key | 走 key 所属账号 |

**⇒ 不需要为 Player2 加任何字段。它只是"一个能填的 url + 一把 key"里的一行。**

> 密钥落点：`<ProgramData>/<应用名>/Awake/AwakeSecrets/image.endpoint.key`，
> Windows DPAPI（`CurrentUser`）+ 附加熵。**fail-closed**：DPAPI 不可用时拒绝保存，绝不退化成明文。
> 槽位名 `awake.image.default`，与 AI 链路的 `awake.provider.default` **各存各处**。

### 4.2 真正需要做的只有"形状"适配

url + key 解决不了的是**请求形状**——各家出图接口长得不一样。**已落两种**（`AwakeImageShape`）：

| 形状 | 请求 | 出图 |
|---|---|---|
| **Player2**（默认） | 无参考图 `POST {root}/image/generate` · `{ prompt, width, height }`；有参考图 `POST {root}/image/edit` · `{ prompt, image:"<base64>", width, height }` | `{ image: "<base64>" }` |
| **OpenAI 兼容** | `POST {root}/images/generations` · `{ prompt, size:"WxH", n:1, response_format:"b64_json"[, image:"data:image/png;base64,…"] }` | `{ data[0].b64_json }` |

**为什么先做 Player2**：base64 进、base64 出，不依赖资产上传、不依赖 project —— 与 §13「把游戏自渲的肖像当参考图」零摩擦。

**自建中转移到哪去了**：绝大多数中转暴露的就是 OpenAI 形状，所以它**归到第二行**，不再单列一种。
真有不一样的中转，是"再加一个适配器"，不是改调用方。

> **参考图字段的两种写法都在用**：Player2 的 `image` 要**裸 base64**；
> 火山方舟 / 百炼那一挂的 `image` 要**带 `data:` 前缀**。方向相反，适配器里各写各的，别串。

### 4.3 模组侧只认一个内部接口

不管后面是哪种形状，`AwakeMediaService` 对外只有一个方法：

```csharp
Task<byte[]> GenerateAsync(ImageRequest req, CancellationToken ct)
// ImageRequest: { Prompt, ReferencePng, Width, Height }
```

拼 URL、包 body、**剥 `data:` 前缀、按 magic 判格式**（§12.5 的坑）、错误分类——全在它内部。

⇒ **换服务商 = 改配置（+ 可能加一个形状适配器），不动调用方。**

> 原先那份自建中转契约（带 `schema` / `route_id` / `idempotency_key`）**仍然有效**，但降级为"形状之一"，不再是唯一形态。

### 4.4 尺寸与体积

212×360 的 PNG 约 60–300 KB，base64 后 80–400 KB —— 走进程内 HTTP 没有帧上限问题。
⚠️ 但**别假定尺寸被遵守**（Player2 的 `/image/edit` 忽略 256、直接出 1024）⇒ 落盘与布局要按**实际**像素来，不能按请求的来。

**已落地**：`AwakeImageShapeAdapter.TryReadDimensions` 直接读文件头拿真实像素（PNG 的 IHDR；JPEG 扫 SOF 段），
日志里 `size=` 打的永远是**实际**值。

### 4.5 已落地的五个零件（2026-09-14）

| 文件 | 干什么 |
|---|---|
| `src/AwakeImageEndpoint.cs` | 把 MCM 那两栏解析成生效端点；形状枚举；地址归一化 |
| `src/AwakeImageShape.cs` | 两种形状的 URL 子路径 / 请求体 / 响应解析；**剥 `data:` 前缀 + magic 判格式 + 读真实宽高** |
| `src/AwakeImageSecretStore.cs` | 模组侧 DPAPI 保护文件，fail-closed |
| `src/AwakeImageClient.cs` | 出网口：TLS 抬到 1.2、`CancellationToken` 控超时（120 s）、状态码→人话 |
| `src/AwakeImageProbe.cs` | **MCM「测试出图」按钮**：真打一发 → 落盘 → 回显路径 |
| `src/AwakeImageConfiguration.cs` | 填钥匙的弹框 |

**`AwakeImageProbe` 就是 §8 的垫脚石**：它走的是真实链路——
缓存里有现成立绘就拿它当参考图（走 `/image/edit`，顺带验 §13 那半段），没有就纯文生图。
落盘名字带 `probe_` 前缀，与正式立绘 `portrait_` 分开，免得探测产物被当成缓存命中。
**这是唯一能证明"填的 url + key 真的能用"的动作。**

> §8 的施工顺序据此推进：① 缓存 + `TextureProvider` 已完成；**② 中转契约 + `AwakeMediaService` 的"填 HTTP"部分已完成**
> （剩下的是把 `AwakeImageClient` 接到 `IMediaService` 形状上 —— §2 的 1 槽 1 行）。③ 是 UI 线。

---

## 5. prompt 从哪来（复用，不另造存放处）

| 来源 | 已有东西 |
|---|---|
| 身份 | `HeroContextProviderId`（`AiTaskConstants.HeroContextProviderId`） |
| 外观锚点 | persona 卡（`PersonaPersistenceService` / `PersonaTagRegistry`） |
| 当前处境 | `NpcDialogueContext`、`NpcMemoryOverviewBuilder` |
| 模板存放 | `AwakePromptRegistry`（已有，别再新造） |

拼装＝**外观锚点**（性别 / 年龄 / 身份 / 文化）+ **当前处境**（地点 / 季节 / 是否带伤）+ **画法模板**（半身、暖调烛光、中世纪、布面）。

用户可改：VM 上一个 `PortraitPromptText`，默认填模板；改了就重算幂等键 ⇒ 天然出新图。

---

## 6. 幂等键与缓存（顺带就是文件命名规则）

```
ModuleData/Awake/portraits/portrait_<sha256(npcKey|prompt|w|h|seed)>.png
```

- 命中即出图 —— 0 成本、0 延迟、离线也能看
- 同一 NPC + 同 prompt ⇒ 必然同一张（第二层保障见 §9.1）
- **失败不落盘** ⇒ 「重试」天然可用，不会留半张坏图

---

## 7. UI 四态 → 07 稿要素映射

07 稿的画位已经把四个状态都画全了（`.vacant` / `.ai` / `.busy` / `.err`，按钮 `.genbtn .ta / .tb`）。

Gauntlet 只认 bool ⇒ **状态机必须在 VM 里收敛成 bool**，prefab 里只写 `IsVisible`：

| VM 属性 | 对应 07 稿 | 含义 |
|---|---|---|
| `HasPortrait` | `.ai` 层 | 有图可显 |
| `IsGeneratingPortrait` | `.busy` 层 | 生成中 |
| `PortraitFailed` | `.err` 条 | 失败可重试 |
| `IsPortraitStale` | `.genbtn .tb` | 提示词改过 / 缓存被清 ⇒ 按钮显示「重新生成」否则「生成全身像」 |

命令按现有约定：prefab `Command.Click="ExecuteGeneratePortrait"` → VM `public void ExecuteGeneratePortrait()`（同 `ExecuteSend` / `ExecuteClose`）。

**范围比看上去小**：07 稿左列 40×44 小头像标注「原生肖像」，`AwakeContactCardVM.TryBuildPortrait` 已经在用 `heroVm.ImageIdentifier` 走游戏自带肖像 ⇒ **那一列不需要 AI**。只有中列 **212×360 全身像**需要。

---

## 8. 施工顺序

| 步 | 做什么 | 为什么先做 |
|---|---|---|
| ① | 缓存 + `TextureProvider` | 与后端无关；**垫脚石**：手放一张 png 进 `ModuleData/Awake/portraits/` 就应能在面板里看到 —— 先把「生产代码里显示侧也通」证掉（上一轮只验了链路，没写代码） |
| ② | 中转契约 + `AwakeMediaService` | 接口一定，这步就只是填 HTTP |
| ③ | VM 四态 + prefab 三列 | UI 线；之后才有看得见的按钮 |
| ④ | 换真后端 | 替换一个实现 |

①② 的产物在 A / B 两条路里**完全一样**，先做不会白做。

---

## 9. 风险 —— 必须认的三件事

**9.1 「同一个 NPC 反复出图要长得像同一个人」是上限所在。**
纯文生图做不到这件事。优先挑支持「角色参考 / 主体参考」的平台，或者固定 `seed` + 严格外观模板。**这条决定功能天花板，其余都是工程。**

**9.2 模组第一次有出网口**（只在 B 路上）。
net472 + 游戏内 `HttpClient`：必须显式设 `ServicePointManager.SecurityProtocol`（TLS1.2 起），必须挂 `AwakeBackgroundTask.Run` 后台跑，**绝不能在 tick / VM 命令里同步等**。

**9.3 时间是 10s~1min 级，不是 1s 级。**
`AwakeConstants.RequestTimeout` = 90s，够用；但每次调用都是钱 ⇒ 手动触发（点按钮才出）、有上限、有取消、失败可重试。UI 上必须有 `busy` 态，不然玩家会连点。

**9.4 prefab 是混编文件**（`GUI/Prefabs/NpcDialogue.xml` 属 UI 线资产）。
按项目纪律：**动前报备，谁认领谁交**。

---

## 10. 线界表

| 线 | 干的事 |
|---|---|
| **代码线（我）** | `AwakeMediaService` / `AwakePortraitCache` / `AwakePortraitTextureProvider`、route 常量、`NpcDialogueVM` 四态与命令、overrides 挂载 |
| **UI 线** | `GUI/Prefabs/NpcDialogue.xml` 三列改版 + 画位状态层 + Brush（动前报备） |
| **美术线** | 面板 / 切角 / 按钮三态（暖调烛光，见 `UI-ART-ASSET-INTERFACE-20260913.md` §4.3） |
| **框架层** | 1 槽 + 1 行（§2），需与主干线报备 |
| **Max** | 中转服务的请求/响应契约拍板 + 密钥分发策略 |

---

## 11. 复核坐标

| 事实 | 坐标 |
|---|---|
| 模组零网络代码 | `AWAKE/src/**` grep `HttpClient|WebClient|System.Net|IHttpDriver|HttpHelper` → 0 |
| Media 是死桩 | `framework/MarcusAwakeFramework/src/HostApi.cs:41,81,150` |
| Assets 是死桩 | `framework/MarcusAwakeFramework/src/HostApi.cs:44,84` |
| overrides 无 Media 槽 | `framework/MarcusAwakeFramework/src/ServiceOverrides.cs:44-55` |
| 生成接缝定义 | `framework/MarcusAwakeFramework/src/Compat/IMediaService.cs` · `ImageGenerationRequest.cs` · `GeneratedAssetResult.cs` · `AssetHandle.cs` · `AssetContent.cs` |
| 出网口在 Runtime 进程 | `framework/MarcusAwakeRuntimeService/src/RuntimeServiceHost.cs` · `ProviderWireAdapter.cs` · `ProviderRegistry.cs` |
| provider 基建（凭据/策略/限流/时钟） | `framework/MarcusAwakeProvider/src/ProviderAdapterBase.cs` |
| key 走 Runtime 保护存储 | `src/AwakeProviderConfiguration.cs`（`CredentialReference` / `SaveCredentialAsync`）· `framework/MarcusAwakeFramework/src/ProviderRuntimeApi.cs:147 IProviderRuntimePort` |
| profile 随 route 自动下发 | `src/AwakeProviderConfiguration.cs:265` 遍历 `AiTaskConstants.AllRouteIds` |
| 权限随 route 自动生成 | `src/PermissionCatalog.cs:166` 遍历 `AiTaskConstants.NewRouteIds` |
| 命令约定 | `GUI/Prefabs/NpcDialogue.xml:31,36,41` `Command.Click="Execute*"` → `src/NpcDialogueVM.cs:141,171,186,191` |
| 左列小头像走原生肖像 | `src/AwakeContactCardVM.cs:121-136` |
| 后台任务入口 | `src/AwakeBackgroundTask.cs`（`Run` / `Run<T>` / `Observe`） |
| 超时 | `src/AwakeConstants.cs:60`（90s） |

---

## 12. 接什么模型、怎么接

**判断：代码里不写模型名。我们接的是一个路由，不是一个模型。**

### 12.1 需求里只有三条是硬的

| # | 硬指标 | 谁决定 |
|---|---|---|
| 1 | 同一 NPC 反复出图要像同一个人 | §9.1 —— **决定功能天花板** |
| 2 | 能吃参考图（不是只靠文字描述） | 指标 1 的实现手段 |
| 3 | 画面调性可锁（暖调烛光 / 中世纪布面） | 与 07 稿一致 |

"画得好不好看""字写得对不对"不列 —— 那是每个模型都在卷的通用项，不构成选型理由。

### 12.2 候选（只列一手可查的）

| | **火山方舟 · Seedream 5.0** | **阿里云百炼 · wan2.7-image** |
|---|---|---|
| 模型 ID | `doubao-seedream-5-0-260128`（lite 版 `…-5-0-lite-260128`） | `wan2.7-image` / `wan2.7-image-pro` |
| 端点 | `https://ark.cn-beijing.volces.com/api/v3/images/generations` | `.../api/v1/services/aigc/multimodal-generation/generation` |
| 参考图 | `image: [...]` 数组，URL 或 base64（`data:image/png;base64,` **必须小写**） | `content` 里 **0–9 个 image 对象**，且必须恰好 1 个 text |
| 组图 | `sequential_image_generation: "auto"` + `sequential_image_generation_options.max_images` | `enable_sequential: true`，`n` 取 1–12（默认 12） |
| 调性锁定 | 靠 prompt | 额外给了 `color_palette`（3–10 色，各带占比） |
| 尺寸 | `size: "2K"` 或像素值 | `size: 1K/2K/4K` 或像素值；**有图输入时输出宽高比跟随最后一张输入图** |
| 出图编码 | `output_format: "png"`、`response_format: "url"` | PNG，返回 URL（**有效期 24h**） |
| 水印 | `watermark: false` 可关 | `watermark: false` 可关 |
| 价格 | **¥0.22/张**（文生图与图生图同价） | ¥0.2/张 量级，按地域计价 |
| 限流 | 见方舟控制台 | RPM 300 / TPM 1,000,000 |
| 地域坑 | 北京 | **华北2(北京) 与 新加坡的 key 和端点不可混用** |

两家都是国内、人民币结算、官方文档一手可查，且都**原生主打「多参考图 + 跨图一致性」** —— 正好是硬指标 1/2。

**推荐：主路由 Seedream 5.0，备路由 wan2.7-image。** 选它的理由不是"模型更强"，是三点工程属性：

1. Seedream 的参考图是 `image` **数组**，与"标准图生图"同形，中转适配更薄；
2. Seedream 是公开固定价 ¥0.22/张，成本可预算；wan2.7 按地域计价、key 绑端点，运维多一个坑；
3. **两条都留** —— 这不是"二选一"，是"主备都要"（见 12.3 ②）。

> 海外候选（Gemini 系 / GPT Image / FLUX.2 / Midjourney）**本轮不进主线**：前两者要外网与外币；Midjourney 无官方 API，走包装违反 ToS ⇒ 对"代码出图"直接出局。等主线跑起来、真要压成本时再评。

### 12.3 怎么接 —— 三条纪律

**① 契约里只有 `route_id`，没有模型名。**
§4 的请求体已经是对的：模组发 `route_id: "AWAKE.route.image.portrait"`，**用哪个模型是中转服务端的事**。回包里的 `resolved_model` 只回填给日志和 UI，模组**不据此分支**。

**② 模型映射放配置，不放代码。**
中转侧一张表就够：

```
route_id                       primary              fallback
AWAKE.route.image.portrait  →  seedream-5-0      →  wan2.7-image
```

换模型＝改配置，不发版；一路 outage 时自动落到另一路。

**③ 为什么必须这样 —— 模型的寿命比功能短。**
近一年内已经发生：Imagen 4 于 2026-08-17 退役；Seedream 4.0 → 4.5 → 5.0 三代；万相 2.6 → 2.7。
⇒ **任何把模型名写进 C# 的方案，一年内必然返工一次。**
⇒ 落点：`AwakeMediaService` 只认 routeId；模型名、端点、key 全在服务端配置里。**模组这侧对"用哪个模型"零知识。**

### 12.4 交付形态没变

模组侧要动的仍是 §2 的 B 路：**1 槽 1 行 + 一个 `AwakeMediaService`**。

**而且"不写死"要贯彻到底 —— 不只模型，连服务商也不写死：**

| 写死的 | 放到哪 |
|---|---|
| 模型名 | 配置（本小节 ②） |
| **端点 url + key** | **也是配置 —— 生图自己一组**（做法与 AI 链路同源，但**不可共用**：模组进程读不到 Runtime 侧那把 Key，见 **§4.1**） |
| 请求形状 | 一个薄适配器（§4.2），**已落两种** |

⇒ 模组这侧对"**用哪家、用哪个模型**"零知识。
⇒ 换模型、换服务商、从自建中转切到 Player2 —— **全是改配置，模组一行代码不动。**

### 12.5 Player2：一个现成的生图接口（有它就不必自己搭 §4 那层）

**判断：能 —— 而且本机已经真打通了（2026-09-14 实测），出图效果正好是我们要的。**

**实测环境**：本机 `127.0.0.1:4315` 跑着 Player2 App（`client_version 0.10.78`）。
**本地 API 不需要认证** —— `GET /v1/joules` 直接返回 `{ "joules": 806, "user_id": "019c…" }` ⇒ **模组直连 `http://127.0.0.1:4315/v1` 即可，OAuth 那一整套可以完全跳过**（只有云 `api.player2.game/v1` 才要 Bearer）。

一手依据：`https://api.player2.game/v1/openapi.json`（OpenAPI 3.1.0，69 条路径，本机实拉解析）。
本地那份在 `http://127.0.0.1:4315/v1/openapi.json` —— **两版端点一致，但本地的 schema 更细**（云端 `/image/generate` 的 requestBody 甚至是空的，本地才写明 `width/height` 范围是 128–1024、默认 512）。

```
servers: https://api.player2.game/v1
auth:    Authorization: Bearer <P2 key>
```

**两条端点，正好对应 §4 契约里的两种请求：**

| 用途 | 端点 | 入 | 出 |
|---|---|---|---|
| 文生图 | `POST /image/generate` | `prompt` · `width` · `height` | `{ "image": "<base64 PNG>" }` |
| 带参考图 | `POST /image/edit` ★ | `prompt` · **`image`（单张 base64）· `images`（数组）** · `aspect_ratio` · `width` · `height` | `{ "image": "<base64>", "mimetype" }` |

调用就长这样，没有别的：

```
POST https://api.player2.game/v1/image/edit
Authorization: Bearer <P2 key>

{
  "prompt": "…",
  "image": "iVBORw0KGgo…",          // 参考图，base64，可带 data:image/png;base64, 前缀
  "width": 768,
  "height": 1280                     // 或者给 aspect_ratio
}
```

**§13 那条正好接上**：游戏自渲的 NPC 肖像编成 base64 塞进 `image`（或 `images`）就完事 —— **不需要先上传、不需要 asset id、不需要 project**。

**实测出来的行为差异 —— 两个端点不是一套，别用同一个解析器：**

| | `POST /image/generate` | `POST /image/edit` |
|---|---|---|
| 出图格式 | **JPEG** | **PNG**（响应另带 `mimetype`） |
| `image` 字段前缀 | **带 `data:image/jpeg;base64,`** | **不带**（纯 base64） |
| `width` / `height` | 遵守（传 256 → 出 256） | **被忽略**（传 256 → 出 1024×1024） |
| 耗时 | 0.5–0.7 s | 9.7 s |
| 花费 | **10 joules** | **27 joules** |
| 吃参考图 | 否 | **是**（base64 单张 `image` 或数组 `images`） |
| 人物一致性 | — | ✅ **保持** |

**⚠️ 最大的坑：`/image/generate` 返回的 base64 带 `data:` 前缀，而文档明写 "without data URI prefix"。**

直接整串 `b64decode` **不会报错**，但 `data:image/jpeg;base64,` 里那 20 个合法字符（`dataimage/jpegbase64`）会被当成数据，**解出 15 字节垃圾顶在文件头**；又因为 20 是 4 的倍数，后面的真数据恰好落在 4 字符边界上 ⇒ **JPEG 本身没坏，只是前面多了 15 字节**。（`d a t a` → `75 AB 5A`，可逐字节验证。）

⇒ 症状：文件头既不是 `89 50 4E 47` 也不是 `FF D8 FF`，可是往后翻十几字节就是一副完整 JPEG，而 PIL 打不开、zlib/gzip 全都解不了。
⇒ **正确做法：先判 `data:` 前缀、剥掉逗号之前的部分再解码；格式不要按文档假定，按解出来的 magic 判。**（`/image/edit` 那条没有前缀 —— 两边都得能处理。）

**接它要处理的只有两件事：**

1. **`402` = 额度不足**（joules 用完），要当正常分支处理，不是异常。
2. **没装 / 没登录 Player2 App 的玩家没有这条路** ⇒ 需要兜底。装了 App 就等于免登录，**我们不用管 token**。

其余错误码按普通错误处理：`403` NSFW · `422` 内容策略 · `500`。

> `width/height` 的说明里点名 "models with custom-size support (e.g. Seed…)" ⇒ 背后代理的是 Seedream 一类。但**本次实测那个模型不吃自定义尺寸**（`/image/edit` 忽略 256、直接出 1024）。

**一致性实测 —— 这才是选它的真正理由：**

拿一张 256×256 的骑士图当参考图（base64 塞 `image`），prompt 写"加一顶金冠，脸 / 胡须 / 头发 / 盔甲 / 烛光保持完全不变" ⇒ 出来 1024×1024，**同一个人，王冠加上了**。

⇒ **§13 那条「用游戏自渲的肖像当身份参考」在这个端点上直接成立** —— 不必去碰它那套 `/assets/*` 世界-角色体系。

**附（本期不碰）**：它另有一整套 `/assets/worlds|projects/...` 内容体系（角色带 `portrait_seed`、批量表情生成、版本管理）。**那套要求把 NPC 塞进它的 world/project 模型**，与 AWAKE 现有 persona 体系是两回事 ⇒ 本期只取上面两个**无状态**端点，`/assets/*` 一概不碰。

**落到路由表**（§12.3 的映射，多一行而已）：

```
route_id                       primary        fallback
AWAKE.route.image.portrait  →  player2     →  self-relay（seedream / wan2.7）
```

**参考实现**：`tools/probe_player2_image.py`（本机实测所用的调用 + 解码，含剥 `data:` 前缀与按 magic 判格式）。

**剩余未验：**

1. **异步 job 拿不到结果**：`/image/generate_job`、`/image/edit_job` 只回 `{ job_id }`，69 条路径里只有 `/video/job/{job_id}` ⇒ **只用同步版**。
2. 计费口径：`/image/generate` 10 joules、`/image/edit` 27 joules —— 但**是否随尺寸/模型浮动未知**（128 与 256 同价，像是按次计）。
3. `/image/edit` 参考图的 base64 体积上限（本次输入 129 K 字符，通过；官方未写上限）。
4. **App 没在跑时**模组该怎么办（回退自建中转，还是提示玩家装 App）。
5. `player2-game-key`（blog 提到用于分成归属）是否必须带 —— OpenAPI 里没有这个参数。

---

## 13. 把 NPC 的 3D 肖像渲成参考图 —— §9.1 天花板的正解

§9.1 说「纯文生图做不到同一 NPC 像同一个人」。**这条现在有解，而且零件全在游戏里。**

### 13.1 两个已经存在的零件

**① 游戏本来就会渲 NPC 的 3D 像。**
`CharacterTableauTextureProvider : TextureProvider`（`TaleWorlds.MountAndBlade.GauntletUI`）可设属性：
`BodyProperties` / `EquipmentCode` / `StanceIndex` / `IsFemale` / `Race` / `ArmorColor1` / `ArmorColor2` / `CharStringId` / `IdleAction` / `CustomRenderScale` …
—— 换上某个 NPC 的 `BodyProperties` 与 `EquipmentCode`，渲出来的就是**他本人**。

**② 渲出来的纹理能落盘。**
`TaleWorlds.Engine.Texture.SaveToFile(string path, bool isRelativePath)`。

合起来 ⇒ **离线把每个 NPC 渲成一张 png**，存成「外观参考图」。这正是硬指标 2 要喂的东西。

### 13.2 怎么用 —— 参考图要有分工，不是塞一堆

| 角色 | 图从哪来 | 管什么 | **不许管什么** |
|---|---|---|---|
| 身份参考 | 游戏自渲的 NPC 肖像 | 脸型 / 发色 / 年龄 / 体型 | 姿势、背景、光照 |
| 画法参考 | 一张固定的风格样张 | 中世纪布面 / 暖调烛光 | 人物长相 |

prompt 里必须**显式写明每张图的职责，并写"不许照搬另一张"**。参考图竞争（两张图里不是同一个人 → 模型自由发挥）是身份漂移的第一大原因。

### 13.3 收益

| | 纯文生图 | 加身份参考 |
|---|---|---|
| 同一 NPC 两次出图 | 两个人 | 同一个人 |
| 提示词负担 | 要把外观特征全写死 | 外观交给图 |

**这才是「怎么接」的核心**：多参考图这个能力能不能吃满，不取决于模型选得对不对，取决于**参考图从哪来**。而我们的参考图，游戏自己会画。

> 顺带白拿一条：纯 UI 需求（比如中列立绘只是要"这个人的样子"）可以**直接渲、完全不调 AI** —— 0 成本 0 延迟。「要不要 AI」是**每个画位单独的事**，不是整块功能的事。

### 13.4 这一节的未验项

- `SaveToFile` 是否要求渲染线程 / 有效场景；能否在 Runtime 进程（无游戏渲染上下文）里跑
- 渲出来的规格（分辨率、是否带背景）够不够当参考图
- 或者改走"游戏内截帧"而非"离线段渲"

**不通也不影响主线** —— 退回"固定 seed + 严格外观模板"，功能仍在，只是天花板矮一截。

---

## 14. 与本方案早先版本的偏差（同步）

| 项 | 早先写的 | 实测后 | 为什么 |
|---|---|---|---|
| 缓存目录 | `ModuleData/Awake/portraits/` | `C:\ProgramData\Mount and Blade II Bannerlord\Awake\AwakePortraits\` | 用 `PlatformFileType.Application` 走**引擎自己的路径解析**：读纹理的 `Texture.CreateTextureFromPath` 与写文件的 `FileHelper` 共用这一套；手拼绝对路径会让两侧分叉 |
| 幂等键 | `sha256(npcKey\|prompt\|w\|h\|seed)` | 同 | 落成 `AwakePortraitCache.BuildKey`；分隔符用 `\u001f`，防边界拼接撞键 |
| 尺寸 | 212×360 | 不变 | 与 07 稿一致。注意 `UI-CONTROL-CONTRACT-20260914.md` 的 `portrait_frame 200×200` 是**另一个物件**，待 UI / 美术线对齐 |

路径一律走 `AwakePortraitCache.PathFor()`，**模组里不出现任何手拼的绝对路径字符串**。

---

## 15. 未验证项（别当成已验）

1. `MaximumFrameBytes` 的实际值，以及 A 路走图片是否为它而必须改设计
2. `Texture.CreateFromMemory` / `CreateFromByteArray` 吃不吃 PNG 编码字节、通道序
3. ~~`ServicePointManager.SecurityProtocol` 在本游戏运行时的实际默认值~~ —— **已不再依赖默认值**：
   `AwakeImageClient.EnsureModernTls()` 显式 `|= Tls12`。仍未验的是**游戏运行时里 TLS 握手是否真的成功**（要等第一次真发请求）。
4. ~~中转平台的「角色参考」能力清单~~ —— **已查，见 §12.2**；仍未验的是「用游戏自渲图当参考」的效果，见 §13.4
5. `AwakePortraitTextureProvider` 反复换图是否漏 GPU 纹理：当前**不调 `Release()`**，依据是官方 14 个 provider 无一处调它、且唯一按路径读 png 的 `OnlineImageTextureProvider` 连 `Clear` 都不重写 —— **这是推论，不是实测**
6. ~~Player2 这个生图接口~~ —— **2026-09-14 本机实测打通，见 §12.5**：本地 App 免认证直连；文生图 0.5–0.7 s / **10 joules**；带参考图的 `/image/edit` 9.7 s / **27 joules**，且**人物一致性成立**。剩余 5 条小项见 §12.5 末。
7. **生图那套 C#（§4.5）只验到"编译过 + 0 错 0 警"，没在游戏里真打过一发。**
   真正能证伪的入口就是 MCM 的「测试出图」——**在游戏里按一下**。在此之前，`HttpClient` 在 Bannerlord 运行时里的行为（代理探测、TLS、线程）都算未验。
8. `AWAKE.Tests/AWAKE.Tests.csproj` 仍是**逐个 `<Compile Include>` 列举**（110 项）。
   本轮新增的 7 个生图相关文件已手动补进清单 —— **下次再往 `src/` 加文件、且被这几个文件引用到时，必须同步补**，
   否则症状是"主构建过了、第二步 `AWAKE.Tests` 才炸 `CS0246`"，极容易误判成"构建没过"。
