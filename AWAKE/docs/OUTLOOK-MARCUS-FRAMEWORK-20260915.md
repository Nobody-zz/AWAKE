# 功能前景评估 · 马库斯框架迁移到手之后

- 日期：2026-09-15
- 出具：全局主控线（**只评估方向，不改代码、不改契约、不排批次**）
- 缘起：Max「基于今天迁移补全马库斯框架功能之后，重新给我评估一下功能前景」

---

## 一、一句话判断

**原版到手，改变的不是"能做什么"，是"做什么要花多少"。**

底座那一层从「**要造**」变成「**要搬 ＋ 接线**」——这是实打实的提速。
但「**玩家看得见什么**」一点没变：天花板仍然卡在同一处——**游戏内零验证**，加上玩法侧那 8 个面没接线。
⇒ 所以前景的正确读法不是"框架补全了、我们可以做得更多"，而是
**"底座不用再投人力了，人力应该全部转到游戏内与内容侧"。**

---

## 二、手里到底有什么（两块拼图，谁也不完整）

| | 原版 `MarcusAIFramework` | AWAKE 本地化版 |
|---|---|---|
| 位置 | `%TEMP%\marcus_repo`（09-15 09:21 克隆）／github `Alexander-Dieros/MarcusAIFramework` | `AWAKE/framework/MarcusAwake*`（5 个工程） |
| 规模 | src 三工程 **71 文件 / 19,791 行**（全仓含 tests/sdk：77 / 22,100） | 5 模块 **153 文件 / 36,732 行**、**37 个契约接口**（据 `REPORT-MARCUS-FRAMEWORK-AND-F015-INTERFACE-20260915.md` §2/§3） |
| **强在哪** | **管理面 ＋ 基建**：游戏内 AI 设置台、八面板诊断台、SDK（manifest/schemas/引用 DLL/FakeHost/test-kit/linter）、打包保护、durable spool、728 行资产 CAS、六类 adapter、TTS、本地 GGUF＋Vulkan | **玩法面 ＋ 内容**：8 个给玩家看的面板（信使 / NPC 对话 / 场景对话状态 / 周报浏览器 / 世界事件收件箱 / 开发检查 / 画位探针 / 画位槽）、**世界书 448 档**、**角色卡 76 张** |
| **弱在哪** | **零玩法**。README 原文：**"没有扩展请求时，框架不会主动生成对话、关系、外交、剧情或战役记录"**。它自己写着"不是 NPC 外交、关系或剧情 Mod" | **管理面薄**：只有 MCM 快速配置顶着；没有设置台、没有诊断台、没有 SDK |
| 自己的证据等级 | 最高只到 `contract-tested`；唯一 `Runtime-verified` 是 GGUF GPU（RTX 5060 Ti 25/25 offload）。**"In-game smoke/stress tests: Not performed"**（`docs/IMPLEMENTATION_STATUS.md` 末行） | 玩法侧真调通的 **8 面** ＋ RAG（09-15 接通）；**游戏内只有一次 ad-hoc 跑** |

> ⚠️ 两者**不是子集关系**：模块切分不同（5 工程 vs 3），行数不能直接比。本地化是**一次分叉**，不是"删了几块"。

**⇒ 拼图结论**：原版有骨架没肉，AWAKE 有肉没骨架。**今天开始，骨架可以拿过来用了。**

---

## 三、前景的正面：七件东西从「造」变成「搬」

这七件以前每件都是"要不要投"的决策；现在原版里都有**可读的实现**，成本从"周"降到"天"。
（生图片 1 是实证：13 文件 / +1210 行 / 一天，提交 `8a947ce`。）

| # | 能力 | 原版出处（可读实现） | 对哪一版有用 |
|---|---|---|---|
| 1 | **事件落盘 / durable spool**（F-033） | `MarcusAIFramework/Core/DurableSpoolWriter.cs` ＋ `RuntimeEventService.cs` | **v0.2 记得住** |
| 2 | **资产库 CAS**（F-049） | `Companion/AssetEngine.cs`（728 行：验证/去重/范围列表/导出收据/固定/引用安全删除/清理） | v0.4 有脸 |
| 3 | **生图**（F-050） | `ProviderRouter.Operations.cs` 三路 ＋ 六类 adapter；**片 1 已落**，片 2–4 待搬 | v0.4 有脸 |
| 4 | **TTS / VoiceProfile 链**（F-051） | 原版 media/TTS 协议 ＋ voice/language 解析 | v0.4／以后 |
| 5 | **本地 GGUF ＋ Vulkan**（F-026） | `ManagedGgufRuntime` ＋ 捆绑 llama.cpp b9173；**原版本机实测过**（25/25 层进显存、3.0 s） | 可选（离线跑模型） |
| 6 | **游戏内 AI 设置台 ＋ 八面板诊断台**（F-055） | `GUI/Prefabs/MarcusAIFrameworkAiSetup.xml`、`MarcusAIFrameworkDiagnostics.xml` | v0.5 撑得住 |
| 7 | **SDK**（F-006～F-008） | `sdk/`：manifest、schemas、按版本隔离引用 DLL、双语模板、FakeHost、test-kit、linter | 对外发版时 |

**⚠️ 唯一没被消掉的成本：片 2 要碰共享协议（`MarcusAwakeTransport`）。**
AWAKE 的协议已经和原版**分叉**（严格白名单校验、typed error 14 路、能力名双写）。
⇒ 每搬一件都要过一次协议。**这是唯一会返工的地方**，其余都是"照形状填"。

---

## 四、前景的四个天花板（搬得再快也绕不过）

1. **游戏内零验证。** 原版自己也是 `In-game smoke: Not performed`；AWAKE 只有 09-14 一次 ad-hoc 跑。
   ⇒ **搬得再多，一天不进游戏，一天不知道能不能用。** 这是唯一真正的门槛。
2. **原版是基础设施，零玩法。** 补全框架**不直接产生任何玩家看得见的东西**。
   ⇒ 别把"框架完整"当成进度。
3. **真正的上限在没读的那批料上**：`MarcusAIFramework_Reference/AuthorSource/src_20260813/`
   （`MarcusAINpc` / `MarcusAIRelationships` / `MarcusAIDiplomacy` / `MarcusAIWorldEvents`，165 文件）——
   矩阵 §8 明文把这四个模组的**全部行为**纳入 AWAKE 适配与验收。现状：**基本未读**。
   ⇒ A0 骨架决定"在不在"，**这四个决定"像不像、好不好玩"**。
4. **发布合规（新出现，必须现在就说）**：
   - 原版是 **Apache-2.0**，带 `LICENSE` 与 `NOTICE`。AWAKE 现在**仓库根与 `AWAKE/` 下都没有 `LICENSE` / `NOTICE`**
     ⇒ 借用了原版代码却缺署名与许可声明，**上创意工坊前必须补**（Apache-2.0 允许再分发，条件是保留声明）。
   - 原版 README 有「**成人内容与边界**」一节：框架**不做内容过滤**、面向 18+。
     ⇒ **这条不能跟着搬。** AWAKE 要走创意工坊，边界得自己定、写清楚，否则 Steam 分级与商店政策会卡死。

---

## 五、结论与排序建议

**把框架当工具箱，不当里程碑。**

1. **不再为"框架功能完整"排优先级。** 需要哪件、搬哪件、当天接线、当天进一次游戏看一眼。
2. **第一优先仍是游戏内**（不变）：09-14 那一跑露出的四个缺陷 ＋ 工具候选（真件已在、玩法侧零调用，**不用搬**）。
3. **第二优先：读 `AuthorSource` 那四个示范模组。** 这是唯一能抬高上限、且**不需要搬框架**（只借设计）的活。
4. **第三优先：按版本阶梯取用**——v0.2 记得住 → durable spool（第 1 件）；v0.3 活起来 → 工具候选 ＋ 事件；
   v0.4 有脸 → 资产 CAS ＋ 生图片 2（第 2、3 件）。

**明确不建议**：不要为了"框架完整"把六类 adapter、TTS、本地 GGUF、诊断台**一次性搬齐全**。
那是"补全"思路，产出的是**没人调的功能**——现成反例就在眼前：`Tools`（工具候选）与 `Capabilities`（能力协商）
**真件都在、玩法侧 0 调用**。

**顺带**：第 6 件（设置台 / 诊断台）对 AWAKE 是**新形态**——AWAKE 现在只有玩家玩法面板，没有管理面。
它是"能自己配起来"那条发布门槛（v0.5 撑得住 / 发布门槛「上手」）的现成答案，**但排在 v0.5**，不是现在。

---

## 出处（全部可核）

- 原版：`%TEMP%\marcus_repo`（`README.md` 特性表与「当前证据」节、`docs/IMPLEMENTATION_STATUS.md`、
  `GUI/Prefabs/`、`sdk/`、`LICENSE`、`NOTICE`）
- AWAKE 侧：`AWAKE/docs/REPORT-MARCUS-FRAMEWORK-AND-F015-INTERFACE-20260915.md`、
  `AWAKE/docs/AUDIT-MARCUS-CAPABILITY-LIVENESS-20260915.md`、`AWAKE/docs/PLAN-IMAGE-PORT-TO-FRAMEWORK-20260915.md`、
  `AWAKE/docs/AWAKE-ROADMAP.md`
