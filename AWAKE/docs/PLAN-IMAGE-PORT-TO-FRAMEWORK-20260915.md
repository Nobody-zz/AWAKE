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
| `Companion/AssetEngine.cs`（728 行） | 资产 CAS | `MarcusAwakeStorage/src/` | ⏳ 片 3 |
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

**片 1（本轮）· Provider 侧 Player2 媒体适配器 + 离线验台**
- 范围：**`MarcusAwakeProvider` 单模块**。新增 `ProviderBinaryResult`；`ProviderRouter` 改 `partial` ＋ 新建 `ProviderRouter.Media.cs`（三路：`player2` / `openai-compatible` / 兜底抛 `media.image_adapter_unsupported`）。
- **不碰** `MarcusAwakeTransport`、不碰 `MarcusAwakeRuntimeService`、不碰 `AWAKE/src`。
- 为什么先它：**风险最低（单模块、纯库、无进程边界）、可离线证死、复用已测逻辑**（`AwakeImageShape` 的 17 条用例已覆盖线上形状）。
- 产出：适配器 + 一个离线验台（照 `tools/image-shape-harness` 的样子，零替身通编 `src/**/*.cs`）。

**片 2 · 打通线**：Transport 加 media 消息 → `RuntimeServiceHost` 加分派 → `HostApi` 换真实现。**这一步才碰共享协议**，也是最容易返工的一步。

**片 3 · 资产落点**：`AssetEngine` 搬进 `MarcusAwakeStorage`。

**片 4 · 调用方切换**：`AWAKE/src` 改走 `Host.Media`；`AwakeImage*.cs`（6 文件、1200+ 行）降级为回退路径或退役。

**ComfyUI 排在片 1–4 之后。**

---

## 5. 验收判据

**片 1（离线，可证死）**
1. 验台**编译通过**——含 `AWAKE.Tests.csproj` 那一步。它是**逐个 `<Compile Include>` 列举**，新增 `src/` 文件必须同步补 include，否则主构建过、第二步才炸 `CS0246`（容易误判成「构建没过」）。
2. 四类形状用例：`player2` 无参考图走 `/image/generate`、有参考图走 `/image/edit`；`openai-compatible` 走 `/images/generations`；未知 adapter 抛 `media.image_adapter_unsupported`。
3. **`Idempotency-Key` 头必须在**——判据要查**请求头**，不是只查 body。
4. **前缀 + 上限**：喂 `data:image/jpeg;base64,` 前缀样本，断言解出字节数 **== 原图字节数**（不是 +15）；喂超限样本，断言抛 `media.provider_payload_too_large`。
5. **变异检验**：至少改坏两处（① 去掉剥前缀 ② 把 8 MB 上限改成 `int.MaxValue`），确认对应用例**会红**。**全绿不算证据。**

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
