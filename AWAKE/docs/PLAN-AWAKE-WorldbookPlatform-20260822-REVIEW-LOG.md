# Plan Review Log: AWAKE 世界知识平台与玩家可编辑世界书

Act 1 (grill) complete — plan locked with the user. MAX_ROUNDS=5.

用户已确认的关键边界：

- 交付边界先做行为映射、统一契约和实施计划，不修改冻结候选。
- 正典事实与 NPC 认知分离。
- 身份采用基础身份加条件修正。
- 知识拥有程度、敏感度和表达方式三轴分离。
- 运行时知识来源限于静态档案、代码周报/事件和玩家传授/记忆。
- 新读取器直接重做；保留 `ModuleData/Worldbook/manifest.json` 入口但升级内部 v2 格式，不兼容 v1。
- AWAKE Core 不绑定世界观；卡拉迪亚默认但非强制；成人拓展是目标世界观专属子模组。
- 一个战役只激活一个主世界观；世界观包和拓展包分目录。
- 开局玩家手动确认，代码检查为硬门槛，AI 只辅助建议。
- 正式包冲突默认阻止；玩家战役覆盖层允许明确覆盖。
- 使用全限定稳定 ID。
- Worker 仅部署于开发者本机，不进入游戏运行时。
- 玩家可在游戏内编辑已有知识；默认作用于当前战役全局但仍受身份权限限制。
- 玩家可导出并分享可复用世界知识包；默认不导出 NPC 个人记忆。

## Pending Act 2

## Round 1 — Codex

VERDICT: REVISE

主要意见：Contract 还不是可机器校验边界；包注册、包激活和 Runtime Snapshot 混用；导入安全缺少路径/大小/深度/原子 staging 约束；Studio 到 Runtime 的编译链不具体；真实调用链替换清单缺失；玩家 Overlay、Save/load、revision、缓存失效和基础包升级重定位被推迟；周报事件契约和 Adapter 最小接口不够明确；测试和性能预算不足；整体范围过大。

### Claude's response

采纳上述问题并重排计划：

- 增加 authoring/runtime/manifest/overlay/export schema、引用闭包、权限算法、结果枚举和错误码作为 Contract 交付物。
- 将包注册清单、单包 manifest、战役激活状态和内存 Snapshot 分开。
- 把 Overlay Save/load 放到玩家 UI 之前，并规定不可变 Snapshot、单调 revision、copy-on-write 和原子切换。
- 增加导入资源配额、路径边界、哈希、引用闭包、staging 和禁止执行代码/网络的要求。
- 增加 `IWorldKnowledgeQuery` 调用图、旧入口替换清单和生产调用残留测试。
- 将周报事件契约和最小 Adapter Contract 独立到最后阶段。
- 将计划拆成只读 Runtime、Overlay/Save、玩家 UI/分享、周报/Adapter 四阶段，并增加固定大包性能门禁。

下一轮请重点复核：分阶段边界、注册/激活/Snapshot 分离、玩家包导入安全、Overlay 存档前置和 v2 Contract 是否已经达到可实施程度。

## Round 2 — Codex

VERDICT: REVISE

主要意见：需要固定 schema 的路径、`$id`、版本值、未知字段和引用闭包；明确 registry/package manifest/campaign activation/runtime snapshot 的持久化边界；点名旧查询入口和新 façade 的静态阻断；定义 overlay 的 CAS revision 和 Save key；固定导入容器与资源/路径配额；给出周报真实触发入口和 Adapter 的接口级边界；补充冷/热/并发/GC 性能口径；再拆分阶段 A 为 A0–A3，并写入旧候选与新候选隔离证据。

### Claude's response

采纳并完成第二轮修订：

- A0 现在固定 Contract 目录、schema artifact 清单、未知字段策略、重复 ID、引用闭包和版本约束。
- A1 明确注册清单只保存受信任根目录下的规范化相对路径；激活保存 ID/version/hash；Snapshot 只在内存重建。
- A2/A3 分离 Snapshot/Loader 和真实调用替换，明确 `IWorldKnowledgeQuery`、旧入口静态阻断和新候选隔离。
- 阶段 C 前置 Save key、SaveDefiner、加载顺序、CAS revision、撤销/恢复和重定位契约。
- 阶段 D 固定 ZIP+JSON 导入容器、配额、Unicode/大小写碰撞、staging 和禁止执行内容。
- 阶段 E 增加周报唯一写入者/触发点和 Adapter schema、接口、能力、版本与 fake adapter。
- 性能门禁增加基准环境、冷热定义、并发/GC 口径和超限阻止新 BuildId 的规则。

下一轮请检查这些 artifact 是否已经形成足够明确的实现入口，以及 A0–A3 是否仍有未拆开的关键依赖。

## Round 3 — Codex

VERDICT: REVISE

主要意见：剩余 material blocker 为新旧候选实际隔离路径/入口/验证规则未固定；事件层缺少 `IWorldEventRecorder`、`IWeeklyReportService` 和 schema artifact；manifest/content/package hash 语义未固定；性能基准环境、seed、预热、并发和采样口径未给定。

### Claude's response

采纳并完成第三轮修订：

- 固定旧冻结候选的受保护产物路径和 BuildId，固定新 v2 候选 artifact 根目录、v2 registry 入口、模块/入口隔离和互相不可发现检查；未过门禁不写游戏目录。
- 将 `event-record.schema.json`、`weekly-report.schema.json`、`IWorldEventRecorder` 和 `IWeeklyReportService` 纳入唯一事件生产路径与 harness。
- 固定 `manifestHash`、`contentHash`、`packageHash` 和组合 revision 的计算输入。
- 固定性能 baseline 文件、当前平台、数据 seed、预热/采样次数、并发度和超限阻止 BuildId 规则。

下一轮请只判断这些剩余实现歧义是否已降为后续实现计划细节；若无 material blocker，给出 APPROVED。

## Round 4 — Codex

VERDICT: REVISE

主要意见：唯一剩余 material blocker 是哈希跨机器复现规则未固定，需补充算法、canonical JSON、文本/路径规范化、packageHash 拼接方式和 golden fixture。

### Claude's response

采纳并完成第四轮修订：

- 固定 SHA-256、UTF-8 canonical JSON、排序键、LF、路径 `/` 和大小写折叠规则。
- 固定 `packageHash = SHA256(manifestHash_bytes || contentHash_bytes)`。
- 增加跨 PowerShell、.NET 和 Studio 编译链的 golden hash fixture。

进入 Round 5 终审。

## Round 5 — Codex

VERDICT: APPROVED

终审结论：没有剩余 material blocker。Contract artifact、注册/激活/Snapshot 边界、哈希和 golden fixture、候选隔离、Save/CAS、导入安全、事件/Adapter façade、性能门禁和阶段范围已经足够确定，可进入独立实现计划；当前冻结候选保持不变。
