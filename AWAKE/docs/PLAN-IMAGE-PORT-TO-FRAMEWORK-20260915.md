# 生图适配器 · 搬回框架（Player2 先行）· 方案

**日期**：2026-09-15
**线**：模组主体（代码）线
**上游**：
- `AWAKE/docs/PLAN-AI-PORTRAIT-IMAGE-20260913.md` —— A/B 双路方案，已落地 6 个生图文件（下称「旧方案」）
- `AWAKE/docs/AUDIT-EXTERNAL-PNG-TO-GAUNTLET-20260913.md` —— 显示侧链路（已验通）
- 原版仓库 `github.com/Alexander-Dieros/MarcusAIFramework`（已获源码授权；本机浅克隆 `%TEMP%\marcus_repo`，HEAD `c3992fa`）

---

## 判断

**框架原本就有生图，是本地化时裁掉的。这次不是造，是把它搬回自己原来的位置。**

---

## 0. 一句话背景

原版 `MarcusAIFramework` 的媒体能力是**六类适配器**里的一类——`openai-compatible` / `anthropic` / `ollama` / **`player2`** / **`comfyui`** / `managed-gguf`（`ProviderPresets.cs` 共 11 条预设）。生图走 `media.image.generate.request` 帧，出图结果进资产库（CAS）。

AWAKE 本地化时只留了纯文本三路。**媒体消息类型、`ProviderBinaryResult`、原版 728 行的 `AssetEngine` 全没了。** 而 `MarcusAwakeFramework/src/Compat/` 里的契约类型（`IMediaService` / `ImageGenerationRequest` / `TtsGenerationRequest` / `GeneratedAssetResult` / `IAssetService` / `AssetHandle` …）**保留下来了**——只是实现是空壳：`HostApi.cs:81` 直接 `media = new UnavailableMediaService();`，`:1053` 返回 `TaskFailure("media","image")`。

所以现状是：**契约在、端口空、线断了。**

---

## 1. 现状取证（本地化版实测，六层）

| 层 | 文件 | 实有 | 缺 |
|---|---|---|---|
| 协议 | `MarcusAwakeTransport/src/ProviderProtocolContract.cs` | 注册 **6** 条 provider 消息（profile_upsert / credential_upsert / profile_remove / models / complete / stream） | **0** 条 media 消息 |
| 契约 | `MarcusAwakeProvider/src/ProviderContracts.cs` | `ProviderKind` **三值**；`IProviderAdapter` **四方法**（ListModels / TestConnection / Complete / Stream） | 无 `Player2`；接口无媒体方法 |
| 路由 | `MarcusAwakeProvider/src/ProviderRouter.cs` | `ListModels / TestConnection / Complete / StreamAsync` | 无 `GenerateImageAsync` |
| 宿主 | `MarcusAwakeRuntimeService/src/RuntimeServiceHost.cs` | `:1719` 分派 **5** 路、`:1767` 响应消息名、`:1776` 响应 schema | 无 media 路 |
| 存储 | `MarcusAwakeStorage/src/` | KV / timeline / RAG | **无资产 CAS**（原版 `AssetEngine` 未搬） |
| 模组侧 | `MarcusAwakeFramework/src/Compat/` | 契约类型**齐全**（47 个文件，含全套媒体/资产契约） | 实现是 `UnavailableMediaService` 空壳 |

> 一句话：**上层契约是全的，下层实现是空的。** 这决定了 port 的形状——**不用改契约，只补实现**。

---

## 2. port 映射表

**关键发现：原版的媒体方法不在 `IProviderAdapter` 上。** 它是一个 `internal sealed partial class ProviderRouter` 的**分部方法**（`ProviderRouter.Operations.cs`），按 `connection.Adapter` **字符串**分派。所以 port **不扩接口**——否则三个既有适配器都得被迫实现媒体方法。

| 原版路径 | 内容 | 本地化落点 | 本轮 |
|---|---|---|---|
| `Companion/ProviderRouter.Operations.cs:123` | `GenerateImageAsync(ImageGenerateRequestPayload)`：入参校验（prompt ≤16000、宽高 128–4096 或全 0、幂等键）→ 按 adapter 分派 → 结果入幂等缓存 | `MarcusAwakeProvider/src/ProviderRouter.Media.cs`（`ProviderRouter` 改 `partial`） | ✅ |
| 同上 `:541` | `GeneratePlayer2ImageAsync`：`POST v1/image/generate`，Bearer，body `{prompt,width,height}`，读 `image` | 同上 | ✅ |
| 同上 `:497` | `GenerateOpenAiImageAsync`：`POST v1/images/generations`，body `{model,prompt,size[,response_format]}`，读 `data[0].b64_json \| image \| url` | 同上 | ✅（顺带，成本低） |
| 同上 `:754` | `BinaryFromBase64`：剥 `data:` 前缀 → 解码 → **8 MB 上限**（`MaximumProviderAssetBytes`） | 同上 | ✅ |
| 同上 `:926` | `ProviderBinaryResult(Content, MediaType, ResolvedModel)` | `MarcusAwakeProvider/src/ProviderBinaryResult.cs` | ✅ |
| `Companion/ProviderPresets.cs` | 11 条预设，其中 `player2` → `https://api.player2.game/v1` | `MarcusAwakeRuntimeService/src/ProviderRegistry.cs` | ⏳ 片 2 |
| `Shared/ProtocolContracts.cs:550 / :564` | `ImageGenerateRequestPayload`、`ProviderAssetResponsePayload` | `MarcusAwakeTransport/src/` | ⏳ 片 2（**碰共享协议**） |
| `Companion/CompanionConnection.cs:601` | `RunImageAsync`：收帧 → 生成 → `ImportGeneratedAsync` 入 CAS → 回帧 | `MarcusAwakeRuntimeService/src/RuntimeServiceHost.cs` | ⏳ 片 2 |
| `Companion/AssetEngine.cs`（728 行） | 资产 CAS | `MarcusAwakeStorage/src/` | ✅ 片 3（E2） |
| `MarcusAIFramework/Core/GovernedServices.cs:150` | `GovernedMediaService`：校验 RouteId / CloudExportClassification → `ensurePersistentSession` → 转桥 | `MarcusAwakeFramework/src/HostApi.cs:1053` 换真实现 | ⏳ 片 2 |
| `MarcusAIFramework/Companion/CompanionClientBridge.cs:507` | 模组侧发帧 / 收帧 | 同上 | ⏳ 片 2 |

---

## 3. 三条 AWAKE 要带过去的差异（port ≠ 照抄）

1. **`/image/edit`（带参考图）是 AWAKE 自加的，原版没有。** 原版 Player2 只有 `/image/generate`。`AwakeImageShape.cs:80` 的 `hasReference ? "/image/edit" : "/image/generate"` 是 AWAKE 实测出来的——**这条必须带过去**，否则带参考图的路径直接断掉。
2. **原版带 `Idempotency-Key` 请求头，AWAKE 的 `AwakeImageClient` 没有。** port 以原版为准：框架有幂等结果缓存，缺这个头等于缓存形同虚设。
3. **原版返回 `ProviderBinaryResult`（字节流），AWAKE 是直接落盘。** 字节流才能继续走 CAS、内存里 sniff；落盘不行。**以原版为准。**

另有一条**原版没有、AWAKE 实测出来的**，要补进去：

> **`data:` 前缀不可信。** Player2 文档说返回裸 base64，实测带前缀；而 `data:image/jpeg;base64,` 这 20 个字符**恰好全是 base64 合法字符**，不剥前缀就会多解出 **15 字节垃圾**顶在文件头。原版 `BinaryFromBase64` 的剥前缀逻辑是对的，但它**没有「实到字节 sniff magic」这一步**——这步是 AWAKE 的（`SniffFormat` / `TryReadDimensions`），要接上。

---

## 4. 分片顺序

**原则**：先补「不碰共享协议」的那一层，把适配器形状和判据做死；协议层最后碰。

**片 1 · Provider 侧 Player2 媒体适配器 + 离线验台**
- 范围：**`MarcusAwakeProvider` 单模块**。新增 `ProviderBinaryResult`；`ProviderRouter` 改 `partial` ＋ 新建 `ProviderRouter.Media.cs`（三路：`player2` / `openai-compatible` / 兜底抛 `media.image_adapter_unsupported`）。
- **不碰** `MarcusAwakeTransport`、不碰 `MarcusAwakeRuntimeService`、不碰 `AWAKE/src`。
- 为什么先它：**风险最低（单模块、纯库、无进程边界）、可离线证死、复用已测逻辑**（`AwakeImageShape` 的 17 条用例已覆盖线上形状）。
- 产出：适配器 + 判据。**验台位置见 §8**——不新建 harness，扩既有的 `MarcusAwakeProvider/tests`。

**片 2 · 打通线**：Transport 加 media 消息 → `RuntimeServiceHost` 加分派 → `HostApi` 换真实现。**这一步才碰共享协议**，也是最容易返工的一步。拆成三段做，每段单独取绿：

**片 2a · 生图走 Route**（**已完成（E2）**，见 §5）
- `provider.image.v1` 登记为 **provider request** ⇒ 白拿幂等台账、取消、截止、错误映射；运行时反射调 `ProviderRouter.GenerateImageAsync`；出图字节**入 CAS**，回帧**只带 `AssetHandle`**。
- 新增 `MarcusAwakeRuntimeService/src/ProviderImageBridge.cs`、`MarcusAwakeFramework/src/RuntimeServiceClient.Media.cs`、`MarcusAwakeRuntimeService/tests/MediaClientTests.cs`。
- **媒体调用方只有逻辑 route**：`ImageGenerationRequest` 不暴露 ProfileId/ProviderId，而 `ProviderProfileEntry.Matches` 要求 ProfileId 逐字相等、anchor 要求 ProviderId 逐字相等 ⇒ 新增 `MatchesRoute` / `DescribeImageCandidates`（**只比 owner/campaign/timeline/session/RouteId**）。逻辑 route 就是身份，profile/provider 由运行时解析。
- **字节绝不回游戏进程**：立绘缓存落在游戏进程的 Application 数据桶，运行时算不出那条路径，而单帧只有 256 KB。

**片 2b · 资产分块读回**（**已完成（E2）**，见 §5）
- 新消息 `asset.read` / `asset_result`，**存储家族 ⇒ payload 不带 `schema`**；单块上限 `ProtocolConstants.MaxAssetChunkBytes = 65536`（64 KiB 原始字节 base64 后约 87 KiB，卡在 128 KiB 载荷上限内）。
- 每块回带**完整 `AssetHandle` 身份字段**，客户端逐块校验 asset_id / offset / 身份一致性，拼完再比总长度。
- `HostApi` 的 `assets` 换成 `runtime as IAssetService ?? new UnavailableAssetService()`；**其余 7 个 `IAssetService` 方法本片显式回 typed `Unsupported`**（不静默）。

**片 2c · 云导出分类**（**框架侧已完成（E2）/ mod 侧只有 E1**，见 §5）
- 片 2a 曾用「非 `none` 一律拒」当占位。片 2c 把它换成**受约束接受**：分类必须非空、且是 ≤64 字节的小写标识符，否则本地拒且**不打 HTTP**。
- **真正的门在 mod 侧，且与文本对话共用同一份实现**：新增 `AWAKE/src/CloudExportGate.cs`，`AiTaskGateway` 改为委托；新增分类 `npc_persona` 与配置开关 `AllowCloudExportNpcPersona`（**默认关闭**，不随玩家状态一起放开）；立绘路径接上这道门。
- **为什么框架不自己当门**：框架的 `IPermissionService` 默认是 `UnavailablePermissionService`，而 AWAKE 用的是自己那套 `PermissionCatalog`/`PermissionGate`；文本路径也是「mod 跑门、框架只承载分类」。让框架当门会把合法调用全拒掉，反而逼调用方用 `none` 撒谎。

**片 3 · 资产落点**：`AssetEngine` 搬进 `MarcusAwakeStorage`。**已完成（E2）**，见 §5。

- 新增 `MarcusAwakeStorage/src/{AssetStoreOptions.cs, AssetContentInspector.cs, ContentAddressedAssetStore.cs}`；`ContentAddressedAssetStore : IAssetService` 八方法全实现，落地形态照设计大纲 §6 的文件树（`objects/<hash 前 2 位>/<hash>`、`metadata/<asset-id>.json`、`temp/`、`quarantine/`、`exports/`）。
- 与原版 `AssetEngine` 的**刻意偏离**：① **不引 SQLite** —— §6 的形态本来就是文件树，于是资产库不推 `SchemaVersion`，也能脱离数据库单独构造、单独测；② **格式判定不复用 `ProviderImageMedia.SniffFormat`** —— Provider 只引用 Transport、Storage 只引用 Framework，跨依赖图复用会把 Provider 拉进 Storage 的引用闭包，所以 `AssetContentInspector` 是第二道防线。
- **与片 2 的接缝（本片新发现的硬约束）**：`IAssetService.ReadAsync` 返回的 `AssetContent` 是**整包字节**，而协议单帧上限只有 256 KB（`ProtocolConstants.MaxFrameBytes`）且**没有分块机制** ⇒ 片 2 必须给「资产读回」加分块，否则 PNG 立绘根本过不去。

**片 4 · 调用方切换**（**mod 侧只有 E1**，见 §5）：`AWAKE/src` 改走 `Host.Media` + `Host.Assets`；`AwakeImage*.cs`（6 文件、1200+ 行）**保留为回退路径**。
- 新增 `AWAKE/src/AwakePortraitGenerator.cs` = 立绘生成的**唯一入口**，顺序固定：**先过云外发门 → 再选路**。首选框架路（`Host.Media` 出图 → `Host.Assets` 取字节；钥匙与出网都在运行时进程里），框架路**不可用**时退回 B 路。
- **退回的边界写死了**：只有「这条路根本没法驱动」才退（运行时端口缺失 / 模型名缺失 / 凭据或 profile 注册失败 / 组合异常）。**框架路跑过但失败（provider 报错、策略拒绝）不退回** —— 再打一发 B 路会双倍消耗额度，还会把策略拒绝伪装成网络抖动。每次退回都写日志说明原因，不静默降级。
- **退回不构成绕过治理**：门在选路之前就跑完，两条路共用同一个判决；`NpcDialogueVM` 不许直连 `AwakeImageClient`。
- 新增 MCM 栏 `PortraitImageModel`（Order=4，其后各项顺延）：框架 profile 的 `default_model` **不能为空**，而 AWAKE 原本压根不发模型名。Player2 形状不认模型名（给非空占位）；OpenAI 兼容形状必须玩家填，留空 ⇒ 退回 B 路（**不猜模型名**）。

**ComfyUI 排在片 1–4 之后。**

---

## 5. 验收判据

**片 1（离线，可证死）**
1. 验台**编译通过**。本片**不碰 `AWAKE/src`**，所以「`AWAKE.Tests.csproj` 是逐个 `<Compile Include>` 列举、新增文件必须同步补 include」这条**片 1 用不上**（`MarcusAwakeProvider.csproj` 是 `src\**\*.cs` 通配）；它适用的是**片 4**（改 `AWAKE/src` 时）。
2. 四类形状用例：`player2` 无参考图走 `/image/generate`、有参考图走 `/image/edit`；`openai-compatible` 走 `/images/generations`；未知 adapter 抛 `media.image_adapter_unsupported`。
3. **`Idempotency-Key` 头必须在**——判据要查**请求头**，不是只查 body。
4. **前缀 + 上限**：喂 `data:image/jpeg;base64,` 前缀样本，断言解出字节数 **== 原图字节数**（不是 +15）；喂超限样本，断言抛 `media.provider_payload_too_large`。
5. **变异检验**：至少改坏两处（① 去掉剥前缀 ② 把 8 MB 上限改成 `int.MaxValue`），确认对应用例**会红**。**全绿不算证据。**

**片 3（离线，可证死）**
1. 判据 6 条进既有验台 `MarcusAwakeStorage/tests`（新增 `AssetStoreTests.cs`，已补 `<Compile Include>`；**该测试工程是显式清单，新增文件必须手改 csproj**）：`asset_import_read_dedup` / `asset_rejects_and_quarantine` / `asset_scope_ownership_and_pin` / `asset_list_paging_and_cleanup` / `asset_export_and_guards` / `asset_corruption_boundaries`。
2. **实测 `PASS ALL`（12/12，含既有 6 条）**；`dotnet build -c Release -m:1 -nodeReuse:false` 0 错 0 警。
3. **变异检验：五处全红，且每处只红对应的那一条**（每轮改坏后逐字节还原，还原后 SHA256 与绿版一致 `80F464E9C427D4C1F34242F3F5C5CF7D485D284BC622F5A805703C44B5308451`）：① 去掉内容格式校验 → `asset_rejects_and_quarantine`；② 去掉「有未读元数据就不回收对象」→ `asset_corruption_boundaries`；③ 去掉引用安全删除 → `asset_import_read_dedup`；④ 去掉所有者范围校验 → `asset_scope_ownership_and_pin`；⑤ 原子写不覆盖（`File.Move(staging, target, true)` 去掉 `overwrite`）→ `asset_scope_ownership_and_pin`。
4. **这一片抓到一个真缺陷。** 早先 `WriteAtomically` 写的是 `File.Move(staging, target)` 并用 `catch (IOException)` 吞掉：**新建文件时成功，改写既有文件时每一次都被静默丢弃** —— 钉住一条资产再读回来，`Pinned` 还是 `false`。判据 3 里那句「钉住必须能在元数据里看见」把它抓出来了。这个缺陷编译得过、新建路径也跑得通，**只有会红的门才看得见它**。

**片 2a / 2b / 2c（离线，可证死）**
1. 判据进 `MarcusAwakeRuntimeService/tests/MediaClientTests.cs`（**该测试工程是显式 `<Compile Include>` 清单 + `Program.cs` 开关表，两处都要手工登记**），跑法 `--media-client`。
2. **实测 `MEDIA_CLIENT 12/12 PASS`**；对照组 `RAG_CLIENT 9/9 PASS`（无回归）；`dotnet build -c Release -m:1 -nodeReuse:false` 0 错 0 警（只剩 `NU1900` 离线警告）。
3. 判据清单：端到端出 handle + CAS 落盘（路径/字节数/SHA256 全对）／同字节去重不新增对象／空 prompt 在任何 HTTP 之前本地拒／合法分类**放行并真的到达 provider**／空分类本地拒且不打 HTTP／非法分类本地拒且不打 HTTP／未知 route 由运行时 typed 拒且不落库／provider 401 映射成 `Denied`／资产分块读回字节逐字节一致（且断言 fixture **大于单块**，否则判据覆盖不到分块）／未知 asset_id ⇒ `asset.not_found`／空白 asset_id ⇒ `asset.asset_id_required`／七个未实现方法逐个 typed `Unsupported`。
4. **变异检验（五处，均已逐字节还原复绿）**：① 把严格 `TryReadScope` 插回 image 分派之前 → 3/6 FAIL `provider.provider_schema_mismatch`；② `ProviderRegistry.ReadImage` 翻末位字节 → 4/6 FAIL `content_hash_invalid`；③ `done` 恒真 → 9/10 FAIL `asset.byte_length_mismatch`；④ 切片翻 1 bit → 9/10 FAIL `asset_read_bytes_corrupt`；⑤ 分类格式校验恒真 → 11/12 FAIL（malformed 那条）；去掉空分类守卫 → 11/12 FAIL（blank 那条）。
5. **这一片抓到两个真缺陷。**
   - `ProviderWireAdapter.TryParse` 里 `TryReadScope` 排在 image 分派**之前**，而媒体契约只暴露逻辑 route ⇒ **每个真调用都被 `provider.provider_schema_mismatch` 拒掉**。
   - **加一条协议消息要改六处白名单，不是两处**：`MarcusAwakeTransport/src/ProtocolValidation.cs` 的 `ValidateMessageType` / `IsTaskMessageType` / `IsResponseMessageType`，加 `MarcusAwakeRuntimeService/src/RuntimeServiceHost.cs` 的 `IsKnownRequestMessage` / `IsTaskMessage` / `RequiredCapabilityForMessage`。漏任一处都在 `ProtocolCodec.SerializeEnvelope` 抛 `ArgumentException`，被客户端包成 `runtime.provider_call_failed/InternalFailure`，**真因完全看不出来**（这次连踩两次）。
6. **mod 侧（片 2c 后半）只有 E1。** `AWAKE` 没有自己的离线验台（无 `Awake.SdkSmoke` 工程），`CloudExportGate` / `npc_persona` / `AwakeConfig.AllowCloudExportNpcPersona` / `NpcDialogueVM` 那条门**只有「编译通过」这一级证据**，真行为必须等 E4。**不许把它说成已验过。**
7. **跑判据必须一次性提权**（沙箱禁命名管道，不提权一律 `client_start_failed`）；跑前把三个环境变量指向临时目录，且**在构造客户端之前**设。

**片 4（mod 侧只有 E1；框架侧新增的两处白名单修正是 E2）**
1. **框架侧修掉了「形状白名单三处同源、只改一处」这个真缺陷。** `player2` 这条形状在片 1 就进了适配器工厂，但**协议层压根到不了它**：白名单在三个地方各写了一遍 —— `MarcusAwakeTransport` 无、`RuntimeServiceHost.TryValidateProviderPayload`（准入）、`ProviderWireAdapter.TryParseProfileUpsert`（线解析）、`ProviderProfileRequest` 构造器（框架客户端）。**只改前两处仍然红**，第三处补上才通。
2. 新增判据 `media_image_player2_profile_kind_is_accepted_end_to_end`：注册 `provider_kind="player2"` 的 profile → 走 Player2 形状出图 → 断言 handle 的 `content_hash`/`media_type` 与假端点一致、且请求路径含 `image/generate`。**这条判据的红→绿就是上面那个缺陷的取证**（首跑 `player2_profile_rejected:provider.provider_schema_mismatch`，逐个补白名单后转绿）。
3. 另修 `Player2Provider` 缺 5 参构造器：`ProviderRegistry.CreateAdapter` 只认「参数个数 == 5」的那一个构造器（`OpenAiCompatibleProvider` 也是同一套），原来 Player2 只有 6 参版 ⇒ 反射造不出来。
4. **mod 侧（`AwakePortraitGenerator` / MCM 新栏 / `NpcDialogueVM` 改线）只有 E1（编译通过）。** `AWAKE` 没有自己的离线验台，这条改动**一次都没红过**，真行为只能等 E4。**不许说成已验。**
5. 实测：`MEDIA_CLIENT 13/13 PASS`、`RAG_CLIENT 9/9 PASS`、Storage `PASS ALL`（连跑 3 次稳定）、Framework `PASS ALL: 12` + `P3D-A2 6`、Provider `PASS ALL`、Transport `PASS_COUNT=7 FAIL_COUNT=0`、AWAKE 主工程 1.4.8 构建 0 错。

**片 2–4**：一律以**游戏内**为准（见 `docs/AWAKE-ROADMAP.md`「现状」节——五条线一次都没进过游戏）。**离线全绿不算过版。**

---

## 6. 与旧方案（`PLAN-AI-PORTRAIT-IMAGE-20260913.md`）的关系

旧方案结论是 **A/B 双路，推荐先 B**：
- **A** ＝ 复用 Runtime Provider 链 —— **就是本文的 port 目标**
- **B** ＝ 模组内直连中转

旧方案选 B 的理由是「形态没定就上协议锁，定错一次是五个工程返工」，并明文写着「**端口形状照 A 写**，将来搬进 Runtime 就是替换实现、不动调用方」。

**本文就是那个「将来」。**

而且取舍变了，原因是一个旧方案当时**没有的前提**：**原版源码现在拿到了**。旧方案成文时手上只有本地化版（媒体已被裁），所以「搬回 A」看起来等于「重建 A」，代价高得离谱；现在 A 的实现是现成的，port 的成本远低于旧方案当年的估价。

⇒ 关系一句话：**旧方案是 B 路的设计与落地记录（6 个文件、17 条用例，仍然有效，并且是片 1 判据的来源）；本文是 A 路的 port 方案（旧方案承诺的那个「将来」）。** 旧 B 路代码在片 4 之前不动，片 4 之后也**保留为回退路径**。

---

## 7. 不做什么

- **不做 TTS。** 原版有 `media.tts.request` ＋ Player2 / OpenAI 两路语音，但 AWAKE 现在没有语音需求。片 1 不写，等有需求时用同一套搬法。
- **不做 `managed-gguf`。** 那是本地 GGUF（LLamaSharp ＋ `llama.dll` / `ggml*`），要有模型文件才有意义，本轮不碰。
- **片 1 不碰共享协议。** 哪怕「顺手加一条消息类型」看着很自然——协议一动，`ProtocolConstants` / 契约表 / 严格校验 / 帧夹具全都要跟，那是片 2 的活。

---

## 8. 片 1 落地记录（09-15，提交 `8a947ce`）

**13 个文件、+1210 行，全部在 `AWAKE/framework/MarcusAwakeProvider/` 内。** 下游四个框架工程编译零警零错；Framework 测试 PASS ALL、RuntimeService **19/19**、Provider 自身 **36/36**（20 条基线 + 16 条新增）。

### 落点（与 §2 的差异）

| §2 原定 | 实际 | 为什么改了 |
|---|---|---|
| 新建 `tools/` 下的离线验台 | 扩既有的 `MarcusAwakeProvider/tests` | 那个工程**本来就是**零替身、通配编译 `*.cs`、带 20 条基线的测试台 —— 再建一个只是复制它。前提条件（测真代码、不要替身）已经满足 |
| 新建 `ProviderBinaryResult.cs` 单文件 | 并入 `ProviderImageContracts.cs` | 契约、能力接口、字节鉴定三样是一组，分三个文件反而难找 |
| `ProviderRouter.Media.cs` ＋ router 改 partial | 同左，另加 `ProviderAdapterFactory.CreateImage` | 路由靠工厂过滤候选，工厂得能回答「谁会生图」，且**不抛错**（不会生图是正常情况） |

### 三条刻意偏离原版的地方

1. **不走「按 adapter 名字符串 switch + default 抛错」。** 原版是 `case "player2"` / `case "openai-compatible"` / `default: throw`。这里改成能力接口 `IProviderImageAdapter` + 工厂过滤 —— 类型系统替你记住谁能生图，而不是靠一个字符串常量对得上。
2. **不做 `url` 下载。** 原版在 OpenAI 形状里支持「响应只给 url 就去取回来」。**本次不做**，只认内联 base64；只给 url 时明确报 `media.image_url_unsupported`。理由：跟着响应里的地址去取，会绕开 `ExactOriginEndpointPolicy` 那条同源规矩，属于**该单独决定的开口子**，不该在 port 里顺手带。AWAKE 两条实际路径都在请求体里显式要了 `b64_json`，这条口子现在没有需求。
3. **8 MB 上限从常量变成实例参数**（`maxImageAssetBytes`，默认 8 MB）。好处不只是可配 —— 它让「超限」这条判据能用 **32 字节**的限额测出来，不用真去造一张 8 MB 的图。

### 一处诚实更正（写进注释了）

§3 里我记的「`data:` 前缀会多解出 15 字节垃圾」**只在解码器对标点宽容时成立**。已核算：去标点后剩 `dataimage/jpegbase64`，正好 **20 字符、全为 base64 合法字符、整除 4** ⇒ 解出 **15 字节**，算术没问题。但**.NET 的 `Convert.FromBase64String` 是严格的**，碰到 `:` 直接抛 `FormatException`。所以本模块的症状是「报 base64 非法」，不是「多 15 字节」。**两种症状，同一个根因：前缀没剥。** 注释按这个口径写。

### 变异检验（三处，全部回红后回滚，零残留）

| 改坏哪里 | 预期 | 实到 |
|---|---|---|
| 不剥前缀 | 剥前缀判据红 | **2 条红**（`..._prefix_stripped_exactly`、`..._sniffing_and_dimensions`）|
| 拆掉字节上限 | 上限判据红 | **1 条红**（`image_asset_limit_enforced`）|
| 不做 sniff、直接假定 `png` | 媒体类型判据红 | **2 条红**（`..._follows_bytes_not_declared_mime`、`..._prefix_stripped_exactly`）|

### 挂账（片 2 处理）—— **已清（E2）**

**`Player2Provider` 的连接检查走基类实现，会给 `TextGeneration = Unverified` —— 而它根本不提供文字。** 根因两条：基类 `TestConnectionAsync` 不是 `virtual`（覆写不了）、`ProviderCapabilityId` 里没有 `ImageGeneration`。

两条都已修：
1. `ProviderAdapterBase.TestConnectionAsync` 改成 `virtual`（带注释说明为什么：**只提供一部分能力的适配器必须能纠正基类的默认假设**）。
2. `ProviderCapabilityId` 追加 `ImageGeneration`（**追加在末尾**，插中间会把既有取值整体挪位）。
3. `Player2Provider` 覆写 `TestConnectionAsync`，如实报：`ModelDiscovery = Available`、`ImageGeneration = Unverified`（适配器提供它，但这次连通性检查**并没有真去出一张图**，不能替它宣称「已验证」）、`TextGeneration`/`Streaming`/`Usage`/`StructuredOutput` 全 `Unsupported`。

判据：`MarcusAwakeProvider/tests` 新增 `player2_connection_report_does_not_claim_text_capability`（**该工程 `EnableDefaultItems=false` + `Program.cs` 开关表，两处都要手工登记**）。变异检验：把 `TextGeneration` 改回 `Unverified` ⇒ 该条 FAIL（`实得 Unverified`），已还原复绿。Provider 套件 `PASS ALL`。
