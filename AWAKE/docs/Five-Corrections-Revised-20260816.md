# 五条修正 · 修订版 v2

_依据：`docs/grill/Grill-Summary-20260816.md` 及五个 50 点审查文件。第二轮 50 点复核仍为 REVISE：本文件是概念修订版，不是已批准契约；下一步需把每条转成 schema/命令/UI/测试契约。_

## C1 原始对话 / 压缩记忆 / 关系状态三层分离

1. 新增明确 transcript 层：建议把 `awake.messenger.v1` 升级为 `awake.transcript.v1`，字段含 `id/day/location/speaker/text/source/conversationId/pinned/kind`，并提供旧 messenger 数据只读迁移。
2. 保留策略：每个联系人 30 游戏日或 200 条，先到先删；固定对话最多 20 条，固定期间不删。
3. 压缩时序：只有压缩记忆写入成功并记录审计后，才允许滚动未固定原文；压缩不得触碰 transcript 存储。
4. AI 上下文：NPC prompt 不再注入原始对话；只使用压缩记忆摘要 + 有界会话摘要。原始 transcript 仅玩家面板可见，不进入 RAG/世界书检索，默认禁止云外发。
5. 存储：transcript 按联系人拆分或分块，任何单值不超过框架 512KB 上限；写操作幂等，支持最终 drain。

玩家观感：玩家需要能分清“我能回看的原文”和“NPC 现在记得的摘要”；固定、压缩、过期三态必须可见，不能感觉记忆被偷删。

开发维护：transcript、memory、relationship 各有独立 schema 和命令，改记忆压缩不会误删聊天记录；旧 messenger 数据走一次性只读迁移，避免长期双写。

## C2 借鉴面板模式，但不做文件编辑和快照回滚

1. 只借鉴布局与交互概念；禁止直接编辑 AI 数据文件、禁止磁盘快照恢复、禁止依赖其他模组存档路径。
2. 所有历史写操作走 `WorldStateStore` 命令：删除、固定、清空、撤销均实现为有 ID 的幂等命令。
3. 撤销使用紧凑逆操作/补偿命令，不保存整份文档快照。
4. 新命令同步注册到 `AiTaskConstants`、`PermissionCatalog`、`CommandRiskPolicy`、框架 manifest。
5. 普通玩家只开放固定/删除/清空确认；导出与撤销等高级功能默认放开发者菜单。

玩家观感：历史操作像游戏内命令一样可确认、可反馈，不出现“文件被改坏、存档对不上”的恐怖感。

开发维护：命令化 + 审计让删除/撤销可测试、可恢复；不依赖磁盘路径和文件快照，也避免 OneDrive/安装目录差异带来的维护坑。

## C3 借鉴 Alice 结构，但不复制资产和实现

1. 资产边界扩大到所有 Alice/AF 素材：贴图、sprite、brush、icon、字体、声音、动画、prefab、VM 代码、命名全部不复制。
2. 只保留布局与交互概念：左头像区、中对话流、动作按钮、写信面板。
3. 头像优先使用游戏原生 `CharacterCode` / `ImageIdentifier`；无名 NPC 用通用占位，不生成自定义立绘。
4. Phase A 优先扩展现有 `AwakeMessengerOverlay/VM/Prefab`，不新建第三套对话 UI。
5. 默认保持内容无关；内容包专属 Tab 通过内容 API 注册，不写死进核心面板。

玩家观感：界面风格与骑砍原生 UI 融合，不出现“一个模组里混着三套画风”的割裂感。

开发维护：禁止复制可执行资产/代码边界，靠 lint 自动检查，而不是靠开发者自觉；扩展现有面板减少两套 UI 双倍维护。

## C4 交互动作先做代码保底

1. v1 只做两个可严格校验动作：给金币、请求/承诺账本；物品与交易待原生 API/游戏数据能力确认后再做。
2. 新增 `awake.action.*` schema、适配器、权限、风险分级、`awake.interactions.v1` 账本。
3. AI 只能生成“动作建议”，必须渲染为待确认按钮，玩家点击后才执行；玩家只能编辑有界字段。
4. 适配器必须校验：目标身份与当前会话一致、数量/物品/余额、输入 schema、快照 token、幂等键。
5. 关系变化由代码根据结算结果推导，不信任 AI 直接给 delta。

玩家观感：每个动作都有明确按钮、确认和结算反馈；玩家不会因为 AI 幻觉而少钱、少物或产生不存在的承诺。

开发维护：动作命令有统一 schema/适配器/测试，不写自由文本解析；v1 只做少量动作，降低命令矩阵和权限矩阵的维护负担。

## C5 统一对话会话与入口

1. `AwakeDialogueSessionCoordinator` 升级为真实会话模型：sessionId、targetId、entrySource、state、correlation、活动服务引用。
2. coordinator 拥有 `NpcDialogueService` 生命周期与 transcript 写入；每个目标同一时间只存在一个活动服务。
3. 场景 T/Y、遭遇面谈、主动对话、事件讨论、通讯录都通过同一入口打开 hub，并支持“显式初始目标”，不再默认自动选最近联系人。
4. 合并 `NpcDialogueOverlay` 与 `AwakeMessengerOverlay` 为一个 hub VM/prefab；事件收件箱、周报不并入。
5. 统一 Escape、输入恢复、layer 优先级、低分辨率布局；关闭/切换采用 session token，不用 source/target 字符串猜测。
6. 挂起事件/主动队列随存档持久化，历史写入幂等，并加入 session 接管、队列去重、v1 迁移、hub 启动的 SdkSmoke。

玩家观感：同一段关系在一个地方延续；场景 T/Y 是否保留轻量覆盖层以玩家手感为准，不为了“统一”而把场景操作变重。

开发维护：只维护一个 session 模型、一个 hub VM、一套历史写入，替代现在两套 overlay/VM 的重复逻辑；迁移和去重有测试兜底。
