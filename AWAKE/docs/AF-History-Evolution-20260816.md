# AF 历史版本演进分析与 AWAKE 开发经验

> 数据来源：`AnimusForge.rar`，包含 v0.8.4 至 v1.3.2.1 共 23 个独立版本包（去重后），另含 `AnimusForgeHotSceneBridge_v0.2.0`。
> 分析方法：逐版本比对 SubModule 版本、DLL 体积、GUI/ModuleData/AssetSources 文件差异、规则 JSON 数量与结构。

## 一、版本线与规模变化

| 版本 | 时间 | DLL 大小（1.3 版） | 规则 JSON 大小 | RulePrompts 数 | Preprocess 版本 | Proactive 请求数 |
|---|---:|---:|---:|---:|---:|---:|
| v0.8.4 | 2026-06-11 | 3.66 MB | 100.7 KB | 15 | 无 | 0 |
| v0.9.2 | 2026-06-21 | 4.43 MB | 107.3 KB | 16 | 无 | 0 |
| v1.2.2.3 | 2026-07-18 | 7.16 MB | 134.4 KB | 18 | 3 | 30 |
| v1.2.3.1 | 2026-07-19 | 7.40 MB | 134.4 KB | 18 | 3 | 30 |
| v1.2.4.1 | 2026-07-20 | 7.52 MB | 136.1 KB | 18 | 4 | 30 |
| v1.2.6.4 | 2026-07-24 | 7.72 MB | 133.4 KB | 16 | 4 | 30 |
| v1.2.7.2 | 2026-07-26 | 7.85 MB | 133.4 KB | 16 | 5 | 30 |
| v1.2.8 | 2026-07-29 | 7.89 MB | 136.9 KB | 16 | 5 | 30 |
| v1.3.0 | 2026-07-31 | 8.48 MB | 138.8 KB | 17 | 5 | 30 |
| v1.3.1 | 2026-08-05 | 8.63 MB | 138.9 KB | 17 | 5 | 30 |
| v1.3.2.1 | 2026-08-14 | 9.10 MB | 139.9 KB | 17 | 5 | 30 |

注意：`RulePrompts` 数量并没有暴涨，但规则 JSON 体积从 100 KB 涨到 140 KB，说明 AF 的主要投入不是“堆规则条数”，而是反复加深现有规则的指令、关键词、后处理标签和运行时约束。

## 二、关键版本差异

### v0.8.4 -> v0.9.2

- 新增/改动有限，主要是：
  - `AnimusForgeConversationHistoryLog.xml` 调整。
  - `ActionPostprocessPrompts.json` 从 2.8 KB 增至 3.4 KB。
  - `RuleBehaviorPrompts.json` 从 100.7 KB 增至 107.3 KB。
  - 新增 `diplomacy` 规则。
- 判断：这个阶段是“对话基座 + 规则后处理”的雏形期，重点在 AI 对话本身的规则收敛。

### v0.9.2 -> v1.2.2.3（最大跃迁）

这是 AF 从“AI 对话模组”膨胀成“AI 世界模组”的版本：

- DLL 从 4.43 MB 跳到 7.16 MB。
- 新增 GUI：
  - `AnimusForgeNativeConversationOverlay`、`ShoutTextInputPopup`：原生对话 AI 模式 + 场景喊话输入。
  - `CourierLetterInputPopup / ReplyPopup`：信件系统。
  - `CustomPolicyCompose/History/Result`、`LocalPolicyCompose/History`：政策系统。
  - `AnimusForgeWorldEventInboxPopup`：世界事件收件箱。
  - `DevWeeklyReportPopup`、`TerminalWeeklyReportBrowserPopup`：周报系统。
  - `PlayerNotorietyPopup`：玩家恶名/声望面板。
  - `FloatingTextLayer`：场景浮动文本。
- 新增 ModuleData：
  - `PreprocessPrompts.json`、`ProactiveNpcRequestPrompts.json`。
  - 无名 NPC 档案目录。
- 新增资源：大量订阅/成就/信件/周报美术资源。
- 判断：开发中心从“对话本身”转向“对话外挂世界系统”：写信、政策、事件、周报、恶名、NPC 主动。

### v1.2.2.x -> v1.2.4.x（高频规则修补期）

- 同一天内多次发版，例如 v1.2.2.3/v1.2.2.4、v1.2.3.1/v1.2.3.2。
- 绝大多数变更只出现在：
  - `RuleBehaviorPrompts.json`
  - `ActionPostprocessPrompts.json`
  - `PreprocessPrompts.json`
- Preprocess 版本从 3 升到 4，说明路由/记忆筛选逻辑在重做。
- 判断：这段是“规则深水区”，开发重点是让 AI 理解什么算已成立、什么不算，并收敛动作标签。

### v1.2.4.x -> v1.2.6.x（规则合并与瘦身）

- `hero_join_party`、`vote_deal`、`propose_agenda`、`settlement_transfer` 等规则被移除或合并。
- RulePrompts 从 18 降到 16，JSON 体积也短暂回落。
- 判断：AF 不是只做加法，也会主动合并/删除规则；但规则 ID 变更对第三方内容是破坏性的。

### v1.2.6.x -> v1.2.7.x

- 新增 `PlayerRpForgePopup.xml`：玩家自制物品/锻造 RP 界面。
- Preprocess 升到 v5，新增 `PlayerRpTemplateSelection`。
- 判断：开发中心再次外扩到“玩家创造物”，并让预处理承担模板选择。

### v1.2.8 -> v1.3.0

- 新增 `WorldDiplomacyComposePopup.xml` 与外交美术资源。
- 新增 `world_diplomacy_discussion` 规则。
- `ProactiveNpcRequestPrompts.json` 从 12.9 KB 增至 13.1 KB。
- `AnimusForgeWorldEventInboxPopup.xml` 升级。
- 判断：方向转向“世界外交 + 世界事件”，AI 对话开始影响王国级局势。

### v1.3.0 -> v1.3.2.1

- v1.3.1：政策 UI 与规则继续调。
- v1.3.2：世界事件收件箱继续升级。
- v1.3.2.1：黄金金币资产整体重做，多个 UI 文件修订，规则与无名档案再次调整。
- 还发现 `EarlyException_2026-06-28.html` 被直接打包进 ModuleData：发布卫生问题。
- 判断：后期进入“内容体验 + 发布质量”阶段，但代码已经非常庞大，单文件维护成本很高。

## 三、AF 的开发中心总结

1. **AI 对话链路是核心**：场景喊话、原生对话 AI 模式、主动聊天、信件，全部围绕“NPC 可以持续、自然地对话”。
2. **规则/后处理是最重的迭代对象**：规则 JSON 从 100 KB 涨到 140 KB，几乎每个小版本都在改；这不是内容堆叠，而是反复校准“什么才算已成立”。
3. **世界玩法持续外扩**：政策、外交、周报、世界事件、恶名、RP 锻造、贵族宴会、攻城处置、臣属关系。
4. **主动聊天从随机变成动机系统**：ProactiveNpcRequestPrompts 从 0 到 30 类请求，覆盖缺粮、缺钱、俘虏、家族、婚姻、政治、外交等。
5. **稳定性投入很多**：FreezeWatchdog、PerfProbe、主线程预算、异常哨兵、ConversationExceptionGuard，说明运行期崩溃/卡死是主要矛盾。
6. **版本节奏快**：一天内多个补丁，主要改规则和修复，但缺少成体系 changelog。

## 四、AWAKE 应保留的经验

- **规则驱动的行为注入**：让 AI 只负责“说话”，动作由结构化标签/命令触发，这比让 AI 直接改游戏数据安全。
- **后处理负责“是否成立”判定**：AF 用独立后处理判定 NPC 是否明确同意，避免把谈判、报价、反问误判成已发生。AWAKE 应保留“已成立才结算”原则。
- **主动聊天要有动机与冷却**：不是随机弹窗，而是按事件、关系、队伍状态、家族/政治压力生成候选。
- **场景喊话 + 原生对话 AI 模式双入口**：这正是用户要求 AWAKE 第一版就具备的两种方式。
- **主线程与性能预算**：mission tick 不能做阻塞 AI/文件/数据库；主线程动作要分批。
- **双版本/多版本构建契约**：AWAKE 已经按 v1.3.15/v1.4.8 双版本构建，应继续保留。
- **无名 NPC 需要身份回退**：普通士兵/平民不能拥有领主知识。

## 五、AWAKE 应避免的教训

- **不要变成巨型单体**：AF 的 `ShoutBehavior.cs` 等文件已经接近百万级字符，后期维护和审查成本极高。AWAKE 必须拆成入口、目标、会话、提示词、规则、命令、存储等模块。
- **不要用近 1GB 暴力扫描 + ONNX 做检索**：AF 的本地知识检索体积和复杂度失控。AWAKE 使用索引 + 分层召回 + 预算组装。
- **不要直接反射写原版对话 VM**：`ConversationHelper` 对 `DialogText`/`CurrentCharacterNameLbl` 的反射写入依赖私有字段，版本一换就崩。AWAKE 用自己的覆盖层。
- **不要无限堆 JSON**：140 KB 的规则 JSON 没有 schema 版本约束、没有规则注册表，改一个 ID 就可能静默失效。AWAKE 需要版本化 schema、注册表、校验器和迁移工具。
- **不要在发布包塞临时文件**：`EarlyException_*.html` 被带进正式包，说明发布校验不严。AWAKE 的发布校验必须做文件白名单。
- **不要让小版本承载过多语义**：AF 的补丁版本同时包含规则调参、新功能、UI 调整和架构修复，外部难以判断兼容性。AWAKE 继续遵循“功能与修复分层、正式提版才改版本号”的规则。
- **不要用硬编码字符串做 UI**：AF 的很多 prefab 文案直接写死中文，AWAKE 已改为本地化 key。
- **内容与运行时耦合过深**：AF 把大量世界内容直接写进核心 DLL/JSON。AWAKE 应保持运行时内容无关，内容包通过注册接口接入。

## 六、结论

AF 的价值不在“它现在很大”，而在它证明了一条可走通的路径：**AI 负责角色化表达，规则/后处理负责世界事实与动作成立性，游戏系统负责结算**。AWAKE 应当继承这个分层，但用更小的模块、更严格的 schema、更强的发布校验和更可观测的日志把它重新实现。
