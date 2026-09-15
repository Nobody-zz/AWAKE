# 功能天花板评估 · 借马库斯的代码，功能上最多能做到什么

- 日期：2026-09-15
- 出具：全局主控线（**只评估能力边界，不排批次、不改代码**）
- 缘起：Max「功能上我们能借助马库斯的代码的天花板」
- 口径：**"天花板"＝能到哪，不是现在在哪。** 原版与本地化版都由本次实测核对，不转述文档自称。

---

## 一、一句话

**借得动十二类能力；借满之后，能做出一个"完全离线可跑、AI 能真的改游戏状态且可控、能记住事、能出图能出声"的叙事模组底座。**
**但借不来三样：内容、中文检索质量、"进过游戏"这件事。**
⇒ 天花板不低，**但摸不到天花板的原因从来不是代码。**

---

## 二、十二类能力：现在有没有、借满能到哪

"原版实现"一列全部来自本次直读源码；"本地化版"一列来自 `AWAKE/framework/` 实测。

| # | 能力 | 原版有没有真实现（实测） | AWAKE 本地化版 | 借满之后的天花板 |
|---|---|---|---|---|
| 1 | 文本生成 / 流式 | 真件：OpenAI 兼容、Anthropic、Ollama、Player2 四路 ＋ 有序 fallback ＋ 模型固定 | **有**（已通） | **不锁定任何一家服务商**；换供应商只改配置，不改代码 |
| 2 | 图像生成 | 真件三路：OpenAI `images/generations`、Player2 `image/generate`、**ComfyUI 工作流**（含 `TryCancelComfyAsync` 取消） | provider 层片 1 已落 | **文生图 ＋ 图生图**（带参考图是 AWAKE 自加的 `/image/edit`）。**没有视频、没有 3D** |
| 3 | 语音合成 TTS | 真件两路：OpenAI speech、Player2 speech ＋ `VoiceProfile` 回落链（声音 id／语言／许可） | **无** | **NPC 能出声**。**没有语音识别——它不能听** |
| 4 | 本地模型（离线） | 真件 `ManagedGgufRuntime`(310 行) ＋ `GgufRuntimeProbe`；捆绑 llama.cpp b9173 Vulkan；**原版本机实测 25/25 层进显存、3.0 s** | **无** | **整条链可以在不联网、不充钱的情况下跑**（含 GPU 加速） |
| 5 | 检索 RAG | 真件：SQLite FTS5 ＋ `bm25` ＋ 访问域 scope ＋ 语料指纹 | **已有**（09-15 接通，`9ca5af7`） | **关键词命中级**。语义级接不上，见 §三.1 |
| 6 | 嵌入 / 重排 | 真件：embedding 两路（OpenAI、**Ollama** 本地）＋ 上限 32 输入/12 万浮点 ＋ 缓存。**rerank 是拿 embedding 现算的**，不是专门重排模型 | 无 | 能做相似度与排序；**精度到不了"专门 rerank 模型"那一档** |
| 7 | 资产库 CAS | 真件 `AssetEngine.cs` **728 行**：字节级验证、去重、按所有者范围列举、不透明导出收据、固定资产拒删、引用安全删除、ephemeral/session 清理 | **无** | 出图配音有地方存、有账可查、能清理 —— **这是"敢公开"的底线**，不是加分项 |
| 8 | 事件与时间线 | 真件：`DurableSpoolWriter` ＋ `RuntimeEventService`、惰性存档锚点、战役/时间线隔离、`TimelineTransferEngine`(459 行) 导出导入与未来序列分支保护 | 事件是**内存版**；无时间线导出 | **记得住 ＋ 记忆可导出、可分支** —— v0.2「记得住」的地基 |
| 9 | 命令权威 | 真件：R0–R3 分级、preflight、revalidation、一次性审批、幂等 receipt、`CommandState.Uncertain`、玩家紧急停止 | **有**（已通，2 处调用） | **AI 可以真的改游戏状态，且可控、可撤、可审计** —— 这是"AI 帮玩家做事"的许可层 |
| 10 | 上下文隔离 | 真件：`ContextPlanner` ＋ `PlayerKnown` 只读 DTO | **有**（已通） | **AI 只看得见它该看见的**，不给全知视角 |
| 11 | 诊断 ＋ 设置台 | 真件：`MarcusAIFrameworkDiagnostics.xml`（八面板）＋ `MarcusAIFrameworkAiSetup.xml`（游戏内配供应商/模型/路由/语音/本地 GGUF）＋ 滚动日志 | **无**（只有 MCM 快速配置） | 玩家**自己配得起来**、出事**查得到** —— 直接对应发布门槛「上手」 |
| 12 | SDK | 完整：manifest、schema、按版本隔离引用 DLL、双语模板、`FakeHost`、test-kit、linter | **无** | **别人能为你的模组写扩展** —— 从"一个模组"变成"一个生态" |

---

## 三、三件"看着有、其实没有"（本次实测，别被文档自称骗到）

### 1. 中文检索：是关键词命中，不是语义 —— 而且**中文没有分词**

- 原版与本地化版**都用 `tokenize='unicode61'`**（`RagEngine.cs:146`／`SqliteStorageAndRagBackend.cs:566`）。
- **实测**（本机 SQLite 3.53.1）：中文句子无空格 ⇒ 整句被当成**一个 token**。拿
  「卡拉迪亚是一块什么样的大陆……」这篇去查：

  | 查询 | 结果 |
  |---|---|
  | `MATCH '卡拉迪亚'` | **0 命中** |
  | `MATCH '德洛修斯'` | **0 命中** |
  | `MATCH '卡拉*'`（前缀） | 1 命中 |
  | `trigram` 分词器查 `'卡拉迪亚'` | **1 命中** |

- AWAKE 侧的查询串逻辑（`BuildFtsQuery`，`SqliteStorageAndRagBackend.cs:587-605`）把整句按"非字母数字"切开再用**引号做精确词**匹配
  ——中文整句切不开，等于**只能整段相等才中**。
- 退路确实有：入库时把 `Title ＋ Keywords` 用空格拼进正文（`KnowledgeService.cs:181-186`）
  ⇒ **命中"预先登记过的关键词"找得到；用自然语言问一句，基本找不到。**
- **想要"意思相近也能找到"，两样马库斯都没替你接好**：换 `trigram`（实测可用）或把**已有的 embedding 接进 RAG**（原版有 embedding，但没接进检索）。
  ⇒ 这正好是 AWAKE 的核心场景（448 档中文世界书 ＋ "NPC 提起旧事"），**属于要自己动手的地方**。

### 2. 只有"生成"，没有"理解"

全仓 **零**多模态输入（无 `image_url`／图片进模型／音频进模型）。
⇒ 它**能出图、能出声，但不能看图、不能听声**。截图给它看、语音给它听，这条路不存在。

### 3. `contract-tested` ≠ 跑通过

原版状态表自己写着末行：**`In-game smoke/stress tests: Not performed`**；六类适配器**没有一个 live 实例**。
⇒ 代码路径是真的（本次实测到 ComfyUI 生成＋取消、两家 TTS、两家 embedding），
但**没对着真服务跑过、没进过游戏**。**"写完了"和"能用"在原版身上也还没合上。**

---

## 四、借不来的四样（无论搬多少）

1. **玩法。** 原版 README 明文：**"没有扩展请求时，框架不会主动生成对话、关系、外交、剧情或战役记录。"**
   框架决定"做得到"，**内容决定"值不值得玩"**。
2. **世界知识。** 它只给通道，不给"卡拉迪亚是什么"——那是 AWAKE 世界书 448 档 ＋ 角色卡 76 张的事。
3. **中文检索质量。** 见 §三.1，这是**要自己补**的一刀。
4. **"进过游戏"。** 原版没进过，AWAKE 只有 09-14 一次 ad-hoc 跑。**这条不是代码问题，是排期问题。**

---

## 五、结论

**天花板 = 一套"完全离线可跑、玩家自带钥匙、AI 能看该看的、能记住事、能主动做事、能出图能出声、出网有规矩、出事能收缩"的叙事模组底座。**

而**摸不到它的原因从来不是代码**：
- 骨架这一层，原版已经把 12 类能力里的 11 类写好了（缺的只有"中文检索质量"和"多模态理解"）；
- 内容这一层，`AuthorSource/` 四个示范模组（165 文件）**基本未读**；
- 验收这一层，**游戏内零判据**。

⇒ **三者里，只有第二、第三是 AWAKE 的活。**

---

## 出处（可核）

- 原版：`%TEMP%\marcus_repo`——`src/MarcusAIFramework.Companion/ProviderRouter.Operations.cs`（`EmbedAsync:66` / `RerankAsync:79` /
  `GenerateImageAsync:123` / `SynthesizeSpeechAsync:325` / `GenerateComfyImageAsync:594`）、`RagEngine.cs(210 行)`、
  `AssetEngine.cs(728 行)`、`TimelineTransferEngine.cs(459 行)`、`ManagedGgufRuntime.cs(310 行)`、
  `Core/DurableSpoolWriter.cs`、`GUI/Prefabs/MarcusAIFramework{AiSetup,Diagnostics}.xml`、`sdk/`、
  `docs/IMPLEMENTATION_STATUS.md`、`README.md`
- 本地化版：`AWAKE/framework/MarcusAwakeStorage/src/SqliteStorageAndRagBackend.cs`（`:566` tokenizer／`:587-605` 查询串／`:151` 只支持关键词）、
  `AWAKE/src/KnowledgeService.cs:181-186`、`AWAKE/framework/MarcusAwakeStorage/src/`（仅 4 文件）、
  `AWAKE/docs/AUDIT-MARCUS-CAPABILITY-LIVENESS-20260915.md`、
  `AWAKE/docs/REPORT-MARCUS-FRAMEWORK-AND-F015-INTERFACE-20260915.md`
- 中文检索实测脚本：`AWAKE/docs/_probe_fts5_cjk_20260915.py`（**自带阳性对照**：同一批语料在 `unicode61` 与 `trigram` 下各跑一遍；本机 SQLite 3.53.1）
